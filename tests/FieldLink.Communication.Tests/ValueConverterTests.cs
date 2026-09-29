using System.Text;
using FieldLink.PlcDrivers.Common;

namespace FieldLink.Communication.Tests;

internal static class ValueConverterTests
{
    internal static Task ExplicitByteOrdersMatchFixedVectorsAsync()
    {
        foreach (var item in new[]
        {
            (ByteOrder.BigEndian, "1234", "12345678", "0123456789ABCDEF"),
            (ByteOrder.BigEndianWithByteSwap, "3412", "34127856", "23016745AB89EFCD"),
            (ByteOrder.LittleEndianWithByteSwap, "1234", "56781234", "CDEF89AB45670123"),
            (ByteOrder.LittleEndian, "3412", "78563412", "EFCDAB8967452301")
        })
        {
            var converter = new ProtocolValueConverter(item.Item1);
            TestAssert.Bytes(H(item.Item2), converter.GetBytes((ushort)0x1234));
            TestAssert.Bytes(H(item.Item3), converter.GetBytes(0x12345678U));
            TestAssert.Bytes(H(item.Item4), converter.GetBytes(0x0123456789ABCDEFUL));
            TestAssert.Equal((ushort)0x1234, converter.ReadUInt16(H("AA" + item.Item2 + "BB"), 1));
            TestAssert.Equal(0x12345678U, converter.ReadUInt32(H("AA" + item.Item3 + "BB"), 1));
            TestAssert.Equal(0x0123456789ABCDEFUL, converter.ReadUInt64(H("AA" + item.Item4 + "BB"), 1));
        }
        return Task.CompletedTask;
    }

    internal static Task SignedFloatingArraysAndBitsMatchFixedVectorsAsync()
    {
        var converter = new ProtocolValueConverter(ByteOrder.BigEndian);
        TestAssert.Bytes(H("80007FFF"), converter.GetBytes(new short[] { short.MinValue, short.MaxValue }));
        TestAssert.Bytes(H("800000007FFFFFFF"), converter.GetBytes(new[] { int.MinValue, int.MaxValue }));
        TestAssert.Bytes(H("80000000000000007FFFFFFFFFFFFFFF"), converter.GetBytes(new[] { long.MinValue, long.MaxValue }));
        TestAssert.Bytes(H("3F800000C0000000"), converter.GetBytes(new[] { 1.0f, -2.0f }));
        TestAssert.Bytes(H("3FF0000000000000C000000000000000"), converter.GetBytes(new[] { 1.0, -2.0 }));
        TestAssert.Equal(-2.0f, converter.ReadSingle(H("C0000000"), 0));
        TestAssert.Equal(-2.0, converter.ReadDouble(H("C000000000000000"), 0));
        TestAssert.Bytes(H("8101"), converter.GetBytes(new[] { true, false, false, false, false, false, false, true, true }));
        TestAssert.True(converter.ReadBoolean(H("8001"), 7));
        TestAssert.True(converter.ReadBoolean(H("8001"), 8));
        int[,] matrix = converter.ReadInt32(H("00000001000000020000000300000004"), 0, 2, 2);
        TestAssert.Equal(3, matrix[1, 0]);
        TestAssert.Equal(4, matrix[1, 1]);
        return Task.CompletedTask;
    }

    internal static Task StringWordSwappingAndPaddingArePreservedAsync()
    {
        var converter = new ProtocolValueConverter(swapStringBytes: true);
        // Word swapping pads the odd source to a whole word before swapping.
        TestAssert.Bytes(H("42410043"), converter.GetBytes("ABC", Encoding.ASCII));
        TestAssert.Bytes(H("4241004300"), converter.GetBytes("ABC", 5, Encoding.ASCII));
        TestAssert.Equal("ABC\0", converter.ReadString(H("EE42410043FF"), 1, 4, Encoding.ASCII));
        return Task.CompletedTask;
    }

    internal static async Task InvalidConfigurationAndInputAreRejectedBeforeAllocationAsync()
    {
        await TestAssert.ThrowsAsync<ArgumentOutOfRangeException>(() => Task.FromResult(new ProtocolValueConverter((ByteOrder)99)));
        var converter = new ProtocolValueConverter();
        await TestAssert.ThrowsAsync<ArgumentNullException>(() => Task.FromResult(converter.GetBytes((int[])null!)));
        await TestAssert.ThrowsAsync<ArgumentOutOfRangeException>(() => Task.FromResult(converter.ReadInt32([], 0, int.MaxValue)));
        await TestAssert.ThrowsAsync<ArgumentOutOfRangeException>(() => Task.FromResult(converter.ReadInt32([], -1)));
        await TestAssert.ThrowsAsync<ArgumentOutOfRangeException>(() => Task.FromResult(converter.ReadInt32([], 0, -1, -1)));
    }

