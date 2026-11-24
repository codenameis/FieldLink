using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
        using OpcUaTestServer server = new();
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
        using OpcUaTestServer server = new();
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
        using OpcUaTestServer server = new();
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
