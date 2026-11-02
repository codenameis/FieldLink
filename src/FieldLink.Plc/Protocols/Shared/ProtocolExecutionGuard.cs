using System;
using FieldLink.Plc.Abstractions;
using FieldLink.Transport;

namespace FieldLink.Plc.Protocols.Shared;

/// <summary>
/// 프로토콜 공통 입력 검증, 바이트·문자열 워드 정규화, 분할 쓰기 부분 완료 예외 포장을 담당합니다.
/// </summary>
internal static class ProtocolExecutionGuard
{
    private const int DefaultBytesPerWord = 2;
    private const int BytesPer32BitUnit = 4;

    /// <summary>
    /// 주소의 비트/워드 구분과 요청 자료형이 서로 호환되는지 검증합니다.
    /// </summary>
    /// <param name="isBitAddress">해석된 주소가 비트 영역인지 여부입니다.</param>
    /// <param name="dataType">요청된 PLC 자료형입니다.</param>
    /// <exception cref="ArgumentException">비트 주소에 비불리언 자료형을 요청했거나 워드 주소에 불리언 자료형을 요청했을 때.</exception>
    public static void ValidateAddressAndDataType(bool isBitAddress, PlcDataType dataType)
    {
        if (isBitAddress && dataType != PlcDataType.Boolean)
        {
            throw new ArgumentException("비트 주소에는 Boolean 자료형만 사용할 수 있습니다.", nameof(dataType));
        }

        if (!isBitAddress && dataType == PlcDataType.Boolean)
        {
            throw new ArgumentException("워드 주소에는 Boolean 자료형을 사용할 수 없습니다.", nameof(dataType));
        }
    }

    /// <summary>
    /// 워드 기반 프로토콜에서 <see cref="PlcDataType.Byte"/> 또는 <see cref="PlcDataType.String"/> 읽기 요청을
    /// 디바이스 단위(기본 16비트 워드, 32비트 카운터는 4바이트) 읽기 요청으로 정규화합니다.
    /// </summary>
    /// <param name="request">원본 읽기 요청입니다.</param>
    /// <param name="bytesPerDeviceUnit">디바이스 주소 1단위당 바이트 수입니다. (기본 2바이트)</param>
    /// <returns>정규화된 읽기 요청입니다.</returns>
    public static PlcReadRequest NormalizeWordReadRequest(
        PlcReadRequest request,
        int bytesPerDeviceUnit = DefaultBytesPerWord)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (request.DataType is not (PlcDataType.Byte or PlcDataType.String))
        {
            return request;
        }

        int unitCount = (request.ByteCount + bytesPerDeviceUnit - 1) / bytesPerDeviceUnit;
        PlcDataType unitDataType = bytesPerDeviceUnit == BytesPer32BitUnit
            ? PlcDataType.UInt32
            : PlcDataType.UInt16;

