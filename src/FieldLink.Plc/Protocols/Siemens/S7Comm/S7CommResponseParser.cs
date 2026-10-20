using System;

namespace FieldLink.Plc.Protocols.Siemens.S7Comm;

/// <summary>S7 통신 응답 프레임(TPKT + COTP + S7 PDU)을 검증하고 값을 꺼내는 순수 함수 모음입니다. (사양서 §3, §6, §7)</summary>
public static class S7CommResponseParser
{
    private const int TpktHeaderLength = 4;
    private const int CotpDataHeaderLength = 3;
    private const int S7PduOffset = TpktHeaderLength + CotpDataHeaderLength;
    private const int S7ResponseHeaderLength = 12;
    private const int MinimumConnectionConfirmLength = 11;
    private const int MinimumNegotiatedPduLength = 29;
    private const byte TpktVersion = 0x03;
    private const byte CotpConnectionConfirm = 0xD0;
    private const byte CotpDataTransfer = 0xF0;
    private const byte ProtocolId = 0x32;
    private const byte MessageTypeAck = 0x02;
    private const byte MessageTypeAckData = 0x03;
    private const byte FunctionSetupCommunication = 0xF0;
    private const byte FunctionReadVariable = 0x04;
    private const byte FunctionWriteVariable = 0x05;
    private const byte DataTransportSizeBit = 0x03;
    private const byte DataTransportSizeByte = 0x04;
    private const int BitsPerByte = 8;
    private const int DataItemHeaderLength = 4;

    /// <summary>COTP 연결 확인 프레임을 검증합니다. (사양서 §3-3)</summary>
    /// <exception cref="PlcException">연결 확인이 아니거나 프레임이 손상되었을 때.</exception>
    public static void ParseConnectionConfirm(byte[] frame)
    {
        ValidateTpkt(frame, MinimumConnectionConfirmLength);

        if (frame[TpktHeaderLength] != frame.Length - TpktHeaderLength - 1)
        {
            throw new PlcException(PlcErrorKind.ConnectionFailed, "COTP 길이 필드가 프레임 크기와 맞지 않습니다.");
        }

        if (frame[TpktHeaderLength + 1] != CotpConnectionConfirm)
        {
            throw new PlcException(
                PlcErrorKind.ConnectionFailed,
                $"COTP 연결 확인(0xD0)이 아닙니다. (수신: 0x{frame[TpktHeaderLength + 1]:X2}) 랙·슬롯 설정을 확인하세요.");
        }
    }

