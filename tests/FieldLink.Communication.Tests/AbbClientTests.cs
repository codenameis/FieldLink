using System.Net;
using FieldLink.Robot.ABB.Clients;
using FieldLink.Robot.ABB.Protocols;

namespace FieldLink.Communication.Tests;
internal static partial class RobotClientTests
{
    internal static async Task AbbUsesRequestParserAndPreservesHttpErrorsAsync()
    {
        using var handler = new AbbHandler(); using var http = new HttpClient(handler);
        var robot = new AbbHttpClient(http, new Uri("http://localhost:8123/"));
        var read = await robot.ExecuteAsync(AbbRequestBuilder.GetCtrlState());
        TestAssert.True(read.IsSuccess); TestAssert.Equal("motoron", read.Content);
        TestAssert.Equal("/rw/panel/ctrlstate", handler.LastPath!);
        handler.Error = true;
        var failed = await robot.ReadStringAsync("CtrlState");
        TestAssert.True(!failed.IsSuccess); TestAssert.Equal(401, failed.ErrorCode);
        TestAssert.Equal("인증 필요", failed.Message);
    }
    private sealed class AbbHandler : HttpMessageHandler
    {
        internal bool Error;
        internal string? LastPath;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastPath = request.RequestUri!.PathAndQuery;
            return Task.FromResult(new HttpResponseMessage(Error ? HttpStatusCode.Unauthorized : HttpStatusCode.OK)
            { Content = new StringContent(Error ? "인증 필요" : "<span class=\"ctrlstate\">motoron</span>") });
        }
    }
}
