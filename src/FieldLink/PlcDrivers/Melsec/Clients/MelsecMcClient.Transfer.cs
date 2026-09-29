using System;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Communication;
using FieldLink.Communication.Diagnostics;
using FieldLink.PlcDrivers.Common;

namespace FieldLink.PlcDrivers.Melsec.Clients
{
    public sealed partial class MelsecMcClient
    {
        private async Task<OperationResult<byte[]>> TransferAsync(string address, int points, bool bits,
            Func<byte[]> encodeWrite, CancellationToken token)
        {
            CheckRequest(token);
            if (string.IsNullOrWhiteSpace(address))
                throw new ArgumentException("주소가 필요합니다.", nameof(address));
            if (points < 1)
                throw new ArgumentOutOfRangeException(nameof(points));
            var parsed = parseAddress(address, 1, bits);
            if (!parsed.IsSuccess)
                return parsed.ConvertFailed<byte[]>();
            if (bits && parsed.Content.McDataType.DataType != 1)
                return Invalid("비트 단위 접근에는 M100 같은 비트 장치 주소가 필요합니다.");
            int addressStep = !bits && parsed.Content.McDataType.DataType == 1 ? 16 : 1;
            if (parsed.Content.AddressStart < 0 || (long)parsed.Content.AddressStart + (long)points * addressStep > 0x1000000L)
                return Invalid("요청 범위가 MC 3E의 24비트 주소 범위를 벗어납니다.");
            // 입력 전체 범위를 검증한 뒤, 첫 await 전에 호출자의 쓰기 데이터를 복사한다.
            byte[] writeData = encodeWrite?.Invoke();
            int unitBytes = bits ? 1 : 2;
            int limit = bits ? maxBits : maxWords;
            int confirmed = 0;
            int inFlight = 0;
            string operation = writeData == null ? "Read" : "Write";
            string unit = bits ? "bit" : "word";

            Func<Func<byte[], int, Task<byte[]>>, Task<OperationResult<byte[]>>> execute = async exchange =>
            {
                byte[] output = writeData == null ? new byte[checked(points * unitBytes)] : new byte[0];
                while (confirmed < points)
                {
                    int blockCount = Math.Min(points - confirmed, limit);
                    var block = new McDeviceAddress { McDataType = parsed.Content.McDataType,
                        AddressStart = parsed.Content.AddressStart + confirmed * addressStep, Length = (ushort)blockCount };
                    byte[] core;
                    if (writeData == null)
                        core = McBinaryCommandBuilder.BuildReadMcCoreCommand(block, bits);
                    else if (bits)
                    {
                        var values = new bool[blockCount];
                        for (int i = 0; i < blockCount; i++)
                            values[i] = writeData[confirmed + i] != 0;
                        core = McBinaryCommandBuilder.BuildWriteBitCoreCommand(block, values);
                    }
                    else
                    {
                        var bytes = new byte[blockCount * 2];
                        Array.Copy(writeData, confirmed * 2, bytes, 0, bytes.Length);
                        core = McBinaryCommandBuilder.BuildWriteWordCoreCommand(block, bytes);
                    }
                    byte[] request = McBinaryCommandBuilder.PackMcCommand(route, core);
                    inFlight = blockCount;
                    byte[] response = await exchange(request, blockCount).ConfigureAwait(false);
                    inFlight = 0;
                    var status = McBinaryResponseParser.CheckResponseContentHelper(response);
                    if (!status.IsSuccess)
                    {
                        var failed = status.ConvertFailed<byte[]>();
                        failed.FailureDetails = new OperationFailureDetails(address, operation, unit, confirmed, 0);
                        return failed;
                    }
                    if (writeData == null)
                    {
                        if (bits)
                        {
                            for (int i = 0; i < blockCount; i++)
                                output[confirmed + i] = (byte)((response[MelsecMc3EBinaryFrame.ResponseDataOffset + i / 2] >> (i % 2 == 0 ? 4 : 0)) & 15);
                        }
                        else
                            Array.Copy(response, MelsecMc3EBinaryFrame.ResponseDataOffset, output, confirmed * 2, blockCount * 2);
                    }
                    confirmed += blockCount;
                }
                return OperationResult.CreateSuccessResult(output);
            };

            try
            {
                if (points <= limit)
                    return await execute((request, count) => transport.ExchangeAsync(request, boundary, timeout,
                        response => Classify(response, request, count, bits, writeData != null), token)).ConfigureAwait(false);
                return await transport.ExecuteTransactionAsync(tx => execute((request, count) => tx.ExchangeAsync(request, boundary,
                    response => Classify(response, request, count, bits, writeData != null))), timeout, token).ConfigureAwait(false);
            }
            catch (CommunicationException error)
            {
                return Failure(error, new OperationFailureDetails(address, operation, unit, confirmed,
                    writeData == null || error.BytesSent == 0 ? 0 : inFlight, error), token);
            }
            catch (OperationCanceledException error)
            {
                throw new PlcOperationCanceledException(new OperationFailureDetails(address, operation, unit, confirmed,
                    writeData == null ? 0 : inFlight, error), token);
            }
        }

