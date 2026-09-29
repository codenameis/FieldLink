using System.Text;
using FieldLink.PlcDrivers.Common;
using FieldLink.PlcDrivers.OpenProtocol;

namespace FieldLink.Communication.Tests;

internal static partial class ProtocolResponseTests
{
    internal static Task OpenProtocolSessionSubscriptionsAndToolCommandsAsync()
    {
        (Func<OperationResult<byte[]>> Build, string Mid)[] commands =
        [
            (() => OpenProtocolSessionCommandBuilder.BuildConnect(1), "0001"),
            (OpenProtocolSessionCommandBuilder.BuildDisconnect, "0003"),
            (OpenProtocolSessionCommandBuilder.BuildHeartbeat, "9999"),
            (ParameterSetMessagesCommandBuilder.BuildParameterSetIDUpload, "0010"),
            (ParameterSetMessagesCommandBuilder.BuildParameterSetSelectedSubscribe, "0014"),
            (ParameterSetMessagesCommandBuilder.BuildParameterSetSelectedUnsubscribe, "0017"),
            (() => JobMessageCommandBuilder.BuildJobIDUpload(), "0030"),
            (JobMessageCommandBuilder.BuildJobInfoSubscribe, "0034"),
            (JobMessageCommandBuilder.BuildJobInfoUnsubscribe, "0037"),
            (() => ToolMessagesCommandBuilder.BuildToolDataUpload(), "0040"),
            (ToolMessagesCommandBuilder.BuildDisableTool, "0042"),
            (ToolMessagesCommandBuilder.BuildEnableTool, "0043"),
            (ToolMessagesCommandBuilder.BuildDisconnectToolRequest, "0044"),
            (() => TighteningResultMessagesCommandBuilder.BuildLastTighteningResultDataSubscribe(1), "0060"),
            (TighteningResultMessagesCommandBuilder.BuildLastTighteningResultDataUnsubscribe, "0063"),
            (AlarmMessagesCommandBuilder.BuildAlarmSubscrib, "0070"),
            (AlarmMessagesCommandBuilder.BuildAlarmUnsubscribe, "0073"),
            (AlarmMessagesCommandBuilder.BuildAcknowledgeAlarmRemotelyOnController, "0078"),
            (TimeMessagesCommandBuilder.BuildReadTimeUpload, "0080")
        ];
        foreach (var item in commands)
        {
            var command = item.Build();
            TestAssert.True(command.IsSuccess, command.Message);
            TestAssert.Bytes(A("0020" + item.Mid + "0010        \0"), command.Content);
        }
        TestAssert.Bytes(A("002000620010        \0"), OpenProtocolSessionCommandBuilder.BuildEventAcknowledgement(A("002000610010        \0")));
        TestAssert.True(OpenProtocolSessionCommandBuilder.BuildEventAcknowledgement(A("002099990010        \0")) == null);
        Failed(OpenProtocolNetResponseParser.CheckRequestReplyMessages(A("002600040010        001801\0")), 1);
        Failed(OpenProtocolNetResponseParser.CheckRequestReplyMessages([]));
        return Task.CompletedTask;
    }

    internal static Task OpenProtocolJobAndParameterListsAsync()
    {
        var jobs = JobMessageResponseParser.PraseMID0031("002600310010        020109");
        TestAssert.True(jobs.IsSuccess && jobs.Content.SequenceEqual(new[] { 1, 9 }), jobs.Message);
        var longIds = JobMessageResponseParser.PraseMID0031("003200310020        000200011234");
        TestAssert.True(longIds.IsSuccess && longIds.Content.SequenceEqual(new[] { 1, 1234 }), longIds.Message);
        var parameters = ParameterSetMessagesResponseParser.PraseMID0011("002900110010        002001999");
        TestAssert.True(parameters.IsSuccess && parameters.Content.SequenceEqual(new[] { 1, 999 }), parameters.Message);
        Failed(JobMessageResponseParser.PraseMID0031("002600310010        0201"));
        Failed(ParameterSetMessagesResponseParser.PraseMID0011("002900110010        002001"));
        return Task.CompletedTask;
    }

    internal static Task OpenProtocolParameterToolAndTimeDataAsync()
    {
        string parameter = FixedText(104, (22, "007"), (27, "시험 설정".PadRight(25)), (54, "1"),
            (57, "12"), (61, "001234"), (69, "005678"), (77, "002500"), (85, "00090"), (92, "00180"), (99, "00120"));
        var parsed = ParameterSetMessagesResponseParser.PraseMID0012(parameter);
        TestAssert.True(parsed.IsSuccess, parsed.Message);
        TestAssert.Equal(7, parsed.Content.ParameterSetID);
        TestAssert.Equal("시험 설정", parsed.Content.ParameterSetName);
        TestAssert.Equal("CW", parsed.Content.RotationDirection);
        TestAssert.Equal(12.34, parsed.Content.TorqueMin);
        TestAssert.Equal(56.78, parsed.Content.TorqueMax);
        TestAssert.Equal(120, parsed.Content.AngleFinalTarget);
        string tool = FixedText(81, (8, "001"), (22, "TOOL0000000001"), (38, "0000001234"),
            (50, "2026-09-23:14:35:59"), (71, "CTRL000001"));
        var toolData = ToolMessagesResponseParser.PraseMID0041(tool);
        TestAssert.True(toolData.IsSuccess, toolData.Message);
        TestAssert.Equal((uint)1234, toolData.Content.ToolNumberOfTightening);
        TestAssert.Equal("CTRL000001", toolData.Content.ControllerSerialNumber);
        var date = new DateTime(2026, 9, 23, 14, 35, 59);
        TestAssert.Equal(date, toolData.Content.LastCalibrationDate);
        TestAssert.Equal(date, OpenProtocolTimeParser.ParseTime("003900810010        2026-09-23:14:35:59"));
        TestAssert.Bytes(A("003900820010        2026-09-23:14:35:59\0"), TimeMessagesCommandBuilder.BuildSetTime(date).Content);
        Failed(ParameterSetMessagesResponseParser.PraseMID0012(""));
        Failed(ToolMessagesResponseParser.PraseMID0041(""));
        return Task.CompletedTask;
    }

    private static string FixedText(int length, params (int Offset, string Value)[] fields)
    {
        char[] text = new string('0', length).ToCharArray();
        foreach (var field in fields) field.Value.CopyTo(0, text, field.Offset, field.Value.Length);
        return new string(text);
    }
}
