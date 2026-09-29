namespace FieldLink.PlcDrivers.LSIS
{
    /// <summary>LS ELECTRIC XGT 전용 TCP 통신의 CPU·모듈 위치와 전송 한도입니다.</summary>
    public sealed class LsFastEnetClientOptions : PlcClientOptions
    {
        /// <summary>요청 헤더의 CPU 종류입니다.</summary>
        public LSCpuInfo Cpu { get; set; } = LSCpuInfo.XGK;
        /// <summary>베이스 번호입니다. 0~15입니다.</summary>
        public byte Base { get; set; }
        /// <summary>FEnet 모듈 슬롯입니다. 0~15이며 기본값은 3입니다.</summary>
        public byte Slot { get; set; } = 3;
        /// <summary>한 번에 전송할 바이트 수입니다. 1~1400이며 기본값은 512입니다.</summary>
        public int MaxBytesPerRequest { get; set; } = 512;
    }
}
