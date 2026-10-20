using System;

namespace FieldLink.Plc.Abstractions;

/// <summary>연속된 원소를 읽는 요청입니다. 생성 후 바뀌지 않습니다.</summary>
public sealed class PlcReadRequest
{
    /// <summary>읽기 요청을 만듭니다.</summary>
    /// <param name="address">제조사 표기 그대로의 시작 주소입니다. (예: "D100", "%MW100", "40001")</param>
    /// <param name="dataType">원소 자료형입니다.</param>
    /// <param name="count">읽을 원소 수입니다. 바이트 수가 아닙니다.</param>
    public PlcReadRequest(string address, PlcDataType dataType, int count)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("주소가 필요합니다.", nameof(address));
        }

        if (count < 1 || count > int.MaxValue / dataType.GetElementSize())
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "원소 수가 범위를 벗어났습니다.");
        }

        Address = address;
        DataType = dataType;
        Count = count;
    }

    /// <summary>제조사 표기 그대로의 시작 주소입니다.</summary>
    public string Address { get; }

    /// <summary>원소 자료형입니다.</summary>
    public PlcDataType DataType { get; }

    /// <summary>읽을 원소 수입니다.</summary>
    public int Count { get; }

    /// <summary>응답으로 돌려줘야 할 바이트 수입니다. (원소 수 × 원소 크기)</summary>
    public int ByteCount => Count * DataType.GetElementSize();
}
