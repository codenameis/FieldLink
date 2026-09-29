# 통신 전송 계층 표준 대조 (2026-09-29)

## 범위와 판정

`src/FieldLink/Communication/`의 TCP·UDP·시리얼·WebSocket 클라이언트와 서버를 소스 수준에서 재검토했다. 제품 대상은 `netstandard2.0`, 콘솔 테스트 실행기는 `net48`이다. 아래 자료는 이날 공식 원문에서 해당 절을 열어 확인했다. 표준은 전송 동작의 근거이고, 연결 세대·응답 판정·요청 제한 시간 등은 FieldLink 계약에 대한 별도 설계 판단이다.

이번 정적 감사에서는 **재현·수정할 수 있는 확정 전송 계층 결함을 찾지 못했다**. 통신 기반 제품 코드나 공개 API는 변경하지 않았다. 기존 회귀 테스트의 단언을 확인했고 최종 통합 러너에서도 해당 테스트를 실행했다. 초기 기준은 기존 산출물의 483개 통과였으며, 누락된 테스트 입력 3개를 내용 변경 없이 `tests/FieldLink.Communication.Tests/Contracts/`로 옮긴 뒤 현재 소스를 정상 빌드했다. 최종 솔루션 빌드는 경고 0 / 오류 0, 전체 검사는 **513 passed, 0 failed (41.37초)**다. [통합 검증 기록](README.md)에 프로토콜 계층 수정과 신규 검사 결과를 함께 남겼다.

| 항목 | 공식 계약과 소스 대조 | 기존 검증 코드 및 한계 |
| --- | --- | --- |
| TCP 바이트 흐름·부분 송신 | TCP는 메시지 경계 없는 순서 있는 바이트 흐름이다 [1]. `TcpExchange.SendAsync`는 `Socket.SendAsync`의 반환 바이트 수 [2]만큼 오프셋을 전진시켜 전량 송신한다. `TcpFrameBuffer`는 프레임 완성 전까지 수신하고 첫 프레임 뒤 남은 바이트를 보존한다. 0바이트 수신은 EOF로 처리한다. | `TcpTests.LargeSendAsync`는 2 MiB 전량 도착을 확인하지만 실제 *부분 송신 반환*을 강제하지 않는다. `FrameTests.HeaderProtocolsAsync`는 바이트별 분할 수신, `CoalescedFramesAsync`와 `DelimiterAndTrailerAsync`는 잉여 바이트 보존, `TcpTests.PeerCloseDiagnosticsAsync`는 부분 프레임 뒤 FIN을 단언한다. |
| TCP 대기·종료·재접속 | `ClientOperation`이 요청 대기부터 하나의 제한 시간을 적용하고, `ClientSocket`은 Close 시 이전 취소 구간을 교체해 옛 작업이 새 소켓에 들어가지 못하게 한다. 소켓 실패 시 현재 소켓만 무효화하며 성공적인 Open마다 세대가 증가한다. 이는 프로젝트 계약이며 RFC의 재접속 명세가 아니다. | `TcpTests.QueuedCancellationAsync`, `QueuedTimeoutAsync`, `DisposeActiveAndQueuedAsync`, `ReconnectAsync`, `SingleDeadlineAsync`, `CompletedDeadlineDoesNotCloseConnectionAsync`가 핵심 결과를 단언한다. `TcpServerStopRegressionTests`는 서버 종료 취소 콜백과 재시작 경합을 다룬다. |
| UDP 데이터그램·상대·길이 | UDP 길이는 헤더를 포함하며 최솟값 8바이트이므로 빈 *payload*도 유효하다 [3]. `UdpClient`는 `ReceiveMessageFromAsync` 결과의 상대 주소·포트를 검사하고, 설정 길이 및 `SocketFlags.Truncated`를 거부한다. 반환 구조체가 송신자와 수신 플래그를 제공한다는 .NET 계약을 확인했다 [4]. | `UdpTests.ZeroLengthDatagramAsync`, `ForeignSenderAndOldSidAsync`, `DatagramBoundaryAsync`, `OversizedDatagramAsync`, `QueueCancellationAsync`, `IgnoredResponsesDeadlineAsync`가 있다. 루프백에서 운영체제의 실제 `Truncated` 플래그를 강제하는 독립 테스트는 없다. |
| 시리얼 유휴 경계·Modbus RTU | Modbus Serial Line V1.02 §2.5.1.1은 프레임 사이 최소 t3.5, 프레임 내부 t1.5 초과 시 폐기를 명시하고, 19200 bps 초과에서는 750µs/1.750ms 고정값을 권고한다 [5]. `SerialLineTiming`은 프레임이 요청한 무통신 간격을 송신 전에 적용한다. `SerialTransaction`은 `IdleGapFrame`의 실제 무수신 시간을 수신 경계로 쓰며 전체 요청 제한 시간을 유지한다. 물리 문자 간격 t1.5 검사는 `ITimedSerialPortChannel`이 시간 정보를 제공할 때만 가능하다. | `SerialTests.ReviewRtuGapHonorsDeadlineBeforeSendAndReopenAsync`, `ReviewRtuGapAcrossSegmentsAndClientsAsync`, `SerialIdleGapTenMillisecondsDoesNotMergeNextFrameAsync`, `ModbusRtuRejectsCrcValidResponseWithExcessiveCharacterGapAsync`, `SerialUntimedReadReturnGapIsNotPhysicalTimingAsync`가 해당 구분을 단언한다. 실 COM 포트·RS-485 신호의 시간 정확도는 검증하지 않았다. |
| WebSocket 메시지·종료 | RFC 6455 §5.4는 조각난 메시지를 합쳐 완성 시 전달하고 중간 제어 프레임을 처리하도록 요구한다 [6]. .NET의 `ReceiveAsync(ArraySegment<byte>, CancellationToken)`은 수신 결과를 반환하며 동시 수신은 직렬화해야 한다 [7]. `WebSocketConnection`은 `EndOfMessage`까지 누적하고 수신을 직렬화한다. `CloseOutputAsync`는 출력 종료를 마칠 뿐 상대 Close 수신 자체를 뜻하지 않는다 [8]. 구현은 Close 메시지를 별도 확인한다. | `WebSocketTransportTests.FragmentedMessagesAndSizeLimitAsync`, `CloseWaitsForActualCloseAfterDataAsync`, `CloseWithPendingReceiveDoesNotStartAnotherReceiveAsync`, `CloseTimeoutAbortsUnresponsivePeerAsync`, `QueuedReceiveCannotAbortCloseAfterLateDataAsync`가 있다. 불응답 상대의 2초 종료 제한은 RFC 수치가 아닌 FieldLink 정책이다. |

