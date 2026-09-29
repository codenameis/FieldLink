# Robot 프로토콜 공식 문서 재검토

확인일: 2026-09-29. 대상: `src/FieldLink/Robot/`의 패킷 빌더, 응답 파서, 장치별 교환 절차.

이 문서는 **실제로 열람한 공개 자료의 해당 부분**과 구현을 대조한 기록이다. 공개 사이트의 현재 문서와 특정 컨트롤러에 설치된 펌웨어는 구분한다. 자료가 없는 제조사까지 최신 규격 준수로 판정하지 않는다. 실제 로봇에 연결하거나 명령을 실행하지 않았다.

## 제조사별 확인 범위

| 제조사·통신 | 확인한 자료와 버전 | 이번 대조 범위 | 남은 확인 |
| --- | --- | --- | --- |
| YASKAWA HSES/UDP | 공식 KB Packet Header / Error and Status Code Tables 2024-03-01, Controller Status Reading 2024-03-12 | 32바이트 공통 헤더, 로봇 제어 구분, ACK, 요청 ID, 블록 번호, 서비스 대응, 상태 조회 요청/8바이트 응답, 추가 오류 코드 | 전체 매뉴얼 최신 개정판, 모든 명령의 본문·배열·장치별 최대 길이 |
| YASKAWA Ethernet Server/TCP | 공식 Ethernet Function 매뉴얼 링크 확인, PDF 본문 403 | 구현과 기존 TCP 단계별 시험의 구조 검토 | CONNECT/HOSTCTRL_REQUEST 전체 규격 및 최신 펌웨어 대조 미완료 |
| ABB HTTP/RWS | 공식 `3HAC050973-001 Revision M` 온라인 API | XHTML 조회 경로·표현, POST form 미디어 타입, 인증/세션의 호출자 책임 | 현재 구현을 OmniCore/RWS 2 및 RobotWare 8 전체 지원으로 판정하지 않음 |
| YAMAHA RCX TCP | Yamaha Robotics Support 공개 `RCX340-PRO_E_V1.20.pdf` 제12장 | LOAD 접두어, Ethernet CRLF, 상태 조회 한 요청의 값/OK 두 행 | 회원 사이트 최신 전체판은 로그인 필요. JOGXY 이동 수명주기 미구현 사항, 관절 조회 형식, RCX 전 모델·Telnet 협상 미검증 |
| KUKA KUKAVARPROXY | 원저자 IMTS의 공개 README, 확인일 현재 master | 길이 필드, 읽기/쓰기 모드, 응답 ID, 3바이트 성공 tail | **KUKA 제조사 표준이 아닌 제3자 서버 규격**. 해당 서버 설치·버전 필요 |
| KUKA Text TCP | 제조사 공개 규격을 특정하지 못함 | 현재 사용자 정의 텍스트 형식과 호출 구조 확인 | 상대 로봇 프로그램 및 그 명령 규약 필요. Ethernet KRL/RSI 준수 판정 불가 |
| FANUC | 공식 MyPortal의 기술 매뉴얼 제공·등록 안내 열람 | 기존 56바이트 프레임·초기화/응답 처리 코드 검토 | 해당 PC Interface/SNPX의 공식 바이트 명세와 컨트롤러 옵션·버전 필요 |
| EFORT | 공식 다운로드 센터 열람 | 현재 38/36바이트 ER7BC10 형식 및 788/784바이트 상태 모델의 구현 범위 확인 | ER7BC10 해당 제어기의 `MessageHead/MessageTail` 명세를 확보하지 못함 |
| Estun | 공식 RCS2 PDF 링크 발견, 본문 열람 실패 | Modbus 위 로봇 레지스터 맵·명령 handshake 분리 확인 | 주소 18/51/99, 명령 코드, 상태 맵을 해당 RCS2 판본으로 확인 필요 |
| Hyundai | 공식 Hi5 On-Line Tracking 목차, Hi5a Pick-it §2.4.3 열람 | UDP 사용 환경과 FieldLink의 64바이트 위치 보정 모델 구분 | S/P/F, 64바이트 offset·단위·handshake 명세 본문 미확보. Hi6/Hi7 공통 규격으로 확장 해석하지 않음 |

