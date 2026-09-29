using System;
using System.Threading;

namespace FieldLink.PlcDrivers.Common
{
    /// <summary>PLC 작업 취소와 그 시점까지 확인된 분할 교환 범위입니다. OperationCanceledException으로 처리할 수 있습니다.</summary>
    public sealed class PlcOperationCanceledException : OperationCanceledException
    {
        internal PlcOperationCanceledException(OperationFailureDetails details, CancellationToken token)
            : base(details.Cause?.Message ?? "PLC 작업이 취소되었습니다.", details.Cause, token)
        {
            FailureDetails = details;
        }

        /// <summary>취소 원인과 확인 완료·미확인 쓰기 범위입니다.</summary>
        public OperationFailureDetails FailureDetails { get; }
    }
}
