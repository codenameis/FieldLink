using System;
using FieldLink.PlcDrivers.Common;

namespace FieldLink.Robot.FANUC.Protocols
{
    /// <summary>연결·세션·G 할당 단계의 초기화 응답을 검사합니다.</summary>
    /// <remarks>참고 구현의 응답 형태와 기존 쓰기 상태 계약을 사용합니다. 기종별 전체 상태 코드 명세는 별도 확인이 필요합니다.</remarks>
    public static class FanucInitializationResponseParser
    {
        /// <summary>0은 연결, 1은 세션, 2 이상은 순서대로 실행한 G 할당 단계입니다.</summary>
        public static OperationResult Parse(int step, byte[] request, byte[] response)
        {
            if (step < 0 || step >= 62)
                throw new ArgumentOutOfRangeException(nameof(step));
            if (request == null || request.Length < 56)
                throw new ArgumentException("초기화 요청 헤더가 필요합니다.", nameof(request));
            if (response == null || response.Length < 56 || response.Length != 56 + BitConverter.ToUInt16(response, 4))
                return new OperationResult("FANUC 초기화 응답 길이가 올바르지 않습니다.");
            // R-016: 첫 연결 승인은 일반 쓰기 상태 바이트를 갖지 않는다.
            if (step == 0)
                return response[0] == 1 && response[8] == 1
                    ? OperationResult.CreateSuccessResult()
                    : new OperationResult(response[0], "FANUC 연결 승인이 올바르지 않습니다.");
            if (response[0] != 3 || response[2] != request[2])
                return new OperationResult("FANUC 초기화 응답이 요청 단계와 일치하지 않습니다.");
            return FanucProtocol.ParseWriteResponse(response);
        }
    }
}