## 자료와 접근 기록

### YASKAWA

- [High Speed Ethernet Server Packet Header](https://knowledge.motoman.com/hc/en-us/articles/21740538183959-High-Speed-Ethernet-Server-Packet-Header): 2024-03-01 갱신. Request packet header / Request packet subheader / Response packet subheader 표를 직접 열람했다.
- [Controller Status Reading](https://knowledge.motoman.com/hc/en-us/articles/21804200504087-High-Speed-Ethernet-Server-Controller-Status-Reading): 2024-03-12 갱신. 명령 `0x72`, instance 1, attribute 0/service 1, 두 4바이트 상태 필드를 확인했다.
- [System Info Request](https://knowledge.motoman.com/hc/en-us/articles/21743780618135-High-Speed-Ethernet-Server-System-Info-Request): 2024-11-01 갱신. 정상 응답의 24/16/8바이트 문자열 필드와 명령 `0x89`/attribute 0/service 1을 `BuildReadSystemInfo`와 대조했다. 이 파서의 잘못된 본문 길이에 대한 전체 검사까지 완료한 것은 아니다.
- [Read Position Data](https://knowledge.motoman.com/hc/en-us/articles/21820464217239-High-Speed-Ethernet-Server-Read-Position-Data): 2024-03-12 갱신. 명령 `0x75`, Cartesian R1 instance 101과 앞의 5개 4바이트 필드 뒤 좌표 배치를 `BuildReadPose`와 대조했다. 현재 API는 좌표 정수 문자열만 반환하므로 앞의 좌표계·형태·공구 정보를 제공하지 않는다. 모델별 축 수와 잘못된 본문 길이의 전체 검증은 별도다.
- [Error and Status Code Tables](https://knowledge.motoman.com/hc/en-us/articles/21743314260631-High-Speed-Ethernet-Server-Error-and-Status-Code-Tables): 2024-03-01 갱신. 상태 `0x00/08/09/1F/28`, 추가 오류 `0x2070/4130/A101`, 전체 매뉴얼 번호 `HW1483358`의 연결을 확인했다.
- [Remote Operations with Yaskawa Motoman Robots](https://knowledge.motoman.com/hc/en-us/articles/4408154769047-Remote-Operations-with-Yaskawa-Motoman-Robots): 2024-09-25 갱신. HSES와 TCP Ethernet Server를 같은 프로토콜로 취급하지 않는다.
- [공식 Ethernet Function PDF, 178942-1CD](https://www.motoman.com/getmedia/38CD89D5-C90D-4C5A-8628-0551C44C9A6C/178942-1CD.pdf.aspx?ext=.pdf): 위 공식 안내가 연결한 자료. 이번 열람은 403으로 실패했다. 최신 개정 번호나 본문을 확인했다고 주장하지 않는다.

현재 공통 응답 검증은 YERC/헤더 길이, 실제 본문 길이, 예약값, 처리 구분, ACK, 요청 ID, 로봇 제어 블록 번호 0, 응답 서비스와 요청 서비스를 비교한다. UDP 요청 ID는 8비트이므로 256번 뒤 같은 ID가 재사용된다. 아주 오래 지연된 동일 ID·서비스 응답을 완전히 구분할 수 있는 프로토콜로 해석하면 안 된다.

KB의 추가 상태 크기 단위 설명은 32바이트 헤더 내 표의 필드 크기와 서로 맞지 않는 부분이 있다. 전체 공식 매뉴얼 또는 장치 캡처 없이 그 설명 하나로 1/2바이트 해석을 변경하지 않았다. 복수 변수의 count prefix와 모든 위치·알람 본문도 이번 상태 조회 검증에 포함되지 않는다.

길이 필드의 최대 표현값 `65535`를 시험하는 것은 필드 overflow 방지 검증이다. 특정 로봇 명령이나 UDP 송신에서 그 크기의 데이터를 지원한다는 의미는 아니다. 추가 오류 `0xA101`에 공식 소프트웨어 제한 의미를 연결했고, 기존 `0xA102` 매핑은 호환성을 위해 유지했다. `0xA102`는 이번 공식 표로 확인한 코드가 아니다.

### ABB

- [Robot Web Services, 3HAC050973-001 Revision M](https://developercenter.robotstudio.com/api/rwsApi/): Introduction의 Mime Type, Authentication, Form data, Session을 직접 열람했다. 기본 XML/XHTML 표현과 form POST가 현재 구현을 대조한 기준이다. 문서 페이지에 발행일은 표시되지 않는다.
- [현행 Robot Web Services 진입점](https://developercenter.robotstudio.com/api/RWS/): 접근은 되었으나 이 도구에서 동적 API 본문을 읽지 못했다. 최신 전체 API 확인 근거로 사용하지 않았다.
- [ABB Control Station and Write Access](https://developercenter.robotstudio.com/api/pcsdk/articles/Manual/Run-time-environment/Running-PC-SDK-Applications/Control-Station-Write-Access.html): RobotWare 8 이후 제어권 계약 변경을 설명하는 공식 자료. RWS 구현을 검증한 자료가 아니라 새 RobotWare에 일괄 지원을 선언하지 않는 근거다.

`AbbHttpClient`의 인증, 쿠키, TLS 설정은 주입된 `HttpClient`/처리기의 책임이다. `WriteAsync("url=...", value)`에는 이미 form 인코딩된 본문을 전달한다. 값을 다시 인코딩하거나 URL마다 필요한 권한·mastership을 자동 취득하지 않는다. HTTP 성공과 비동기 작업 완료도 구분해야 한다.

### YAMAHA

- [RCX340-PRO_E_V1.20.pdf, 제12장 Online commands](https://yamaharobotics.zendesk.com/hc/en-us/article_attachments/360006547732): 본문을 직접 열람했다. §1 p.12-1의 Ethernet CRLF, §2.1 p.12-9의 LOAD 예, §3.3 p.12-24의 MOTOR 값/OK 응답이 기준이다. 문서 파일명은 V1.20이며 발행일은 이 발췌본에서 확인하지 못했다. PDF 화면 캡처는 도구의 cache miss로 실패하여 추출 본문을 기준으로 확인했다.
- [공식 다운로드 안내](https://global.yamaha-motor.com/business/robot/download/) → [매뉴얼 회원 페이지](https://www2.yamaha-motor.co.jp/en/robot/manual/manindex): 실제 로그인 페이지로 이동하는 것을 확인했다. 공개 V1.20을 최신 회원판이라고 표기하지 않는다.

상태 조회는 명령 한 번을 보낸 뒤 값과 OK를 차례로 받는 교환이다. 행마다 명령을 다시 보내면 두 번째 응답이 남아 다음 요청과 섞인다. 기존 테스트가 이 반복 전송과 LOAD의 ASCII `?` 접두어를 보존하고 있었으므로, 해당 기대값을 위 문서의 교환 의미로 교정했다. 테스트 삭제나 원본 golden JSON 재생성은 하지 않았다.

같은 문서 §4.4 p.12-40의 `JOGXY`는 이동 시작/종료 응답 `RUN`/`END` 및 200ms 간격의 실행 계속 신호 `0x16`을 명시한다. 기존 `JogXYAsync`는 1행 `OK` 판정을 재사용하고 지속 신호·중단 수명주기를 구현하지 않아 정상 이동 응답도 실패로 판정하고 남은 행을 소비하지 못했다. 현재 API 계약으로 이 이동 수명주기를 지원할 수 없으므로 **공개 시그니처를 유지하되 전송 전에 미지원 실패 결과를 반환**하도록 수정했다. 오류 설명에 200ms 및 RUN/END 미지원 이유를 포함한다. 자동 지속 이동은 추가하지 않았다. `ReadJointsAsync`의 `@?WHERE` 1행/관절 좌표 해석도 해당 컨트롤러 명세의 별도 확인이 필요하다.

### KUKA

- [IMTS KUKAVARPROXY 원저자 저장소](https://github.com/ImtsSrl/KUKAVARPROXY): README의 `Communicate with KukavarProxy`를 직접 열람했다. 2019년 공개 출처가 표시되며 README 자체에 규격 개정 번호는 없다. **IMTS가 정의한 프로토콜의 1차 자료**이고 KUKA 공식 Ethernet KRL/RSI 매뉴얼은 아니다.

KUKAVARPROXY 응답은 ID/선언 길이/모드/값 길이/값/3바이트 tail로 구성된다. FieldLink의 과거 단일 tail 바이트 성공 판정과 ID·모드 미검증을 대조 대상으로 삼았다. `KukaTextProtocol`의 `00`, `01`, `06` 등 텍스트 명령은 이 바이너리 규격과 별개다. 대응하는 로봇 측 프로그램의 명세가 필요하다.

### FANUC, EFORT, Estun, Hyundai의 미확인 자료

- [FANUC MyPortal](https://www.fanucamerica.com/support/myportal): 공식 등록·기술 매뉴얼 안내를 열람했다. 공개 웹에서 해당 바이너리 프로토콜의 최신 원문을 확보하지 못했다. 서드파티 구현과 같다는 사실로 제조사 공식 적합성을 대신하지 않는다.
- [EFORT 다운로드 센터](https://www.efort.com.cn/index.php/service/downservice.html): 제품별 문서 목록을 확인했다. [ECR5 공식 PDF 링크](https://www.efort.com.cn/web/upload/2025/01/17/1737108412172tiyzi.pdf)는 본문 접근 실패이며, 제품군도 ER7BC10과 다르므로 해당 구현의 바이트 명세로 사용하지 않는다.
- [Estun RCS2 Command System Manual 공식 PDF 링크](https://en.estun.com/static/upload/file/20230815/1692076252545493.pdf): 검색 색인에는 V1.13/RCS2 V1.30 이상 표기가 보였으나 직접 본문을 열지 못했다. [ER Series Operation Manual 공식 PDF 링크](https://www.estun.com/static/upload/file/20220829/1661740608919158.pdf)는 redirect loop로 실패했다. 표지·절·레지스터 맵의 직접 대조 완료 자료로 계산하지 않는다.
- [Hyundai Hi5 On-Line Tracking, Hi5OT120701FMEN1](https://manual2-hd.web.app/docs/en/hi5/fn/Hi5OT120701FMEN1/): 공식 온라인 목차를 열람했으나 `2. Use Methods` 본문은 cache miss로 실패했다. [Hi5a Pick-it, Hi5aPI230628FMEN2 §2.4.3](https://manual2-hd.web.app/docs/en/hi5a/fn/Hi5aPI230628FMEN2/2/4/2-4-3/)의 UDP 설명을 열람했지만 이는 위치 보정 바이트 명세가 아니다. [현행 HRBOOK](https://hrbook-hrc.web.app/)은 동적 본문을 읽지 못했다.

FANUC 상태 코드·초기화, EFORT marker/heartbeat와 상태 offset, Estun 로봇 맵, Hyundai 위치 보정의 시간·단위·종료 조건을 완료 판정하려면 **실제 사용 컨트롤러의 모델·펌웨어·통신 옵션에 대응하는 공식 매뉴얼**이 필요하다. 기존 fixture/loopback 시험 통과는 이 공식 문서 공백을 해소하지 않는다.

## 이번 수정과 호환성

| 대상 | 확인된 문제 | 변경한 동작 |
| --- | --- | --- |
| YASKAWA HSES | 16비트 본문 길이 overflow, 짧은 상태 헤더의 인덱스 예외, 상태 본문의 추가 바이트 허용 | 빌드 전에 길이 범위 검사, 짧은 응답은 실패 결과, 상태 본문은 정확히 8바이트 검사 |
| YASKAWA 추가 오류 | 서보 OFF와 위치 데이터 없음의 반대 의미, 공식 `0xA101` 누락 | 공식 오류 의미로 수정하고 원래 상태 코드 보존 |
| ABB RWS POST | form 본문을 `text/plain`으로 전송 | `application/x-www-form-urlencoded` 사용. 호출자가 인코딩한 본문은 그대로 유지 |
| YAMAHA LOAD | 전각 접두어가 ASCII `?`로 변환됨 | ASCII `@`로 구성 |
| YAMAHA 다중 행 | 매 응답 행마다 동일 명령 재전송 | 명령은 한 번 전송하고 남은 행은 수신만 수행 |
| YAMAHA JOGXY | RUN/END 대신 1행 OK 판정, 실행 계속 신호·중단 수명주기 미구현 | 이동 명령 전송 전에 미지원 실패 결과 반환. 새 이동 API는 추가하지 않음 |
| KUKAVARPROXY | 1바이트 tail도 성공 처리, 선언 길이 및 응답 ID/모드 불일치 수용 | 전체 길이와 3바이트 성공 tail, 요청 ID/모드 일치 검사 |

공개 API의 이름·시그니처는 유지했다. 잘못된 응답을 성공으로 수용하던 동작과 잘못된 명령 바이트는 변경했다. `YamahaRcxProtocol.BuildExchangeSequence`는 첫 단계에만 명령 사본을 반환하고 다음 단계에 빈 배열을 반환한다. 빈 배열은 추가 송신 없이 수신할 단계를 뜻하므로, 이 공개 도우미를 직접 사용하는 호출자는 이 의미를 적용해야 한다. 라이브러리 클라이언트는 `ReceiveAsync`를 직접 호출한다.

## 회귀 검증

독립적인 문서 기대값과 검증 위치는 [`RobotStandardsTests.cs`](../../tests/FieldLink.Communication.Tests/RobotStandardsTests.cs)에 둔다. 신규 11개 검사는 YASKAWA 상태 조회 요청 벡터·추가 오류 의미·길이 overflow/짧은 헤더/상태 본문 크기, ABB POST 미디어 타입, YAMAHA LOAD/단일 전송 다중 행/JOGXY 전송 방지, KUKAVARPROXY tail·길이·ID·모드가 대상이다. 수정 전 정상 YASKAWA 요청 벡터 1개는 통과했고, 결함 재현 10개는 실패하는 것을 확인한 뒤 제품 코드를 수정했다.

기존 `robot-golden.json`과 해시는 그대로 보존했다. [`RobotGoldenTests.cs`](../../tests/FieldLink.Communication.Tests/RobotGoldenTests.cs)는 총 134개 벡터를 계속 실행하며, 기존 Job 선택 교정과 같은 방식으로 규격 교정의 옛 기대값과 새 기대값을 각각 고정한다. 이번 변경은 KUKAVARPROXY `ExtractActualData#58`의 짧은 tail 성공을 실패로, `#59`의 1바이트 가짜 상태를 로컬 형식 오류로, YASKAWA `CheckResponseContent#79`의 짧은 헤더 예외를 실패 결과로 변경한다. 모든 결과 필드를 비교하며 해당 사례를 제외하지 않는다.

검증 실행 결과와 전체 솔루션 결과는 이 폴더의 통합 검토 기록에서 확인한다. 장치가 명령을 처리했으나 응답이 유실된 경우의 재실행은 이번 수정에서도 자동으로 수행하지 않는다.
