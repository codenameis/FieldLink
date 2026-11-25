using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.OpcUa.Services;
using FieldLink.OpcUa.Tests.Fakes;
using FieldLink.OpcUa.Tests.Fixtures;
using FieldLink.OpcUa.Transport;
using Opc.Ua;
using Xunit;

namespace FieldLink.OpcUa.Tests.Services;

public sealed class ServiceTests
{
    private static ExpandedNodeId TestNode(string id) => new(id, OpcUaTestNodeManager.NamespaceUri);

    [Fact]
    public async Task D01_ExpandedNodeIdWithNamespaceUri_ResolvesAcrossDifferentSessionNamespaceIndexes()
    {
        FakeDiscoveryClient discovery = new();
        FakeSessionFactory sessionFactory = new();
        FakeSessionAdapter session1 = new();
        session1.NamespaceUris.Append("urn:other:one");
        session1.NamespaceUris.Append(OpcUaTestNodeManager.NamespaceUri); // index 2

        FakeSessionAdapter session2 = new();
        session2.NamespaceUris.Append("urn:other:one");
        session2.NamespaceUris.Append("urn:other:two");
        session2.NamespaceUris.Append(OpcUaTestNodeManager.NamespaceUri); // index 3

        ushort observedIndex1 = 0;
        ushort observedIndex2 = 0;
        session1.OnRead = (_, nodes, _) =>
        {
            observedIndex1 = nodes[0].NodeId.NamespaceIndex;
            return Task.FromResult(new ReadResponse { Results = new DataValueCollection { new DataValue(new Variant(11)) } });
        };
        session2.OnRead = (_, nodes, _) =>
        {
            observedIndex2 = nodes[0].NodeId.NamespaceIndex;
            return Task.FromResult(new ReadResponse { Results = new DataValueCollection { new DataValue(new Variant(22)) } });
        };

        int creation = 0;
        sessionFactory.OnCreateSession = _ =>
            Task.FromResult<IOpcUaSessionAdapter>(Interlocked.Increment(ref creation) == 1 ? session1 : session2);

        using OpcUaClient client = new(CreateFakeClientOptions(), discovery, sessionFactory);
        ExpandedNodeId target = TestNode("TestRoot.Scalar.Int32");

        await client.OpenAsync();
        DataValue first = await client.ReadValueAsync(target);
        await client.CloseAsync();

        await client.OpenAsync();
        DataValue second = await client.ReadValueAsync(target);
        await client.CloseAsync();

        Assert.Equal((ushort)2, observedIndex1);
        Assert.Equal((ushort)3, observedIndex2);
        Assert.Equal(11, first.Value);
        Assert.Equal(22, second.Value);
    }