        return new PlcReadRequest(request.Address, unitDataType, unitCount);
    }

    /// <summary>
    /// 워드 단위로 읽은 바이트 버퍼를 원본 요청의 바이트 길이에 맞게 잘라 반환합니다.
    /// </summary>
    /// <param name="buffer">워드 단위로 읽은 바이트 배열입니다.</param>
    /// <param name="requestedByteCount">원본 요청이 기대하는 바이트 수입니다.</param>
    /// <returns>길이가 맞춰진 바이트 배열입니다.</returns>
    public static byte[] TrimReadResult(byte[] buffer, int requestedByteCount)
    {
        if (buffer is null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        if (buffer.Length == requestedByteCount)
        {
            return buffer;
        }

        byte[] trimmed = new byte[requestedByteCount];
        Buffer.BlockCopy(buffer, 0, trimmed, 0, requestedByteCount);
        return trimmed;
    }

    /// <summary>
    /// 워드 기반 프로토콜에서 <see cref="PlcDataType.Byte"/> 또는 <see cref="PlcDataType.String"/> 쓰기 요청을
    /// 디바이스 단위(기본 16비트 워드, 32비트 카운터는 4바이트) 쓰기 요청으로 검증·정규화합니다.
    /// 인접 레지스터 데이터 오염을 방지하기 위해 바이트 수가 디바이스 단위의 배수가 아니면 <see cref="ArgumentException"/>을 발생시킵니다.
    /// </summary>
    /// <param name="request">원본 쓰기 요청입니다.</param>
    /// <param name="originalElementsPerUnit">정규화된 1단위당 원본 원소(바이트) 수입니다.</param>
    /// <param name="bytesPerDeviceUnit">디바이스 주소 1단위당 바이트 수입니다. (기본 2바이트)</param>
    /// <returns>정규화된 쓰기 요청입니다.</returns>
    /// <exception cref="ArgumentException">바이트 길이가 <paramref name="bytesPerDeviceUnit"/>의 배수가 아닐 때.</exception>
    public static PlcWriteRequest NormalizeWordWriteRequest(
        PlcWriteRequest request,
        out int originalElementsPerUnit,
        int bytesPerDeviceUnit = DefaultBytesPerWord)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (request.DataType is not (PlcDataType.Byte or PlcDataType.String))
        {
            originalElementsPerUnit = 1;
            return request;
        }

        originalElementsPerUnit = bytesPerDeviceUnit;
        byte[] rawBytes = request.GetData();
        if (rawBytes.Length % bytesPerDeviceUnit != 0)
        {
            throw new ArgumentException(
                $"워드 디바이스 쓰기 데이터 바이트 수({rawBytes.Length})는 {bytesPerDeviceUnit}의 배수여야 합니다.",
                nameof(request));
        }

        PlcDataType unitDataType = bytesPerDeviceUnit == BytesPer32BitUnit
            ? PlcDataType.UInt32
            : PlcDataType.UInt16;

        return new PlcWriteRequest(request.Address, unitDataType, rawBytes);
    }

    /// <summary>
    /// 정규화된 쓰기 청크 완료 시 원본 요청 기준의 완료 원소 수를 갱신합니다.
    /// </summary>
    public static int AdvanceCompletedCount(
        int currentCompleted,
        int chunkElementCount,
        int originalElementsPerUnit,
        int totalOriginalCount)
    {
        int nextCompleted = currentCompleted + (chunkElementCount * originalElementsPerUnit);
        return Math.Min(totalOriginalCount, nextCompleted);
    }

    /// <summary>
    /// 분할 쓰기 도중 발생한 예외에 부분 완료 원소 수를 담아야 하는지 판별합니다.
    /// </summary>
    public static bool ShouldWrapWriteException(Exception exception, int completedElements)
    {
        return completedElements > 0 && exception is PlcException or TransportException;
    }

    /// <summary>
    /// 분할 쓰기 도중 발생한 예외에 완료된 원소 수를 기록한 새 예외를 생성합니다.
    /// </summary>
    public static Exception WrapWriteException(Exception exception, int completedElements)
    {
        if (exception is null)
        {
            throw new ArgumentNullException(nameof(exception));
        }

        if (completedElements <= 0)
        {
            return exception;
        }

        if (exception is PlcDeviceException deviceException)
        {
            return new PlcDeviceException(deviceException.DeviceErrorCode, deviceException.Message)
            {
                CompletedCount = completedElements,
            };
        }

        if (exception is PlcException plcException)
        {
            return new PlcException(plcException.Kind, plcException.Message, plcException.InnerException)
            {
                CompletedCount = completedElements,
            };
        }

        if (exception is TransportException transportException)
        {
            PlcErrorKind errorKind = MapTransportFailure(transportException.Failure);
            return new PlcException(errorKind, transportException.Message, transportException)
            {
                CompletedCount = completedElements,
            };
        }

        return exception;
    }

    private static PlcErrorKind MapTransportFailure(TransportFailure failure)
    {
        return failure switch
        {
            TransportFailure.ConnectFailed => PlcErrorKind.ConnectionFailed,
            TransportFailure.InvalidFrame => PlcErrorKind.InvalidResponse,
            _ => PlcErrorKind.ConnectionLost,
        };
    }
}
