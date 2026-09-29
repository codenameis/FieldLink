# SECS/GEM 이식과 사용법

2026-09-29 기준으로 SECS-II 값·XML, HSMS TCP 클라이언트·서버, 기본 GEM 명령과 SECS-I 패킷 생성기를 추가했다. 제품은 `netstandard2.0`을 유지하며 패키지·외부 프로젝트·참고 DLL 의존성을 추가하지 않았다.

## 범위

| 참고 기능 | FieldLink 진입점 | 상태 |
| --- | --- | --- |
| SECS-II 타입·숫자·중첩 목록·XML | `Secs.Types.SecsValue`, `Secs.Protocols.Secs2Codec` | 이식, big-endian 숫자·1~3바이트 길이 검증 |
| 4바이트 길이·10바이트 HSMS 헤더 | `Secs.Protocols.HsmsCodec` | 생성·파싱·프레임 경계 |
| HSMS 연결·요청·응답·수신 이벤트 | `Secs.HsmsClient`, `Secs.HsmsSession` | 비동기 연결, System Bytes별 동시 요청 대응 |
| HSMS 서버·개별 응답·전체 발행 | `Secs.HsmsServer`, `HsmsSession.ReplyAsync` | 다중 연결, 부분 수신·연속 프레임 처리 |
| Select·Linktest·Deselect·Separate | `HsmsSession` | Select/Linktest 요청·응답, 수신 Deselect 응답, Separate 종료 |
| S1F1·S1F11·S1F13·S1F15·S1F17·S2F13 | `Secs.Gem` | 장비 식별·상태 변수·통신 수립·오프라인·온라인·장비 상수 |
| SECS-I 분할·헤더·체크섬 | `Secs.Protocols.Secs1MessageBuilder` | 참고 생성 규칙 보존 |
| Serial `ExecuteCommand` | 없음 | 원본이 ENQ/EOT 뒤 명령을 무한 반복하는 미완성 구현. 통신 클라이언트 이식 보류 |
| OPC UA | 없음 | 지정 작업본과 그 상위 소스 트리에서 구현을 찾지 못함. 원본 위치 확인 필요 |

이는 전체 SEMI GEM 기능 또는 인증된 HSMS-SS 구현을 뜻하지 않는다. 별도의 T5 재연결 정책, T7 선택 제한, T8 문자 간 시간 제한, 주기적 Linktest, S9 오류의 내장 헤더 대응, 보고서·이벤트 정의 같은 원본 밖의 기능은 추가하지 않았다. 데이터 요청 대기와 제어 요청은 생성자의 전체 작업 제한 시간을 사용한다.

## 클라이언트

```csharp
using System.Net;
using System.Text;
using FieldLink.Secs;

using (var client = new HsmsClient(
    new IPEndPoint(IPAddress.Parse("192.168.0.30"), 5000),
    deviceID: 1, selectOnOpen: true, stringEncoding: Encoding.ASCII))
{
    await client.OpenAsync(cancellationToken);
    var identity = await client.Gem.AreYouThereAsync(cancellationToken);
    var communication = await client.Gem.EstablishCommunicationsAsync(cancellationToken);
    byte onlineAck = await client.Gem.OnlineRequestAsync(cancellationToken);
    // onlineAck는 장비가 반환한 코드다. 전송 성공과 온라인 전환 성공을 구분한다.
    await client.CloseAsync(cancellationToken);
}
```

`OpenAsync` 전·진행 중 요청은 거부한다. 자동 재접속·재전송은 없다. 재연결하려면 다시 Open하고 새 `Session`을 사용한다. 이전 세션 객체는 새 연결에서 재사용되지 않는다. `ConnectionGeneration`은 성공한 Open마다 증가한다.

`selectOnOpen` 기본값은 원본과 같은 `false`다. `true`이면 TCP 연결과 Select 응답까지 하나의 Open 제한 시간으로 확인한다. 이식된 호환 모드는 Select 없이 데이터 교환도 허용한다. 생성자 기본 제한 시간은 10초, 최대 프레임은 길이 헤더 포함 1 MiB다.

