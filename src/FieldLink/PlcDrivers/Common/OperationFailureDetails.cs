using System;

namespace FieldLink.PlcDrivers.Common
{
    /// <summary>실패 원인과 분할 교환의 확인 범위입니다. 부분 쓰기를 전체 실패 또는 전체 성공으로 숨기지 않습니다.</summary>
    public sealed class OperationFailureDetails
    {
        internal OperationFailureDetails(string address, string operation, string unit,
            int confirmedCount, int uncertainCount, Exception cause = null)
        {
            Address = address;
            Operation = operation;
            Unit = unit;
            ConfirmedCount = confirmedCount;
            UncertainCount = uncertainCount;
            Cause = cause;
        }

        /// <summary>사용자가 요청한 시작 주소입니다.</summary>
        public string Address { get; }
        /// <summary>실패한 작업 이름입니다.</summary>
        public string Operation { get; }
        /// <summary>개수 단위입니다. MC에서는 word 또는 bit이며 자료형의 원소 수와 다릅니다.</summary>
        public string Unit { get; }
        /// <summary>앞선 블록에서 정상 응답으로 확인한 개수입니다.</summary>
        public int ConfirmedCount { get; }
        /// <summary>쓰기 응답을 확인하지 못한 현재 블록의 최대 개수입니다. 실제 반영 개수를 뜻하지 않습니다.</summary>
        public int UncertainCount { get; }
        /// <summary>전송 실패의 원인 예외입니다. 통신 예외의 단계·소켓 코드·송신량을 보존합니다.</summary>
        public Exception Cause { get; }
    }
}
