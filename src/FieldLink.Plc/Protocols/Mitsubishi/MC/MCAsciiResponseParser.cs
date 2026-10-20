using System;

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

    /// <summary>4E 응답 프레임의 시리얼 번호를 읽습니다. 늦게 도착한 응답을 걸러낼 때 씁니다.</summary>
    /// <exception cref="PlcException">프레임이 너무 짧거나 16진 문자가 아닐 때.</exception>
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

        return (ushort)ReadHex(frame, 4, 4);
    }

    /// <summary>일괄 읽기 응답에서 데이터를 꺼냅니다.</summary>
    /// <param name="options">프레임 종류 설정입니다.</param>
    /// <param name="expectedSerialNumber">4E에서 기대하는 시리얼 번호입니다.</param>
    /// <param name="frame">수신한 응답 프레임 전체입니다.</param>
    /// <param name="address">읽기를 요청한 주소입니다. 비트·워드 구분에 씁니다.</param>
    /// <param name="pointCount">요청한 점수입니다.</param>
    /// <returns>워드 디바이스는 점수 × 2바이트(리틀 엔디언), 비트 디바이스는 점수 × 1바이트(0 또는 1)입니다.</returns>
    /// <exception cref="PlcDeviceException">종료 코드가 정상이 아닐 때.</exception>
    /// <exception cref="PlcException">프레임이 손상되었거나 형식에 맞지 않을 때.</exception>
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

    /// <summary>일괄 쓰기 응답을 검증합니다.</summary>
    /// <exception cref="PlcDeviceException">종료 코드가 정상이 아닐 때.</exception>
    /// <exception cref="PlcException">프레임이 손상되었거나 형식에 맞지 않을 때.</exception>
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
        MCEndCodeExtensions.EnsureSuccess((ushort)ReadHex(frame, headerLength, EndCodeCharacterCount));

        var data = new byte[frame.Length - headerLength - EndCodeCharacterCount];
        Array.Copy(frame, headerLength + EndCodeCharacterCount, data, 0, data.Length);
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
        int lengthField = ReadHex(frame, headerLength - 4, 4);
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

        // 사양서 §4: 워드는 상위 바이트가 먼저 오므로 리틀 엔디언 바이트로 뒤집어 돌려준다.
        var words = new byte[pointCount * 2];
        for (int wordIndex = 0; wordIndex < pointCount; wordIndex++)
        {
            int word = ReadHex(data, wordIndex * WordCharacterCount, WordCharacterCount);
            words[wordIndex * 2] = (byte)word;
            words[(wordIndex * 2) + 1] = (byte)(word >> 8);
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

    private static int ReadHex(byte[] buffer, int offset, int characterCount)
    {
        int value = 0;
        for (int index = 0; index < characterCount; index++)
        {
            value = (value << 4) | GetHexDigitValue(buffer[offset + index]);
        }

        return value;
    }

    private static int GetHexDigitValue(byte character)
    {
        if (character is >= (byte)'0' and <= (byte)'9')
        {
            return character - '0';
        }

        if (character is >= (byte)'A' and <= (byte)'F')
        {
            return character - 'A' + 10;
        }

        if (character is >= (byte)'a' and <= (byte)'f')
        {
            return character - 'a' + 10;
        }

        throw new PlcException(PlcErrorKind.InvalidResponse, $"16진 문자가 아닙니다. (0x{character:X2})");
    }
}