    [Fact]
    public async Task D02_UnregisteredNamespaceUriOrNonZeroServerIndexOrNullNodeId_RejectedBeforeSend()
    {
        FakeDiscoveryClient discovery = new();
        FakeSessionFactory sessionFactory = new();
        FakeSessionAdapter session = new();
        int readCalls = 0;
        session.OnRead = (_, _, _) =>
        {
            readCalls++;
            return Task.FromResult(new ReadResponse { Results = new DataValueCollection() });
        };
        sessionFactory.OnCreateSession = _ => Task.FromResult<IOpcUaSessionAdapter>(session);

        using OpcUaClient client = new(CreateFakeClientOptions(), discovery, sessionFactory);
        await client.OpenAsync();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.ReadValueAsync(new ExpandedNodeId("SomeNode", "urn:unknown:namespace")));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.ReadValueAsync(new ExpandedNodeId("SomeNode", 0, null, 1)));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.ReadValueAsync(ExpandedNodeId.Null));

        Assert.Equal(0, readCalls);
    }

    [Fact]
    public async Task D03_ScalarAndOneDimensionalArrays_ReadAndWritePreserveValuesAndTimestamps()
    {
        await using OpcUaTestServer server = new();
        await server.StartServerAsync();

        using TestClientCertificateSetup clientSetup = new(server.RootDirectory);
        await clientSetup.TrustServerAsync(server);

        using OpcUaClient client = new(clientSetup.CreateClientOptions(server.EndpointUrl));
        await client.OpenAsync();

        StatusCode[] writeStatuses =
        {
            await client.WriteValueAsync(new OpcUaWriteRequest(TestNode("TestRoot.Scalar.Boolean"), new Variant(false))),
            await client.WriteValueAsync(new OpcUaWriteRequest(TestNode("TestRoot.Scalar.Int32"), new Variant(108))),
            await client.WriteValueAsync(new OpcUaWriteRequest(TestNode("TestRoot.Scalar.Double"), new Variant(9.25))),
            await client.WriteValueAsync(new OpcUaWriteRequest(TestNode("TestRoot.Scalar.String"), new Variant("updated"))),
            await client.WriteValueAsync(new OpcUaWriteRequest(TestNode("TestRoot.Array.Int32"), new Variant(new[] { 10, 20, 30, 40 }))),
            await client.WriteValueAsync(new OpcUaWriteRequest(TestNode("TestRoot.Array.Bytes"), new Variant(new byte[] { 1, 2, 3, 4, 5 })))
        };

        Assert.All(writeStatuses, static status => Assert.True(StatusCode.IsGood(status)));

        IReadOnlyList<DataValue> values = await client.ReadValuesAsync(new[]
        {
            TestNode("TestRoot.Scalar.Boolean"),
            TestNode("TestRoot.Scalar.Int32"),
            TestNode("TestRoot.Scalar.Double"),
            TestNode("TestRoot.Scalar.String"),
            TestNode("TestRoot.Array.Int32"),
            TestNode("TestRoot.Array.Bytes")
        });

        Assert.Equal(6, values.Count);
        Assert.Equal(false, values[0].Value);
        Assert.Equal(108, values[1].Value);
        Assert.Equal(9.25, values[2].Value);
        Assert.Equal("updated", values[3].Value);
        Assert.Equal(new[] { 10, 20, 30, 40 }, (int[])values[4].Value);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, (byte[])values[5].Value);

        Assert.All(values, static dv =>
        {
            Assert.True(StatusCode.IsGood(dv.StatusCode));
            Assert.NotEqual(DateTime.MinValue, dv.SourceTimestamp);
            Assert.NotEqual(DateTime.MinValue, dv.ServerTimestamp);
        });
    }

    [Fact]
    public async Task D04_BatchReadWithMixedQualities_PreservesOrderAndQualityWithoutDropping()
    {
        await using OpcUaTestServer server = new();
        await server.StartServerAsync();

        using TestClientCertificateSetup clientSetup = new(server.RootDirectory);
        await clientSetup.TrustServerAsync(server);

        using OpcUaClient client = new(clientSetup.CreateClientOptions(server.EndpointUrl));
        await client.OpenAsync();

        IReadOnlyList<DataValue> results = await client.ReadValuesAsync(new[]
        {
            TestNode("TestRoot.Scalar.Int32"),
            TestNode("TestRoot.DoesNotExist"),
            TestNode("TestRoot.Special.UncertainInt32"),
            TestNode("TestRoot.Special.BadInt32")
        });

        Assert.Equal(4, results.Count);
        Assert.True(StatusCode.IsGood(results[0].StatusCode));
        Assert.Equal(42, results[0].Value);
        Assert.Equal(StatusCodes.BadNodeIdUnknown, results[1].StatusCode);
        Assert.Equal(StatusCodes.UncertainSensorNotAccurate, results[2].StatusCode);
        Assert.Equal(123, results[2].Value);
        Assert.Equal(StatusCodes.BadWaitingForInitialData, results[3].StatusCode);
    }

    [Fact]
    public async Task D04_D05_OperationLimitsExceeded_RejectedBeforeSendingWithoutAutoSplit()
    {
        FakeDiscoveryClient discovery = new();
        FakeSessionFactory sessionFactory = new();
        FakeSessionAdapter session = new()
        {
            OperationLimits = new OperationLimits
            {
                MaxNodesPerRead = 2,
                MaxNodesPerWrite = 2
            }
        };
        session.NamespaceUris.Append(OpcUaTestNodeManager.NamespaceUri);

        int readCalls = 0;
        session.OnRead = (_, _, _) =>
        {
            readCalls++;
            return Task.FromResult(new ReadResponse());
        };
        sessionFactory.OnCreateSession = _ => Task.FromResult<IOpcUaSessionAdapter>(session);

        using OpcUaClient client = new(CreateFakeClientOptions(), discovery, sessionFactory);
        await client.OpenAsync();

        ExpandedNodeId node = TestNode("TestRoot.Scalar.Int32");
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.ReadValuesAsync(new[] { node, node, node }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.WriteValuesAsync(new[]
            {
                new OpcUaWriteRequest(node, new Variant(1)),
                new OpcUaWriteRequest(node, new Variant(2)),
                new OpcUaWriteRequest(node, new Variant(3))
            }));

        Assert.Equal(0, readCalls);
        Assert.Equal(0, session.WriteCallCount);
    }

    [Fact]
    public async Task D05_BatchWriteWithPartialSuccess_PreservesOrderAndStatusCodes()
    {
        await using OpcUaTestServer server = new();
        await server.StartServerAsync();

        using TestClientCertificateSetup clientSetup = new(server.RootDirectory);
        await clientSetup.TrustServerAsync(server);

        using OpcUaClient client = new(clientSetup.CreateClientOptions(server.EndpointUrl));
        await client.OpenAsync();

        IReadOnlyList<StatusCode> results = await client.WriteValuesAsync(new[]
        {
            new OpcUaWriteRequest(TestNode("TestRoot.Scalar.Int32"), new Variant(555)),
            new OpcUaWriteRequest(TestNode("TestRoot.Special.ReadOnlyInt32"), new Variant(100)),
            new OpcUaWriteRequest(TestNode("TestRoot.Scalar.Int32"), new Variant("wrong-type"))
        });

        Assert.Equal(3, results.Count);
        Assert.True(StatusCode.IsGood(results[0]));
        Assert.Equal(StatusCodes.BadNotWritable, results[1]);
        Assert.Equal(StatusCodes.BadTypeMismatch, results[2]);

        DataValue updated = await client.ReadValueAsync(TestNode("TestRoot.Scalar.Int32"));
        Assert.Equal(555, updated.Value);
    }

    [Fact]
    public async Task D05_GoodCompletesAsynchronously_PreservedWithoutDowngrade()
    {
        FakeDiscoveryClient discovery = new();
        FakeSessionFactory sessionFactory = new();
        FakeSessionAdapter session = new();
        session.NamespaceUris.Append(OpcUaTestNodeManager.NamespaceUri);
        session.OnWrite = (_, _, _) => Task.FromResult(new WriteResponse
        {
            Results = new StatusCodeCollection { StatusCodes.GoodCompletesAsynchronously }
        });
        sessionFactory.OnCreateSession = _ => Task.FromResult<IOpcUaSessionAdapter>(session);

        using OpcUaClient client = new(CreateFakeClientOptions(), discovery, sessionFactory);
        await client.OpenAsync();

        StatusCode status = await client.WriteValueAsync(
            new OpcUaWriteRequest(TestNode("TestRoot.Scalar.Int32"), new Variant(1)));
        Assert.Equal(StatusCodes.GoodCompletesAsynchronously, status);
    }

    [Fact]
    public async Task D06_SlowWriteTimeout_ThrowsTimeoutExceptionWithExactlyOneServerExecution()
    {
        await using OpcUaTestServer server = new();
        await server.StartServerAsync();
        server.NodeManager.ResetCounters();
        server.NodeManager.SetSlowWriteDelay(TimeSpan.FromMilliseconds(400));

        using TestClientCertificateSetup clientSetup = new(server.RootDirectory);
        await clientSetup.TrustServerAsync(server);

        using OpcUaClient client = new(clientSetup.CreateClientOptions(server.EndpointUrl));
        await client.OpenAsync();

        await Assert.ThrowsAsync<TimeoutException>(() => client.WriteValueAsync(
            new OpcUaWriteRequest(TestNode("TestRoot.Special.SlowWriteInt32"), new Variant(77)),
            timeout: TimeSpan.FromMilliseconds(100)));

        await Task.Delay(500);
        Assert.Equal(1, server.NodeManager.WriteExecutionCount);
    }

    [Fact]
    public async Task D07_MultiPageBrowseOnLoopbackAndEmptyPageWithContinuationPoint_CollectsAllReferences()
    {
        await using OpcUaTestServer server = new();
        await server.StartServerAsync();

        using TestClientCertificateSetup clientSetup = new(server.RootDirectory);
        await clientSetup.TrustServerAsync(server);

        using OpcUaClient client = new(clientSetup.CreateClientOptions(server.EndpointUrl));
        await client.OpenAsync();

        IReadOnlyList<ReferenceDescription> loopbackRefs = await client.BrowseAsync(
            TestNode("TestRoot.BrowseFolder"),
            new OpcUaBrowseOptions { MaxReferencesPerPage = 2 });

        Assert.Equal(25, loopbackRefs.Count);
        Assert.Equal("Item00", loopbackRefs[0].BrowseName.Name);
        Assert.Equal("Item24", loopbackRefs[24].BrowseName.Name);

        // 빈 페이지가 반환되더라도 ContinuationPoint가 있으면 계속 탐색해야 한다.
        FakeDiscoveryClient discovery = new();
        FakeSessionFactory sessionFactory = new();
        FakeSessionAdapter session = new();
        session.NamespaceUris.Append(OpcUaTestNodeManager.NamespaceUri);

        session.OnBrowse = (_, _, _, _) => Task.FromResult(new BrowseResponse
        {
            Results = new BrowseResultCollection
            {
                new BrowseResult
                {
                    StatusCode = StatusCodes.Good,
                    ContinuationPoint = new byte[] { 1 },
                    References = new ReferenceDescriptionCollection
                    {
                        new ReferenceDescription { BrowseName = new QualifiedName("A") },
                        new ReferenceDescription { BrowseName = new QualifiedName("B") }
                    }
                }
            }
        });

        int nextPage = 0;
        session.OnBrowseNext = (_, release, _, _) =>
        {
            if (release)
            {
                return Task.FromResult(new BrowseNextResponse { Results = new BrowseResultCollection { new BrowseResult() } });
            }

            nextPage++;
            if (nextPage == 1)
            {
                return Task.FromResult(new BrowseNextResponse
                {
                    Results = new BrowseResultCollection
                    {
                        new BrowseResult
                        {
                            StatusCode = StatusCodes.Good,
                            ContinuationPoint = new byte[] { 2 },
                            References = new ReferenceDescriptionCollection()
                        }
                    }
                });
            }

            return Task.FromResult(new BrowseNextResponse
            {
                Results = new BrowseResultCollection
                {
                    new BrowseResult
                    {
                        StatusCode = StatusCodes.Good,
                        References = new ReferenceDescriptionCollection
                        {
                            new ReferenceDescription { BrowseName = new QualifiedName("C") }
                        }
                    }
                }
            });
        };
        sessionFactory.OnCreateSession = _ => Task.FromResult<IOpcUaSessionAdapter>(session);

        using OpcUaClient fakeClient = new(CreateFakeClientOptions(), discovery, sessionFactory);
        await fakeClient.OpenAsync();
        IReadOnlyList<ReferenceDescription> fakeRefs = await fakeClient.BrowseAsync(TestNode("TestRoot.BrowseFolder"));
        Assert.Equal(new[] { "A", "B", "C" }, fakeRefs.Select(static r => r.BrowseName.Name).ToArray());
    }

    [Fact]
    public async Task D08_BrowseExceedsCapOrCanceled_FailsExplicitlyAndReleasesContinuationPoint()
    {
        FakeDiscoveryClient discovery = new();
        FakeSessionFactory sessionFactory = new();
        FakeSessionAdapter session = new();
        session.NamespaceUris.Append(OpcUaTestNodeManager.NamespaceUri);

        session.OnBrowse = (_, _, _, _) => Task.FromResult(new BrowseResponse
        {
            Results = new BrowseResultCollection
            {
                new BrowseResult
                {
                    StatusCode = StatusCodes.Good,
                    ContinuationPoint = new byte[] { 9 },
                    References = new ReferenceDescriptionCollection
                    {
                        new ReferenceDescription { BrowseName = new QualifiedName("One") },
                        new ReferenceDescription { BrowseName = new QualifiedName("Two") }
                    }
                }
            }
        });

        CancellationTokenSource cancelSource = new();
        session.OnBrowseNext = (_, release, _, _) =>
        {
            if (release)
            {
                return Task.FromResult(new BrowseNextResponse { Results = new BrowseResultCollection { new BrowseResult() } });
            }

            cancelSource.Cancel();
            return Task.FromResult(new BrowseNextResponse
            {
                Results = new BrowseResultCollection
                {
                    new BrowseResult
                    {
                        StatusCode = StatusCodes.Good,
                        ContinuationPoint = new byte[] { 10 },
                        References = new ReferenceDescriptionCollection
                        {
                            new ReferenceDescription { BrowseName = new QualifiedName("Three") }
                        }
                    }
                }
            });
        };
        sessionFactory.OnCreateSession = _ => Task.FromResult<IOpcUaSessionAdapter>(session);

        using OpcUaClient fakeClient = new(CreateFakeClientOptions(), discovery, sessionFactory);
        await fakeClient.OpenAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fakeClient.BrowseAsync(TestNode("TestRoot.BrowseFolder"), cancellationToken: cancelSource.Token));
        Assert.Equal(1, session.BrowseNextReleaseCallCount);

        await using OpcUaTestServer server = new();
        await server.StartServerAsync();
        using TestClientCertificateSetup clientSetup = new(server.RootDirectory);
        await clientSetup.TrustServerAsync(server);

        using OpcUaClient realClient = new(clientSetup.CreateClientOptions(server.EndpointUrl));
        await realClient.OpenAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            realClient.BrowseAsync(
                TestNode("TestRoot.BrowseFolder"),
                new OpcUaBrowseOptions { MaxReferencesPerPage = 2, MaxTotalReferences = 5 }));
    }

    [Fact]
    public async Task D09_MethodCall_SuccessFailureAndTimeoutNeverRetries()
    {
        await using OpcUaTestServer server = new();
        await server.StartServerAsync();
        server.NodeManager.ResetCounters();

        using TestClientCertificateSetup clientSetup = new(server.RootDirectory);
        await clientSetup.TrustServerAsync(server);

        using OpcUaClient client = new(clientSetup.CreateClientOptions(server.EndpointUrl));
        await client.OpenAsync();

        ExpandedNodeId methodsObject = TestNode("TestRoot.Methods");

        CallMethodResult addResult = await client.CallAsync(
            methodsObject,
            TestNode("TestRoot.Methods.Add"),
            new Variant[] { new(15), new(27) });
        Assert.True(StatusCode.IsGood(addResult.StatusCode));
        Assert.Single(addResult.OutputArguments);
        Assert.Equal(42, addResult.OutputArguments[0].Value);

        CallMethodResult invalidArgResult = await client.CallAsync(
            methodsObject,
            TestNode("TestRoot.Methods.Add"),
            new Variant[] { new(15) });
        Assert.True(StatusCode.IsBad(invalidArgResult.StatusCode));

        CallMethodResult deniedResult = await client.CallAsync(
            methodsObject,
            TestNode("TestRoot.Methods.DenyAccess"),
            Array.Empty<Variant>());
        Assert.Equal(StatusCodes.BadUserAccessDenied, deniedResult.StatusCode);

        server.NodeManager.ResetCounters();
        server.NodeManager.SetSlowMethodDelay(TimeSpan.FromMilliseconds(400));

        await Assert.ThrowsAsync<TimeoutException>(() =>
            client.CallAsync(
                methodsObject,
                TestNode("TestRoot.Methods.SlowExecute"),
                new Variant[] { new(5) },
                timeout: TimeSpan.FromMilliseconds(100)));

        await Task.Delay(500);
        Assert.Equal(1, server.NodeManager.MethodExecutionCount);
    }

    [Fact]
    public async Task D10_BrowseAsync_SkipsNullReferencesAndCountsInitialEmptyPageTowardCap()
    {
        FakeDiscoveryClient discovery = new();
        FakeSessionFactory sessionFactory = new();
        FakeSessionAdapter session = new();
        session.NamespaceUris.Append(OpcUaTestNodeManager.NamespaceUri);

        session.OnBrowse = (_, _, _, _) => Task.FromResult(new BrowseResponse
        {
            Results = new BrowseResultCollection
            {
                new BrowseResult
                {
                    StatusCode = StatusCodes.Good,
                    References = new ReferenceDescriptionCollection
                    {
                        null!,
                        new ReferenceDescription { BrowseName = new QualifiedName("ValidEntry") },
                        null!
                    }
                }
            }
        });
        sessionFactory.OnCreateSession = _ => Task.FromResult<IOpcUaSessionAdapter>(session);

        using OpcUaClient client = new(CreateFakeClientOptions(), discovery, sessionFactory);
        await client.OpenAsync();

        IReadOnlyList<ReferenceDescription> filteredReferences = await client.BrowseAsync(TestNode("TestRoot.BrowseFolder"));
        ReferenceDescription single = Assert.Single(filteredReferences);
        Assert.Equal("ValidEntry", single.BrowseName.Name);

        int browseNextCalls = 0;
        session.OnBrowse = (_, _, _, _) => Task.FromResult(new BrowseResponse
        {
            Results = new BrowseResultCollection
            {
                new BrowseResult
                {
                    StatusCode = StatusCodes.Good,
                    ContinuationPoint = new byte[] { 1 },
                    References = new ReferenceDescriptionCollection()
                }
            }
        });
        session.OnBrowseNext = (_, release, _, _) =>
        {
            if (release)
            {
                return Task.FromResult(new BrowseNextResponse { Results = new BrowseResultCollection { new BrowseResult() } });
            }

            browseNextCalls++;
            return Task.FromResult(new BrowseNextResponse
            {
                Results = new BrowseResultCollection
                {
                    new BrowseResult
                    {
                        StatusCode = StatusCodes.Good,
                        ContinuationPoint = new byte[] { 2 },
                        References = new ReferenceDescriptionCollection()
                    }
                }
            });
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.BrowseAsync(TestNode("TestRoot.BrowseFolder")));
        // Initial empty page counts as 1, so cap of 64 triggers on the 63rd BrowseNext call.
        Assert.Equal(63, browseNextCalls);
        Assert.True(session.BrowseNextReleaseCallCount >= 1);
    }

    [Fact]
    public async Task D11_ReadAndWriteValuesAsync_SnapshotCallerListsBeforeWaitingOnConcurrencySemaphore()
    {
        FakeDiscoveryClient discovery = new();
        FakeSessionFactory sessionFactory = new();
        FakeSessionAdapter session = new();
        session.NamespaceUris.Append(OpcUaTestNodeManager.NamespaceUri);

        TaskCompletionSource<bool> firstReadStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<ReadResponse> releaseFirstRead = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int readInvocationCount = 0;
        int capturedSecondReadNodeCount = 0;
        int capturedWriteNodeCount = 0;

        session.OnRead = (_, nodesToRead, _) =>
        {
            int invocation = Interlocked.Increment(ref readInvocationCount);
            if (invocation == 1)
            {
                firstReadStarted.TrySetResult(true);
                return releaseFirstRead.Task;
            }

            capturedSecondReadNodeCount = nodesToRead.Count;
            DataValueCollection values = new();
            for (int i = 0; i < nodesToRead.Count; i++)
            {
                values.Add(new DataValue(new Variant(42)));
            }

            return Task.FromResult(new ReadResponse { Results = values });
        };

        session.OnWrite = (_, nodesToWrite, _) =>
        {
            capturedWriteNodeCount = nodesToWrite.Count;
            StatusCodeCollection statuses = new();
            for (int i = 0; i < nodesToWrite.Count; i++)
            {
                statuses.Add(StatusCodes.Good);
            }

            return Task.FromResult(new WriteResponse { Results = statuses });
        };

        sessionFactory.OnCreateSession = _ => Task.FromResult<IOpcUaSessionAdapter>(session);

        OpcUaClientOptions singleConcurrencyOptions = new()
        {
            EndpointUrl = new Uri("opc.tcp://127.0.0.1:4840"),
            MessageSecurityMode = MessageSecurityMode.None,
            AllowedSecurityPolicies = new[] { SecurityPolicies.None },
            MaxConcurrentOperations = 1,
            Certificates = CreateFakeClientOptions().Certificates
        };

        using OpcUaClient client = new(singleConcurrencyOptions, discovery, sessionFactory);
        await client.OpenAsync();

        Task<DataValue> blockingReadTask = client.ReadValueAsync(TestNode("TestRoot.Scalar.Int32"));
        await firstReadStarted.Task;

        List<ExpandedNodeId> mutableReadNodes = new() { TestNode("TestRoot.Scalar.Int32") };
        Task<IReadOnlyList<DataValue>> queuedReadTask = client.ReadValuesAsync(mutableReadNodes);
        mutableReadNodes.Clear();

        List<OpcUaWriteRequest> mutableWrites = new()
        {
            new OpcUaWriteRequest(TestNode("TestRoot.Scalar.Int32"), new DataValue(new Variant(77)))
        };
        Task<IReadOnlyList<StatusCode>> queuedWriteTask = client.WriteValuesAsync(mutableWrites);
        mutableWrites.Clear();

        releaseFirstRead.SetResult(new ReadResponse { Results = new DataValueCollection { new DataValue(new Variant(1)) } });

        await blockingReadTask;
        IReadOnlyList<DataValue> readResults = await queuedReadTask;
        IReadOnlyList<StatusCode> writeResults = await queuedWriteTask;

        Assert.Equal(1, capturedSecondReadNodeCount);
        Assert.Single(readResults);
        Assert.Equal(1, capturedWriteNodeCount);
        Assert.Single(writeResults);
    }

    private static OpcUaClientOptions CreateFakeClientOptions()
    {
        return new OpcUaClientOptions
        {
            EndpointUrl = new Uri("opc.tcp://127.0.0.1:4840"),
            MessageSecurityMode = MessageSecurityMode.None,
            AllowedSecurityPolicies = new[] { SecurityPolicies.None },
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
