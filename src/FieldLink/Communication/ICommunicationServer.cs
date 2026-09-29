using System;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Communication
{
    /// <summary>명시적으로 시작·종료하는 수신 서버의 수명 계약입니다.</summary>
    public interface ICommunicationServer : IDisposable
    {
        /// <summary>현재 수신 루프가 실행 중인지 나타냅니다.</summary>
        bool IsRunning { get; }
        /// <summary>수신 루프의 완료 또는 실패를 관찰합니다. 시작 전에는 완료된 작업입니다.</summary>
        Task Completion { get; }
        /// <summary>수신을 시작합니다. 중복 시작은 허용하지 않으며 종료 완료 후 다시 시작할 수 있습니다.</summary>
        Task StartAsync(CancellationToken cancellationToken = default(CancellationToken));
        /// <summary>수신을 중단하고 활성 연결과 작업의 종료를 기다립니다. 서버 콜백 내부에서 기다리지 마세요.</summary>
        Task StopAsync();
    }
}
