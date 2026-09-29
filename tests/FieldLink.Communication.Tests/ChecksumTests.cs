using System.Text;
using FieldLink.PlcDrivers.Common;

namespace FieldLink.Communication.Tests;

internal static class ChecksumTests
{
    internal static Task EmptyLrcFrameIsRejectedAsync()
    {
        TestAssert.True(!LrcChecksum.Verify(Array.Empty<byte>()));
        return Task.CompletedTask;
    }

    internal static Task PublishedCrcAndLrcVectorsAsync()
    {
        // Modbus Serial Line V1.02 Appendix B: init FFFF, reflected polynomial A001,
        // low byte first. Independent check string: ASCII 123456789 -> CRC 4B37.
        TestAssert.Bytes(TestBytes.FromHexString("313233343536373839374B"), Crc16.Append(Encoding.ASCII.GetBytes("123456789")));
        TestAssert.Bytes(TestBytes.FromHexString("01030000000AC5CD"), Crc16.Append(TestBytes.FromHexString("01030000000A")));
        // 01 + 03 + 00 + 00 + 00 + 0A = 0E; its 8-bit two's complement is F2.
        TestAssert.Bytes(TestBytes.FromHexString("01030000000AF2"), LrcChecksum.Append(TestBytes.FromHexString("01030000000A")));
        TestAssert.Bytes(TestBytes.FromHexString("3132333435363738393DBB"), Crc16.Append(Encoding.ASCII.GetBytes("123456789"), 0xA001, 0));
        return Task.CompletedTask;
    }

    internal static async Task SegmentsEmptyInputsAndAsciiChecksumBoundsAsync()
    {
        byte[] data = TestBytes.FromHexString("FF01030000000AFF");
        TestAssert.Equal((ushort)0xCDC5, Crc16.Compute(new ArraySegment<byte>(data, 1, 6)));
        TestAssert.Equal((byte)0xF2, LrcChecksum.Compute(new ArraySegment<byte>(data, 1, 6)));
        TestAssert.Equal((ushort)0xFFFF, Crc16.Compute(new ArraySegment<byte>(Array.Empty<byte>())));
        TestAssert.Bytes(new byte[] { 0xFF, 0xFF }, Crc16.Append([]));
        TestAssert.Bytes(new byte[] { 0 }, LrcChecksum.Append([]));
        TestAssert.True(!Crc16.Verify(null!) && !Crc16.Verify([]) && !Crc16.Verify([0]));
        TestAssert.True(!LrcChecksum.Verify(null!));
        await TestAssert.ThrowsAsync<ArgumentException>(() => Task.FromResult(Crc16.Compute(default)));
        await TestAssert.ThrowsAsync<ArgumentException>(() => Task.FromResult(LrcChecksum.Compute(default)));
        await TestAssert.ThrowsAsync<ArgumentNullException>(() => Task.FromResult(Crc16.Append(null!)));
        await TestAssert.ThrowsAsync<ArgumentNullException>(() => Task.FromResult(LrcChecksum.Append(null!)));
        for (int sum = 0; sum <= 255; sum++)
        {
            byte[] frame = [0xCC, (byte)sum, 0, 0, 0xDD];
            AdditiveChecksum.WriteAscii(frame, 1, 2);
            TestAssert.Equal(sum.ToString("X2"), Encoding.ASCII.GetString(frame, 2, 2));
            TestAssert.True(AdditiveChecksum.VerifyAscii(frame, 1, 2));
            TestAssert.Equal((byte)0xCC, frame[0]);
            TestAssert.Equal((byte)0xDD, frame[4]);
            frame[3] ^= 1;
            TestAssert.True(!AdditiveChecksum.VerifyAscii(frame, 1, 2));
        }
        foreach (var range in new[] { (-1, 0), (0, -1), (2, 1), (0, 3), (int.MaxValue, int.MaxValue) })
        {
            byte[] frame = [1, 2, 3, 4], saved = (byte[])frame.Clone();
            await TestAssert.ThrowsAsync<ArgumentOutOfRangeException>(() => { AdditiveChecksum.WriteAscii(frame, range.Item1, range.Item2); return Task.CompletedTask; });
            TestAssert.Bytes(saved, frame);
        }
    }

    internal static Task EverySingleBitCorruptionIsRejectedAsync()
    {
        byte[] payload = Enumerable.Range(0, 254).Select(i => (byte)i).ToArray();
        byte[] original = (byte[])payload.Clone();
        byte[] crc = Crc16.Append(payload), lrc = LrcChecksum.Append(payload);
        TestAssert.Bytes(original, payload);
        foreach (byte[] frame in new[] { crc, lrc })
        {
            for (int index = 0; index < frame.Length; index++)
                for (int bit = 0; bit < 8; bit++)
                {
                    frame[index] ^= (byte)(1 << bit);
                    TestAssert.True(!(frame == crc ? Crc16.Verify(frame) : LrcChecksum.Verify(frame)), $"Undetected bit {index}:{bit}");
                    frame[index] ^= (byte)(1 << bit);
                }
        }
        return Task.CompletedTask;
    }
}
