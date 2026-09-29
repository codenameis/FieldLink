using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.PlcDrivers.Common;
using FieldLink.PlcDrivers.Common.Clients;

namespace FieldLink.PlcDrivers
{
    public sealed partial class PlcClient
    {
        /// <summary>원시 데이터를 byteLength 바이트 읽습니다. 워드 프로토콜은 짝수 길이가 필요합니다.</summary>
        public Task<OperationResult<byte[]>> ReadBytesAsync(string address, int byteLength, CancellationToken cancellationToken = default(CancellationToken)) =>
            ExecuteAsync(new PlcTransferRequest(address, PlcValueType.Bytes, byteLength), cancellationToken);
        /// <summary>원시 바이트를 기록합니다. 태그 프로토콜에서는 SINT 배열에 대응합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, byte[] values, CancellationToken cancellationToken = default(CancellationToken)) =>
            WriteValuesAsync(address, values, PlcValueType.Bytes, bytes => (byte[])bytes.Clone(), cancellationToken);
        /// <summary>비트 또는 BOOL 태그 한 개를 읽습니다.</summary>
        public Task<OperationResult<bool>> ReadBoolAsync(string address, CancellationToken cancellationToken = default(CancellationToken)) =>
            FirstAsync(ReadBoolAsync(address, 1, cancellationToken));
        /// <summary>비트 count개를 연속 읽습니다. 제조사별 주소 형식을 사용합니다.</summary>
        public async Task<OperationResult<bool[]>> ReadBoolAsync(string address, int count, CancellationToken cancellationToken = default(CancellationToken))
        {
            var read = await ExecuteAsync(new PlcTransferRequest(address, PlcValueType.Boolean, count), cancellationToken).ConfigureAwait(false);
            if (!read.IsSuccess)
                return read.ConvertFailed<bool[]>();
            var values = new bool[count];
            for (int i = 0; i < count; i++)
                values[i] = read.Content[i] != 0;
            return WithContent(read, values);
        }
        /// <summary>비트 또는 BOOL 태그 한 개를 기록합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, bool value, CancellationToken cancellationToken = default(CancellationToken)) =>
            WriteValueAsync(address, 1, PlcValueType.Boolean, () => new[] { value ? (byte)1 : (byte)0 }, cancellationToken);
        /// <summary>비트를 연속 기록합니다. 입력 배열은 호출 시 복사합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, bool[] values, CancellationToken cancellationToken = default(CancellationToken)) =>
            WriteValuesAsync(address, values, PlcValueType.Boolean, EncodeBits, cancellationToken);
        private static byte[] EncodeBits(bool[] values)
        {
            var bytes = new byte[values.Length];
            for (int i = 0; i < values.Length; i++)
                bytes[i] = values[i] ? (byte)1 : (byte)0;
            return bytes;
        }

        /// <summary>문자열을 읽습니다. byteLength는 헤더를 제외한 용량이며 고정 영역에서는 끝의 NUL만 제거합니다.</summary>
        public async Task<OperationResult<string>> ReadStringAsync(string address, int byteLength, Encoding encoding = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            CheckRequest(cancellationToken);
            Encoding selected = encoding == null ? stringEncoding : StrictEncoding(encoding);
            var read = await ExecuteAsync(new PlcTransferRequest(address, PlcValueType.String, byteLength), cancellationToken).ConfigureAwait(false);
            if (!read.IsSuccess)
                return read.ConvertFailed<string>();
            try
            {
                string value = selected.GetString(read.Content);
                return WithContent(read, driver.HasStringLength ? value : value.TrimEnd('\0'));
            }
            catch (DecoderFallbackException error) { return new OperationResult<string>(-1, error.Message); }
        }

        /// <summary>문자열을 지정한 바이트 용량에 기록합니다. 초과/인코딩 실패 시 전송하지 않으며 잘라 쓰지 않습니다.</summary>
        public Task<OperationResult> WriteStringAsync(string address, string value, int byteLength, Encoding encoding = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            CheckRequest(cancellationToken);
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            if (byteLength < 1)
                throw new ArgumentOutOfRangeException(nameof(byteLength));
            Encoding selected = encoding == null ? stringEncoding : StrictEncoding(encoding);
            try
            {
                int actual = selected.GetByteCount(value);
                if (actual > byteLength)
                    return Task.FromResult<OperationResult>(new OperationResult(-1, "문자열이 지정한 용량을 초과합니다."));
                return WriteTextAsync(new PlcTransferRequest(address, PlcValueType.String, byteLength, () =>
                {
                    var bytes = new byte[byteLength];
                    selected.GetBytes(value, 0, value.Length, bytes, 0);
                    return bytes;
                }, actual), cancellationToken);
            }
            catch (EncoderFallbackException error) { return Task.FromResult<OperationResult>(new OperationResult(-1, error.Message)); }
        }
        private async Task<OperationResult> WriteTextAsync(PlcTransferRequest request, CancellationToken token) =>
            await ExecuteAsync(request, token).ConfigureAwait(false);

        /// <summary>문자열을 필요한 최소 크기로 기록합니다. 고정 영역 전체를 지우려면 WriteStringAsync로 용량을 지정하세요.</summary>
        public Task<OperationResult> WriteAsync(string address, string value, CancellationToken cancellationToken = default(CancellationToken))
        {
            CheckRequest(cancellationToken);
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            try
            {
                int bytes = Math.Max(1, stringEncoding.GetByteCount(value));
                int aligned = checked(bytes + (driver.StringAlignment - bytes % driver.StringAlignment) % driver.StringAlignment);
                return WriteStringAsync(address, value, aligned, null, cancellationToken);
            }
            catch (EncoderFallbackException error) { return Task.FromResult<OperationResult>(new OperationResult(-1, error.Message)); }
        }
    }
}
