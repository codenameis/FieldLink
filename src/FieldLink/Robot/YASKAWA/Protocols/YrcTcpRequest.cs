using System;
using FieldLink.PlcDrivers.Common;

namespace FieldLink.Robot.YASKAWA.Protocols
{
    /// <summary>YRC TCP의 헤더와 본문, 최종 응답 변환입니다. 헤더 승인 전에는 본문을 전송하지 않아야 합니다.</summary>
    public sealed class YrcTcpRequest<T>
    {
        private readonly string command;
        private readonly string data;
        private readonly Func<string, OperationResult<T>> parser;
        internal YrcTcpRequest(string command, string data, Func<string, OperationResult<T>> parser)
        {
            this.command = command;
            this.data = data;
            this.parser = parser;
        }

        /// <summary>첫 교환에 사용할 명령 헤더를 구성합니다.</summary>
        public byte[] BuildHeader() => YrcTcpProtocol.BuildHeader(command, data);
        /// <summary>헤더 승인 후 전송할 본문을 구성합니다.</summary>
        public byte[] BuildBody() => YrcTcpProtocol.BuildBody(data);
        /// <summary>CR까지 수신한 최종 응답을 해석합니다.</summary>
        public OperationResult<T> ParseResponse(string response) => parser(response);
        internal YrcTcpRequest<TResult> Map<TResult>(Func<T, TResult> convert) => new YrcTcpRequest<TResult>(command, data, response =>
        {
            var result = parser(response);
            return result.IsSuccess ? OperationResult.CreateSuccessResult(convert(result.Content)) : OperationResult.CreateFailedResult<TResult>(result);
        });
    }
}
