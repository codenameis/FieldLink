namespace FieldLink.PlcDrivers.Siemens
{
    /// <summary>S7 TCP의 PLC 기종과 COTP 접근 경로입니다.</summary>
    public sealed class SiemensS7ClientOptions : PlcClientOptions
    {
        /// <summary>연결 및 STRING 메모리 형식을 선택합니다.</summary>
        public SiemensPLCS Model { get; set; } = SiemensPLCS.S1200;
        /// <summary>선택적인 (rack &lt;&lt; 5) | slot 값입니다. 생략하면 기종 기본값을 사용합니다.</summary>
        public byte? RackSlot { get; set; }
        /// <summary>선택적인 로컬 TSAP입니다.</summary>
        public ushort? LocalTsap { get; set; }
        /// <summary>선택적인 원격 TSAP입니다.</summary>
        public ushort? RemoteTsap { get; set; }
    }
}