`RequestAsync`는 W=1인 홀수 함수 primary를 보내고 같은 장치 ID·스트림·System Bytes의 짝수 secondary를 기다린다. `RequestBytesAsync`는 이미 인코딩한 본문을 받는다. `SendAsync`/`SendBytesAsync`는 송신 완료까지만 확인하며, 장비 처리 성공이나 응답 수신을 보장하지 않는다.

`SecsValue`는 원소 수·문자 수를 `Length`로 반환한다. 문자열의 실제 바이트 길이는 지정한 Encoding으로 계산한다. 시스템 기본 인코딩을 원본대로 유지했으므로 장비 인코딩을 명시하는 편이 좋다. `byte[]` 생성자는 Binary이며 U1 배열은 `new SecsValue(SecsItemType.Byte, bytes)`로 지정한다.

## 서버와 수신 이벤트

컴파일되는 [클라이언트·서버 예제](../samples/FieldLink.Communication/SecsGemExample.cs)를 제공한다. 서버는 `StartAsync` 후 사용하고 `StopAsync`로 연결과 수신 작업을 정리한다. 시작 전·중지 후 `PublishAsync`는 실패한다.

- `MessageReceived`는 세션별 직렬 콜백 큐에서 호출한다. 콜백 안에서 요청·응답·종료를 동기 대기해도 수신 펌프를 막지 않는다. `async void` 이벤트 핸들러는 사용하지 않는다.
- 수신 primary의 응답은 `session.ReplyAsync(request, value)`로 보낸다. 수신 장치 ID·System Bytes를 보존한다.
- `PublishAsync`는 호출 시점의 연결 목록에 순서대로 W=0 메시지를 전송한다. 일부 연결에 전송된 후 실패할 수 있으며 자동 반복하지 않는다.
- 클라이언트의 `automaticGemReplies` 기본값은 true, 서버는 false다. 활성화하면 W=1 S1F1에 빈 List, S1F13에 성공 COMMACK와 빈 장비 목록, S2F17에 현재 시각을 자동 응답한다. 장비 정보 등 사용자 응답이 필요하면 비활성화한다.
- 사용자 콜백 예외는 `LastMessageHandlerError`, 수신·연결 실패는 `LastError` 및 서버의 `LastConnectionError`에서 확인한다. 오류를 성공 응답으로 바꾸지 않는다.

## 수명·실패 계약

정상 `CloseAsync`는 신규 요청을 즉시 차단하고 Separate.req를 전송한 뒤 정리한다. 중복 세션 Close는 같은 종료 작업을 반환한다. `Dispose`와 서버 Stop은 즉시 연결을 정리하며 Separate를 보장하지 않는다. 완료된 세션의 `Completion`은 정상 완료되고 원인 오류는 `LastError`에 남는다.

요청 취소는 `OperationCanceledException`, 전체 마감 초과는 `TimeoutException`, 소켓·수신 프레임 경계 오류는 기존 `CommunicationException`으로 전달한다. SECS 본문·제어 헤더 해석 오류는 `InvalidDataException`, 장비 Abort·Select/GEM 통신 거절은 `SecsProtocolException.Code`로 구분한다. 원격 종료로 진행 중인 요청도 실패한다. 통신 실패가 장비의 명령 미실행을 뜻하지 않는다.

송신 완료 후 응답 대기 취소·시간 초과는 해당 요청만 제거한다. 이후 도착한 응답은 새 요청의 응답으로 사용하지 않고 비요청 콜백으로 전달한다. 실제 송신 중 실패·취소는 부분 전송 가능성 때문에 연결을 닫는다. 송신 대기 중 취소는 활성 송신을 중단하지 않는다.

동시 대기 요청과 미처리 콜백은 각각 최대 1,024개다. 요청 한도 초과는 새 요청을 거부하고 콜백 큐 초과는 연결을 종료하며 오류를 남긴다. Close 때 미실행 콜백은 폐기하지만 실행 중인 사용자 코드를 강제 중단하거나 기다리지는 않는다. 서버 기본 최대 연결 수는 100이다.

`SecsValue`의 타입·값 속성은 생성 후 교체할 수 없다. 배열 값 자체는 복사하지 않으므로 직렬화 중 수정하면 안 된다. `SecsMessage`는 본문을 복사하여 소유하며 `Data`도 복사본을 반환한다.

## 보존한 규칙과 의도적인 교정

