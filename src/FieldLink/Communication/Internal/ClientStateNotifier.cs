using FieldLink.Communication.Diagnostics;
using System;
using System.Collections.Generic;
using System.Threading;

namespace FieldLink.Communication.Internal
{
    // 전송 잠금 안에서는 상태와 알림만 기록한다. 사용자 코드는 별도 작업에서 순서대로 실행한다.
    internal sealed class ClientStateNotifier
    {
        private readonly object sync = new object();
        private readonly object sender;
        private readonly Queue<Action> notifications = new Queue<Action>();
        private ClientStateChangedEventArgs lastChange =
            new ClientStateChangedEventArgs(ClientState.Closed, ClientState.Closed, 0, null);
        private Exception lastNotificationError;
        private bool dispatching;

        internal ClientStateNotifier(object sender) { this.sender = sender; }

        internal event EventHandler<ClientStateChangedEventArgs> Changed;
        internal ClientStateChangedEventArgs LastChange { get { lock (sync) return lastChange; } }
        internal Exception LastNotificationError { get { lock (sync) return lastNotificationError; } }

        internal void Change(ClientState state, Exception error = null)
        {
            lock (sync)
            {
                if (lastChange.State == state || lastChange.State == ClientState.Disposed)
                    return;
                var change = new ClientStateChangedEventArgs(lastChange.State, state, lastChange.Sequence + 1, error);
                lastChange = change;
                EventHandler<ClientStateChangedEventArgs> handlers = Changed;
                if (handlers == null)
                    return;

                notifications.Enqueue(() => Notify(handlers, change));
                if (dispatching)
                    return;
                dispatching = true;
                ThreadPool.QueueUserWorkItem(_ => Dispatch());
            }
        }

        private void Dispatch()
        {
            while (true)
            {
                Action notification;
                lock (sync)
                {
                    if (notifications.Count == 0)
                    {
                        dispatching = false;
                        return;
                    }
                    notification = notifications.Dequeue();
                }
                notification();
            }
        }

        private void Notify(EventHandler<ClientStateChangedEventArgs> handlers, ClientStateChangedEventArgs change)
        {
            foreach (EventHandler<ClientStateChangedEventArgs> handler in handlers.GetInvocationList())
            {
                try { handler(sender, change); }
                catch (Exception error)
                {
                    // 구독자의 버그가 통신을 끊거나 다음 구독자의 알림을 막지 않도록 별도 진단 정보로 보존한다.
                    lock (sync) lastNotificationError = error;
                }
            }
        }
    }
}
