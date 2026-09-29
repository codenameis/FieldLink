using System;
using FieldLink.PlcDrivers.Common;

namespace FieldLink.Robot.ABB.Protocols
{
    /// <summary>ABB HTTP 경로와 응답 본문 변환입니다. 인증과 HTTP 요청 실행은 포함하지 않습니다.</summary>
    public sealed class AbbRequest<T>
    {
        private readonly Func<string, OperationResult<T>> parser;
        internal AbbRequest(string path, Func<string, OperationResult<T>> parser)
        {
            Path = path;
            this.parser = parser;
        }

        /// <summary>컨트롤러 기준 상대 요청 경로입니다.</summary>
        public string Path { get; }

        /// <summary>성공적으로 수신한 HTTP 본문을 해석합니다. HTTP 상태 검사는 호출자가 수행해야 합니다.</summary>
        public OperationResult<T> ParseResponse(string body) => parser(body);
        internal AbbRequest<TResult> Then<TResult>(Func<T, OperationResult<TResult>> convert) => new AbbRequest<TResult>(Path, body =>
        {
            var result = parser(body);
            return result.IsSuccess ? convert(result.Content) : OperationResult.CreateFailedResult<TResult>(result);
        });
    }
}
