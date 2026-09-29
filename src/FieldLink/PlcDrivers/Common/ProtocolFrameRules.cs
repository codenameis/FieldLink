using System;
using System.IO;
using FieldLink.Communication;

namespace FieldLink.PlcDrivers.Common
{
    /// <summary>프레임 규칙의 공통 인수 검사입니다. 요청별 버퍼나 수신 상태를 저장하지 않습니다.</summary>
    public abstract class ProtocolFrameRules : IProtocolFrameRules
    {
        /// <inheritdoc />
        public abstract int HeaderLength { get; }

        /// <inheritdoc />
        public int GetBodyLength(byte[] header, byte[] request = null)
        {
            RequireLength(header, HeaderLength, nameof(header));
            int length = ReadBodyLength(header, request);
            if (length < 0 || length > int.MaxValue - HeaderLength)
                throw new InvalidDataException("The declared frame length is outside the supported range.");
            return length;
        }

        /// <inheritdoc />
        public bool IsHeaderValid(byte[] header, byte[] request = null) =>
            header != null && header.Length >= HeaderLength && ValidateHeader(header, request);

        /// <inheritdoc />
        public int? GetSequenceId(byte[] header)
        {
            RequireLength(header, HeaderLength, nameof(header));
            return ReadSequenceId(header);
        }

        /// <inheritdoc />
        public virtual int FindHeaderOffset(byte[] bufferedData)
        {
            if (bufferedData == null)
                throw new ArgumentNullException(nameof(bufferedData));
            return 0;
        }

        /// <inheritdoc />
        public bool IsComplete(byte[] request, byte[] received)
        {
            if (received == null || received.Length == 0)
                return false;
            return IsCompleteCore(request, received);
        }

        /// <inheritdoc />
        public ResponseDisposition ClassifyResponse(byte[] request, byte[] response)
        {
            if (request == null || response == null || response.Length == 0 || response.Length < HeaderLength)
                return ResponseDisposition.Reject;
            return ClassifyResponseCore(request, response);
        }

        /// <summary>검사된 길이의 헤더에서 본문 크기를 읽습니다.</summary>
        protected abstract int ReadBodyLength(byte[] header, byte[] request);
        /// <summary>프로토콜 시그니처를 검사합니다. 별도 시그니처가 없으면 true입니다.</summary>
        protected virtual bool ValidateHeader(byte[] header, byte[] request) => true;
        /// <summary>프로토콜에서 제공하는 순서 식별자를 읽습니다.</summary>
        protected virtual int? ReadSequenceId(byte[] header) => null;
        /// <summary>고정 헤더 방식의 완성 여부입니다. 종료 문자 방식은 재정의합니다.</summary>
        protected virtual bool IsCompleteCore(byte[] request, byte[] received) =>
            HeaderLength > 0 && IsHeaderValid(received, request) &&
            received.Length == HeaderLength + GetBodyLength(received, request);
        /// <summary>프로토콜의 요청 식별 규칙을 적용합니다.</summary>
        protected virtual ResponseDisposition ClassifyResponseCore(byte[] request, byte[] response) => ResponseDisposition.Accept;
        /// <summary>필수 요청의 최소 길이를 검사합니다.</summary>
        protected static void RequireRequest(byte[] request, int minimumLength) => RequireLength(request, minimumLength, nameof(request));

        private static void RequireLength(byte[] data, int minimumLength, string parameterName)
        {
            if (data == null)
                throw new ArgumentNullException(parameterName);
            if (data.Length < minimumLength)
                throw new ArgumentException("The buffer is shorter than the required header.", parameterName);
        }
    }
}
