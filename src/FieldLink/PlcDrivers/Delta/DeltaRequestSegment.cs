namespace FieldLink.PlcDrivers.Delta
{
    /// <summary>Delta DVP 영역 경계에서 나눈 요청의 주소와 데이터 범위입니다.</summary>
    public sealed class DeltaRequestSegment
    {
        /// <summary>구간의 시작 주소입니다.</summary>
        public string Address { get; set; }
        /// <summary>원본 데이터 배열에서 이 구간이 시작하는 인덱스입니다.</summary>
        public int Offset { get; set; }
        /// <summary>읽기는 비트 또는 워드 수, 쓰기는 입력 배열의 원소 수입니다.</summary>
        public int Length { get; set; }
    }
}
