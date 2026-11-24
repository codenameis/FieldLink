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
    private const ushort DataTypeContinuous = 0x0014;
    private const byte SourceServer = 0x11;
    private const int HeaderLength = 20;
    private const int SourceOffset = 13;
    private const int InvokeIdOffset = 14;
    private const int InstructionLengthOffset = 16;
    private const int HeaderChecksumOffset = 19;
    private const int MinimumErrorCheckBodyLength = 8;
    private const int MinimumReadBodyLength = 12;
    private const int MinimumWriteBodyLength = 8;
    private const int StandardWriteBodyLength = 10;
    private const int LegacyReadBodyLength = 14;
    private const int DataTypeFieldOffset = 2;
    private const int ErrorStateFieldOffset = 6;
    private const int BlockCountFieldOffset = 8;
    private const int DataLengthFieldOffset = 10;
    private const int LegacyDataLengthFieldOffset = 12;
    private const ushort SingleBlockCount = 1;
    private const int LengthFieldBytes = 2;
    private const int ErrorStateAndCodeBytes = 4;
    private const int ErrorCodeRelativeOffset = 2;
    private const ushort SuccessErrorState = 0;

    /// <summary>연속 읽기 응답 프레임을 검증하고 데이터 바이트를 추출합니다.</summary>
    public static byte[] ParseContinuousRead(byte[] responseFrame, ushort expectedInvokeId)
    {
        ValidateHeader(responseFrame, expectedInvokeId);

        int offset = HeaderLength;
        if (responseFrame.Length < offset + MinimumErrorCheckBodyLength)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"FEnet 읽기 응답 길이가 너무 짧습니다. (길이: {responseFrame.Length})");
        }

        ushort command = BinaryBuffer.ReadUInt16LittleEndian(responseFrame, offset);
        if (command != CommandReadResponse)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"기대하지 않은 읽기 응답 명령어 코드: 0x{command:X4}");
        }

        ushort dataType = BinaryBuffer.ReadUInt16LittleEndian(responseFrame, offset + DataTypeFieldOffset);
        if (dataType != DataTypeContinuous)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"기대하지 않은 읽기 응답 데이터 타입: 0x{dataType:X4}");
        }

        // 사양서 §3-3: 30바이트 오류 응답(헤더 20B + 오류 본문 10B)은 정상 읽기 본문(12B 이상)보다 짧으므로 오류 상태를 먼저 확인한다.
        CheckErrorState(responseFrame, offset + ErrorStateFieldOffset);

        if (responseFrame.Length < offset + MinimumReadBodyLength)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"FEnet 읽기 응답 길이가 너무 짧습니다. (길이: {responseFrame.Length})");
        }

        int dataLengthOffset = ResolveDataLengthOffset(responseFrame, offset);
        int dataStartOffset = dataLengthOffset + LengthFieldBytes;
        ushort dataLength = BinaryBuffer.ReadUInt16LittleEndian(responseFrame, dataLengthOffset);

        if (responseFrame.Length != dataStartOffset + dataLength)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"응답 프레임 크기({responseFrame.Length})가 선언된 데이터 크기({dataStartOffset + dataLength})와 일치하지 않습니다.");
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
        if (responseFrame.Length < offset + MinimumErrorCheckBodyLength)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"FEnet 쓰기 응답 길이가 너무 짧습니다. (길이: {responseFrame.Length})");
        }

        ushort command = BinaryBuffer.ReadUInt16LittleEndian(responseFrame, offset);
        if (command != CommandWriteResponse)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"기대하지 않은 쓰기 응답 명령어 코드: 0x{command:X4}");
        }

        ushort dataType = BinaryBuffer.ReadUInt16LittleEndian(responseFrame, offset + DataTypeFieldOffset);
        if (dataType != DataTypeContinuous)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"기대하지 않은 쓰기 응답 데이터 타입: 0x{dataType:X4}");
        }

        CheckErrorState(responseFrame, offset + ErrorStateFieldOffset);

        if (responseFrame.Length != offset + MinimumWriteBodyLength && responseFrame.Length != offset + StandardWriteBodyLength)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"FEnet 쓰기 응답 크기가 올바르지 않습니다. (길이: {responseFrame.Length})");
        }
    }

    /// <summary>20바이트 공통 헤더의 정합성을 검증합니다.</summary>
    public static void ValidateHeader(byte[] frame, ushort expectedInvokeId)
    {
        if (frame is null)
        {
            throw new ArgumentNullException(nameof(frame));
        }

        if (frame.Length < HeaderLength)
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

        if (frame[SourceOffset] != SourceServer)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"응답 프레임 방향(Source)이 서버 응답(0x{SourceServer:X2})이 아닙니다. (수신: 0x{frame[SourceOffset]:X2})");
        }

        ushort receivedInvokeId = BinaryBuffer.ReadUInt16LittleEndian(frame, InvokeIdOffset);
        if (receivedInvokeId != expectedInvokeId)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"트랜잭션 ID 불일치: 기대={expectedInvokeId}, 수신={receivedInvokeId}");
        }

        ushort declaredLength = BinaryBuffer.ReadUInt16LittleEndian(frame, InstructionLengthOffset);
        if (declaredLength != frame.Length - HeaderLength)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"헤더 길이 필드({declaredLength})가 실제 본문 크기({frame.Length - HeaderLength})와 일치하지 않습니다.");
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

    private static int ResolveDataLengthOffset(byte[] responseFrame, int bodyOffset)
    {
        ushort firstWord = BinaryBuffer.ReadUInt16LittleEndian(responseFrame, bodyOffset + BlockCountFieldOffset);
        if (firstWord == SingleBlockCount)
        {
            return bodyOffset + DataLengthFieldOffset;
        }

        if (firstWord == SuccessErrorState
            && responseFrame.Length >= bodyOffset + LegacyReadBodyLength
            && BinaryBuffer.ReadUInt16LittleEndian(responseFrame, bodyOffset + DataLengthFieldOffset) == SingleBlockCount)
        {
            return bodyOffset + LegacyDataLengthFieldOffset;
        }

        throw new PlcException(PlcErrorKind.InvalidResponse, $"FEnet 읽기 응답의 블록 수가 {SingleBlockCount}이 아닙니다.");
    }

    private static void CheckErrorState(byte[] frame, int errorStateOffset)
    {
        ushort errorState = BinaryBuffer.ReadUInt16LittleEndian(frame, errorStateOffset);
        if (errorState != SuccessErrorState)
        {
            if (frame.Length < errorStateOffset + ErrorStateAndCodeBytes)
            {
                throw new PlcException(PlcErrorKind.InvalidResponse, "FEnet 에러 응답에 에러 코드 필드가 누락되었습니다.");
            }

            ushort errorCode = BinaryBuffer.ReadUInt16LittleEndian(frame, errorStateOffset + ErrorCodeRelativeOffset);
            var feErrorCode = (FEnetErrorCode)errorCode;
            throw new PlcDeviceException(errorCode, feErrorCode.GetDescription());
        }
    }
}
