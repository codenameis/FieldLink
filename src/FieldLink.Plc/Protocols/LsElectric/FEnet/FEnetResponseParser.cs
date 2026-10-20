using System;
using System.Text;

namespace FieldLink.Plc.Protocols.LsElectric.FEnet;

/// <summary>LS ELECTRIC XGT FEnet 전용 통신 응답 프레임을 해석하는 파서입니다. (사양서 §3, §6)</summary>
public static class FEnetResponseParser
{
    private static readonly byte[] CompanyId = Encoding.ASCII.GetBytes("LSIS-XGT");

    private const ushort CommandReadResponse = 0x0055;
    private const ushort CommandWriteResponse = 0x0059;

    /// <summary>연속 읽기 응답 프레임을 검증하고 데이터 바이트를 추출합니다.</summary>
    /// <param name="responseFrame">수신된 전체 응답 프레임입니다.</param>
    /// <param name="expectedInvokeId">기대하는 트랜잭션 번호입니다.</param>
    /// <returns>PLC에서 수신된 원시 바이트 데이터입니다.</returns>
    public static byte[] ParseContinuousRead(byte[] responseFrame, ushort expectedInvokeId)
    {
        ValidateHeader(responseFrame, expectedInvokeId);

        int offset = 20;
        ushort command = ReadUInt16LE(responseFrame, offset);
        if (command != CommandReadResponse)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"기대하지 않은 읽기 응답 명령어 코드: 0x{command:X4}");
        }

        CheckErrorState(responseFrame, offset + 6);

        ushort dataLength = ReadUInt16LE(responseFrame, offset + 12);
        if (responseFrame.Length < offset + 14 + dataLength)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "응답 프레임 크기가 반환된 데이터 크기보다 작습니다.");
        }

        byte[] data = new byte[dataLength];
        Array.Copy(responseFrame, offset + 14, data, 0, dataLength);
        return data;
    }

    /// <summary>연속 쓰기 응답 프레임을 검증합니다.</summary>
    /// <param name="responseFrame">수신된 전체 응답 프레임입니다.</param>
    /// <param name="expectedInvokeId">기대하는 트랜잭션 번호입니다.</param>
    public static void ParseContinuousWrite(byte[] responseFrame, ushort expectedInvokeId)
    {
        ValidateHeader(responseFrame, expectedInvokeId);

        int offset = 20;
        ushort command = ReadUInt16LE(responseFrame, offset);
        if (command != CommandWriteResponse)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"기대하지 않은 쓰기 응답 명령어 코드: 0x{command:X4}");
        }

        CheckErrorState(responseFrame, offset + 6);
    }

    /// <summary>20바이트 공통 헤더의 정합성을 검증합니다.</summary>
    public static void ValidateHeader(byte[] frame, ushort expectedInvokeId)
    {
        if (frame is null || frame.Length < 20)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "프레임 크기가 헤더 최소 크기(20바이트)보다 작습니다.");
        }

        for (int i = 0; i < CompanyId.Length; i++)
        {
            if (frame[i] != CompanyId[i])
            {
                throw new PlcException(PlcErrorKind.InvalidResponse, "헤더 Company ID가 'LSIS-XGT'와 일치하지 않습니다.");
            }
        }

        ushort receivedInvokeId = ReadUInt16LE(frame, 14);
        if (receivedInvokeId != expectedInvokeId)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"트랜잭션 ID 불일치: 기대={expectedInvokeId}, 수신={receivedInvokeId}");
        }

        byte calculatedBcc = 0;
        for (int i = 0; i < 19; i++)
        {
            calculatedBcc = (byte)(calculatedBcc + frame[i]);
        }

        if (frame[19] != calculatedBcc)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"헤더 BCC 오류: 계산값=0x{calculatedBcc:X2}, 수신값=0x{frame[19]:X2}");
        }
    }

    private static void CheckErrorState(byte[] frame, int errorStateOffset)
    {
        ushort errorState = ReadUInt16LE(frame, errorStateOffset);
        if (errorState != 0)
        {
            ushort errorCode = ReadUInt16LE(frame, errorStateOffset + 2);
            var feErrorCode = (FEnetErrorCode)errorCode;
            throw new PlcDeviceException(errorCode, feErrorCode.GetDescription());
        }
    }

    private static ushort ReadUInt16LE(byte[] buffer, int offset)
    {
        return (ushort)(buffer[offset] | (buffer[offset + 1] << 8));
    }
}
