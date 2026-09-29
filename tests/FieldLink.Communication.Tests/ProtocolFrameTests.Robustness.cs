using FieldLink.PlcDrivers.Modbus;
using System.Reflection;
using FieldLink.PlcDrivers.Common;
using FieldLink.PlcDrivers.Melsec;
using FieldLink.PlcDrivers.Omron;
using FieldLink.PlcDrivers.Siemens;
using FieldLink.PlcDrivers.Turck;

namespace FieldLink.Communication.Tests;

internal static partial class ProtocolFrameTests
{
    private static IProtocolFrameRules[] AllRules() => typeof(IProtocolFrameRules).Assembly.GetTypes()
        .Where(t => !t.IsAbstract && typeof(IProtocolFrameRules).IsAssignableFrom(t))
        .Select(t => t == typeof(MelsecFxLinksFrameRules) ? new MelsecFxLinksFrameRules(1, true) :
            t == typeof(ModbusTcpFrameRules) ? new ModbusTcpFrameRules() :
            (IProtocolFrameRules)Activator.CreateInstance(t)!).ToArray();

    internal static async Task EveryRuleRejectsNullAndTruncatedHeadersAsync()
    {
        foreach (IProtocolFrameRules rule in AllRules())
        {
            TestAssert.True(!rule.IsHeaderValid(null!));
            TestAssert.True(!rule.IsComplete([], null!));
            TestAssert.True(!rule.IsComplete([], []));
            TestAssert.Equal(ResponseDisposition.Reject, rule.ClassifyResponse([], null!));
            TestAssert.Equal(ResponseDisposition.Reject, rule.ClassifyResponse(null!, new byte[64]));
            await TestAssert.ThrowsAsync<ArgumentNullException>(() => Task.FromResult(rule.GetBodyLength(null!)));
            await TestAssert.ThrowsAsync<ArgumentNullException>(() => Task.FromResult(rule.GetSequenceId(null!)));
            await TestAssert.ThrowsAsync<ArgumentNullException>(() => Task.FromResult(rule.FindHeaderOffset(null!)));
            for (int length = 0; length < rule.HeaderLength; length++)
            {
                byte[] header = new byte[length];
                TestAssert.True(!rule.IsHeaderValid(header), rule.GetType().Name);
                await TestAssert.ThrowsAsync<ArgumentException>(() => Task.FromResult(rule.GetBodyLength(header)));
                await TestAssert.ThrowsAsync<ArgumentException>(() => Task.FromResult(rule.GetSequenceId(header)));
                TestAssert.Equal(ResponseDisposition.Reject, rule.ClassifyResponse(new byte[64], header));
            }
        }
    }

    internal static async Task SharedRulesAreReentrantAndDoNotMutateBuffersAsync()
    {
        foreach (var item in LengthCases())
        {
            byte[] snapshot = (byte[])item.Header.Clone();
            await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() =>
            {
                for (int iteration = 0; iteration < 128; iteration++)
                {
                    TestAssert.Equal(item.Length, item.Parser.GetBodyLength(item.Header));
                    item.Parser.IsHeaderValid(item.Header);
                    item.Parser.GetSequenceId(item.Header);
                }
            })));
            TestAssert.Bytes(snapshot, item.Header);
        }
        // Each connection supplies its own request to the same rule instance.
        var rules = new MelsecA1EBinaryFrameRules();
        await Task.WhenAll(Enumerable.Range(1, 255).Select(count => Task.Run(() =>
        {
            byte[] request = new byte[12];
            request[0] = 1;
            request[10] = (byte)count;
            for (int iteration = 0; iteration < 32; iteration++)
                TestAssert.Equal(count * 2, rules.GetBodyLength([0x81, 0], request));
        })));
    }

    internal static Task FrameRulesCannotRetainMutableRequestStateAsync()
    {
        foreach (IProtocolFrameRules rule in AllRules())
        {
            for (Type? type = rule.GetType(); type != null && type != typeof(object); type = type.BaseType)
                foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    TestAssert.True(field.IsInitOnly && (field.FieldType.IsPrimitive || field.FieldType.IsEnum),
                        type.Name + "." + field.Name + " can retain mutable state");
        }
        return Task.CompletedTask;
    }

    internal static async Task RequestDependentLengthsRejectMissingRequestAsync()
    {
        foreach (var item in new (IProtocolFrameRules Rule, byte[] Header, int Required)[]
        {
            (new MelsecA1EBinaryFrameRules(), [0x81, 0], 12),
            (new MelsecA1EAsciiFrameRules(), A("8100"), 22),
            (new FetchWriteFrameRules(), Header(16, (5, "06")), 16)
        })
        {
            await TestAssert.ThrowsAsync<ArgumentNullException>(() => Task.FromResult(item.Rule.GetBodyLength(item.Header)));
            for (int length = 0; length < item.Required; length++)
                await TestAssert.ThrowsAsync<ArgumentException>(() => Task.FromResult(item.Rule.GetBodyLength(item.Header, new byte[length])));
        }
    }

    internal static async Task ModbusMbapLengthFieldIsCheckedExhaustivelyAsync()
    {
        // Modbus Messaging Implementation Guide V1.0b §3.1: maximum ADU 260,
        // Length counts Unit Identifier + PDU; this rule consumes function code too.
        var rule = new ModbusTcpFrameRules();
        byte[] header = H("1234000000060103");
        for (int length = 0; length <= ushort.MaxValue; length++)
        {
            header[4] = (byte)(length >> 8);
            header[5] = (byte)length;
            if (length >= 2 && length <= 254)
                TestAssert.Equal(length - 2, rule.GetBodyLength(header));
            else
                await TestAssert.ThrowsAsync<InvalidDataException>(() => Task.FromResult(rule.GetBodyLength(header)));
        }
        header[4] = 0;
        header[5] = 6;
        for (int protocolId = 1; protocolId <= ushort.MaxValue; protocolId++)
        {
            header[2] = (byte)(protocolId >> 8);
            header[3] = (byte)protocolId;
            TestAssert.True(!rule.IsHeaderValid(header));
        }
    }

    internal static Task ShortIdentifiedResponsesAreRejectedAsync()
    {
        foreach (IProtocolFrameRules rule in new IProtocolFrameRules[] { new ModbusTcpFrameRules(), new FinsUdpFrameRules() })
            for (int requestLength = 0; requestLength < (rule is ModbusTcpFrameRules ? 8 : 10); requestLength++)
                TestAssert.Equal(ResponseDisposition.Reject, rule.ClassifyResponse(new byte[requestLength], new byte[26]));
        var turck = new TurckReaderFrameRules();
        TestAssert.Equal(ResponseDisposition.Reject, turck.ClassifyResponse(H("AA000868"), H("AA0707")));
        return Task.CompletedTask;
    }
}
