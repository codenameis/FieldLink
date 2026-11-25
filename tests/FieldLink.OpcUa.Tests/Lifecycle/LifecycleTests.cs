using System;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.OpcUa.Tests.Fakes;
using FieldLink.OpcUa.Tests.Fixtures;
using FieldLink.OpcUa.Transport;
using Opc.Ua;
using Xunit;

namespace FieldLink.OpcUa.Tests.Lifecycle;

public sealed class LifecycleTests
{
    private static readonly ExpandedNodeId ScalarIntNode =
        new("TestRoot.Scalar.Int32", OpcUaTestNodeManager.NamespaceUri);

    [Fact]
    public async Task L01_RepeatedOpenReadCloseCycles_SucceedCleanly()
    {
        await using OpcUaTestServer server = new();
        await server.StartServerAsync();

        using TestClientCertificateSetup clientSetup = new(server.RootDirectory);
        await clientSetup.TrustServerAsync(server);

        using OpcUaClient client = new(clientSetup.CreateClientOptions(server.EndpointUrl));

        for (int cycle = 0; cycle < 2; cycle++)
        {
            await client.OpenAsync();
            Assert.Equal(OpcUaClientState.Open, client.State);

            DataValue value = await client.ReadValueAsync(ScalarIntNode);
            Assert.True(StatusCode.IsGood(value.StatusCode));
            Assert.Equal(42, value.Value);

            await client.CloseAsync();
            Assert.Equal(OpcUaClientState.Closed, client.State);
        }
    }

    [Fact]
    public async Task L02_OpenCanceledOrRacedWithCloseAsync_CleansUpSessionAndReturnsClosed()
    {
        FakeDiscoveryClient discovery = new();
        FakeSessionFactory sessionFactory = new();
        FakeSessionAdapter createdSession = new();
        TaskCompletionSource<bool> createStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> releaseCreate = new(TaskCreationOptions.RunContinuationsAsynchronously);

        sessionFactory.OnCreateSession = async ct =>
        {
            createStarted.TrySetResult(true);
            await releaseCreate.Task;
            return createdSession;
        };

        OpcUaClientOptions options = CreateFakeClientOptions(discoveryTimeoutSeconds: 10);
        using OpcUaClient client = new(options, discovery, sessionFactory);

        Task openTask = client.OpenAsync();
        await createStarted.Task;
        Assert.Equal(OpcUaClientState.Opening, client.State);

        Task closeTask = client.CloseAsync();
        releaseCreate.TrySetResult(true);

        await closeTask;
        await Assert.ThrowsAnyAsync<Exception>(() => openTask);

        Assert.Equal(OpcUaClientState.Closed, client.State);
        Assert.Equal(1, createdSession.DisposeCount);
    }

