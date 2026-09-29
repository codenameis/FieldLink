namespace FieldLink.PlcDrivers.Modbus
{
    /// <summary>Modbus TCP 간편 클라이언트 설정입니다.</summary>
    public sealed class ModbusTcpClientOptions : PlcClientOptions
    {
        /// <summary>기본 Unit ID입니다. 응답을 받는 1~255를 허용합니다.</summary>
        public byte UnitId { get; set; } = 1;
        /// <summary>주소 문자열이 0부터 시작하는지 지정합니다.</summary>
        public bool AddressStartWithZero { get; set; } = true;
        /// <summary>한 블록의 최대 레지스터 수입니다. 1~120을 허용합니다.</summary>
        public int MaxRegistersPerRequest { get; set; } = 120;
        /// <summary>한 블록의 최대 비트 수입니다. 1~1968을 허용합니다.</summary>
        public int MaxBitsPerRequest { get; set; } = 1968;
    }
}