    /// <summary>Setup Communication 응답에서 협상된 PDU 길이를 꺼냅니다. (사양서 §3-6)</summary>
    /// <returns>협상된 PDU 길이(바이트)입니다.</returns>
    /// <exception cref="PlcException">응답이 손상되었거나 협상된 길이가 너무 작을 때.</exception>
    public static int ParseSetupCommunication(byte[] frame, ushort expectedPduReference)
    {
        ParseS7Pdu(frame, expectedPduReference, out byte[] parameters, out byte[] dataSection);

        if (parameters.Length != 8 || parameters[0] != FunctionSetupCommunication || dataSection.Length != 0)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "Setup Communication 응답 형식이 올바르지 않습니다.");
        }

        int negotiatedPduLength = (parameters[6] << 8) | parameters[7];
        if (negotiatedPduLength < MinimumNegotiatedPduLength)
        {
            throw new PlcException(PlcErrorKind.ConnectionFailed, $"협상된 PDU 길이({negotiatedPduLength})가 너무 작습니다.");
        }

        return negotiatedPduLength;
    }

    /// <summary>응답 프레임의 PDU 참조를 읽습니다. 늦게 도착한 응답을 걸러낼 때 씁니다.</summary>
    /// <exception cref="PlcException">프레임이 너무 짧을 때.</exception>
    public static ushort ReadPduReference(byte[] frame)
    {
        if (frame is null)
        {
            throw new ArgumentNullException(nameof(frame));
        }

        if (frame.Length < S7PduOffset + S7ResponseHeaderLength)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"응답 프레임이 너무 짧습니다. (길이: {frame.Length})");
        }

        return (ushort)((frame[S7PduOffset + 4] << 8) | frame[S7PduOffset + 5]);
    }

    /// <summary>읽기 응답에서 값을 꺼냅니다. (사양서 §3-7)</summary>
    /// <param name="frame">수신한 응답 프레임 전체입니다.</param>
    /// <param name="expectedPduReference">요청에 사용한 PDU 참조입니다.</param>
    /// <param name="address">읽기를 요청한 주소입니다. 비트·바이트 구분에 씁니다.</param>
    /// <param name="count">요청한 바이트 수입니다. 비트 주소는 1입니다.</param>
    /// <returns>바이트 접근은 요청한 바이트들, 비트 접근은 0 또는 1 한 바이트입니다.</returns>
    /// <exception cref="PlcDeviceException">장치가 읽기를 거절했을 때.</exception>
    /// <exception cref="PlcException">응답이 손상되었거나 형식에 맞지 않을 때.</exception>
    public static byte[] ParseReadResponse(byte[] frame, ushort expectedPduReference, S7CommAddress address, ushort count)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        ParseS7Pdu(frame, expectedPduReference, out byte[] parameters, out byte[] dataSection);
        ValidateItemParameters(parameters, FunctionReadVariable);

        if (dataSection.Length == 0)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "읽기 응답에 데이터 구역이 없습니다.");
        }

        S7CommReturnCodeExtensions.EnsureSuccess(dataSection[0]);
        return address.IsBit ? ExtractBit(dataSection) : ExtractBytes(dataSection, count);
    }

    /// <summary>쓰기 응답을 검증합니다. (사양서 §3-8)</summary>
    /// <exception cref="PlcDeviceException">장치가 쓰기를 거절했을 때.</exception>
    /// <exception cref="PlcException">응답이 손상되었거나 형식에 맞지 않을 때.</exception>
    public static void ParseWriteResponse(byte[] frame, ushort expectedPduReference)
    {
        ParseS7Pdu(frame, expectedPduReference, out byte[] parameters, out byte[] dataSection);
        ValidateItemParameters(parameters, FunctionWriteVariable);

        if (dataSection.Length != 1)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "쓰기 응답 데이터 구역은 반환 코드 1바이트여야 합니다.");
        }

        S7CommReturnCodeExtensions.EnsureSuccess(dataSection[0]);
    }

    private static void ValidateTpkt(byte[] frame, int minimumLength)
    {
        if (frame is null)
        {
            throw new ArgumentNullException(nameof(frame));
        }

        if (frame.Length < minimumLength)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"응답 프레임이 너무 짧습니다. (길이: {frame.Length})");
        }

        if (frame[0] != TpktVersion)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"TPKT 버전이 3이 아닙니다. (수신: {frame[0]})");
        }

        int declaredLength = (frame[2] << 8) | frame[3];
        if (declaredLength != frame.Length)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"TPKT 길이({declaredLength})가 실제 프레임 크기({frame.Length})와 맞지 않습니다.");
        }
    }

    private static void ParseS7Pdu(byte[] frame, ushort expectedPduReference, out byte[] parameters, out byte[] dataSection)
    {
        ValidateTpkt(frame, S7PduOffset + S7ResponseHeaderLength);

        bool isDataTransfer = frame[TpktHeaderLength] == 0x02
            && frame[TpktHeaderLength + 1] == CotpDataTransfer
            && (frame[TpktHeaderLength + 2] & 0x7F) == 0;
        if (!isDataTransfer)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "COTP 데이터 전송 머리(02 F0 번호 0)가 아닙니다.");
        }

        ValidateS7Header(frame, expectedPduReference);

        int parameterLength = (frame[S7PduOffset + 6] << 8) | frame[S7PduOffset + 7];
        int dataLength = (frame[S7PduOffset + 8] << 8) | frame[S7PduOffset + 9];
        if (frame.Length != S7PduOffset + S7ResponseHeaderLength + parameterLength + dataLength)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "S7 파라미터·데이터 길이의 합이 프레임 크기와 맞지 않습니다.");
        }

        parameters = new byte[parameterLength];
        dataSection = new byte[dataLength];
        Array.Copy(frame, S7PduOffset + S7ResponseHeaderLength, parameters, 0, parameterLength);
        Array.Copy(frame, S7PduOffset + S7ResponseHeaderLength + parameterLength, dataSection, 0, dataLength);
    }

    private static void ValidateS7Header(byte[] frame, ushort expectedPduReference)
    {
        if (frame[S7PduOffset] != ProtocolId)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"S7 프로토콜 ID가 0x32가 아닙니다. (수신: 0x{frame[S7PduOffset]:X2})");
        }

        byte messageType = frame[S7PduOffset + 1];
        if (messageType != MessageTypeAck && messageType != MessageTypeAckData)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"응답 메시지 종류가 아닙니다. (수신: 0x{messageType:X2})");
        }

        // 사양서 §3-5: 오류 클래스·코드가 둘 다 0이 아니면 실패. 의미표는 미확인이므로 값을 그대로 노출한다.
        byte errorClass = frame[S7PduOffset + 10];
        byte errorCode = frame[S7PduOffset + 11];
        if (errorClass != 0 || errorCode != 0)
        {
            throw new PlcDeviceException(
                (errorClass << 8) | errorCode,
                $"S7 헤더 오류입니다. (클래스 0x{errorClass:X2}, 코드 0x{errorCode:X2}) 의미표는 확인되지 않았습니다.");
        }

        if (ReadPduReference(frame) != expectedPduReference)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "응답 PDU 참조가 요청과 다릅니다.");
        }
    }

    private static void ValidateItemParameters(byte[] parameters, byte expectedFunction)
    {
        if (parameters.Length != 2 || parameters[0] != expectedFunction || parameters[1] != 1)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"응답 파라미터가 기능 0x{expectedFunction:X2}의 항목 1개 응답이 아닙니다.");
        }
    }

    private static byte[] ExtractBytes(byte[] dataSection, ushort count)
    {
        if (dataSection.Length != DataItemHeaderLength + count)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"읽은 데이터 크기가 맞지 않습니다. (기대: {DataItemHeaderLength + count}바이트, 수신: {dataSection.Length}바이트)");
        }

        int declaredBitLength = (dataSection[2] << 8) | dataSection[3];
        if (dataSection[1] != DataTransportSizeByte || declaredBitLength != count * BitsPerByte)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "읽기 응답의 전송 크기 또는 길이가 요청과 맞지 않습니다.");
        }

        var values = new byte[count];
        Array.Copy(dataSection, DataItemHeaderLength, values, 0, count);
        return values;
    }

    private static byte[] ExtractBit(byte[] dataSection)
    {
        bool isValid = dataSection.Length == DataItemHeaderLength + 1
            && dataSection[1] == DataTransportSizeBit
            && ((dataSection[2] << 8) | dataSection[3]) == 1;
        if (!isValid)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "비트 읽기 응답 형식이 올바르지 않습니다. (전송 크기 0x03, 길이 1, 값 1바이트)");
        }

        return new[] { dataSection[DataItemHeaderLength] != 0 ? (byte)1 : (byte)0 };
    }
}
