using System;
using System.Text;
using FieldLink.Plc.Conversion;

namespace FieldLink.Plc.Protocols.LsElectric.FEnet;

/// <summary>LS ELECTRIC XGT FEnet 전용 통신 응답 프레임을 해석하는 파서입니다. (사양서 §3, §6)</summary>
public static class FEnetResponseParser
{
    private static readonly byte[] CompanyId = Encoding.ASCII.GetBytes("LSIS-XGT");

    private const ushort CommandReadResponse = 0x0055;
    private const ushort CommandWriteResponse = 0x0059;
    private const int HeaderLength = 20;
    private const int InvokeIdOffset = 14;
    private const int HeaderChecksumOffset = 19;
    private const int MinimumReadBodyLength = 12;
    private const int MinimumWriteBodyLength = 8;
    private const int LegacyReadBodyLength = 14;
    private const int ErrorStateFieldOffset = 6;
    private const int BlockCountFieldOffset = 8;
    private const int DataLengthFieldOffset = 10;
    private const int LegacyDataLengthFieldOffset = 12;
    private const ushort LegacySingleBlockMarker = 1;
    private const int LengthFieldBytes = 2;
    private const int ErrorStateAndCodeBytes = 4;
    private const int ErrorCodeRelativeOffset = 2;
    private const ushort SuccessErrorState = 0;

    /// <summary>연속 읽기 응답 프레임을 검증하고 데이터 바이트를 추출합니다.</summary>
    public static byte[] ParseContinuousRead(byte[] responseFrame, ushort expectedInvokeId)
    {
        ValidateHeader(responseFrame, expectedInvokeId);

        int offset = HeaderLength;
        if (responseFrame.Length < offset + MinimumReadBodyLength)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"FEnet 읽기 응답 길이가 너무 짧습니다. (길이: {responseFrame.Length})");
        }

        ushort command = BinaryBuffer.ReadUInt16LE(responseFrame, offset);
        if (command != CommandReadResponse)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"기대하지 않은 읽기 응답 명령어 코드: 0x{command:X4}");
        }

        CheckErrorState(responseFrame, offset + ErrorStateFieldOffset);

        // XGT 공식 매뉴얼 규격:
        // offset + 0 (2B): Command (0x0055)
        // offset + 2 (2B): DataType (0x0014)
        // offset + 4 (2B): Reserved (0x0000)
        // offset + 6 (2B): ErrorState (0x0000)
        // offset + 8 (2B): BlockCount (0x0001)
        // offset + 10 (2B): DataByteLength
        // offset + 12 (NB): Data
        ushort blockCount = BinaryBuffer.ReadUInt16LE(responseFrame, offset + BlockCountFieldOffset);
        int dataLengthOffset = (blockCount == 0
            && responseFrame.Length >= offset + LegacyReadBodyLength
            && BinaryBuffer.ReadUInt16LE(responseFrame, offset + DataLengthFieldOffset) == LegacySingleBlockMarker)
            ? offset + LegacyDataLengthFieldOffset // 레거시 14바이트 명령어 헤더 호환
            : offset + DataLengthFieldOffset; // 공식 XGT FEnet 매뉴얼 표준 (12바이트 명령어 헤더)

        int dataStartOffset = dataLengthOffset + LengthFieldBytes;
        ushort dataLength = BinaryBuffer.ReadUInt16LE(responseFrame, dataLengthOffset);

        if (responseFrame.Length < dataStartOffset + dataLength)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "응답 프레임 크기가 반환된 데이터 크기보다 작습니다.");
        }

        byte[] data = new byte[dataLength];
        Buffer.BlockCopy(responseFrame, dataStartOffset, data, 0, dataLength);
        return data;
    }

    /// <summary>연속 쓰기 응답 프레임을 검증합니다.</summary>
    public static void ParseContinuousWrite(byte[] responseFrame, ushort expectedInvokeId)
    {
        ValidateHeader(responseFrame, expectedInvokeId);

        int offset = HeaderLength;
        if (responseFrame.Length < offset + MinimumWriteBodyLength)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"FEnet 쓰기 응답 길이가 너무 짧습니다. (길이: {responseFrame.Length})");
        }

        ushort command = BinaryBuffer.ReadUInt16LE(responseFrame, offset);
        if (command != CommandWriteResponse)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"기대하지 않은 쓰기 응답 명령어 코드: 0x{command:X4}");
        }

        CheckErrorState(responseFrame, offset + ErrorStateFieldOffset);
    }

    /// <summary>20바이트 공통 헤더의 정합성을 검증합니다.</summary>
    public static void ValidateHeader(byte[] frame, ushort expectedInvokeId)
    {
        if (frame is null || frame.Length < HeaderLength)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"프레임 크기가 헤더 최소 크기({HeaderLength}바이트)보다 작습니다.");
        }

        for (int i = 0; i < CompanyId.Length; i++)
        {
            if (frame[i] != CompanyId[i])
            {
                throw new PlcException(PlcErrorKind.InvalidResponse, "헤더 Company ID가 'LSIS-XGT'와 일치하지 않습니다.");
            }
        }

        ushort receivedInvokeId = BinaryBuffer.ReadUInt16LE(frame, InvokeIdOffset);
        if (receivedInvokeId != expectedInvokeId)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"트랜잭션 ID 불일치: 기대={expectedInvokeId}, 수신={receivedInvokeId}");
        }

        byte calculatedBcc = 0;
        for (int i = 0; i < HeaderChecksumOffset; i++)
        {
            calculatedBcc = (byte)(calculatedBcc + frame[i]);
        }

        if (frame[HeaderChecksumOffset] != calculatedBcc)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"헤더 BCC 오류: 계산값=0x{calculatedBcc:X2}, 수신값=0x{frame[HeaderChecksumOffset]:X2}");
        }
    }

    private static void CheckErrorState(byte[] frame, int errorStateOffset)
    {
        ushort errorState = BinaryBuffer.ReadUInt16LE(frame, errorStateOffset);
        if (errorState != SuccessErrorState)
        {
            if (frame.Length < errorStateOffset + ErrorStateAndCodeBytes)
            {
                throw new PlcException(PlcErrorKind.InvalidResponse, "FEnet 에러 응답에 에러 코드 필드가 누락되었습니다.");
            }

            ushort errorCode = BinaryBuffer.ReadUInt16LE(frame, errorStateOffset + ErrorCodeRelativeOffset);
            var feErrorCode = (FEnetErrorCode)errorCode;
            throw new PlcDeviceException(errorCode, feErrorCode.GetDescription());
        }
    }
}
