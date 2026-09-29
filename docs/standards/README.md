# 통신·프로토콜 공식 문서 검증 보고서

확인일: **2026-09-29**. 현재 FieldLink 소스의 통신 기반과 주요 PLC/Robot 프로토콜을 제조사·표준화 기관의 자료에 대조했다. 확인된 결함을 수정하고 실패 재현 검사 28개와 정상 규격 예제 2개를 추가했다.

**결과: 솔루션 빌드 경고 0 / 오류 0, 전체 회귀 검사 513개 통과 / 실패 0.** 실제 PLC·로봇·COM 포트에는 연결하지 않았다. 모든 제조사·기종의 최신 명세 또는 모든 공개 API를 전수 검증한 결과는 아니다.

## 범위와 공식 자료

| 구분 | 이번 확인 | 상세 근거와 남은 범위 |
| --- | --- | --- |
| TCP·UDP·Serial·WebSocket | 바이트 흐름·부분 전송 처리·프레임 경계·요청 제한 시간·취소·재연결·종료 수명, 기존 회귀 검사 | [통신 기반](transport-audit.md) — 전송 계층 자체의 새 확정 결함은 발견하지 않음. 실제 partial send/UDP truncation 분기·물리 Serial 타이밍 한계 명시 |
| Mitsubishi·Modbus | 3E 기본 일괄 명령과 응답, MBAP 길이, 코일 패딩을 공식 문서와 비교·교정 | [주요 PLC](plc-primary-audit.md) — SLMP Ver.N, MC Ver.AB, Modbus V1.1b3 등 |
| Siemens | TPKT·PDU 한계·STRING 형식·GET/PUT 설정 문서 대조 및 기존 회귀 확인 | [Siemens 범위](plc-primary-audit.md#siemens-s7) — 전체 S7 wire 명세는 미확보 |
| LS·Omron·CIP·ADS·Yokogawa·MEMOBUS | 주요 TCP 드라이버와 저수준 응답의 선언 길이·상관관계·장치 오류 검토 | [기타 PLC](plc-secondary-audit.md) — 문서 버전·절·URL 및 11개 추가 제조사의 미완료 범위 |
| YASKAWA·ABB·YAMAHA·KUKAVARPROXY | 공개 규격에 근거한 헤더·상태·HTTP 미디어 타입·행 교환·응답 식별자 교정 | [Robot](robot-audit.md) — FANUC·EFORT·Estun·Hyundai를 포함한 8사별 접근 상태와 제한 |

각 상세 문서에 직접 확인한 URL, 판본·발행일, 절/페이지, 열람일과 코드/테스트를 연결했다. 공식 사이트의 현행 공개본과 유료·회원 전용 최신판은 구분했다. 문서를 열지 못한 경우 검색 결과만으로 본문 준수를 판정하지 않았다. 제조사 PDF 전체를 재배포하지 않는다.

## 바뀐 동작

| 대상 | 이전 문제 | 수정 결과 |
| --- | --- | --- |
| MC Binary/ASCII | 잘못된 서브헤더·길이 허용, ASCII 숫자 예외, 홀수 워드·범위 초과 직렬화 | 응답 실패 결과 및 전송 전 인수 검증. 정상 읽기·쓰기 패킷 유지 |
| Modbus TCP | 불가능한 MBAP 길이의 본문을 대기, 과대 패킷 생성, 비트 패딩 무시 | 헤더 단계 `InvalidFrame`, ADU 제한, FC01/02 패딩 검사 |
| LS XGT | 불완전한 ACK 성공, 첫 블록만 반환, 오류 코드 잘림, CPU명 오류 | 헤더/블록 전체 검사, 순서대로 데이터 반환, 16비트 오류 보존 |
| ADS·Yokogawa·MEMOBUS | 짧거나 길이/요청 식별자가 다른 응답 성공 | 각 규격에서 확인한 외피·본문·요청 대응 검증 |
| YASKAWA HSES | payload 길이 잘림, 짧은 헤더 예외, 상태 본문 크기 무검증, 오류 설명 반전 | 길이 검증과 실패 결과, 8바이트 상태 본문 확인, 공식 오류 의미 적용 |
| ABB RWS | POST 기본 미디어 타입 `text/plain` | `application/x-www-form-urlencoded`. 호출자는 form 인코딩된 본문을 전달 |
| YAMAHA RCX | LOAD의 잘못된 접두어, 응답 행마다 명령 재송신 | ASCII `@`, 한 번 송신 후 후속 행 수신 |
| YAMAHA JOGXY | 200ms 실행 계속 신호·RUN/END 처리가 없는 상태로 이동 명령 전송 | `JogXYAsync`는 **전송 없이 미지원 실패**. 이동 수명주기 자체는 미구현 |
| KUKAVARPROXY | 불완전한 상태 tail·틀린 ID/모드 허용 | 길이·3바이트 tail·ID/모드 확인. KUKA 제조사 표준이 아닌 IMTS 서버 규격 |

공개 API 시그니처와 런타임 의존성은 유지했다. 잘못된 입력·응답에 대한 동작은 의도적으로 바뀐다. 특히 저수준 MC 빌더의 null·빈 데이터·범위 초과는 인수 예외가 된다. Yamaha `BuildExchangeSequence`의 두 번째 이후 빈 배열은 수신 전용 단계를 뜻한다. LS 다중 읽기 결과는 첫 블록만이 아니라 모든 블록을 포함한다.

## 실행한 검증

실행 환경은 Windows, .NET SDK **10.0.401**, 제품/테스트 `netstandard2.0`, 콘솔 러너 `net48`이다.

| 단계 | 결과 |
| --- | --- |
| 변경 전 기존 빌드 산출물의 기준 러너 | 483 통과 / 0 실패, 41.27초 |
| 신규 공식 규격 검사 | 총 30개 추가. 정상 예제 2개는 기존 구현에서 통과, 결함 재현 28개는 수정 전 실패 확인 |
| 현재 소스의 `dotnet build FieldLink.slnx -c Release` | 성공, 경고 0 / 오류 0. 임시 MSBuild 우회 설정 없이 실행 |
| 현재 산출물의 전체 콘솔 러너 | **513 통과 / 0 실패, 41.37초** |
| 공개 API·의존성·골든 해시 | 전체 러너에서 통과. 기존 골든 JSON 및 해시는 변경하지 않음 |
| 실제 장비 | 미실행 |

실행 명령과 필터는 [테스트 안내](../testing.md)에 있다. 초기 작업 폴더에서 기존 `docs` 자료가 삭제되어 일반 빌드가 누락 파일 3개로 실패했다. RED 검사에는 Git 기준과 일치함을 확인한 임시 복사본을 사용했다. 최종적으로 `public-api.txt`, `fixture-integrity.json`, `fanuc-member-migration.json`을 내용 변경 없이 `tests/FieldLink.Communication.Tests/Contracts/`로 옮기고 프로젝트 참조를 고쳐 정상 빌드로 검증했다.

기존 LS/MEMOBUS 합성 응답의 누락 필드는 공식 정상 형식으로 교정했다. KUKA/YRC 과거 골든 사례 3개는 원본 입력·기대값을 그대로 검사하면서 규격 교정 후의 결과도 정확히 비교한다. 기대값을 새 구현에서 자동 생성하거나 사례를 제외하지 않았다.

## 완료하지 않은 부분

- Siemens 전체 S7 wire 명세, ODVA 최신 유료 전문, FANUC의 해당 인터페이스 공식 바이트 명세 등은 확보하지 못했다.
- KEYENCE 및 Modbus 파생 PLC의 전 모델 주소표, 나머지 저수준·Serial 프로토콜의 모든 빌더/파서·명령은 전수 대조하지 않았다. 상세 표의 미완료 항목을 지원 인증으로 해석하지 않는다.
- YAMAHA의 JOGXY 이동 수명주기는 미지원이다. 이번에 자동 지속 이동 기능을 추가하지 않았다.
- 현장 검증에는 정확한 CPU·통신 모듈·로봇 컨트롤러·펌웨어와 활성화 옵션, 해당 판본 매뉴얼, 실기기 송수신 캡처가 필요하다. 합성 패킷·루프백 통과만으로 기기 호환성을 보장하지 않는다.
