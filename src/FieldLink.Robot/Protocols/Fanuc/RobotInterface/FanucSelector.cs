namespace FieldLink.Robot.Protocols.Fanuc.RobotInterface;

/// <summary>FANUC Robot Interface (SRTP/SNPX) 메모리 영역 셀렉터 코드입니다.</summary>
public enum FanucSelector : byte
{
    /// <summary>데이터 레지스터 및 SNPX 할당 영역 (<c>0x08</c>, Word 단위).</summary>
    Data = 8,

    /// <summary>아날로그 및 그룹 입력 (<c>0x0A</c>, Word 단위).</summary>
    AnalogInput = 10,

    /// <summary>아날로그 및 그룹 출력 (<c>0x0C</c>, Word 단위).</summary>
    AnalogOutput = 12,

    /// <summary>SNPX 할당 명령 전송 영역 (<c>0x38</c>, Byte 단위).</summary>
    SnpxCommand = 56,

    /// <summary>입력 릴레이 및 SDO/RDO/UO/SO (<c>0x46</c>, Bit 단위).</summary>
    InputRelay = 70,

    /// <summary>출력 릴레이 및 SDI/RDI/UI/SI (<c>0x48</c>, Bit 단위).</summary>
    OutputRelay = 72,

    /// <summary>내부 릴레이 및 확장 SDO (<c>0x4C</c>, Bit 단위).</summary>
    InternalRelay = 76,
}
