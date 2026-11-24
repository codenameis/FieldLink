using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Transport.Http;

namespace FieldLink.Robot.Tests.Fakes;

internal sealed class FakeHttpTransport : IHttpTransport
{
    private readonly Queue<Func<HttpTransportRequest, HttpTransportResponse>> _handlers = new();

    public List<HttpTransportRequest> SentRequests { get; } = new();

    public bool IsDisposed { get; private set; }

    public void EnqueueResponse(int statusCode, string body, string? contentType = "text/html")
    {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(body);
        _handlers.Enqueue(_ => new HttpTransportResponse(statusCode, string.Empty, bytes, contentType));
    }

    public void EnqueueResponseBytes(int statusCode, byte[] bodyBytes, string? contentType = "text/html")
    {
        _handlers.Enqueue(_ => new HttpTransportResponse(statusCode, string.Empty, bodyBytes, contentType));
    }

    public void EnqueueHandler(Func<HttpTransportRequest, HttpTransportResponse> handler)
    {
        _handlers.Enqueue(handler);
    }

    public Task<HttpTransportResponse> SendAsync(HttpTransportRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SentRequests.Add(request);

        if (_handlers.Count == 0)
        {
            throw new InvalidOperationException("No queued HTTP response in FakeHttpTransport.");
        }

        return Task.FromResult(_handlers.Dequeue()(request));
    }

    public void Dispose()
    {
        IsDisposed = true;
    }
}
