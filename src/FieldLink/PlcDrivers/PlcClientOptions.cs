using System;
using System.Text;
using FieldLink.PlcDrivers.Common;

namespace FieldLink.PlcDrivers
{
    /// <summary>간편 PLC 클라이언트의 공통 설정입니다. 생성 시 복사합니다.</summary>
    public class PlcClientOptions
    {
        /// <summary>연결/협상 또는 한 요청의 대기·모든 분할 교환에 적용하는 제한 시간입니다.</summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(3);
        /// <summary>숫자 바이트 순서입니다. null이면 선택한 프로토콜의 기본 순서를 사용합니다.</summary>
        public ByteOrder? ByteOrder { get; set; }
        /// <summary>문자열 인코딩입니다. 표현할 수 없는 문자는 실패시킵니다.</summary>
        public Encoding StringEncoding { get; set; } = Encoding.ASCII;
    }
}
