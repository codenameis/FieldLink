namespace FieldLink.Plc.Protocols.Siemens.S7Comm;

/// <summary>S7 통신의 메모리 영역과 영역 코드입니다. (사양서 §2)</summary>
public enum S7CommArea : byte
{
    /// <summary>프로세스 입력 (I).</summary>
    ProcessInput = 0x81,

    /// <summary>프로세스 출력 (Q).</summary>
    ProcessOutput = 0x82,

    /// <summary>메모리, 플래그 (M).</summary>
    Memory = 0x83,

    /// <summary>데이터 블록 (DB).</summary>
    DataBlock = 0x84,
}
