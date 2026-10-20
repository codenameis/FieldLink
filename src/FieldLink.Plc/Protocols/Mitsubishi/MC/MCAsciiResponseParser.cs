using System;
using FieldLink.Plc.Conversion;

namespace FieldLink.Plc.Protocols.Mitsubishi.MC;

/// <summary>MC 프로토콜 ASCII 응답 프레임(3E·4E)을 검증하고 데이터를 꺼내는 순수 함수 모음입니다. (사양서 §3-1, §3-4)</summary>
public static class MCAsciiResponseParser
{
    private const int Header3ELength = 18;
    private const int Header4ELength = 26;
    private const int EndCodeCharacterCount = 4;
    private const int WordCharacterCount = 4;
    private const string Response3ESubheader = "D000";
    private const string Response4ESubheader = "D400";

    /// <summary>4E ASCII 응답 프레임의 시리얼 번호를 읽습니다.</summary>
    /// <param name="frame">수신된 응답 프레임 바이트 배열입니다.</param>
    /// <returns>응답 시리얼 번호입니다.</returns>
    public static ushort ReadSerialNumber(byte[] frame)
    {
        if (frame is null)
        {
            throw new ArgumentNullException(nameof(frame));
        }

        if (frame.Length < Header4ELength)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"4E 응답 프레임이 너무 짧습니다. (길이: {frame.Length})");
        }

        return (ushort)BinaryBuffer.ReadAsciiHex(frame, 4, 4);
    }

    /// <summary>ASCII 읽기 응답 프레임에서 데이터를 추출합니다.</summary>
    /// <param name="options">MC 프로토콜 옵션입니다.</param>
    /// <param name="expectedSerialNumber">4E 프레임에서 기대하는 시리얼 번호입니다.</param>
    /// <param name="frame">수신된 응답 프레임 바이트 배열입니다.</param>
    /// <param name="address">읽기를 요청한 디바이스 주소입니다.</param>
    /// <param name="pointCount">읽기를 요청한 점수입니다.</param>
    /// <returns>추출된 바이트 배열(워드는 리틀 엔디언, 비트는 0/1 바이트)입니다.</returns>
    public static byte[] ParseRead(
        MCOptions options,
        ushort expectedSerialNumber,
        byte[] frame,
        MCAddress address,
        ushort pointCount)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        byte[] data = ExtractData(options, expectedSerialNumber, frame);
        return address.IsBit ? ParseBits(data, pointCount) : ParseWords(data, pointCount);
    }

    /// <summary>ASCII 쓰기 응답 프레임을 검증합니다.</summary>
    /// <param name="options">MC 프로토콜 옵션입니다.</param>
    /// <param name="expectedSerialNumber">4E 프레임에서 기대하는 시리얼 번호입니다.</param>
    /// <param name="frame">수신된 응답 프레임 바이트 배열입니다.</param>
    public static void ParseWrite(MCOptions options, ushort expectedSerialNumber, byte[] frame)
    {
        byte[] data = ExtractData(options, expectedSerialNumber, frame);
        if (data.Length != 0)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"쓰기 응답에 불필요한 데이터가 있습니다. ({data.Length}문자)");
        }
    }

    private static byte[] ExtractData(MCOptions options, ushort expectedSerialNumber, byte[] frame)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (frame is null)
        {
            throw new ArgumentNullException(nameof(frame));
        }

        int headerLength = options.FrameType == MCFrameType.Frame4E ? Header4ELength : Header3ELength;
        if (frame.Length < headerLength + EndCodeCharacterCount)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"응답 프레임이 너무 짧습니다. (길이: {frame.Length})");
        }

        ValidateHeader(options.FrameType, expectedSerialNumber, frame);
        ValidateLength(frame, headerLength);
        MCEndCodeExtensions.EnsureSuccess((ushort)BinaryBuffer.ReadAsciiHex(frame, headerLength, EndCodeCharacterCount));

        var data = new byte[frame.Length - headerLength - EndCodeCharacterCount];
        Buffer.BlockCopy(frame, headerLength + EndCodeCharacterCount, data, 0, data.Length);
        return data;
    }

    private static void ValidateHeader(MCFrameType frameType, ushort expectedSerialNumber, byte[] frame)
    {
        string expectedSubheader = frameType == MCFrameType.Frame4E ? Response4ESubheader : Response3ESubheader;
        for (int index = 0; index < expectedSubheader.Length; index++)
        {
            if (frame[index] != expectedSubheader[index])
            {
                throw new PlcException(PlcErrorKind.InvalidResponse, $"응답 서브헤더가 '{expectedSubheader}'가 아닙니다.");
            }
        }

        if (frameType == MCFrameType.Frame4E && ReadSerialNumber(frame) != expectedSerialNumber)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "응답 시리얼 번호가 요청과 다릅니다.");
        }
    }

    private static void ValidateLength(byte[] frame, int headerLength)
    {
        int lengthField = BinaryBuffer.ReadAsciiHex(frame, headerLength - 4, 4);
        if (frame.Length != headerLength + lengthField)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"응답 길이 필드({lengthField})가 실제 프레임 크기와 맞지 않습니다. (프레임: {frame.Length}문자)");
        }
    }

    private static byte[] ParseWords(byte[] data, ushort pointCount)
    {
        if (data.Length != pointCount * WordCharacterCount)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"읽은 워드 데이터 크기가 맞지 않습니다. (기대: {pointCount * WordCharacterCount}문자, 수신: {data.Length}문자)");
        }

        var words = new byte[pointCount * 2];
        for (int wordIndex = 0; wordIndex < pointCount; wordIndex++)
        {
            ushort word = (ushort)BinaryBuffer.ReadAsciiHex(data, wordIndex * WordCharacterCount, WordCharacterCount);
            BinaryBuffer.WriteUInt16LE(words, wordIndex * 2, word);
        }

        return words;
    }

    private static byte[] ParseBits(byte[] data, ushort pointCount)
    {
        if (data.Length != pointCount)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"읽은 비트 데이터 크기가 맞지 않습니다. (기대: {pointCount}문자, 수신: {data.Length}문자)");
        }

        var bits = new byte[pointCount];
        for (int bitIndex = 0; bitIndex < pointCount; bitIndex++)
        {
            bits[bitIndex] = data[bitIndex] switch
            {
                (byte)'1' => 1,
                (byte)'0' => 0,
                _ => throw new PlcException(PlcErrorKind.InvalidResponse, "비트 데이터에 '0' 또는 '1'이 아닌 문자가 있습니다.")
            };
        }

        return bits;
    }
}
