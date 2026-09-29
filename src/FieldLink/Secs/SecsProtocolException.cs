using System.IO;

namespace FieldLink.Secs
{
    /// <summary>장비의 명시적인 거절, Abort 또는 프로토콜 오류입니다.</summary>
    public sealed class SecsProtocolException : IOException
    {
        /// <summary>프로토콜 오류와 장비 응답 코드를 보존합니다.</summary>
        public SecsProtocolException(string message, int code) : base(message) { Code = code; }
        /// <summary>장비가 반환한 상태 또는 함수 번호입니다.</summary>
        public int Code { get; }
    }
}
