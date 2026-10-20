using System;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Conversion;

namespace FieldLink.Plc;

public sealed partial class PlcClient
{
    /// <summary>불리언(비트) 한 개를 읽습니다.</summary>
    public Task<PlcResult<bool>> ReadBooleanAsync(string address, CancellationToken cancellationToken = default)
    {
        return ReadCoreAsync(address, PlcDataType.Boolean, 1, bytes => bytes[0] != 0, cancellationToken);
    }

    /// <summary>연속된 불리언(비트) 배열을 읽습니다.</summary>
    public Task<PlcResult<bool[]>> ReadBooleanAsync(string address, int count, CancellationToken cancellationToken = default)
    {
        return ReadCoreAsync(address, PlcDataType.Boolean, count, bytes => PlcValueConverter.ToBooleanArray(bytes, 0, count), cancellationToken);
    }

    /// <summary>불리언(비트) 한 개를 씁니다.</summary>
    public Task<PlcResult> WriteBooleanAsync(string address, bool value, CancellationToken cancellationToken = default)
    {
        return WriteCoreAsync(address, PlcDataType.Boolean, PlcValueConverter.GetBytes(value), cancellationToken);
    }

    /// <summary>연속된 불리언(비트) 배열을 씁니다.</summary>
    public Task<PlcResult> WriteBooleanAsync(string address, bool[] values, CancellationToken cancellationToken = default)
    {
        return WriteCoreAsync(address, PlcDataType.Boolean, PlcValueConverter.GetBytes(values), cancellationToken);
    }

    /// <summary>PLC 메모리에서 문자열을 읽습니다.</summary>
    /// <param name="address">시작 주소입니다.</param>
    /// <param name="characterCapacity">읽을 최대 문자(바이트) 수입니다.</param>
    /// <param name="cancellationToken">취소 토큰입니다.</param>
    public Task<PlcResult<string>> ReadStringAsync(string address, int characterCapacity, CancellationToken cancellationToken = default)
    {
        int storageSize = _stringFormat.GetStorageSize(characterCapacity);
        return ReadCoreAsync(
            address,
            PlcDataType.Byte,
            storageSize,
            bytes => _stringFormat.Decode(bytes, _options.StringEncoding),
            cancellationToken);
    }

    /// <summary>PLC 메모리에 문자열을 씁니다.</summary>
    /// <param name="address">시작 주소입니다.</param>
    /// <param name="value">기록할 문자열입니다.</param>
    /// <param name="characterCapacity">할당된 최대 문자 수입니다.</param>
    /// <param name="cancellationToken">취소 토큰입니다.</param>
    public Task<PlcResult> WriteStringAsync(string address, string value, int characterCapacity, CancellationToken cancellationToken = default)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        byte[] encoded = _stringFormat.Encode(value, _options.StringEncoding, characterCapacity);
        return WriteCoreAsync(address, PlcDataType.Byte, encoded, cancellationToken);
    }

    /// <summary>PLC 메모리에서 원시 바이트 배열을 읽습니다.</summary>
    public Task<PlcResult<byte[]>> ReadBytesAsync(string address, int length, CancellationToken cancellationToken = default)
    {
        return ReadCoreAsync(address, PlcDataType.Byte, length, bytes => (byte[])bytes.Clone(), cancellationToken);
    }

    /// <summary>PLC 메모리에 원시 바이트 배열을 씁니다.</summary>
    public Task<PlcResult> WriteBytesAsync(string address, byte[] bytes, CancellationToken cancellationToken = default)
    {
        if (bytes is null)
        {
            throw new ArgumentNullException(nameof(bytes));
        }

        return WriteCoreAsync(address, PlcDataType.Byte, bytes, cancellationToken);
    }
}
