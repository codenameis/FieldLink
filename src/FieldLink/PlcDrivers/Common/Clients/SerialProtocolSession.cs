using FieldLink.Communication;
using FieldLink.Communication.Diagnostics;
using FieldLink.Communication.Serial;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.PlcDrivers.Common.Clients
{
    /// <summary>시리얼 포트 연결마다 프로토콜을 초기화하고, 해당 연결의 세션으로만 명령을 실행합니다.</summary>
    /// <typeparam name="TSession">연결에 종속되는 식별자·협상 값·캐시를 보관하는 객체입니다.</typeparam>
    /// <remarks>
    /// 하나의 전송에는 하나의 세션 소유자를 조립하세요. 전송의 OpenAsync·Close·Dispose는 호출자가 담당합니다.
    /// 초기화와 명령은 같은 독점 교환에 포함됩니다. 연결 이벤트의 전달 시점에 의존하지 않습니다.
    /// 세션 객체는 콜백 밖에 저장하거나 다른 연결에서 재사용하지 마세요. 새 연결에는 새 객체를 반환해야 합니다.
    /// 세션 객체의 자원 해제나 장치별 정상 종료 명령은 이 클래스가 수행하지 않습니다.
    /// </remarks>
    public sealed class SerialProtocolSession<TSession> where TSession : class
    {
        private readonly ISerialClient transport;
        private readonly Func<ISerialTransaction, Task<TSession>> initialize;
        private readonly object sync = new object();
        private SessionEntry current;

        /// <summary>전송과 장치별 초기화 함수를 지정합니다. 생성 시에는 통신하지 않습니다.</summary>
        /// <param name="transport">호출자가 명시적으로 열고 닫는 시리얼 포트 전송입니다.</param>
        /// <param name="initialize">새 세션 객체를 반환합니다. 실패하면 예외를 던지며 해당 연결도 폐기됩니다.</param>
        public SerialProtocolSession(ISerialClient transport, Func<ISerialTransaction, Task<TSession>> initialize)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            this.initialize = initialize ?? throw new ArgumentNullException(nameof(initialize));
        }

        /// <summary>현재 열린 연결에 유효한 초기화 결과가 있는지 조회합니다. 이후 통신 성공을 보장하지 않습니다.</summary>
        public bool IsInitialized
        {
            get
            {
                lock (sync)
                    return current != null && current.Value != null && transport.State == ClientState.Open &&
                        current.Generation == transport.ConnectionGeneration;
            }
        }

        /// <summary>필요한 세션 협상과 명령을 같은 연결의 독점 구간에서 순서대로 실행합니다.</summary>
        /// <typeparam name="TResult">명령 결과 형식입니다.</typeparam>
        /// <param name="command">세션을 사용하는 명령입니다. 각 교환을 await하고 트랜잭션 밖으로 작업을 넘기지 마세요.</param>
        /// <param name="timeout">대기·초기화·명령 전체의 제한 시간입니다. 1~Int32.MaxValue 밀리초입니다.</param>
        /// <param name="cancellationToken">호출자 취소입니다. 콜백의 대기는 트랜잭션의 취소 토큰을 사용하세요.</param>
        /// <returns>명령 결과입니다. 정상 장치 거절은 예외 대신 결과로 반환해야 연결과 세션을 유지합니다.</returns>
        /// <remarks>
        /// 자동 접속·재전송은 하지 않습니다. 초기화 또는 명령의 예외는 세션을 무효화합니다.
        /// 대기 중 취소된 요청은 이미 사용 중인 세션을 무효화하지 않습니다.
        /// 재접속 전 요청·늦게 완료된 초기화는 새 연결의 세션을 변경하지 않습니다.
        /// </remarks>
        public Task<TResult> ExecuteAsync<TResult>(Func<ISerialTransaction, TSession, Task<TResult>> command,
            TimeSpan timeout, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            if (timeout < TimeSpan.FromMilliseconds(1) || timeout.TotalMilliseconds > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(timeout));
            // Open 상태, 이미 취소된 요청, 대기 중 재접속 검증은 전송 계약에서 수행한다.
            return ExecuteCoreAsync(command, timeout, cancellationToken);
        }

        private async Task<TResult> ExecuteCoreAsync<TResult>(Func<ISerialTransaction, TSession, Task<TResult>> command,
            TimeSpan timeout, CancellationToken cancellationToken)
        {
            SessionEntry entry = null;
            try
            {
                return await transport.ExecuteTransactionAsync(async transaction =>
                {
                    lock (sync)
                    {
                        RequireCurrentConnection(transaction);
                        if (current == null || current.Generation != transaction.ConnectionGeneration)
                            current = new SessionEntry(transaction.ConnectionGeneration);
                        entry = current;
                    }

                    if (entry.Value == null)
                    {
                        TSession value = await initialize(transaction).ConfigureAwait(false);
                        if (value == null)
                            throw new InvalidOperationException("프로토콜 초기화가 세션 객체를 반환하지 않았습니다.");
                        lock (sync)
                        {
                            // 취소를 무시한 초기화가 늦게 끝나도 새 연결에 결과를 게시할 수 없다.
                            RequireCurrentConnection(transaction);
                            if (!ReferenceEquals(current, entry))
                                throw new InvalidOperationException("프로토콜 초기화 중 세션이 교체되었습니다.");
                            entry.Value = value;
                        }
                    }

                    RequireCurrentConnection(transaction);
                    return await command(transaction, entry.Value).ConfigureAwait(false);
                }, timeout, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // 대기 중 취소에는 entry가 없다. 이전 호출의 실패로 새 세션을 지우지 않는다.
                lock (sync)
                    if (entry != null && ReferenceEquals(current, entry))
                        current = null;
                throw;
            }
        }

        private void RequireCurrentConnection(ISerialTransaction transaction)
        {
            transaction.CancellationToken.ThrowIfCancellationRequested();
            if (transport.State != ClientState.Open || transport.ConnectionGeneration != transaction.ConnectionGeneration)
                throw new InvalidOperationException("프로토콜 작업이 시작된 시리얼 포트 연결이 종료되거나 교체되었습니다.");
        }

        private sealed class SessionEntry
        {
            internal SessionEntry(long generation) { Generation = generation; }
            internal long Generation { get; }
            internal TSession Value { get; set; }
        }
    }
}
