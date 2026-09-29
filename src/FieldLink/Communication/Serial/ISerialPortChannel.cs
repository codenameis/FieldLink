using System;

namespace FieldLink.Communication.Serial
{
    /// <summary>한 번의 포트 열기에 사용하는 저수준 입출력입니다. SerialClient가 소유·해제합니다.</summary>
    /// <remarks>읽기·쓰기는 직렬화됩니다. Dispose는 다른 스레드에서 호출될 수 있으며 진행 중인 I/O를 중단해야 합니다.</remarks>
    public interface ISerialPortChannel : IDisposable
    {
        /// <summary>포트를 엽니다. 실패하면 원인 예외를 전달합니다.</summary>
        void Open();
        /// <summary>최대 지정한 시간만큼 읽습니다. 수신이 없으면 TimeoutException을 던집니다.</summary>
        int Read(byte[] buffer, int offset, int count, int timeoutMilliseconds);
        /// <summary>모든 바이트를 전송하거나 예외를 던집니다. 실패한 쓰기는 자동 재시도하지 않습니다.</summary>
        void Write(byte[] buffer, int offset, int count, int timeoutMilliseconds);
        /// <summary>운영체제 입력 버퍼를 비웁니다.</summary>
        void DiscardInput();
    }
}
