namespace FieldLink.PlcDrivers.Omron
{
    /// <summary>FINS/TCP의 자동 노드 할당과 직접 연결 메모리 접근 설정입니다.</summary>
    public sealed class FinsTcpClientOptions : PlcClientOptions
    {
        /// <summary>주소 파서가 사용할 PLC 계열입니다.</summary>
        public OmronPlcType PlcType { get; set; } = OmronPlcType.CSCJ;
        /// <summary>한 메모리 요청의 최대 워드 수입니다. 1~500을 허용합니다.</summary>
        public int MaxWordsPerRequest { get; set; } = 500;
        /// <summary>한 메모리 요청의 최대 비트 수입니다. 1~1990을 허용합니다.</summary>
        public int MaxBitsPerRequest { get; set; } = 1990;
    }
}
