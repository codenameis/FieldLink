# 장치 수명과 재접속 예제

이 폴더에는 가상 프로토콜의 수명 관리 예제와 Melsec 제품 클라이언트의 사용 예제가 있습니다. FieldLink 제품 DLL에 포함되지 않습니다. 이 소스는 `netstandard2.0` 테스트 라이브러리에 직접 포함하여 컴파일하고 별도의 `net48` 실행기로 루프백 검증합니다. 제품 라이브러리도 `netstandard2.0`을 유지합니다.

- [SessionDevice.cs](SessionDevice.cs): 연결별 초기화, 읽기·쓰기, 장치 오류 전달, 정상 종료와 동시 요청 차단.
- [ReconnectionExample.cs](ReconnectionExample.cs): 애플리케이션이 명시적으로 선택하는 연결 재시도. 횟수·간격·취소를 받으며 장치 명령은 재전송하지 않습니다.
- [MelsecReadWordsExample.cs](MelsecReadWordsExample.cs): MC 3E Binary TCP로 D100부터 50워드를 읽고 전송 자원을 해제합니다. [사용 계약](../../docs/plc-drivers/melsec-client.md)을 확인하세요.
- [MelsecTypedExample.cs](MelsecTypedExample.cs): `Plc.MelsecMcTcp` 하나로 연결하고 `int[]` 연속 쓰기·읽기를 수행합니다. D100~D105를 변경하는 예제이며 [간편 API 계약](../../docs/plc-drivers/mc-client-api.md)을 확인하세요.
- [PlcTypedExamples.cs](PlcTypedExamples.cs): Siemens·OMRON·LS·Allen-Bradley·Modbus를 생성하고 같은 `WriteAsync`/`ReadInt32Async`로 int 배열을 다룹니다. [주소·문자열·지원 범위](../../docs/plc-drivers/simple-client-api.md)를 확인하세요.
- [PLC 제조사별 연결 예제](../../docs/plc-drivers/usage-guide.md) · [로봇 연결 예제](../../docs/robot-usage.md) · [HSL 코드 비교](../../docs/hsl-comparison.md).
- [계약과 사용법](../../docs/networking-lifecycle.md).

## 가상 프로토콜

| 명령 | 요청 | 응답 |
|---|---|---|
| 초기화 | `10` | `[오류 코드, 세션]` |
| 읽기 | `20 세션` | `[오류 코드, 값]` |
| 쓰기 | `21 세션 값` | `[오류 코드, 00]` |
| 정상 종료 | `30 세션` | `[오류 코드, 00]` |

표의 요청은 16진수입니다. 응답은 정확히 2바이트이며 오류 코드 0은 성공입니다. 초기화 거절은 연결을 종료하고, 정상 읽기·쓰기 거절은 오류 코드를 보존하면서 연결을 유지합니다. 정상 종료 응답 대기는 예제에서 2초로 지정합니다. 이 예제의 명령과 응답 형식은 통신 수명주기 설명용으로 정의한 것입니다.

## 소유권

하나의 `SessionDevice`가 하나의 `ITcpClient`를 사용합니다. 드라이버 생성 후 열기·읽기·쓰기·정상 종료는 드라이버를 통해 호출하세요. 다른 드라이버나 직접적인 클라이언트 호출과 섞으면 장치 세션과 정상 종료의 순서를 보장할 수 없습니다. 클라이언트의 최종 Dispose는 생성한 애플리케이션이 담당합니다.

읽기·쓰기는 동시 호출할 수 있습니다. 종료를 시작하면 대기·신규 명령은 거부합니다. Open과 Disconnect의 동시 실행은 거부하므로 애플리케이션의 한 제어 흐름에서 수명을 관리하세요. 드라이버 대기는 호출자의 취소 토큰을 따르며, 전송 제한 시간은 클라이언트 설정을 따릅니다. 전체 드라이버 호출 시간을 제한하려면 마감 시간이 있는 토큰을 전달합니다.

```powershell
dotnet run --project tests/FieldLink.Communication.TestRunner/FieldLink.Communication.TestRunner.csproj -c Release -- DeviceLifecycleTests
```

위 명령은 FieldLink 저장소 루트에서 실행합니다.