## 확인된 한계와 후속 검증

- TCP의 실제 부분 송신 반환은 루프백 대용량 송신만으로 결정적으로 만들 수 없다. 현재 반복 송신 코드는 .NET의 반환값 계약과 맞지만, 그 분기 자체를 실행했다고 단정할 수 없다.
- UDP `SocketFlags.Truncated` 분기는 65,535바이트 수신 버퍼 때문에 일반 IPv4 루프백에서는 독립적으로 도달시키기 어렵다. 설정한 최대 길이 초과의 거부는 기존 테스트가 별도로 검증한다.
- 시리얼의 물리 t1.5, 송신 완료 시점, 실제 장비 통신은 가상 포트 테스트로 증명할 수 없다. 운영체제 `SerialPort.ReadTimeout`은 밀리초 단위 API이며 [9], 수신 호출 반환 간격을 물리 문자 간격으로 간주하지 않는 현재 구현이 맞다.
- 통신 기반 자체의 신규 테스트나 실장비 연결은 추가하지 않았다. 위 기존 테스트는 최종 솔루션 빌드 후 전체 러너에서 실행했다. 새 프로토콜 검사는 별도 감사 문서에 기록했으며 실제 장비·운영체제의 모든 분기 검증을 뜻하지 않는다.

## 확인한 공식 자료

1. IETF [RFC 9293, §2.2 Key TCP Concepts](https://www.rfc-editor.org/rfc/rfc9293.html#section-2.2), August 2022 — 순서 있는 바이트 흐름과 TCP 자체의 생존 확인 부재.
2. Microsoft Learn [Socket.SendAsync(ArraySegment&lt;Byte&gt;, SocketFlags)](https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.socket.sendasync?view=netframework-4.8), .NET Framework 4.8 API 보기, 2026-09-29 열람 — `Task<int>`은 송신한 바이트 수를 반환한다. 페이지의 다른 최신 오버로드는 이 프로젝트의 사용 근거로 삼지 않았다.
3. IETF [RFC 768, Fields: Length](https://www.rfc-editor.org/rfc/rfc768.html), August 1980 — UDP 길이는 8바이트 헤더를 포함한다.
4. Microsoft Learn [SocketReceiveMessageFromResult](https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.socketreceivemessagefromresult?view=netframework-4.8.1), .NET Framework 4.8.1 API 보기, 2026-09-29 열람 — `ReceivedBytes`, `RemoteEndPoint`, `SocketFlags`; [SocketFlags.Truncated](https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.socketflags?view=netframework-4.8.1), .NET Framework 4.8.1 API 보기 — 수신 버퍼에 들어가지 못해 잘린 메시지 표시.
5. Modbus Organization [MODBUS over Serial Line Specification and Implementation Guide V1.02, §2.5.1.1, pp. 13–14](https://www.modbus.org/file/secure/modbusoverserial.pdf), 2006-12-20 — t1.5·t3.5·고속 고정값.
6. IETF [RFC 6455, §5.4 Fragmentation, §5.5.1 Close](https://www.rfc-editor.org/rfc/rfc6455.html#section-5.4), December 2011 — 조각 메시지와 제어·종료 프레임.
7. Microsoft Learn [WebSocket.ReceiveAsync(ArraySegment&lt;Byte&gt;, CancellationToken)](https://learn.microsoft.com/en-us/dotnet/api/system.net.websockets.websocket.receiveasync?view=netframework-4.8.1), .NET Framework 4.8.1 API 보기, 2026-09-29 열람 — 수신 직렬화 설명과 반환 결과.
8. Microsoft Learn [WebSocket.CloseOutputAsync](https://learn.microsoft.com/en-us/dotnet/api/system.net.websockets.websocket.closeoutputasync?view=netframework-4.8.1), .NET Framework 4.8.1 API 보기, 2026-09-29 열람 — 출력 종료 Task의 완료 의미.
9. Microsoft Learn [SerialPort.ReadTimeout](https://learn.microsoft.com/en-us/dotnet/api/system.io.ports.serialport.readtimeout?view=netframework-4.8.1), .NET Framework 4.8.1 API 보기, 2026-09-29 열람 — 밀리초 속성과 양의 제한 시간. [SerialPort.Read(byte[], int, int)](https://learn.microsoft.com/en-us/dotnet/api/system.io.ports.serialport.read?view=netframework-4.8), .NET Framework 4.8 API 보기 — 포트의 버퍼에서 읽으며 BaseStream 버퍼와 섞으면 충돌할 수 있다. 제품의 `System.IO.Ports` 패키지 10.0.12 및 `netstandard2.0` 빌드 적합성은 이 문서 열람만으로 확정하지 않고 빌드 결과로 판단한다.
