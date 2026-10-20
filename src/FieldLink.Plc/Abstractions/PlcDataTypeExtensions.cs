using System;

namespace FieldLink.Plc.Abstractions;

/// <summary><see cref="PlcDataType"/>의 크기 정보입니다.</summary>
public static class PlcDataTypeExtensions
{
    /// <summary>원소 하나가 요청·응답 바이트에서 차지하는 바이트 수입니다.</summary>
    /// <param name="dataType">자료형입니다.</param>
    /// <returns>바이트 수입니다. Boolean과 String은 1입니다.</returns>
    public static int GetElementSize(this PlcDataType dataType) => dataType switch
    {
        PlcDataType.Boolean or PlcDataType.Byte or PlcDataType.String => 1,
        PlcDataType.Int16 or PlcDataType.UInt16 => 2,
        PlcDataType.Int32 or PlcDataType.UInt32 or PlcDataType.Single => 4,
        PlcDataType.Int64 or PlcDataType.UInt64 or PlcDataType.Double => 8,
        _ => throw new ArgumentOutOfRangeException(nameof(dataType), dataType, "정의되지 않은 자료형입니다."),
    };
}
