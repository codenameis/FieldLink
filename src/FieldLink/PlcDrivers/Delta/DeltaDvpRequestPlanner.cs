using System;
using FieldLink.PlcDrivers.Common;

namespace FieldLink.PlcDrivers.Delta
{
    /// <summary>DVP의 M1536·D4096 경계를 가로지르는 읽기·쓰기 요청을 나눕니다.</summary>
    /// <remarks>각 구간의 실행과 응답 연결은 호출자가 담당합니다. 분할 여부와 관계없이 지정 국번을 보존합니다.</remarks>
    public static class DeltaDvpRequestPlanner
    {
        /// <summary>M1536을 경계로 비트 읽기를 나눕니다.</summary>
        /// <param name = "address">원본 DVP 주소입니다.</param>
        /// <param name = "length">읽을 비트 수입니다.</param>
        /// <returns>실행 순서대로 나눈 구간입니다.</returns>
        public static DeltaRequestSegment[] SplitReadBits(string address, ushort length) => Split(address, length, "M", 1536, 1);
        /// <summary>D4096을 경계로 워드 읽기를 나눕니다.</summary>
        /// <param name = "address">원본 DVP 주소입니다.</param>
        /// <param name = "length">읽을 워드 수입니다.</param>
        /// <returns>실행 순서대로 나눈 구간입니다.</returns>
        public static DeltaRequestSegment[] SplitReadWords(string address, ushort length) => Split(address, length, "D", 4096, 1);
        /// <summary>M1536을 경계로 비트 쓰기의 배열 범위를 나눕니다.</summary>
        /// <param name = "address">원본 DVP 주소입니다.</param>
        /// <param name = "valueCount">입력 bool 배열의 원소 수입니다.</param>
        /// <returns>Offset·Length가 bool 배열 인덱스인 구간입니다.</returns>
        public static DeltaRequestSegment[] SplitWriteBits(string address, int valueCount) => Split(address, valueCount, "M", 1536, 1);
        /// <summary>D4096을 경계로 워드 쓰기의 바이트 범위를 나눕니다.</summary>
        /// <param name = "address">원본 DVP 주소입니다.</param>
        /// <param name = "byteCount">입력 byte 배열의 원소 수입니다.</param>
        /// <returns>Offset·Length가 바이트 인덱스인 구간입니다. 홀수 길이의 마지막 바이트도 유지합니다.</returns>
        public static DeltaRequestSegment[] SplitWriteWords(string address, int byteCount) => Split(address, byteCount, "D", 4096, 2);
        /// <summary>Split 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <param name = "area">area에 사용할 입력값입니다.</param>
        /// <param name = "boundary">boundary에 사용할 입력값입니다.</param>
        /// <param name = "elementSize">elementSize에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        private static DeltaRequestSegment[] Split(string address, int length, string area, int boundary, int elementSize)
        {
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));
            string station = string.Empty;
            OperationResult<int> parameter = AddressParameters.ExtractParameter(ref address, "s");
            if (parameter.IsSuccess)
                station = $"s={parameter.Content};";
            if (address.StartsWith(area) && int.TryParse(address.Substring(1), out int start) && start < boundary && start + length / elementSize > boundary)
            {
                // 원본은 첫 구간의 워드·비트 수를 ushort로 변환한다.
                int firstLength = (ushort)(boundary - start) * elementSize;
                return new[]
                {
                    new DeltaRequestSegment
                    {
                        Address = station + address,
                        Offset = 0,
                        Length = firstLength
                    },
                    new DeltaRequestSegment
                    {
                        Address = station + area + boundary,
                        Offset = firstLength,
                        Length = length - firstLength
                    }
                };
            }

            return new[]
            {
                new DeltaRequestSegment
                {
                    // 이전 구현: Address = address;
                    // R-014: 추출한 국번을 복원하지 않으면 기본 국번의 다른 장치에 쓰기가 전송된다.
                    Address = station + address,
                    Offset = 0,
                    Length = length
                }
            };
        }
    }
}
