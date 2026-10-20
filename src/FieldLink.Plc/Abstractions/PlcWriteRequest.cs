using System;

namespace FieldLink.Plc.Abstractions;

/// <summary>연속된 원소를 쓰는 요청입니다. 생성 시 데이터를 복사하므로 이후 원본을 바꿔도 영향이 없습니다.</summary>
public sealed class PlcWriteRequest
{
    private readonly byte[] _data;

    /// <summary>쓰기 요청을 만듭니다.</summary>
    /// <param name="address">제조사 표기 그대로의 시작 주소입니다.</param>
    /// <param name="dataType">원소 자료형입니다.</param>
    /// <param name="data">
    /// 이미 PLC 바이트 순서로 변환된 데이터입니다. 길이는 원소 크기의 배수여야 합니다.
    /// Boolean은 원소 하나당 0 또는 1인 바이트 하나입니다.
    /// </param>
    public PlcWriteRequest(string address, PlcDataType dataType, byte[] data)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("주소가 필요합니다.", nameof(address));
        }

        if (data is null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        int elementSize = dataType.GetElementSize();
        if (data.Length == 0 || data.Length % elementSize != 0)
        {
            throw new ArgumentException("데이터 길이는 0보다 크고 원소 크기의 배수여야 합니다.", nameof(data));
        }

        Address = address;
        DataType = dataType;
        _data = (byte[])data.Clone();
        Count = data.Length / elementSize;
    }

    /// <summary>제조사 표기 그대로의 시작 주소입니다.</summary>
    public string Address { get; }

    /// <summary>원소 자료형입니다.</summary>
    public PlcDataType DataType { get; }

    /// <summary>쓸 원소 수입니다.</summary>
    public int Count { get; }

    /// <summary>쓸 바이트 수입니다.</summary>
    public int ByteCount => _data.Length;

    /// <summary>쓸 데이터 중 지정한 원소 구간의 복사본을 돌려줍니다. 분할 전송에서 사용합니다.</summary>
    /// <param name="startElement">시작 원소 위치입니다. 0부터 셉니다.</param>
    /// <param name="elementCount">원소 수입니다.</param>
    /// <returns>해당 구간의 바이트 복사본입니다.</returns>
    public byte[] GetData(int startElement, int elementCount)
    {
        if (startElement < 0 || elementCount < 1 || startElement > Count - elementCount)
        {
            throw new ArgumentOutOfRangeException(nameof(startElement));
        }

        int elementSize = DataType.GetElementSize();
        var slice = new byte[elementCount * elementSize];
        Buffer.BlockCopy(_data, startElement * elementSize, slice, 0, slice.Length);
        return slice;
    }

    /// <summary>쓸 데이터 전체의 복사본을 돌려줍니다.</summary>
    public byte[] GetData() => GetData(0, Count);
}
