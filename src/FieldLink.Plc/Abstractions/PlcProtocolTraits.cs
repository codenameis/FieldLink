using System;

namespace FieldLink.Plc.Abstractions;

/// <summary>프로토콜마다 다른, 값 변환에 필요한 고정 특성입니다. 생성 후 바뀌지 않습니다.</summary>
public sealed class PlcProtocolTraits
{
    /// <summary>프로토콜 특성을 만듭니다.</summary>
    /// <param name="defaultByteOrder">사용자가 지정하지 않았을 때 쓰는 숫자 바이트 순서입니다.</param>
    /// <param name="stringFormat">문자열 저장 형식입니다.</param>
    public PlcProtocolTraits(ByteOrder defaultByteOrder, IPlcStringFormat stringFormat)
    {
        DefaultByteOrder = defaultByteOrder;
        StringFormat = stringFormat ?? throw new ArgumentNullException(nameof(stringFormat));
    }

    /// <summary>사용자가 지정하지 않았을 때 쓰는 숫자 바이트 순서입니다.</summary>
    public ByteOrder DefaultByteOrder { get; }

    /// <summary>문자열 저장 형식입니다.</summary>
    public IPlcStringFormat StringFormat { get; }
}
