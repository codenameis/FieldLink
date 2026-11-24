namespace FieldLink.Robot.Protocols.Yaskawa.HighSpeedEthernet;

/// <summary>YASKAWA High-Speed Ethernet (<c>YERC</c>) 서비스 코드입니다.</summary>
public enum YercService : byte
{
    /// <summary>전체 속성 읽기 (<c>0x01</c>).</summary>
    GetAll = 0x01,

    /// <summary>전체 속성 쓰기 (<c>0x02</c>).</summary>
    SetAll = 0x02,

    /// <summary>단일 속성 읽기 (<c>0x0E</c>).</summary>
    GetSingle = 0x0E,

    /// <summary>단일 속성 쓰기 (<c>0x10</c>).</summary>
    SetSingle = 0x10,

    /// <summary>다중 요소 배치 읽기 (<c>0x33</c>).</summary>
    ReadMulti = 0x33,

    /// <summary>다중 요소 배치 쓰기 (<c>0x34</c>).</summary>
    WriteMulti = 0x34,
}