        private static ResponseDisposition Classify(byte[] response, byte[] request, int count, bool bits, bool writing)
        {
            const int dataOffset = MelsecMc3EBinaryFrame.ResponseDataOffset;
            if (response == null || response.Length < dataOffset || response[0] != 0xD0 || response[1] != 0 ||
                (response[7] | response[8] << 8) != response.Length - MelsecMc3EBinaryFrame.HeaderLength)
                return ResponseDisposition.Reject;
            for (int i = 2; i <= 6; i++)
                if (response[i] != request[i])
                    return ResponseDisposition.Reject;
            if (response[9] != 0 || response[10] != 0)
                return ResponseDisposition.Accept;
            int dataLength = writing ? 0 : bits ? (count + 1) / 2 : count * 2;
            if (response.Length != dataOffset + dataLength)
                return ResponseDisposition.Reject;
            if (bits && !writing)
                for (int i = 0; i < dataLength; i++)
                    if ((response[dataOffset + i] >> 4) > 1 || (response[dataOffset + i] & 15) > 1 ||
                        (i == dataLength - 1 && count % 2 != 0 && (response[dataOffset + i] & 15) != 0))
                        return ResponseDisposition.Reject;
            return ResponseDisposition.Accept;
        }

        /// <summary>워드 영역을 짝수 바이트 수로 읽습니다. 분할 실패 시 부분 데이터는 반환하지 않습니다.</summary>
        public Task<OperationResult<byte[]>> ReadBytesAsync(string address, int byteLength, CancellationToken cancellationToken = default(CancellationToken))
        {
            CheckRequest(cancellationToken);
            ValidateByteLength(byteLength);
            return TransferAsync(address, byteLength / 2, false, null, cancellationToken);
        }

        /// <summary>원시 워드 바이트를 기록합니다. 입력은 호출 시 복사하며 길이는 양의 짝수여야 합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, byte[] values, CancellationToken cancellationToken = default(CancellationToken))
        {
            CheckRequest(cancellationToken);
            if (values == null)
                throw new ArgumentNullException(nameof(values));
            ValidateByteLength(values.Length);
            return WriteBytesAsync(address, values.Length, () => (byte[])values.Clone(), cancellationToken);
        }

        private async Task<OperationResult> WriteBytesAsync(string address, int byteLength, Func<byte[]> encode, CancellationToken token) =>
            await TransferAsync(address, byteLength / 2, false, encode, token).ConfigureAwait(false);

        private Task<OperationResult> WriteValuesAsync<T>(string address, T[] values, int width,
            Func<T[], byte[]> encode, CancellationToken token)
        {
            CheckRequest(token);
            if (values == null)
                throw new ArgumentNullException(nameof(values));
            if (values.Length < 1 || values.Length > int.MaxValue / width)
                throw new ArgumentOutOfRangeException(nameof(values));
            return WriteBytesAsync(address, values.Length * width, () => encode(values), token);
        }

        private static void ValidateByteLength(int length)
        {
            if (length < 2 || length % 2 != 0)
                throw new ArgumentOutOfRangeException(nameof(length), "워드 데이터 길이는 양의 짝수 바이트여야 합니다.");
        }

        private async Task<OperationResult<T[]>> ReadValuesAsync<T>(string address, int count, int width,
            Func<byte[], int, int, T[]> decode, CancellationToken token)
        {
            CheckRequest(token);
            if (count < 1 || count > int.MaxValue / width)
                throw new ArgumentOutOfRangeException(nameof(count));
            var read = await ReadBytesAsync(address, count * width, token).ConfigureAwait(false);
            return read.IsSuccess ? OperationResult.CreateSuccessResult(decode(read.Content, 0, count)) : read.ConvertFailed<T[]>();
        }

        private static async Task<OperationResult<T>> FirstAsync<T>(Task<OperationResult<T[]>> pending)
        {
            var result = await pending.ConfigureAwait(false);
            return result.IsSuccess ? OperationResult.CreateSuccessResult(result.Content[0]) : result.ConvertFailed<T>();
        }

    }
}
