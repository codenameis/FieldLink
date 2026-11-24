using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Transport.Http;
using Xunit;

namespace FieldLink.Transport.Tests.Http;

public sealed class HttpTransportTests
{
    [Fact]
    public void Options_Validation_ThrowsForInvalidInputs()
    {
        Assert.Throws<ArgumentNullException>(() => new HttpTransportOptions((Uri)null!));
        Assert.Throws<ArgumentException>(() => new HttpTransportOptions(new Uri("ftp://192.168.0.1")));
        Assert.Throws<ArgumentNullException>(() => new HttpTransportOptions((string)null!, 80));
        Assert.Throws<ArgumentException>(() => new HttpTransportOptions("   ", 80));
        Assert.Throws<ArgumentOutOfRangeException>(() => new HttpTransportOptions("192.168.0.1", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new HttpTransportOptions("192.168.0.1", 65536));
        Assert.Throws<ArgumentOutOfRangeException>(() => new HttpTransportOptions("192.168.0.1", 80) { Timeout = TimeSpan.Zero });
    }

    [Fact]
    public void Request_ValidationAndDefensiveCopy_WorkCorrectly()
    {
        Assert.Throws<ArgumentException>(() => new HttpTransportRequest(HttpMethodKind.Get, "   "));

        byte[] payload = new byte[] { 0x41, 0x42 };
        var headers = new Dictionary<string, string> { ["X-Custom"] = "1" };
        var request = new HttpTransportRequest(HttpMethodKind.Post, "/rw/test", payload, "text/plain")
        {
            Headers = headers
        };

        payload[0] = 0xFF;
        headers["X-Custom"] = "2";

        byte[]? copiedBody = request.GetBodyBytes();
        Assert.NotNull(copiedBody);
        Assert.Equal(new byte[] { 0x41, 0x42 }, copiedBody);
        Assert.Equal("1", request.Headers["X-Custom"]);
    }

    [Fact]
    public async Task SendAsync_RelativeAndAbsoluteUrls_ResolveAndPreserveRawBytes()
    {
        HttpRequestMessage? lastRequest = null;
        byte[] isoBytes = Encoding.GetEncoding("ISO-8859-1").GetBytes("café");

        using var handler = new StubHttpMessageHandler((req, token) =>
        {
            lastRequest = req;
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(isoBytes)
            };
            response.Content.Headers.TryAddWithoutValidation("Content-Type", "text/html; charset=invalid-charset");
            return Task.FromResult(response);
        });

        var options = new HttpTransportOptions("192.168.125.1", 80, useHttps: false);
        using var transport = new HttpTransport(options, handler, disposeHandler: false);

        HttpTransportResponse relativeResponse = await transport.SendAsync(
            new HttpTransportRequest(HttpMethodKind.Get, "/rw/panel/ctrlstate"));

        Assert.NotNull(lastRequest);
        Assert.Equal("http://192.168.125.1/rw/panel/ctrlstate", lastRequest!.RequestUri!.ToString());
        Assert.Equal(200, relativeResponse.StatusCode);
        Assert.True(relativeResponse.IsSuccessStatusCode);
        Assert.Equal(isoBytes, relativeResponse.GetBodyBytes());

        var customPortOptions = new HttpTransportOptions("192.168.125.1", 8080, useHttps: false);
        using var customPortTransport = new HttpTransport(customPortOptions, handler, disposeHandler: false);
        await customPortTransport.SendAsync(new HttpTransportRequest(HttpMethodKind.Get, "rw/system"));
        Assert.Equal("http://192.168.125.1:8080/rw/system", lastRequest!.RequestUri!.ToString());

        HttpTransportResponse absoluteResponse = await transport.SendAsync(
            new HttpTransportRequest(HttpMethodKind.Get, "https://10.0.0.5:8443/custom/path"));

        Assert.Equal("https://10.0.0.5:8443/custom/path", lastRequest!.RequestUri!.ToString());
        Assert.Equal(200, absoluteResponse.StatusCode);
    }

