using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using FieldLink.OpcUa.Subscriptions;
using FieldLink.OpcUa.Tests.Fakes;
using FieldLink.OpcUa.Tests.Fixtures;
using FieldLink.OpcUa.Transport;
using Opc.Ua;
using Xunit;

namespace FieldLink.OpcUa.Tests.Subscriptions;

public sealed class SubscriptionTests
{
    private static ExpandedNodeId TestNode(string id) => new(id, OpcUaTestNodeManager.NamespaceUri);

    [Fact]
    public async Task B01_LoopbackSubscription_DeliversInitialAndSequentialDataChangesInOrder()
    {
        await using OpcUaTestServer server = new();
        await server.StartServerAsync();

        using TestClientCertificateSetup clientSetup = new(server.RootDirectory);
        await clientSetup.TrustServerAsync(server);

        using OpcUaClient client = new(clientSetup.CreateClientOptions(server.EndpointUrl));
        await client.OpenAsync();

        await using OpcUaSubscription subscription = await client.SubscribeAsync(
            new OpcUaSubscriptionOptions
            {
                PublishingIntervalMilliseconds = 50,
                KeepAliveCount = 10,
                LifetimeCount = 30
            },
            new[]
            {
                new OpcUaMonitoredItemOptions
                {
                    ClientItemKey = "counter",
                    NodeId = TestNode("TestRoot.Special.StreamCounter"),
                    SamplingIntervalMilliseconds = 0,
                    QueueSize = 10
                }
            });

        List<int> observed = new();
        OpcUaDataChangeBatch? initialBatch = await subscription.ReadNextAsync();
        Assert.NotNull(initialBatch);
        Assert.Equal("counter", initialBatch!.Items[0].ClientItemKey);
        observed.Add((int)initialBatch.Items[0].Value.Value);

        for (int nextValue = 1; nextValue <= 3; nextValue++)
        {
            server.NodeManager.UpdateStreamCounter(nextValue);
            using CancellationTokenSource waitCts = new(TimeSpan.FromSeconds(5));
            OpcUaDataChangeBatch? batch = await subscription.ReadNextAsync(waitCts.Token);
            Assert.NotNull(batch);
            observed.Add((int)batch!.Items[0].Value.Value);
        }

        Assert.Equal(new[] { 0, 1, 2, 3 }, observed);
        await subscription.CloseAsync();
    }

    [Fact]
    public async Task B02_PartialMonitoredItemSuccess_KeepsSubscriptionAndPreservesFailedStatusCode()
    {
        await using OpcUaTestServer server = new();
        await server.StartServerAsync();

        using TestClientCertificateSetup clientSetup = new(server.RootDirectory);
        await clientSetup.TrustServerAsync(server);

        using OpcUaClient client = new(clientSetup.CreateClientOptions(server.EndpointUrl));
        await client.OpenAsync();

        await using OpcUaSubscription subscription = await client.SubscribeAsync(
            new OpcUaSubscriptionOptions { PublishingIntervalMilliseconds = 50 },
            new[]
            {
                new OpcUaMonitoredItemOptions
                {
                    ClientItemKey = "counter",
                    NodeId = TestNode("TestRoot.Special.StreamCounter")
                },
                new OpcUaMonitoredItemOptions
                {
                    ClientItemKey = "scalar",
                    NodeId = TestNode("TestRoot.Scalar.Int32")
                },
                new OpcUaMonitoredItemOptions
                {
                    ClientItemKey = "missing",
                    NodeId = TestNode("TestRoot.Missing.Node")
                }
            });

        Assert.Equal(3, subscription.ItemResults.Count);
        Assert.True(subscription.ItemResults[0].IsSuccess);
        Assert.True(subscription.ItemResults[1].IsSuccess);
        Assert.False(subscription.ItemResults[2].IsSuccess);
        Assert.Equal(StatusCodes.BadNodeIdUnknown, subscription.ItemResults[2].StatusCode);

        using CancellationTokenSource waitCts = new(TimeSpan.FromSeconds(5));
        OpcUaDataChangeBatch? batch = await subscription.ReadNextAsync(waitCts.Token);
        Assert.NotNull(batch);
        Assert.Contains(batch!.Items, static item => item.ClientItemKey is "counter" or "scalar");
    }

