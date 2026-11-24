using System;
using FieldLink.Plc.Conversion;

namespace FieldLink.Plc.Protocols.Yokogawa;

/// <summary>Yokogawa FA-M3 바이너리 링크 요청 프레임 조립기입니다. (사양서 §1.1~§1.4)</summary>
public static class YokogawaRequestBuilder
{
    private const byte MinCpuNumber = 1;
    private const byte MaxCpuNumber = 4;
    private const byte CommandReadBits = 0x01;
    private const byte CommandWriteBits = 0x02;
    private const byte CommandReadWords = 0x11;
    private const byte CommandWriteWords = 0x12;
    private const byte BitOffValue = 0x00;
    private const byte BitOnValue = 0x01;

    private const int HeaderLength = 4;
    private const ushort AddressParameterSize = 8;
    private const int WritePayloadOffset = HeaderLength + AddressParameterSize;

    private const int CommandOffset = 0;
    private const int CpuNumberOffset = 1;
    private const int ParameterSizeOffset = 2;
    private const int AttributeOffset = 4;
    private const int DeviceNumberOffset = 6;
    private const int CountOffset = 10;
    private const int BytesPerWord = 2;

    /// <summary>연속 워드/비트 읽기 요청 프레임을 생성합니다. (명령 0x11 / 0x01)</summary>
    public static byte[] BuildReadRequest(byte cpuNumber, YokogawaAddress address, ushort count)
    {
        ValidateCpuNumber(cpuNumber);
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (count == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "개수는 1 이상이어야 합니다.");
        }

        byte command = address.IsBit ? CommandReadBits : CommandReadWords;
        byte[] frame = new byte[HeaderLength + AddressParameterSize];
        WriteHeaderAndAddress(frame, command, cpuNumber, AddressParameterSize, address, count);
        return frame;
    }

    /// <summary>연속 워드 쓰기 요청 프레임을 생성합니다. (명령 0x12)</summary>
    public static byte[] BuildWriteWordRequest(byte cpuNumber, YokogawaAddress address, ushort count, byte[] wordBytes)
    {
        ValidateCpuNumber(cpuNumber);
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (address.IsBit)
        {
            throw new ArgumentException($"워드 영역 주소가 필요합니다: {address.Original}", nameof(address));
        }

        if (count == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "개수는 1 이상이어야 합니다.");
        }

        if (wordBytes is null)
        {
            throw new ArgumentNullException(nameof(wordBytes));
        }

        if (wordBytes.Length != count * BytesPerWord)
        {
            throw new ArgumentException($"워드 바이트 길이({wordBytes.Length})가 개수({count})와 일치하지 않습니다.", nameof(wordBytes));
        }

        return BuildWriteFrame(CommandWriteWords, cpuNumber, address, count, wordBytes);
    }

    /// <summary>연속 비트 쓰기 요청 프레임을 생성합니다. (명령 0x02)</summary>
    public static byte[] BuildWriteBitRequest(byte cpuNumber, YokogawaAddress address, ushort count, byte[] bitBytes)
    {
        ValidateCpuNumber(cpuNumber);
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (!address.IsBit)
        {
            throw new ArgumentException($"비트 접점 주소가 필요합니다: {address.Original}", nameof(address));
        }

        if (count == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "개수는 1 이상이어야 합니다.");
        }

        if (bitBytes is null)
        {
            throw new ArgumentNullException(nameof(bitBytes));
        }

        if (bitBytes.Length != count)
        {
            throw new ArgumentException($"비트 바이트 길이({bitBytes.Length})가 개수({count})와 일치하지 않습니다.", nameof(bitBytes));
        }

        for (int i = 0; i < bitBytes.Length; i++)
        {
            if (bitBytes[i] != BitOffValue && bitBytes[i] != BitOnValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(bitBytes),
                    bitBytes[i],
                    $"Yokogawa 비트 쓰기 값은 0x{BitOffValue:X2} 또는 0x{BitOnValue:X2}여야 합니다. (인덱스: {i})");
            }
        }

        return BuildWriteFrame(CommandWriteBits, cpuNumber, address, count, bitBytes);
    }

    private static void ValidateCpuNumber(byte cpuNumber)
    {
        if (cpuNumber < MinCpuNumber || cpuNumber > MaxCpuNumber)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cpuNumber),
                cpuNumber,
                $"CPU 번호는 {MinCpuNumber}에서 {MaxCpuNumber} 사이여야 합니다.");
        }
    }

    private static byte[] BuildWriteFrame(
        byte command,
        byte cpuNumber,
        YokogawaAddress address,
        ushort count,
        byte[] payload)
    {
        int parameterSizeValue = AddressParameterSize + payload.Length;
        if (parameterSizeValue > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(payload),
                payload.Length,
                $"파라미터 크기({parameterSizeValue})가 16비트 길이 상한({ushort.MaxValue})을 초과합니다.");
        }

        ushort parameterSize = (ushort)parameterSizeValue;
        byte[] frame = new byte[HeaderLength + parameterSize];
        WriteHeaderAndAddress(frame, command, cpuNumber, parameterSize, address, count);
        Buffer.BlockCopy(payload, 0, frame, WritePayloadOffset, payload.Length);
        return frame;
    }

    private static void WriteHeaderAndAddress(
        byte[] frame,
        byte command,
        byte cpuNumber,
        ushort parameterSize,
        YokogawaAddress address,
        ushort count)
    {
        frame[CommandOffset] = command;
        frame[CpuNumberOffset] = cpuNumber;
        BinaryBuffer.WriteUInt16BigEndian(frame, ParameterSizeOffset, parameterSize);
        BinaryBuffer.WriteUInt16BigEndian(frame, AttributeOffset, address.Attribute);
        BinaryBuffer.WriteUInt32BigEndian(frame, DeviceNumberOffset, (uint)address.Number);
        BinaryBuffer.WriteUInt16BigEndian(frame, CountOffset, count);
    }
}
