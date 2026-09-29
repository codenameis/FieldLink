using FieldLink.Communication;
using FieldLink.Communication.Framing;
using FieldLink.PlcDrivers.Common;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.PlcDrivers.Common.Clients
{
    // 제조사 빌더·파서는 호출자가 선택하며 이 타입은 순서대로 교환하고 첫 실패에서 중단하는 역할만 맡습니다.
    internal sealed class SerialProtocolExchange
    {
        private readonly ISerialClient transport;
        private readonly IFrameBoundary boundary;
        private readonly TimeSpan timeout;
        internal SerialProtocolExchange(ISerialClient transport, IFrameBoundary boundary, TimeSpan? timeout)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            this.boundary = boundary ?? throw new ArgumentNullException(nameof(boundary));
            this.timeout = timeout ?? TimeSpan.FromSeconds(3);
            if (this.timeout <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(timeout));
        }
        internal async Task<OperationResult<T>> ExecuteAsync<T>(OperationResult<byte[]> command,
            Func<byte[], OperationResult<T>> parse, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (!command.IsSuccess)
                return command.ConvertFailed<T>();
            byte[] response = await transport.ExchangeAsync(command.Content, boundary, timeout, cancellationToken: token).ConfigureAwait(false);
            return parse(response);
        }
        internal async Task<OperationResult> ExecuteAsync(OperationResult<byte[]> command,
            Func<byte[], OperationResult> parse, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (!command.IsSuccess)
                return command;
            return parse(await transport.ExchangeAsync(command.Content, boundary, timeout, cancellationToken: token).ConfigureAwait(false));
        }
        internal Task<OperationResult<T[]>> ExecuteSequenceAsync<T>(OperationResult<List<byte[]>> commands,
            Func<int, byte[], OperationResult<T[]>> parse, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (!commands.IsSuccess)
                return Task.FromResult(commands.ConvertFailed<T[]>());
            return transport.ExecuteTransactionAsync(async tx =>
            {
                var values = new List<T>();
                for (int i = 0; i < commands.Content.Count; i++)
                {
                    var response = parse(i, await tx.ExchangeAsync(commands.Content[i], boundary).ConfigureAwait(false));
                    if (!response.IsSuccess)
                        return response;
                    values.AddRange(response.Content);
                }
                return OperationResult.CreateSuccessResult(values.ToArray());
            }, timeout, token);
        }
    }
}
