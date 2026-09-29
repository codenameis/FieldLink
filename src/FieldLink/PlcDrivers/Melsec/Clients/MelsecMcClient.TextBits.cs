using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.PlcDrivers.Common;

namespace FieldLink.PlcDrivers.Melsec.Clients
{
    public sealed partial class MelsecMcClient
    {
        /// <summary>M100 등 비트 장치의 한 점을 읽습니다. 워드 내부 점 주소는 지원하지 않습니다.</summary>
        public Task<OperationResult<bool>> ReadBoolAsync(string address, CancellationToken cancellationToken = default(CancellationToken)) =>
            FirstAsync(ReadBoolAsync(address, 1, cancellationToken));

        /// <summary>비트 장치에서 count개를 읽습니다. 홀수 개수의 패딩 비트는 결과에 포함하지 않습니다.</summary>
        public async Task<OperationResult<bool[]>> ReadBoolAsync(string address, int count, CancellationToken cancellationToken = default(CancellationToken))
        {
            var read = await TransferAsync(address, count, true, null, cancellationToken).ConfigureAwait(false);
            if (!read.IsSuccess)
                return read.ConvertFailed<bool[]>();
            var values = new bool[count];
            for (int i = 0; i < count; i++)
                values[i] = read.Content[i] != 0;
            return OperationResult.CreateSuccessResult(values);
        }

        /// <summary>비트 장치의 한 점에 기록합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, bool value, CancellationToken cancellationToken = default(CancellationToken)) =>
            WriteAsync(address, new[] { value }, cancellationToken);

        /// <summary>비트 배열을 연속 기록합니다. 입력은 호출 시 복사합니다.</summary>
        public async Task<OperationResult> WriteAsync(string address, bool[] values, CancellationToken cancellationToken = default(CancellationToken))
        {
            CheckRequest(cancellationToken);
            if (values == null)
                throw new ArgumentNullException(nameof(values));
            return await TransferAsync(address, values.Length, true, () =>
            {
                var bytes = new byte[values.Length];
                for (int i = 0; i < values.Length; i++)
                    bytes[i] = values[i] ? (byte)1 : (byte)0;
                return bytes;
            }, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>짝수 byteLength의 고정 문자열 영역을 읽고 끝의 NUL 패딩만 제거합니다. 길이는 문자 수가 아닙니다.</summary>
        public async Task<OperationResult<string>> ReadStringAsync(string address, int byteLength, Encoding encoding = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            CheckRequest(cancellationToken);
            ValidateByteLength(byteLength);
            Encoding selected = encoding == null ? stringEncoding : StrictEncoding(encoding);
            var read = await ReadBytesAsync(address, byteLength, cancellationToken).ConfigureAwait(false);
            if (!read.IsSuccess)
                return read.ConvertFailed<string>();
            try
            {
                return OperationResult.CreateSuccessResult(converter.ReadString(read.Content, selected).TrimEnd('\0'));
            }
            catch (DecoderFallbackException error)
            {
                return Invalid(error.Message).ConvertFailed<string>();
            }
        }

        /// <summary>고정 문자열 영역 전체를 기록합니다. 남는 바이트는 0, 용량/인코딩 초과는 실패 결과이며 자동 절단하지 않습니다.</summary>
        public Task<OperationResult> WriteStringAsync(string address, string value, int byteLength, Encoding encoding = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            CheckRequest(cancellationToken);
            ValidateByteLength(byteLength);
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            Encoding selected = encoding == null ? stringEncoding : StrictEncoding(encoding);
            try
            {
                if (selected.GetByteCount(value) > byteLength)
                    return Task.FromResult<OperationResult>(Invalid("인코딩된 문자열이 예약 영역보다 큽니다."));
                return WriteBytesAsync(address, byteLength, () => converter.GetBytes(value, byteLength, selected), cancellationToken);
            }
            catch (EncoderFallbackException error)
            {
                return Task.FromResult<OperationResult>(Invalid(error.Message));
            }
        }

        /// <summary>문자열을 필요한 최소 워드 수에 기록합니다. 홀수 바이트는 0으로 채우며 뒤의 별도 영역은 지우지 않습니다.</summary>
        public Task<OperationResult> WriteAsync(string address, string value, CancellationToken cancellationToken = default(CancellationToken))
        {
            CheckRequest(cancellationToken);
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            try
            {
                int bytes = stringEncoding.GetByteCount(value);
                if (bytes == 0)
                    return Task.FromResult<OperationResult>(Invalid("빈 문자열을 기록하려면 WriteStringAsync로 지울 영역 크기를 지정하세요."));
                return WriteStringAsync(address, value, checked(bytes + bytes % 2), null, cancellationToken);
            }
            catch (EncoderFallbackException error)
            {
                return Task.FromResult<OperationResult>(Invalid(error.Message));
            }
        }
    }
}