    internal static Task AllSixteenBitValuesAndSignedInterpretationsAsync()
    {
        foreach (ByteOrder order in Enum.GetValues(typeof(ByteOrder)))
        {
            var converter = new ProtocolValueConverter(order);
            bool highFirst = order == ByteOrder.BigEndian || order == ByteOrder.LittleEndianWithByteSwap;
            for (int value = 0; value <= ushort.MaxValue; value++)
            {
                byte[] expected = highFirst ? [(byte)(value >> 8), (byte)value] : [(byte)value, (byte)(value >> 8)];
                TestAssert.Bytes(expected, converter.GetBytes((ushort)value));
                TestAssert.Bytes(expected, converter.GetBytes(unchecked((short)value)));
                TestAssert.Equal((ushort)value, converter.ReadUInt16(expected, 0));
                TestAssert.Equal(unchecked((short)value), converter.ReadInt16(expected, 0));
            }
        }
        return Task.CompletedTask;
    }

    internal static Task FloatingPointBitPatternsArePreservedAsync()
    {
        var converter = new ProtocolValueConverter(ByteOrder.BigEndian);
        foreach (string bits in new[] { "00000000", "80000000", "7F800000", "FF800000", "7FC01234", "00000001", "7F7FFFFF" })
            TestAssert.Bytes(H(bits), converter.GetBytes(converter.ReadSingle(H(bits), 0)));
        foreach (string bits in new[] { "0000000000000000", "8000000000000000", "7FF0000000000000", "FFF0000000000000", "7FF8123456789ABC", "0000000000000001" })
            TestAssert.Bytes(H(bits), converter.GetBytes(converter.ReadDouble(H(bits), 0)));
        return Task.CompletedTask;
    }

    internal static async Task ImmutableSettingsAndParallelUseAsync()
    {
        var converter = new ProtocolValueConverter(ByteOrder.BigEndian, true);
        var changed = converter.WithByteOrder(ByteOrder.LittleEndian);
        TestAssert.Equal(ByteOrder.BigEndian, converter.ByteOrder);
        TestAssert.Equal(ByteOrder.LittleEndian, changed.ByteOrder);
        TestAssert.True(changed.SwapStringBytes);
        TestAssert.Equal("ABC\0", converter.ReadString(H("42410043"), Encoding.ASCII));
        await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() =>
        {
            for (int i = 0; i < 256; i++)
            {
                byte[] data = H("CC12345678DD");
                TestAssert.Equal(0x12345678, converter.ReadInt32(data, 1));
                TestAssert.Bytes(H("CC12345678DD"), data);
                TestAssert.Bytes(H("12345678"), converter.GetBytes(0x12345678));
            }
        })));
    }

    internal static async Task EveryNumericShapeValidatesRangeBeforeAllocationAsync()
    {
        var converter = new ProtocolValueConverter(ByteOrder.BigEndian);
        foreach (string name in new[] { "Int16", "UInt16", "Int32", "UInt32", "Int64", "UInt64", "Single", "Double" })
        {
            foreach (int argumentCount in new[] { 2, 3, 4 })
            {
                var method = typeof(ProtocolValueConverter).GetMethods().Single(m => m.Name == "Read" + name && m.GetParameters().Length == argumentCount);
                object[] args = argumentCount == 2 ? new object[] { new byte[1], 0 } :
                    argumentCount == 3 ? new object[] { new byte[1], 0, int.MaxValue } : new object[] { new byte[1], 0, int.MaxValue, 2 };
                var error = await TestAssert.ThrowsAsync<System.Reflection.TargetInvocationException>(() => Task.FromResult(method.Invoke(converter, args)));
                TestAssert.True(error.InnerException is ArgumentOutOfRangeException, name);
            }
        }
        await TestAssert.ThrowsAsync<ArgumentOutOfRangeException>(() => Task.FromResult(converter.ReadBoolean([0], 8)));
        await TestAssert.ThrowsAsync<ArgumentOutOfRangeException>(() => Task.FromResult(converter.ReadBoolean([0], 1, int.MaxValue)));
        TestAssert.Equal(0, converter.ReadUInt32([], 0, 0).Length);
        TestAssert.Equal(0, converter.ReadUInt32([], 0, 0, 2).Length);
        TestAssert.Equal(0, converter.GetBytes(Array.Empty<int>()).Length);
        TestAssert.Equal(0, converter.GetBytes(Array.Empty<bool>()).Length);
    }

    private static byte[] H(string hex) => TestBytes.FromHexString(hex);
}