    [Fact]
    public async Task B03_ZeroSuccessfulMonitoredItems_DeletesServerSubscriptionAndThrowsFailure()
    {
        await using OpcUaTestServer server = new();
        await server.StartServerAsync();

        using TestClientCertificateSetup clientSetup = new(server.RootDirectory);
        await clientSetup.TrustServerAsync(server);

        using OpcUaClient client = new(clientSetup.CreateClientOptions(server.EndpointUrl));
        await client.OpenAsync();

        ServiceResultException ex = await Assert.ThrowsAsync<ServiceResultException>(() => client.SubscribeAsync(
            new OpcUaSubscriptionOptions { PublishingIntervalMilliseconds = 50 },
            new[]
            {
                new OpcUaMonitoredItemOptions
                {
                    ClientItemKey = "bad1",
                    NodeId = TestNode("TestRoot.Missing.One")
                },
                new OpcUaMonitoredItemOptions
                {
                    ClientItemKey = "bad2",
                    NodeId = TestNode("TestRoot.Missing.Two")
                }
            }));

        Assert.Equal(StatusCodes.BadNodeIdUnknown, ex.StatusCode);
        Assert.Equal(0, server.ActiveSubscriptionCount);
    }

    [Fact]
    public async Task B04_B05_BatchAndByteLimitsExceeded_FailClosedWithQueueOverflowWithoutSilentDrop()
    {
        FakeDiscoveryClient discovery = new();
        FakeSessionFactory sessionFactory = new();
        FakeSessionAdapter session = new();
        session.NamespaceUris.Append(OpcUaTestNodeManager.NamespaceUri);

        FakeSubscriptionAdapter? batchAdapter = null;
        session.OnCreateSubscription = (_, cb) =>
        {
            batchAdapter = new FakeSubscriptionAdapter(cb);
            return batchAdapter;
        };
        sessionFactory.OnCreateSession = _ => Task.FromResult<IOpcUaSessionAdapter>(session);

        using OpcUaClient client = new(CreateFakeClientOptions(), discovery, sessionFactory);
        await client.OpenAsync();

        // B-04: 배치 개수 한도 초과
        await using OpcUaSubscription batchSub = await client.SubscribeAsync(
            new OpcUaSubscriptionOptions { MaxQueuedBatches = 2, MaxQueuedBytes = 1024 * 1024 },
            new[] { new OpcUaMonitoredItemOptions { ClientItemKey = "k", NodeId = TestNode("TestRoot.Scalar.Int32") } });

        batchAdapter!.EmitCallback(CreateSingleItemRawBatch(1, new DataValue(1)));
        batchAdapter.EmitCallback(CreateSingleItemRawBatch(2, new DataValue(2)));
        batchAdapter.EmitCallback(CreateSingleItemRawBatch(3, new DataValue(3))); // 3번째: 한도 2 초과

        Assert.Equal(OpcUaSubscriptionTerminationReason.QueueOverflow, batchSub.TerminationReason);
        Assert.NotNull(batchSub.TerminationException);
        Assert.Equal(1, batchAdapter.DeleteCallCount);
        await Assert.ThrowsAsync<InvalidOperationException>(() => batchSub.ReadNextAsync());

        // B-05: 바이트 크기 상한 초과
        await using OpcUaSubscription byteSub = await client.SubscribeAsync(
            new OpcUaSubscriptionOptions { MaxQueuedBatches = 64, MaxQueuedBytes = 1024 },
            new[] { new OpcUaMonitoredItemOptions { ClientItemKey = "big", NodeId = TestNode("TestRoot.Array.Bytes") } });

        batchAdapter!.EmitCallback(CreateSingleItemRawBatch(1, new DataValue(new byte[2048])));

        Assert.Equal(OpcUaSubscriptionTerminationReason.QueueOverflow, byteSub.TerminationReason);
        Assert.NotNull(byteSub.TerminationException);
        await Assert.ThrowsAsync<InvalidOperationException>(() => byteSub.ReadNextAsync());
    }