    [Fact]
    public async Task L03_DuplicateOpenWhileOpenOrOpening_ThrowsInvalidOperationException()
    {
        FakeDiscoveryClient discovery = new();
        FakeSessionFactory sessionFactory = new();
        TaskCompletionSource<bool> gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> started = new(TaskCreationOptions.RunContinuationsAsynchronously);

        sessionFactory.OnCreateSession = async ct =>
        {
            started.TrySetResult(true);
            await gate.Task;
            return new FakeSessionAdapter();
        };

        using OpcUaClient client = new(CreateFakeClientOptions(), discovery, sessionFactory);
        Task firstOpen = client.OpenAsync();
        await started.Task;

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.OpenAsync());
        gate.TrySetResult(true);
        await firstOpen;

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.OpenAsync());
        Assert.Equal(OpcUaClientState.Open, client.State);
        Assert.Equal(1, sessionFactory.CreateCallCount);
    }

    [Fact]
    public async Task L04_DataRequestsWhenNotOpen_RejectedImmediately()
    {
        FakeDiscoveryClient discovery = new();
        FakeSessionFactory sessionFactory = new();
        FakeSessionAdapter fakeSession = new();
        sessionFactory.OnCreateSession = _ => Task.FromResult<IOpcUaSessionAdapter>(fakeSession);

        using OpcUaClient client = new(CreateFakeClientOptions(), discovery, sessionFactory);
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.ReadValueAsync(ScalarIntNode));

        await client.OpenAsync();
        fakeSession.RaiseKeepAlive(StatusCodes.BadSessionClosed);
        Assert.Equal(OpcUaClientState.Faulted, client.State);

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.ReadValueAsync(ScalarIntNode));
        await client.CloseAsync();
        Assert.Equal(OpcUaClientState.Closed, client.State);
    }

    [Fact]
    public async Task L05_AllCallsAfterDispose_ThrowObjectDisposedException()
    {
        FakeDiscoveryClient discovery = new();
        FakeSessionFactory sessionFactory = new();
        using OpcUaClient client = new(CreateFakeClientOptions(), discovery, sessionFactory);
        await client.OpenAsync();

        client.Dispose();
        client.Dispose();

        Assert.Equal(OpcUaClientState.Disposed, client.State);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.OpenAsync());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.ReadValueAsync(ScalarIntNode));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.CloseAsync());
    }

    [Fact]
    public async Task L06_ServerForcesSessionClosure_FaultsWithoutAutoReconnectAndRecoversAfterExplicitClose()
    {
        await using OpcUaTestServer server = new();
        await server.StartServerAsync();

        using TestClientCertificateSetup clientSetup = new(server.RootDirectory);
        await clientSetup.TrustServerAsync(server);

        OpcUaClientOptions options = new()
        {
            EndpointUrl = server.EndpointUrl,
            ApplicationName = clientSetup.ApplicationName,
            ApplicationUri = clientSetup.ApplicationUri,
            Certificates = clientSetup.ToCertificateOptions(),
            KeepAliveIntervalMilliseconds = 250,
            MaxKeepAliveFailuresBeforeFault = 1
        };

        using OpcUaClient client = new(options);
        await client.OpenAsync();
        server.ForceCloseAllSessions();

        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        while (client.State == OpcUaClientState.Open && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50);
        }

        Assert.Equal(OpcUaClientState.Faulted, client.State);
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.OpenAsync());

        await client.CloseAsync();
        Assert.Equal(OpcUaClientState.Closed, client.State);

        await client.OpenAsync();
        Assert.Equal(OpcUaClientState.Open, client.State);
        DataValue readResult = await client.ReadValueAsync(ScalarIntNode);
        Assert.Equal(42, readResult.Value);
    }

    [Fact]
    public async Task L07_QueuedConcurrentRequestsDuringCloseAndReopen_NeverLeakAcrossSessionGenerations()
    {
        FakeDiscoveryClient discovery = new();
        FakeSessionFactory sessionFactory = new();
        FakeSessionAdapter firstSession = new();
        FakeSessionAdapter secondSession = new();
        int generationIndex = 0;

        TaskCompletionSource<bool> firstTwoEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> releaseFirstTwo = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int enteredCount = 0;

        firstSession.NamespaceUris.Append(OpcUaTestNodeManager.NamespaceUri);
        firstSession.OnRead = async (_, nodes, _) =>
        {
            if (Interlocked.Increment(ref enteredCount) == 2)
            {
                firstTwoEntered.TrySetResult(true);
            }

            await releaseFirstTwo.Task;
            return new ReadResponse
            {
                Results = new DataValueCollection { new DataValue(1) }
            };
        };

        int secondSessionReadCalls = 0;
        secondSession.NamespaceUris.Append(OpcUaTestNodeManager.NamespaceUri);
        secondSession.OnRead = (_, _, _) =>
        {
            Interlocked.Increment(ref secondSessionReadCalls);
            return Task.FromResult(new ReadResponse { Results = new DataValueCollection { new DataValue(2) } });
        };

        sessionFactory.OnCreateSession = _ =>
        {
            int current = Interlocked.Increment(ref generationIndex);
            return Task.FromResult<IOpcUaSessionAdapter>(current == 1 ? firstSession : secondSession);
        };

        OpcUaClientOptions options = CreateFakeClientOptions(maxConcurrentOperations: 2);
        using OpcUaClient client = new(options, discovery, sessionFactory);
        await client.OpenAsync();

        Task<DataValue>[] batch =
        {
            client.ReadValueAsync(ScalarIntNode),
            client.ReadValueAsync(ScalarIntNode),
            client.ReadValueAsync(ScalarIntNode),
            client.ReadValueAsync(ScalarIntNode),
            client.ReadValueAsync(ScalarIntNode)
        };

        await firstTwoEntered.Task;
        await Task.Delay(50);

        await client.CloseAsync();
        await client.OpenAsync();
        releaseFirstTwo.TrySetResult(true);

        await Task.WhenAll(Array.ConvertAll(batch, static async task =>
        {
            try
            {
                await task;
            }
            catch (Exception)
            {
            }
        }));

        // Close/Reopen 중 대기하던 generation 1 요청 3건은 새 세션(secondSession)으로 절대 누수되지 않아야 한다.
        Assert.Equal(0, secondSessionReadCalls);
        for (int i = 2; i < batch.Length; i++)
        {
            Assert.True(batch[i].IsFaulted || batch[i].IsCanceled);
        }
    }

    [Fact]
    public async Task L08_SlowServerResponseExceedsDeadline_ThrowsTimeoutAndObservesOrphanedTask()
    {
        Exception? unobservedException = null;
        void OnUnobserved(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            unobservedException = e.Exception;
            e.SetObserved();
        }

        TaskScheduler.UnobservedTaskException += OnUnobserved;
        try
        {
            FakeDiscoveryClient discovery = new();
            FakeSessionFactory sessionFactory = new();
            FakeSessionAdapter session = new();
            session.NamespaceUris.Append(OpcUaTestNodeManager.NamespaceUri);

            TaskCompletionSource<ReadResponse> delayedSdkTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            session.OnRead = (_, _, _) => delayedSdkTcs.Task;
            sessionFactory.OnCreateSession = _ => Task.FromResult<IOpcUaSessionAdapter>(session);

            using OpcUaClient client = new(CreateFakeClientOptions(), discovery, sessionFactory);
            await client.OpenAsync();

            await Assert.ThrowsAsync<TimeoutException>(() =>
                client.ReadValueAsync(ScalarIntNode, timeout: TimeSpan.FromMilliseconds(80)));

            delayedSdkTcs.SetException(new ServiceResultException(StatusCodes.BadTimeout, "Delayed orphan fault"));
            await Task.Delay(50);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Assert.Null(unobservedException);
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= OnUnobserved;
        }
    }

    [Fact]
    public async Task L09_DisposeDuringInFlightOpen_CancelsCleanlyWithoutDoubleDisposeOfOpenCancellationSource()
    {
        FakeDiscoveryClient discovery = new();
        FakeSessionFactory sessionFactory = new();
        TaskCompletionSource<bool> sessionCreateEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<IOpcUaSessionAdapter> sessionGate = new(TaskCreationOptions.RunContinuationsAsynchronously);

        sessionFactory.OnCreateSession = cancellationToken =>
        {
            sessionCreateEntered.TrySetResult(true);
            cancellationToken.Register(() => sessionGate.TrySetCanceled(cancellationToken));
            return sessionGate.Task;
        };

        using OpcUaClient client = new(CreateFakeClientOptions(), discovery, sessionFactory);
        Task openTask = client.OpenAsync();
        await sessionCreateEntered.Task;

        client.Dispose();
        Assert.Equal(OpcUaClientState.Disposed, client.State);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => openTask);
    }

    [Fact]
    public async Task L10_CanceledCloseAsync_ObservesBackgroundCloseFaultWithoutUnobservedTaskException()
    {
        Exception? unobservedException = null;
        void OnUnobserved(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            unobservedException = e.Exception;
            e.SetObserved();
        }

        TaskScheduler.UnobservedTaskException += OnUnobserved;
        try
        {
            FakeDiscoveryClient discovery = new();
            FakeSessionFactory sessionFactory = new();
            FakeSessionAdapter session = new();
            TaskCompletionSource<bool> closeEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> delayedCloseFaultTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            session.OnClose = (_, _) =>
            {
                closeEntered.TrySetResult(true);
                return delayedCloseFaultTcs.Task;
            };
            sessionFactory.OnCreateSession = _ => Task.FromResult<IOpcUaSessionAdapter>(session);

            using OpcUaClient client = new(CreateFakeClientOptions(), discovery, sessionFactory);
            await client.OpenAsync();

            using CancellationTokenSource callerCancel = new(TimeSpan.FromMilliseconds(50));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.CloseAsync(callerCancel.Token));
            await closeEntered.Task;

            delayedCloseFaultTcs.SetException(new InvalidOperationException("Delayed close background fault"));
            await Task.Delay(50);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Assert.Null(unobservedException);
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= OnUnobserved;
        }
    }

    private static OpcUaClientOptions CreateFakeClientOptions(
        int maxConcurrentOperations = 8,
        int discoveryTimeoutSeconds = 10)
    {
        return new OpcUaClientOptions
        {
            EndpointUrl = new Uri("opc.tcp://127.0.0.1:4840"),
            MessageSecurityMode = MessageSecurityMode.None,
            AllowedSecurityPolicies = new[] { SecurityPolicies.None },
            MaxConcurrentOperations = maxConcurrentOperations,
            ConnectTimeout = TimeSpan.FromSeconds(discoveryTimeoutSeconds),
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
