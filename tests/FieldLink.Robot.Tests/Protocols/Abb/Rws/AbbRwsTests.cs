using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using FieldLink.Robot.Protocols.Abb.Rws;
using FieldLink.Robot.Tests.Fakes;
using FieldLink.Transport.Http;
using Xunit;

namespace FieldLink.Robot.Tests.Protocols.Abb.Rws;

public sealed class AbbRwsTests
{
    [Fact]
    public void Address_Parse_ResolvesAllShortcutsLogCountAndRawUrl()
    {
        var ctrl = AbbRwsAddress.Parse("ctrlstate");
        Assert.False(ctrl.IsRawUrl);
        Assert.Equal(AbbRwsShortcutKind.CtrlState, ctrl.Shortcut);

        var defaultLog = AbbRwsAddress.Parse("LOG");
        Assert.Equal(AbbRwsShortcutKind.Log, defaultLog.Shortcut);
        Assert.Equal(10, defaultLog.LogCount);

        var customLog = AbbRwsAddress.Parse("log25");
        Assert.Equal(AbbRwsShortcutKind.Log, customLog.Shortcut);
        Assert.Equal(25, customLog.LogCount);

        var rawUrl = AbbRwsAddress.Parse("URL=/rw/rapid/symbol/data/RAPID/T_ROB1/user/num1?json=1");
        Assert.True(rawUrl.IsRawUrl);
        Assert.Equal("/rw/rapid/symbol/data/RAPID/T_ROB1/user/num1?json=1", rawUrl.RawUrl);

        Assert.Throws<FormatException>(() => AbbRwsAddress.Parse("unknown_shortcut"));
        Assert.Throws<FormatException>(() => AbbRwsAddress.Parse("url="));
        Assert.Throws<FormatException>(() => AbbRwsAddress.Parse("log0"));
    }

    [Fact]
    public void RequestBuilder_EventLogs_FixesAmpersandAndUsesConfiguredLang()
    {
        HttpTransportRequest request = AbbRwsRequestBuilder.BuildEventLogsRequest("en");
        Assert.Equal(HttpMethodKind.Get, request.Method);
        Assert.Equal("/rw/elog/0?lang=en&resource=title", request.PathOrUri);
    }

    [Fact]
    public void ResponseParser_Vector1JointTarget_ParsesScientificNotationInvariantCulture()
    {
        // 출처: docs/specs/robot/01-abb-web-api.md §9.2 Vector 1 GetJointTarget("ROB_1")
        const string Xhtml =
            "<div class=\"state\"><ul><li class=\"ms-jointtarget\" title=\"jointtarget\">" +
            "<span class=\"rax_1\">10.5</span>" +
            "<span class=\"rax_2\">-20.25</span>" +
            "<span class=\"rax_3\">30.0</span>" +
            "<span class=\"rax_4\">0.0</span>" +
            "<span class=\"rax_5\">45.5</span>" +
            "<span class=\"rax_6\">-90.0</span>" +
            "<span class=\"eax_a\">9E+09</span>" +
            "</li></ul></div>";

        double[] joints = AbbRwsResponseParser.ParseJointTarget(Xhtml);

        Assert.Equal(new[] { 10.5, -20.25, 30.0, 0.0, 45.5, -90.0, 9000000000.0 }, joints);
    }

    [Fact]
    public void ResponseParser_Vector2RapidExecution_ParsesObjectDictionary()
    {
        // 출처: docs/specs/robot/01-abb-web-api.md §9.2 Vector 2 GetRapidExecution()
        const string Xhtml =
            "<div class=\"state\"><ul><li class=\"rap-execution\" title=\"execution\">" +
            "<span class=\"ctrlexecstate\">running</span>" +
            "<span class=\"cycle\">forever</span>" +
            "</li></ul></div>";

        IReadOnlyDictionary<string, string> dict = AbbRwsResponseParser.ParseObjectByClass(Xhtml, "rap-execution");

        Assert.Equal("running", dict["ctrlexecstate"]);
        Assert.Equal("forever", dict["cycle"]);
    }

    [Fact]
    public void ResponseParser_WhenSpanOrListItemMissing_ThrowsParseError()
    {
        RobotException spanError = Assert.Throws<RobotException>(
            () => AbbRwsResponseParser.ParseSpanByClass("<div></div>", "ctrlstate"));
        Assert.Equal(RobotErrorKind.ParseError, spanError.Kind);

        RobotException liError = Assert.Throws<RobotException>(
            () => AbbRwsResponseParser.ParseObjectByClass("<div></div>", "rap-execution"));
        Assert.Equal(RobotErrorKind.ParseError, liError.Kind);
    }

