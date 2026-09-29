# Communication

지원 기능은 **TCP·UDP·Serial 클라이언트, TCP·UDP 서버, 표준 WebSocket 클라이언트·서버, 공정 Push·파일 전송**입니다. 일반 전송 계약은 이 폴더에서, 구현은 `Tcp`·`Udp`·`Serial`에서 찾습니다. 비요청 메시지를 주고받는 WebSocket 계약은 [WebSocket](WebSockets/WebSocketConnection.cs)에 있으며 [사용법](../../../docs/websocket.md)을 참고하세요. [파일·Push 사용법](../../../docs/file-push.md)도 제공합니다.

| 목적 | 계약 | 구현 |
|---|---|---|
| TCP 연결·송수신 | [ITcpClient](ITcpClient.cs) | [TcpClient](Tcp/TcpClient.cs) |
| UDP 데이터그램 송수신 | [IUdpClient](IUdpClient.cs) | [UdpClient](Udp/UdpClient.cs) |
| Serial 포트 설정·송수신 | [ISerialClient](ISerialClient.cs) | [SerialClient](Serial/SerialClient.cs) |
| 전송 공통 장치 코드 | [ICommunicationClient](ICommunicationClient.cs) | 세 클라이언트 모두 지원 |
| TCP 다단계 독점 교환 | [ITcpTransaction](Tcp/ITcpTransaction.cs) | ITcpClient.ExecuteTransactionAsync에서 제공 |
| Serial 다단계 독점 교환 | [ISerialTransaction](Serial/ISerialTransaction.cs) | ISerialClient.ExecuteTransactionAsync에서 제공 |
| 서버 시작·종료 | [ICommunicationServer](ICommunicationServer.cs) | [TcpServer](Tcp/TcpServer.cs), [UdpServer](Udp/UdpServer.cs) |
| TCP 서버 연결 | [ITcpServerConnection](ITcpServerConnection.cs) | TcpServer의 연결별 콜백에서 제공 |
| 메시지 경계 규칙 | [IFrameBoundary](Framing/IFrameBoundary.cs) | 고정 길이·헤더 길이·종료 문자·이스케이프·무수신 간격 |
| WebSocket 텍스트·바이너리 | [WebSocketConnection](WebSockets/WebSocketConnection.cs) | [WebSocketClient](WebSockets/WebSocketClient.cs), [WebSocketServer](WebSockets/WebSocketServer.cs) |
| 토픽별 공정 Push | [PushServer](Push/PushServer.cs) | [PushClient](Push/PushClient.cs) |
| HTTP(S) 파일 전송 | [FileTransferServer](FileTransfer/FileTransferServer.cs) | [FileTransferClient](FileTransfer/FileTransferClient.cs) |

장치 코드는 필요한 인터페이스를 생성자로 받고, 구체적인 클라이언트 선택은 프로그램을 조립하는 곳에서 합니다.

```csharp
using FieldLink.Communication;
using FieldLink.Communication.Framing;
using FieldLink.Communication.Tcp;

using (ITcpClient client = new TcpClient(endpoint, new DelimitedFrame(new byte[] { 13, 10 })))
{
    await client.OpenAsync();
    byte[] response = await client.ExchangeAsync(request);
}
```

UDP는 `FieldLink.Communication.Udp`의 `UdpClient`를 생성해 `IUdpClient`로 사용합니다. 호출별 제한 시간도 인터페이스에 있습니다.

```csharp
using FieldLink.Communication;
using FieldLink.Communication.Udp;

using (IUdpClient client = new UdpClient(endpoint))
{
    await client.OpenAsync();
    byte[] response = await client.ExchangeAsync(request, System.TimeSpan.FromSeconds(2));
}
```

`endpoint`와 `request`는 장치 규격에 맞춰 호출자가 제공합니다. 두 예제 모두 기본 제한 시간과 취소 토큰을 사용할 수 있습니다.

Open 완료 전에 요청하면 대기 없이 실패합니다. 송신 배열은 복사하지 않으므로 각 호출의 Task가 끝날 때까지 수정·재사용·풀 반환하지 마세요. 자세한 오류 의미와 이전 스냅샷 계약에서의 변경은 [호출 조건과 버퍼 소유권](../../../docs/networking.md#호출-조건과-버퍼-소유권)을 참고하세요.

클라이언트는 한 번 열고 여러 요청에 계속 사용합니다. TCP·UDP 모두 끊김·활성 작업 실패를 오류로 전달하며 자동 재접속·재전송하지 않습니다. 재개할 때는 호출자가 `OpenAsync()`를 다시 호출합니다. UDP는 원격 연결 상태가 없으므로 응답 시간 초과나 소켓 오류로 실패를 확인합니다.

장치의 `ReadAsync`·`WriteAsync` 내부에서 명령 생성·교환·응답 해석을 처리하세요. TCP의 연결별 초기화도 함께 묶으려면 `ExecuteTransactionAsync` 안에서 `transaction.ConnectionGeneration`을 확인합니다. 콜백의 별도 비동기 대기에는 `transaction.CancellationToken`을 전달합니다. [장치 읽기 예제](../../../docs/networking.md#여러-tcp-교환을-묶는-경우)는 이 흐름을 보여줍니다.

정상 장치 거절은 트랜잭션 결과로 받은 뒤 해석하면 연결을 유지할 수 있습니다. 초기화 실패와 콜백 예외는 연결을 폐기합니다. 정상 종료와 애플리케이션 재접속 정책은 [수명 계약과 실행 가능한 예제](../../../docs/networking-lifecycle.md)를 참고하세요.

## 문제가 생겼을 때

| 확인할 문제 | 추적 경로 |
|---|---|
| TCP 송수신 | Tcp/TcpClient → Tcp/TcpExchange → Tcp/TcpFrameBuffer |
| UDP 송수신·상대 주소 검사 | Udp/UdpClient |
| Serial 포트·송수신 | Serial/SerialClient → Serial/ISerialPortChannel |
| 서버 수신·종료 | Tcp/TcpServer·Tcp/TcpServerConnection, Udp/UdpServer |
| 메시지 길이·경계 | Framing/IFrameBoundary와 선택한 규칙 |
| 연결·종료·동시 요청 | Internal/ClientSocket |
| 시간 초과·취소·진단 예외 | Internal/ClientOperation → Diagnostics/CommunicationException |
| 상태 변경 알림 | Diagnostics/ClientStateChangedEventArgs → Internal/ClientStateNotifier |

`ConnectionGeneration`은 연결 성공마다 증가하는 번호입니다. 재연결 전후를 구분해 이전 연결에서 만든 장치 세션의 재사용 여부를 판단할 때 사용합니다. 연결 종료 감지는 `State`·`StateChanged`·작업 예외로 확인합니다.

루트에는 사용 계약을 두며, 작은 응답 판정 열거형 `ResponseDisposition`은 이를 사용하는 `ICommunicationClient.cs`에 함께 정의합니다. 상태·오류 타입은 `Diagnostics`, 실제 공유되는 내부 구현만 `Internal`에 둡니다. 전송 폴더 안에 다시 `Client`·`Internal` 폴더를 만들지 않습니다.

Serial 설정과 제조사별 클라이언트, 서버의 시작·종료 계약은 [Robot·Serial 사용 문서](../../../docs/robot-serial-migration.md)에 있습니다. 기존 TCP·UDP 계약과 API 변경 대응은 [네트워크 사용 문서](../../../docs/networking.md)에 있습니다.
