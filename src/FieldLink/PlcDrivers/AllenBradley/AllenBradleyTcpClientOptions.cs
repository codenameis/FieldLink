namespace FieldLink.PlcDrivers.AllenBradley
{
    /// <summary>Logix 비연결 CIP 태그 통신의 경로와 분할 전송 설정입니다.</summary>
    public sealed class AllenBradleyTcpClientOptions : PlcClientOptions
    {
        /// <summary>기본 백플레인 경로의 CPU 슬롯입니다.</summary>
        public byte Slot { get; set; }
        /// <summary>라우팅 경로입니다. null이면 [1, Slot], 빈 배열이면 직접 Message Router로 요청합니다. 생성 시 복사합니다.</summary>
        public byte[] RoutePath { get; set; }
        /// <summary>한 쓰기 조각의 최대 데이터 바이트입니다. 8~400이며 실제 경로 길이에 따라 더 작아집니다.</summary>
        public int MaxWriteBytesPerRequest { get; set; } = 400;
    }
}
