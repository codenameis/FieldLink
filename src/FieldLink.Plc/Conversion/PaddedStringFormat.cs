using System;
using System.Text;
using FieldLink.Plc.Abstractions;

namespace FieldLink.Plc.Conversion;

/// <summary>고정 크기 메모리 공간에 패딩 바이트를 채워 문자열을 저장하는 형식입니다.</summary>
/// <remarks>
/// Modbus 및 MC 프로토콜 등 워드(2바이트) 단위로 널(0x00) 바이트를 채우는 일반적인 PLC 문자열에 사용합니다.
/// </remarks>
public sealed class PaddedStringFormat : IPlcStringFormat
{
    private readonly byte _paddingByte;
    private readonly int _wordSize;

    /// <summary>패딩 바이트와 워드 정렬 단위로 인스턴스를 초기화합니다.</summary>
    /// <param name="paddingByte">빈 공간을 채울 바이트 값입니다. 기본값은 0x00(널)입니다.</param>
    /// <param name="wordSize">저장소 정렬 단위(바이트 수)입니다. 기본값은 2(워드 단위)입니다.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="wordSize"/>가 1 미만일 때.</exception>
    public PaddedStringFormat(byte paddingByte = 0x00, int wordSize = 2)
    {
        if (wordSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(wordSize), wordSize, "워드 정렬 크기는 1 이상이어야 합니다.");
        }

        _paddingByte = paddingByte;
        _wordSize = wordSize;
    }

    /// <inheritdoc />
    public int GetStorageSize(int characterCapacity)
    {
        if (characterCapacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(characterCapacity), characterCapacity, "문자 용량은 0 이상이어야 합니다.");
        }

        int remainder = characterCapacity % _wordSize;
        return remainder == 0 ? characterCapacity : characterCapacity + (_wordSize - remainder);
    }

    /// <inheritdoc />
    public byte[] Encode(string value, Encoding encoding, int characterCapacity)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (encoding is null)
        {
            throw new ArgumentNullException(nameof(encoding));
        }

        int storageSize = GetStorageSize(characterCapacity);
        byte[] rawBytes = encoding.GetBytes(value);

        if (rawBytes.Length > characterCapacity)
        {
            throw new ArgumentException(
                $"인코딩된 문자열 바이트 수({rawBytes.Length})가 허용 용량({characterCapacity})을 초과합니다.",
                nameof(value));
        }

        byte[] storage = new byte[storageSize];
        for (int i = 0; i < storageSize; i++)
        {
            storage[i] = _paddingByte;
        }

        Array.Copy(rawBytes, 0, storage, 0, rawBytes.Length);
        return storage;
    }

    /// <inheritdoc />
    public string Decode(byte[] storage, Encoding encoding)
    {
        if (storage is null)
        {
            throw new ArgumentNullException(nameof(storage));
        }

        if (encoding is null)
        {
            throw new ArgumentNullException(nameof(encoding));
        }

        int effectiveLength = 0;
        while (effectiveLength < storage.Length && storage[effectiveLength] != _paddingByte)
        {
            effectiveLength++;
        }

        return encoding.GetString(storage, 0, effectiveLength);
    }
}
