using System;

namespace FieldLink.Communication.Diagnostics
{
    /// <summary>변경 시점의 상태와 원인을 보존하는 불변 알림입니다.</summary>
    public sealed class ClientStateChangedEventArgs : EventArgs
    {
        internal ClientStateChangedEventArgs(ClientState previousState, ClientState state,
            long sequence, Exception error)
        {
            PreviousState = previousState;
            State = state;
            Sequence = sequence;
            Error = error;
        }

        /// <summary>변경 전 상태입니다.</summary>
        public ClientState PreviousState { get; }
        /// <summary>변경 후 상태입니다. 알림 처리 시점의 현재 상태는 더 진행되어 있을 수 있습니다.</summary>
        public ClientState State { get; }
        /// <summary>상태 변경마다 증가하는 번호입니다. 초기 상태는 0입니다.</summary>
        public long Sequence { get; }
        /// <summary>변경을 일으킨 예외입니다. 정상 열기·로컬 닫기·Dispose는 null입니다.</summary>
        public Exception Error { get; }
    }
}
