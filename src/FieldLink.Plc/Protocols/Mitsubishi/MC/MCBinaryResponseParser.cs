using System;

namespace FieldLink.Plc.Protocols.Mitsubishi.MC;

/// <summary>MC 프로토콜 바이너리 응답 프레임(3E·4E)을 검증하고 데이터를 꺼내는 순수 함수 모음입니다. (사양서 §3-1, §3-4)</summary>
public static class MCBinaryResponseParser
{
    private const int Header3ELength = 9;
    private const int Header4ELength = 13;
    private const int EndCodeByteCount = 2;
    private const ushort Response3ESubheader = 0x00D0;
    private const ushort Response4ESubheader = 0x00D4;

    /// <summary>4E 응답 프레임의 시리얼 번호를 읽습니다. 늦게 도착한 응답을 걸러낼 때 씁니다.</summary>
    /// <exception cref="PlcException">프레임이 너무 짧을 때.</exception>
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

        return (ushort)(frame[2] | (frame[3] << 8));
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
        return address.IsBit ? UnpackBits(data, pointCount) : CopyWords(data, pointCount);
    }

    /// <summary>일괄 쓰기 응답을 검증합니다.</summary>
    /// <exception cref="PlcDeviceException">종료 코드가 정상이 아닐 때.</exception>
    /// <exception cref="PlcException">프레임이 손상되었거나 형식에 맞지 않을 때.</exception>
    public static void ParseWrite(MCOptions options, ushort expectedSerialNumber, byte[] frame)
    {
        byte[] data = ExtractData(options, expectedSerialNumber, frame);
        if (data.Length != 0)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"쓰기 응답에 불필요한 데이터가 있습니다. ({data.Length}바이트)");
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
        if (frame.Length < headerLength + EndCodeByteCount)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"응답 프레임이 너무 짧습니다. (길이: {frame.Length})");
        }

        ValidateHeader(options.FrameType, expectedSerialNumber, frame);
        ValidateLength(frame, headerLength);
        MCEndCodeExtensions.EnsureSuccess((ushort)(frame[headerLength] | (frame[headerLength + 1] << 8)));

        var data = new byte[frame.Length - headerLength - EndCodeByteCount];
        Array.Copy(frame, headerLength + EndCodeByteCount, data, 0, data.Length);
        return data;
    }

    private static void ValidateHeader(MCFrameType frameType, ushort expectedSerialNumber, byte[] frame)
    {
        ushort subheader = (ushort)(frame[0] | (frame[1] << 8));
        ushort expectedSubheader = frameType == MCFrameType.Frame4E ? Response4ESubheader : Response3ESubheader;
        if (subheader != expectedSubheader)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"응답 서브헤더가 올바르지 않습니다. (수신: 0x{subheader:X4}, 기대: 0x{expectedSubheader:X4})");
        }

        if (frameType == MCFrameType.Frame4E && ReadSerialNumber(frame) != expectedSerialNumber)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "응답 시리얼 번호가 요청과 다릅니다.");
        }
    }

    private static void ValidateLength(byte[] frame, int headerLength)
    {
        int lengthField = frame[headerLength - 2] | (frame[headerLength - 1] << 8);
        if (frame.Length != headerLength + lengthField)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"응답 길이 필드({lengthField})가 실제 프레임 크기와 맞지 않습니다. (프레임: {frame.Length}바이트)");
        }
    }

    private static byte[] CopyWords(byte[] data, ushort pointCount)
    {
        if (data.Length != pointCount * 2)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"읽은 워드 데이터 크기가 맞지 않습니다. (기대: {pointCount * 2}바이트, 수신: {data.Length}바이트)");
        }

        return data;
    }

    private static byte[] UnpackBits(byte[] data, ushort pointCount)
    {
        // 사양서 §4: 바이트 하나에 두 점. 첫 점은 상위 니블, 두 번째 점은 하위 니블.
        if (data.Length != (pointCount + 1) / 2)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"읽은 비트 데이터 크기가 맞지 않습니다. (기대: {(pointCount + 1) / 2}바이트, 수신: {data.Length}바이트)");
        }

        var bits = new byte[pointCount];
        for (int bitIndex = 0; bitIndex < pointCount; bitIndex++)
        {
            byte packed = data[bitIndex / 2];
            bool isOn = bitIndex % 2 == 0 ? (packed & 0x10) != 0 : (packed & 0x01) != 0;
            bits[bitIndex] = isOn ? (byte)1 : (byte)0;
        }

        return bits;
    }
}