    [Fact]
    public async Task SendAsync_PostWithBodyAndCustomHeaders_SendsContentAndContentType()
    {
        string? capturedBody = null;
        string? capturedContentType = null;
        string? capturedHeader = null;

        using var handler = new StubHttpMessageHandler(async (req, token) =>
        {
            capturedBody = req.Content is null ? null : await req.Content.ReadAsStringAsync(token).ConfigureAwait(false);
            capturedContentType = req.Content?.Headers.ContentType?.ToString();
            capturedHeader = req.Headers.TryGetValues("X-Abb-Test", out IEnumerable<string>? vals) ? vals.FirstOrDefault() : null;
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });

        var options = new HttpTransportOptions("127.0.0.1", 80)
        {
            DefaultContentType = "application/x-www-form-urlencoded;v=2.0"
        };
        using var transport = new HttpTransport(options, handler, disposeHandler: false);

        byte[] body = Encoding.UTF8.GetBytes("ctrl-state=motoron");
        var headers = new Dictionary<string, string> { ["X-Abb-Test"] = "ok" };

        HttpTransportResponse response = await transport.SendAsync(
            new HttpTransportRequest(HttpMethodKind.Post, "rw/panel/ctrlstate?action=set", body)
            {
                Headers = headers
            });

        Assert.Equal(204, response.StatusCode);
        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal("ctrl-state=motoron", capturedBody);
        Assert.Equal("application/x-www-form-urlencoded; v=2.0", capturedContentType);
        Assert.Equal("ok", capturedHeader);
    }

    [Fact]
    public async Task SendAsync_WhenHttpRequestFails_ThrowsTransportException()
    {
        using var handler = new StubHttpMessageHandler((_, _) => throw new HttpRequestException("Host unreachable"));
        var options = new HttpTransportOptions("127.0.0.1", 80);
        using var transport = new HttpTransport(options, handler, disposeHandler: false);

        TransportException exception = await Assert.ThrowsAsync<TransportException>(
            () => transport.SendAsync(new HttpTransportRequest(HttpMethodKind.Get, "/rw/system")));

        Assert.Equal(TransportFailure.ReceiveFailed, exception.Failure);
    }

    [Fact]
    public async Task SendAsync_WhenInternalTimeoutFires_ThrowsTransportExceptionWithTimeoutInner()
    {
        using var handler = new StubHttpMessageHandler((_, _) => throw new TaskCanceledException("HttpClient timed out"));
        var options = new HttpTransportOptions("127.0.0.1", 80);
        using var transport = new HttpTransport(options, handler, disposeHandler: false);

        TransportException exception = await Assert.ThrowsAsync<TransportException>(
            () => transport.SendAsync(new HttpTransportRequest(HttpMethodKind.Get, "/rw/system"), CancellationToken.None));

        Assert.Equal(TransportFailure.ReceiveFailed, exception.Failure);
        Assert.IsType<TimeoutException>(exception.InnerException);
    }

    [Fact]
    public async Task Options_UseHttpsWithoutPort_DefaultsTo443AndSupportsIpv6AndBaseSubpath()
    {
        var httpsOptions = new HttpTransportOptions("192.168.0.10", useHttps: true);
        Assert.Equal(443, httpsOptions.BaseUri.Port);
        Assert.Equal(Uri.UriSchemeHttps, httpsOptions.BaseUri.Scheme);

        var ipv6Options = new HttpTransportOptions("::1", 8443, useHttps: true);
        Assert.Equal("[::1]", ipv6Options.BaseUri.Host);

        Uri? capturedUri = null;
        using var handler = new StubHttpMessageHandler((request, _) =>
        {
            capturedUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var subpathOptions = new HttpTransportOptions(new Uri("http://127.0.0.1:8080/api/v1"));
        using var transport = new HttpTransport(subpathOptions, handler, disposeHandler: false);
        await transport.SendAsync(new HttpTransportRequest(HttpMethodKind.Get, "rw/system"));

        Assert.Equal("http://127.0.0.1:8080/api/v1/rw/system", capturedUri!.ToString());
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            _handler(request, cancellationToken);
    }
}
