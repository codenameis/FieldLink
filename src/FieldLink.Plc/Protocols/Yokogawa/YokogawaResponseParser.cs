using System;
using FieldLink.Plc.Conversion;

namespace FieldLink.Plc.Protocols.Yokogawa;

/// <summary>Yokogawa FA-M3 바이너리 링크 응답 파서입니다. (사양서 §2)</summary>
public static class YokogawaResponseParser
{
    private const int HeaderLength = 4;
    private const int SubheaderOffset = 0;
    private const int EndCodeOffset = 1;
    private const int SizeFieldOffset = 2;
    private const int ErrorDetailPayloadLength = 2;
    private const int DetailCpuOffset = 4;
    private const int DetailCodeOffset = 5;
    private const int UpperNibbleMask = 0xF0;
    private const byte ResponseFlagMask = 0x80;
    private const byte EndCodeSuccess = 0x00;

    /// <summary>읽기 응답 프레임에서 데이터 바이트를 추출합니다.</summary>
    public static byte[] ParseReadResponse(byte[] frame, byte expectedCommand)
    {
        int payloadSize = ValidateHeader(frame, expectedCommand);
        byte[] data = new byte[payloadSize];
        Buffer.BlockCopy(frame, HeaderLength, data, 0, payloadSize);
        return data;
    }

    /// <summary>쓰기 응답 프레임의 성공 여부를 검증합니다.</summary>
    public static void VerifyWriteResponse(byte[] frame, byte expectedCommand)
    {
        int payloadSize = ValidateHeader(frame, expectedCommand);
        if (payloadSize != 0)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"Yokogawa 쓰기 응답에 예상치 못한 데이터({payloadSize}바이트)가 포함되었습니다.");
        }
    }

    private static int ValidateHeader(byte[] frame, byte expectedCommand)
    {
        if (frame is null || frame.Length < HeaderLength)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"응답 길이가 너무 짧습니다: {frame?.Length ?? 0}바이트");
        }

        byte expectedSubheader = (byte)(expectedCommand | ResponseFlagMask);
        if (frame[SubheaderOffset] != expectedSubheader)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"서브헤더 응답 불일치: 기대=0x{expectedSubheader:X2}, 수신=0x{frame[SubheaderOffset]:X2}");
        }

        // IM 34M06H24-08E §5.3.2 / 사양서 §2.2 4단계: 선언된 크기와 실제 본문 길이(frame.Length - 4)가 다르면 손상 프레임으로 처리한다.
        int declaredSize = BinaryBuffer.ReadUInt16BigEndian(frame, SizeFieldOffset);
        if (frame.Length != HeaderLength + declaredSize)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"응답 프레임 길이가 선언된 크기와 일치하지 않습니다: 선언={declaredSize}, 실제 본문={frame.Length - HeaderLength}");
        }

        byte endCode = frame[EndCodeOffset];
        if (endCode != EndCodeSuccess)
        {
            ThrowDeviceException(frame, endCode, declaredSize);
        }

        return declaredSize;
    }

    private static void ThrowDeviceException(byte[] frame, byte endCode, int declaredSize)
    {
        if (declaredSize != 0 && declaredSize != ErrorDetailPayloadLength)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"Yokogawa 오류 응답 파라미터 길이가 올바르지 않습니다: {declaredSize}바이트");
        }

        var errorCode = (YokogawaErrorCode)endCode;
        string baseMessage = errorCode.GetDescription();

        if (declaredSize == ErrorDetailPayloadLength && HasValidDetailCode(errorCode))
        {
            byte cpuNumber = frame[DetailCpuOffset];
            byte detailCode = frame[DetailCodeOffset];
            string detailCategory = DescribeDetailCategory(detailCode);
            throw new PlcDeviceException(
                endCode,
                $"{baseMessage} [CPU={cpuNumber}, 상세 코드=0x{detailCode:X2}: {detailCategory}]");
        }

        throw new PlcDeviceException(endCode, baseMessage);
    }

    private static bool HasValidDetailCode(YokogawaErrorCode errorCode) => errorCode is
        YokogawaErrorCode.DeviceError or
        YokogawaErrorCode.SettingValueOutOfRange or
        YokogawaErrorCode.DataCountOutOfRange or
        YokogawaErrorCode.ParameterError or
        YokogawaErrorCode.CpuProcessingError;

    private static string DescribeDetailCategory(byte detailCode) => (detailCode & UpperNibbleMask) switch
    {
        0x10 => "자기진단 오류",
        0x20 => "프로그램/파라미터 오류",
        0x40 => "CPU 통신 오류",
        0x80 => "디바이스 액세스 오류",
        0x90 => "명령 오류",
        0xA0 => "파라미터 오류",
        0xB0 => "운전 모드 오류",
        0xC0 => "파라미터 오류",
        0xF0 => "시스템 오류",
        _ => "기타 상세 오류",
    };
}