    [Fact]
    public async Task Client_ShortcutAndParameterizedReads_UseExpectedPathsAndParsers()
    {
        var http = new FakeHttpTransport();
        http.EnqueueResponse(200, "<html><body><div class=\"state\"><ul><li class=\"pnl-ctrlstate-ev\"><span class=\"ctrlstate\">motoron</span></li></ul></div></body></html>");
        http.EnqueueResponse(200, "<div class=\"state\"><ul><li class=\"ms-jointtarget\"><span class=\"rax_1\">10.5</span><span class=\"eax_a\">9E+09</span></li></ul></div>");
        http.EnqueueResponse(200, "<ul><li class=\"elog-message-li\"><span class=\"title\">E1</span></li><li class=\"elog-message-li\"><span class=\"title\">E2</span></li><li class=\"elog-message-li\"><span class=\"title\">E3</span></li></ul>");

        using var client = new AbbRwsClient(http, new AbbRwsOptions { EventLogLanguage = "en" });
        await client.OpenAsync();

        RobotResult<string> ctrlState = await client.GetCtrlStateAsync();
        Assert.True(ctrlState.IsSuccess);
        Assert.Equal("motoron", ctrlState.Value);
        Assert.Equal("/rw/panel/ctrlstate", http.SentRequests[0].PathOrUri);

        RobotResult<double[]> jointTarget = await client.GetJointTargetAsync("ROB_2");
        Assert.True(jointTarget.IsSuccess);
        Assert.Equal(new[] { 10.5, 9000000000.0 }, jointTarget.Value);
        Assert.Equal("/rw/motionsystem/mechunits/ROB_2/jointtarget", http.SentRequests[1].PathOrUri);

        RobotResult<IReadOnlyList<IReadOnlyDictionary<string, string>>> logs = await client.GetEventLogsAsync(maxCount: 2);
        Assert.True(logs.IsSuccess);
        Assert.Equal(2, logs.Value.Count);
        Assert.Equal("E1", logs.Value[0]["title"]);
        Assert.Equal("E2", logs.Value[1]["title"]);
        Assert.Equal("/rw/elog/0?lang=en&resource=title", http.SentRequests[2].PathOrUri);
    }

    [Fact]
    public async Task Client_Iso88591RawUrlAndWriteRules_BehaveAsSpecified()
    {
        var http = new FakeHttpTransport();
        byte[] isoBytes = Encoding.GetEncoding("ISO-8859-1").GetBytes("café");
        http.EnqueueResponseBytes(200, isoBytes);
        http.EnqueueResponse(204, string.Empty);

        using var client = new AbbRwsClient(http, new AbbRwsOptions { UseIso88591 = true });
        await client.OpenAsync();

        RobotResult<string> rawRead = await client.ReadStringAsync("url=/rw/custom");
        Assert.True(rawRead.IsSuccess);
        Assert.Equal("café", rawRead.Value);

        RobotResult unsupportedWrite = await client.WriteStringAsync("CtrlState", "motoron");
        Assert.False(unsupportedWrite.IsSuccess);
        Assert.Equal(RobotErrorKind.Unsupported, unsupportedWrite.Error.Kind);

        RobotResult validWrite = await client.WriteStringAsync("url=/rw/panel/ctrlstate?action=set", "ctrl-state=motoron");
        Assert.True(validWrite.IsSuccess);
        Assert.Equal(HttpMethodKind.Post, http.SentRequests[1].Method);
        Assert.Equal("/rw/panel/ctrlstate?action=set", http.SentRequests[1].PathOrUri);
    }

    [Fact]
    public async Task Client_Http401And500_MapToAuthenticationFailedAndDeviceError()
    {
        var http = new FakeHttpTransport();
        http.EnqueueResponse(401, "Unauthorized");
        http.EnqueueResponse(500, "Internal Error");

        using var client = new AbbRwsClient(http);
        await client.OpenAsync();

        RobotResult<string> authFail = await client.GetCtrlStateAsync();
        Assert.False(authFail.IsSuccess);
        Assert.Equal(RobotErrorKind.AuthenticationFailed, authFail.Error.Kind);
        Assert.Equal(401, authFail.Error.DeviceErrorCode);

        RobotResult<string> serverFail = await client.GetCtrlStateAsync();
        Assert.False(serverFail.IsSuccess);
        Assert.Equal(RobotErrorKind.DeviceError, serverFail.Error.Kind);
        Assert.Equal(500, serverFail.Error.DeviceErrorCode);
    }

    [Fact]
    public async Task Client_WriteOnNonUrlAddresses_ReturnsUnsupportedWithoutFormatException()
    {
        var http = new FakeHttpTransport();
        using var client = new AbbRwsClient(http);
        await client.OpenAsync();

        RobotResult rapidShortcut = await client.WriteStringAsync("rapid/symbol/data/RAPID/T_ROB1/Module1/nCount", "5");
        Assert.False(rapidShortcut.IsSuccess);
        Assert.Equal(RobotErrorKind.Unsupported, rapidShortcut.Error.Kind);

        RobotResult rawPathWithoutPrefix = await client.WriteRawAsync("/rw/panel/ctrlstate", new byte[] { 0x41 });
        Assert.False(rawPathWithoutPrefix.IsSuccess);
        Assert.Equal(RobotErrorKind.Unsupported, rawPathWithoutPrefix.Error.Kind);

        await Assert.ThrowsAsync<FormatException>(() => client.WriteStringAsync("url=   ", "val"));
        Assert.Empty(http.SentRequests);
    }

    [Fact]
    public void ResponseParser_DecodesXmlEntitiesAndIncludesRawFrameOnMissingElement()
    {
        string spanHtml = "<span class=\"ctrlstate\">A &amp; B &lt;1&gt;</span>";
        Assert.Equal("A & B <1>", AbbRwsResponseParser.ParseSpanByClass(spanHtml, "ctrlstate"));

        string xml = "<li class=\"elog-message-li\"><span class=\"title\">A &amp; B &lt;1&gt;</span></li>";
        IReadOnlyList<IReadOnlyDictionary<string, string>> logs = AbbRwsResponseParser.ParseArrayByClass(xml, "elog-message-li", maxCount: 10);
        Assert.Single(logs);
        Assert.Equal("A & B <1>", logs[0]["title"]);

        string missingSpanHtml = "<div>empty</div>";
        RobotException missing = Assert.Throws<RobotException>(
            () => AbbRwsResponseParser.ParseSpanByClass(missingSpanHtml, "ctrlstate"));
        Assert.Equal(RobotErrorKind.ParseError, missing.Kind);
        Assert.Equal(Encoding.UTF8.GetBytes(missingSpanHtml), missing.GetRawFrame());
    }
}