| 항목 | 내용 |
| --- | --- |
| 정상 SECS-II 데이터 | 15개 형식, 숫자 배열·스칼라, Boolean true의 FF 바이트, Binary/JIS8 배열, 중첩 List 보존 |
| GEM 요청 본문 | 기본 S1F1/S1F11/S1F13/S1F15/S1F17은 본문 없음, 지정 S1F11 ID는 I4 배열, S2F13은 List라는 참고 호출 규칙 유지. 장비별 다른 본문은 RequestAsync 사용 |
| SECS-I | 244바이트까지 단일 블록, 초과 시 224바이트 분할, 마지막 E 비트, 체크섬, 빈 입력의 빈 블록 목록 보존. 분할 블록 번호를 증가시키지 않는 원본 동작도 보존하므로 완성된 Serial 전송기로 사용하지 말 것 |
| HSMS 장치 ID | 원본 공용 파서가 상위 비트를 SECS-I 방향 비트로 제거하던 동작을 교정하여 16비트 유지 |
| 자동 응답 식별자 | 원본의 신규 메시지 번호 발급을 교정하여 수신 System Bytes 반환 |
| 제어 메시지 | 모든 홀수 SType에 +1 응답하던 동작을 명시적 Select/Deselect/Linktest/Separate 처리로 교정 |
| 데이터 방어 | 알 수 없는 형식, 길이 0바이트 헤더, 숫자 원소 길이 불일치, 잘린/남는 본문, 64단계 초과 중첩 거부 |
| XML | 단일 원소·빈 숫자 배열을 복원하고 숫자를 문화권과 무관하게 저장·파싱 |
| API | 동기 결과 객체·상속 기반 통신에서 명시적 Open과 Task/예외 계약으로 전환. 이전 외부 라이브러리와 소스·바이너리 API 호환을 제공하지 않음 |

기존 FieldLink 공개 API는 삭제·변경하지 않았으며 SECS 관련 선언만 추가했다. `ISecs`는 GEM이 실제 사용하는 요청 계약만 갖는다. 일반 요청형 `ITcpClient`로 비요청 메시지를 억지로 처리하지 않고, TCP 송수신 부품을 `Communication/Tcp/TcpDuplexConnection`에 두고 HSMS 의미·식별자·제어 절차를 `Secs`에 두었다.

## 근거와 검증

참고 작업본 기준 커밋은 `2f54bb4666e7494d15f085c443d57e63017421f7`이다. 확인한 14개 SECS 소스와 배열 분할 근거 파일의 SHA-256은 [출처 기록](secs-source-manifest.json)에 있다. 고정 기대 바이트는 해당 소스의 형식 상수·헤더 위치·체크섬 규칙에서 독립적으로 적었으며 제품 코덱으로 기대값을 생성하지 않았다.

HSMS 제어 종류·System Bytes 응답 관계는 [Secs4Net의 제어 처리 소스](https://github.com/mkjeff/secs4net/blob/base/src/Secs4Net/HsmsConnection.cs), 길이·형식 해석은 [SECS-II 디코더 소스](https://github.com/mkjeff/secs4net/blob/base/src/Secs4Net/Item.Decode.cs)와 대조했다. 이것은 구현 교차 확인이며 SEMI 유료 규격 원문이나 인증 시험의 대체가 아니다. 패키지나 소스 의존성은 추가하지 않았다.

```powershell
dotnet build FieldLink.slnx -c Release
dotnet run --project tests/FieldLink.Communication.TestRunner/FieldLink.Communication.TestRunner.csproj -c Release -- SecsTests
dotnet run --project tests/FieldLink.Communication.TestRunner/FieldLink.Communication.TestRunner.csproj -c Release
```

기준 변경 전 513개 회귀가 통과했다. 최종 Release 솔루션 빌드는 경고·오류 0개이며 신규 SECS 20개를 포함한 전체 533개 테스트가 통과했다(실패 0개, 41.65초). 기존 공개 API 삭제·변경은 0개이고 SECS 선언 147개가 추가됐다. 신규 검증은 고정 패킷, 모든 숫자 형식, XML·길이 경계, 손상 프레임, Select/Linktest, 자동 응답, GEM 거절, 비요청 이벤트, 동시 요청, 늦은 응답, 취소, 종료·재연결을 포함한다. 실제 장비·COM 포트에 연결하지 않았다.
