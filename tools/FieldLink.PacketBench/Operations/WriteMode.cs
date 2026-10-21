namespace FieldLink.PacketBench.Operations;

/// <summary>연속 쓰기를 한 번에 보낼지, 주소를 늘려 가며 한 건씩 보낼지 정합니다.</summary>
internal enum WriteMode
{
    /// <summary>시작 주소부터 값을 한 번의 요청으로 연속 기록합니다.</summary>
    Block,

    /// <summary>주소를 하나씩 늘려 가며 값을 하나씩 기록합니다.</summary>
    PerElement,
}