    [Fact]
    public async Task B06_ServerOverflowInfoBitAndImmutableDeepCopy_ArePreserved()
    {
        FakeDiscoveryClient discovery = new();
        FakeSessionFactory sessionFactory = new();
        FakeSessionAdapter session = new();
        session.NamespaceUris.Append(OpcUaTestNodeManager.NamespaceUri);

        FakeSubscriptionAdapter? subAdapter = null;
        session.OnCreateSubscription = (_, cb) =>
        {
            subAdapter = new FakeSubscriptionAdapter(cb);
            return subAdapter;
        };
        sessionFactory.OnCreateSession = _ => Task.FromResult<IOpcUaSessionAdapter>(session);

        using OpcUaClient client = new(CreateFakeClientOptions(), discovery, sessionFactory);
        await client.OpenAsync();

        await using OpcUaSubscription sub = await client.SubscribeAsync(
            new OpcUaSubscriptionOptions { MaxQueuedBatches = 8, MaxQueuedBytes = 64 * 1024 },
            new[] { new OpcUaMonitoredItemOptions { ClientItemKey = "bytes", NodeId = TestNode("TestRoot.Array.Bytes"), QueueSize = 1 } });

        byte[] sourceBuffer = { 1, 2, 3, 4 };
        StatusCode statusWithOverflow = StatusCodes.Good;
        statusWithOverflow.Overflow = true;

        DataValue rawDataValue = new(new Variant(sourceBuffer), statusWithOverflow, DateTime.UtcNow, DateTime.UtcNow);
        subAdapter!.EmitCallback(CreateSingleItemRawBatch(1, rawDataValue));

        // SDK 측 버퍼가 콜백 직후 재사용·변조되더라도 큐에 저장된 스냅샷에는 영향이 없어야 한다.
        sourceBuffer[0] = 255;

        OpcUaDataChangeBatch? batch = await sub.ReadNextAsync();
        Assert.NotNull(batch);
        Assert.True(batch!.Items[0].ServerOverflow);
        Assert.True(batch.Items[0].Value.StatusCode.Overflow);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, (byte[])batch.Items[0].Value.Value);
    }

    [Fact]
    public async Task B07_B08_SingleConsumerEnforcedAndCancellationOnlyCancelsWait()
    {
        FakeDiscoveryClient discovery = new();
        FakeSessionFactory sessionFactory = new();
        FakeSessionAdapter session = new();
        session.NamespaceUris.Append(OpcUaTestNodeManager.NamespaceUri);

        FakeSubscriptionAdapter? subAdapter = null;
        session.OnCreateSubscription = (_, cb) =>
        {
            subAdapter = new FakeSubscriptionAdapter(cb);
            return subAdapter;
        };
        sessionFactory.OnCreateSession = _ => Task.FromResult<IOpcUaSessionAdapter>(session);

        using OpcUaClient client = new(CreateFakeClientOptions(), discovery, sessionFactory);
        await client.OpenAsync();

        await using OpcUaSubscription sub = await client.SubscribeAsync(
            new OpcUaSubscriptionOptions(),
            new[] { new OpcUaMonitoredItemOptions { ClientItemKey = "k", NodeId = TestNode("TestRoot.Scalar.Int32") } });

        using CancellationTokenSource firstWaitCts = new();
        Task<OpcUaDataChangeBatch?> firstWait = sub.ReadNextAsync(firstWaitCts.Token);
        await Task.Delay(30);

        // B-07: 동시 소비자 거부
        await Assert.ThrowsAsync<InvalidOperationException>(() => sub.ReadNextAsync());

        // B-08: 첫 호출 취소 후에도 구독은 유지되어 후속 알림을 정상 수신
        firstWaitCts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => firstWait);
        Assert.Equal(OpcUaSubscriptionTerminationReason.None, sub.TerminationReason);

        subAdapter!.EmitCallback(CreateSingleItemRawBatch(42, new DataValue(new Variant(99))));
        OpcUaDataChangeBatch? received = await sub.ReadNextAsync();
        Assert.NotNull(received);
        Assert.Equal(99, received!.Items[0].Value.Value);
    }

    [Fact]
    public async Task B09_ParentCloseAndSessionFault_TerminateSubscriptionAndIsolateLateCallbacks()
    {
        FakeDiscoveryClient discovery = new();
        FakeSessionFactory sessionFactory = new();
        FakeSessionAdapter session = new();
        session.NamespaceUris.Append(OpcUaTestNodeManager.NamespaceUri);

        FakeSubscriptionAdapter? subAdapter = null;
        session.OnCreateSubscription = (_, cb) =>
        {
            subAdapter = new FakeSubscriptionAdapter(cb);
            return subAdapter;
        };
        sessionFactory.OnCreateSession = _ => Task.FromResult<IOpcUaSessionAdapter>(session);

        using OpcUaClient client = new(CreateFakeClientOptions(), discovery, sessionFactory);
        await client.OpenAsync();

        OpcUaSubscription subOnClose = await client.SubscribeAsync(
            new OpcUaSubscriptionOptions(),
            new[] { new OpcUaMonitoredItemOptions { ClientItemKey = "k", NodeId = TestNode("TestRoot.Scalar.Int32") } });

        await client.CloseAsync();
        Assert.Equal(OpcUaSubscriptionTerminationReason.ParentClosed, subOnClose.TerminationReason);
        Assert.Null(await subOnClose.ReadNextAsync());

        // 이전 세대의 지연 콜백 유입 시 무시 확인
        subAdapter!.EmitCallback(CreateSingleItemRawBatch(1, new DataValue(123)));
        Assert.Null(await subOnClose.ReadNextAsync());

        // 부모 세션 Fault 시 ParentFaulted 전이 확인
        await client.OpenAsync();
        OpcUaSubscription subOnFault = await client.SubscribeAsync(
            new OpcUaSubscriptionOptions(),
            new[] { new OpcUaMonitoredItemOptions { ClientItemKey = "k", NodeId = TestNode("TestRoot.Scalar.Int32") } });

        session.RaiseKeepAlive(StatusCodes.BadSessionClosed);
        Assert.Equal(OpcUaClientState.Faulted, client.State);
        Assert.Equal(OpcUaSubscriptionTerminationReason.ParentFaulted, subOnFault.TerminationReason);
        await Assert.ThrowsAsync<ServiceResultException>(() => subOnFault.ReadNextAsync());
    }

    [Fact]
    public async Task B10_QueueOverflow_EstimatesComplexVariantsAndDeletesAndDisposesAdapterOnce()
    {
        XmlDocument xmlDocument = new();
        xmlDocument.LoadXml("<Payload>" + new string('x', 2048) + "</Payload>");

        Variant[] oversizedVariants =
        {
            new(new Matrix(new int[2000], BuiltInType.Int32, 40, 50)),
            new(new ExtensionObject(new ExpandedNodeId(1), new byte[2048])),
            new(new[] { new ExtensionObject(new ExpandedNodeId(1), new byte[2048]) }),
            new(new LocalizedText("en-US", new string('a', 1500))),
            new(new QualifiedName(new string('q', 1500))),
            new(xmlDocument.DocumentElement!)
        };

        foreach (Variant variant in oversizedVariants)
        {
            OpcUaRawDataChangeBatch sampleBatch = CreateSingleItemRawBatch(1, new DataValue(variant));
            long estimatedBytes = OpcUaBatchSnapshot.EstimateRawBatchBytes(sampleBatch);
            Assert.True(estimatedBytes > 2048, $"Expected variant estimation > 2048 bytes, got {estimatedBytes}");
        }

        // Deeply nested Variant[] chain must respect MaxEstimationDepth without stack overflow.
        Variant nestedVariant = new(new byte[128]);
        for (int depth = 0; depth < 32; depth++)
        {
            nestedVariant = new Variant(new[] { nestedVariant });
        }

        long deepEstimate = OpcUaBatchSnapshot.EstimateRawBatchBytes(
            CreateSingleItemRawBatch(1, new DataValue(nestedVariant)));
        Assert.True(deepEstimate > 0);
        Assert.Equal(long.MaxValue, OpcUaBatchSnapshot.AddSaturating(long.MaxValue - 10, 100));

        FakeDiscoveryClient discovery = new();
        FakeSessionFactory sessionFactory = new();
        FakeSessionAdapter session = new();
        session.NamespaceUris.Append(OpcUaTestNodeManager.NamespaceUri);

        FakeSubscriptionAdapter? subAdapter = null;
        session.OnCreateSubscription = (_, cb) =>
        {
            subAdapter = new FakeSubscriptionAdapter(cb);
            return subAdapter;
        };
        sessionFactory.OnCreateSession = _ => Task.FromResult<IOpcUaSessionAdapter>(session);

        using OpcUaClient client = new(CreateFakeClientOptions(), discovery, sessionFactory);
        await client.OpenAsync();

        OpcUaSubscription sub = await client.SubscribeAsync(
            new OpcUaSubscriptionOptions { MaxQueuedBatches = 16, MaxQueuedBytes = 1024 },
            new[] { new OpcUaMonitoredItemOptions { ClientItemKey = "k", NodeId = TestNode("TestRoot.Scalar.Int32") } });

        subAdapter!.EmitCallback(CreateSingleItemRawBatch(1, new DataValue(new Variant(new ExtensionObject(new ExpandedNodeId(1), new byte[2048])))));

        await Assert.ThrowsAsync<InvalidOperationException>(() => sub.ReadNextAsync());
        Assert.Equal(OpcUaSubscriptionTerminationReason.QueueOverflow, sub.TerminationReason);

        // Background close triggered by QueueOverflow must delete and dispose the adapter once.
        for (int attempt = 0; attempt < 50 && subAdapter.DisposeCount == 0; attempt++)
        {
            await Task.Delay(10);
        }

        await sub.CloseAsync();
        Assert.Equal(1, subAdapter.DeleteCallCount);
        Assert.Equal(1, subAdapter.DisposeCount);
    }

    [Fact]
    public async Task B11_SubscriptionOptions_EnforcesIec62541LifetimeRatioAndUintOverflow()
    {
        FakeDiscoveryClient discovery = new();
        FakeSessionFactory sessionFactory = new();
        FakeSessionAdapter session = new();
        session.NamespaceUris.Append(OpcUaTestNodeManager.NamespaceUri);
        sessionFactory.OnCreateSession = _ => Task.FromResult<IOpcUaSessionAdapter>(session);

        using OpcUaClient client = new(CreateFakeClientOptions(), discovery, sessionFactory);
        await client.OpenAsync();

        OpcUaMonitoredItemOptions[] items =
        {
            new() { ClientItemKey = "k", NodeId = TestNode("TestRoot.Scalar.Int32") }
        };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.SubscribeAsync(new OpcUaSubscriptionOptions { KeepAliveCount = 5, LifetimeCount = 14 }, items));

        // Must check in ulong arithmetic so KeepAliveCount * 3 cannot silently overflow uint.MaxValue.
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.SubscribeAsync(new OpcUaSubscriptionOptions { KeepAliveCount = uint.MaxValue / 2, LifetimeCount = uint.MaxValue }, items));
    }

    [Fact]
    public async Task B12_SubscribeAsync_EnforcesOperationLimitsMaxMonitoredItemsPerCall()
    {
        FakeDiscoveryClient discovery = new();
        FakeSessionFactory sessionFactory = new();
        FakeSessionAdapter session = new()
        {
            OperationLimits = new OperationLimits { MaxMonitoredItemsPerCall = 2 }
        };
        session.NamespaceUris.Append(OpcUaTestNodeManager.NamespaceUri);
        sessionFactory.OnCreateSession = _ => Task.FromResult<IOpcUaSessionAdapter>(session);

        using OpcUaClient client = new(CreateFakeClientOptions(), discovery, sessionFactory);
        await client.OpenAsync();

        OpcUaMonitoredItemOptions[] tooManyItems =
        {
            new() { ClientItemKey = "a", NodeId = TestNode("TestRoot.Scalar.Int32") },
            new() { ClientItemKey = "b", NodeId = TestNode("TestRoot.Scalar.Double") },
            new() { ClientItemKey = "c", NodeId = TestNode("TestRoot.Scalar.String") }
        };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.SubscribeAsync(new OpcUaSubscriptionOptions(), tooManyItems));
    }

    [Fact]
    public async Task B13_SubscribeAsync_AcrossSessionGenerationBoundary_PreventsHijackAndCleansUpAdapter()
    {
        FakeDiscoveryClient discovery = new();
        FakeSessionFactory sessionFactory = new();
        FakeSessionAdapter session1 = new();
        session1.NamespaceUris.Append(OpcUaTestNodeManager.NamespaceUri);
        FakeSessionAdapter session2 = new();
        session2.NamespaceUris.Append(OpcUaTestNodeManager.NamespaceUri);

        TaskCompletionSource<bool> createMonitoredItemsStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> releaseGen1Setup = new(TaskCreationOptions.RunContinuationsAsynchronously);
        FakeSubscriptionAdapter? gen1SubAdapter = null;

        session1.OnCreateSubscription = (_, cb) =>
        {
            gen1SubAdapter = new FakeSubscriptionAdapter(cb)
            {
                OnBeforeCreateMonitoredItems = async (_, _) =>
                {
                    createMonitoredItemsStarted.TrySetResult(true);
                    await releaseGen1Setup.Task.ConfigureAwait(false);
                }
            };
            return gen1SubAdapter;
        };

        int openCount = 0;
        sessionFactory.OnCreateSession = _ =>
        {
            openCount++;
            return Task.FromResult<IOpcUaSessionAdapter>(openCount == 1 ? session1 : session2);
        };

        using OpcUaClient client = new(CreateFakeClientOptions(), discovery, sessionFactory);
        await client.OpenAsync();

        Task<OpcUaSubscription> gen1SubscribeTask = client.SubscribeAsync(
            new OpcUaSubscriptionOptions(),
            new[] { new OpcUaMonitoredItemOptions { ClientItemKey = "k", NodeId = TestNode("TestRoot.Scalar.Int32") } });

        await createMonitoredItemsStarted.Task;

        // Fault generation 1, close, and reopen generation 2 before gen1SubscribeTask resumes.
        session1.RaiseKeepAlive(StatusCodes.BadSessionClosed);
        await client.CloseAsync();
        await client.OpenAsync();
        Assert.Equal(OpcUaClientState.Open, client.State);

        releaseGen1Setup.TrySetResult(true);

        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => gen1SubscribeTask);
        Assert.NotNull(gen1SubAdapter);

        for (int attempt = 0; attempt < 50 && gen1SubAdapter!.DisposeCount == 0; attempt++)
        {
            await Task.Delay(10);
        }

        Assert.Equal(1, gen1SubAdapter!.DeleteCallCount);
        Assert.Equal(1, gen1SubAdapter.DisposeCount);

        // Generation 2 remains open and unaffected.
        Assert.Equal(OpcUaClientState.Open, client.State);
    }

    [Fact]
    public async Task B14_SubscribeAsync_InitialBatchesDeepCopiedBoundedAndDeliveredInFifoOrder()
    {
        FakeDiscoveryClient discovery = new();
        FakeSessionFactory sessionFactory = new();
        FakeSessionAdapter session = new();
        session.NamespaceUris.Append(OpcUaTestNodeManager.NamespaceUri);

        FakeSubscriptionAdapter? subAdapter = null;
        DataValue callerOwnedInitialValue = new(new Variant(111));

        session.OnCreateSubscription = (_, cb) =>
        {
            subAdapter = new FakeSubscriptionAdapter(cb)
            {
                OnBeforeCreateMonitoredItems = (_, _) =>
                {
                    cb(CreateSingleItemRawBatch(1, callerOwnedInitialValue));
                    callerOwnedInitialValue.WrappedValue = new Variant(999);
                    cb(CreateSingleItemRawBatch(2, new DataValue(new Variant(222))));
                    return Task.CompletedTask;
                }
            };
            return subAdapter;
        };
        sessionFactory.OnCreateSession = _ => Task.FromResult<IOpcUaSessionAdapter>(session);

        using OpcUaClient client = new(CreateFakeClientOptions(), discovery, sessionFactory);
        await client.OpenAsync();

        await using OpcUaSubscription sub = await client.SubscribeAsync(
            new OpcUaSubscriptionOptions { MaxQueuedBatches = 8, MaxQueuedBytes = 1024 * 1024 },
            new[] { new OpcUaMonitoredItemOptions { ClientItemKey = "k", NodeId = TestNode("TestRoot.Scalar.Int32") } });

        subAdapter!.EmitCallback(CreateSingleItemRawBatch(3, new DataValue(new Variant(333))));

        OpcUaDataChangeBatch? batch1 = await sub.ReadNextAsync();
        OpcUaDataChangeBatch? batch2 = await sub.ReadNextAsync();
        OpcUaDataChangeBatch? batch3 = await sub.ReadNextAsync();

        Assert.Equal((uint)1, batch1!.SequenceNumber);
        Assert.Equal(111, batch1.Items[0].Value.Value);
        Assert.Equal((uint)2, batch2!.SequenceNumber);
        Assert.Equal(222, batch2.Items[0].Value.Value);
        Assert.Equal((uint)3, batch3!.SequenceNumber);
        Assert.Equal(333, batch3.Items[0].Value.Value);
    }

    [Fact]
    public async Task B15_SubscribeAsync_InitialBatchOverflow_TerminatesWithQueueOverflow()
    {
        FakeDiscoveryClient discovery = new();
        FakeSessionFactory sessionFactory = new();
        FakeSessionAdapter session = new();
        session.NamespaceUris.Append(OpcUaTestNodeManager.NamespaceUri);

        FakeSubscriptionAdapter? subAdapter = null;
        session.OnCreateSubscription = (_, cb) =>
        {
            subAdapter = new FakeSubscriptionAdapter(cb)
            {
                OnBeforeCreateMonitoredItems = (_, _) =>
                {
                    // Emit 5 initial batches when MaxQueuedBatches = 2; initial queue must stay bounded and overflow.
                    for (uint seq = 1; seq <= 5; seq++)
                    {
                        cb(CreateSingleItemRawBatch(seq, new DataValue(new Variant((int)seq))));
                    }

                    return Task.CompletedTask;
                }
            };
            return subAdapter;
        };
        sessionFactory.OnCreateSession = _ => Task.FromResult<IOpcUaSessionAdapter>(session);

        using OpcUaClient client = new(CreateFakeClientOptions(), discovery, sessionFactory);
        await client.OpenAsync();

        OpcUaSubscription sub = await client.SubscribeAsync(
            new OpcUaSubscriptionOptions { MaxQueuedBatches = 2, MaxQueuedBytes = 1024 * 1024 },
            new[] { new OpcUaMonitoredItemOptions { ClientItemKey = "k", NodeId = TestNode("TestRoot.Scalar.Int32") } });

        Assert.Equal(OpcUaSubscriptionTerminationReason.QueueOverflow, sub.TerminationReason);
        await Assert.ThrowsAsync<InvalidOperationException>(() => sub.ReadNextAsync());

        for (int attempt = 0; attempt < 50 && subAdapter!.DisposeCount == 0; attempt++)
        {
            await Task.Delay(10);
        }

        await sub.CloseAsync();
        Assert.Equal(1, subAdapter!.DeleteCallCount);
        Assert.Equal(1, subAdapter.DisposeCount);
    }

    private static OpcUaRawDataChangeBatch CreateSingleItemRawBatch(uint sequenceNumber, DataValue dataValue)
    {
        return new OpcUaRawDataChangeBatch(
            sequenceNumber,
            DateTimeOffset.UtcNow,
            new[] { new OpcUaRawDataChangeItem(1, dataValue) });
    }

    private static OpcUaClientOptions CreateFakeClientOptions()
    {
        return new OpcUaClientOptions
        {
            EndpointUrl = new Uri("opc.tcp://127.0.0.1:4840"),
            MessageSecurityMode = MessageSecurityMode.None,
            AllowedSecurityPolicies = new[] { SecurityPolicies.None },
            MaxKeepAliveFailuresBeforeFault = 1,
            Certificates = new OpcUa.Security.OpcUaCertificateOptions
            {
                ApplicationCertificateStorePath = "own",
                TrustedPeerCertificatesStorePath = "trusted",
                TrustedIssuerCertificatesStorePath = "issuer",
                RejectedCertificateStorePath = "rejected"
            }
        };
    }
}
