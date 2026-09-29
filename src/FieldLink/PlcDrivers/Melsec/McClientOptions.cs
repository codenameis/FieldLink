using System;
using System.Text;
using FieldLink.PlcDrivers.Common;

namespace FieldLink.PlcDrivers.Melsec
{
    /// <summary>MC 3E Binary 간편 클라이언트 설정입니다. 생성 시 복사하며 연결 후 원본 변경은 반영하지 않습니다.</summary>
    public sealed class McClientOptions
    {
        /// <summary>Open 또는 한 공개 요청의 대기·분할 교환 전체 제한 시간입니다.</summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(3);
        /// <summary>MC 접근 경로입니다.</summary>
        public McFrameOptions Route { get; set; } = new McFrameOptions();
        /// <summary>숫자 값의 메모리 바이트 순서입니다.</summary>
        public ByteOrder ByteOrder { get; set; } = ByteOrder.LittleEndian;
        /// <summary>기본 문자열 인코딩입니다. 표현 불가능한 문자는 실패시키도록 복사합니다.</summary>
        public Encoding StringEncoding { get; set; } = Encoding.ASCII;
        /// <summary>문자열의 워드 안 두 바이트를 교환할지 지정합니다.</summary>
        public bool SwapStringBytes { get; set; }
        /// <summary>한 요청의 워드 수입니다. 지원 정책은 1~950이며 실제 장치 한도에 맞게 낮출 수 있습니다.</summary>
        public int MaxWordsPerRequest { get; set; } = 950;
        /// <summary>한 요청의 비트 점수입니다. 보수적인 지원 정책으로 1~950을 사용합니다.</summary>
        public int MaxBitsPerRequest { get; set; } = 950;
    }
}
