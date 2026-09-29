# Mitsubishi MC · Modbus · Siemens 검증

확인일: 2026-09-29. 공식 문서에서 확인한 필드·한계와 FieldLink 구현을 대조했다. 모든 명령, CPU, 펌웨어 또는 물리 장비의 적합성 인증을 의미하지 않는다.

## 확인한 공식 자료

| 대상 | 문서·판본 | 열람 범위와 최신성 |
| --- | --- | --- |
| Mitsubishi SLMP | [SH-080956ENG-N](https://www.mitsubishielectric.com/dl/fa/document/manual/plc/sh080956eng/sh080956engn.pdf), 2025-10 | 공식 [다운로드 목록](https://www.mitsubishielectric.com/fa/download/search.page?kisyu=/plcr&mode=manual)의 Ver.N과 PDF를 대조. §4.1 요청, §4.2 응답, §5.2 디바이스 일괄 읽기·쓰기 |
| Mitsubishi MC | [SH-080008-AB](https://www.mitsubishielectric.com/dl/fa/document/manual/plc/sh080008/sh080008ab.pdf), 2022-05 | 같은 목록의 Ver.AB 및 PDF 확인. 이번 교정은 3E 공통 형식 중심이며 1E/A3C/FX 전 명령 대조는 미완료 |
| Modbus | [Application Protocol V1.1b3](https://www.modbus.org/file/secure/modbusprotocolspecification.pdf), 2012-04-26 | 현재 [공식 규격 목록](https://www.modbus.org/modbus-specifications)이 안내하는 판본. §4.1 PDU/ADU, §4.2 바이트 순서, §6.1/6.2 비트 읽기, §6.3/6.12 레지스터 한계, §7 예외 응답 |
| Modbus TCP | [Messaging Implementation Guide V1.0b](https://www.modbus.org/file/secure/messagingimplementationguide.pdf), 2006-10-24 | 공식 목록과 검색에 노출된 §3.1.3 MBAP 표 확인. PDF 전체 다운로드는 시간 초과로 실패. 전체 본문을 열람한 근거로 사용하지 않음 |
| Siemens S7 통신 | [TIA V21 S7 communication](https://docs.tia.siemens.cloud/r/en-us/v21/s7-communication-s7-1200-s7-1500-s7-1200-g2/useful-information-about-the-instructions-for-s7-communication-s7-1200-s7-1500-s7-1200-g2), 11/2025 | 단일 범위의 PDU별 읽기·쓰기 데이터 한계 표 열람 |
| Siemens STRING | [TIA V21 STRING](https://docs.tia.siemens.cloud/r/en-us/v21/data-types/character-strings/character-strings/string), 11/2025 | 최대/현재 길이 2바이트, 최대 254문자, 메모리 크기와 문자 표현을 대조 |
| Siemens 장치 설정 | [S7-1200 V20 GET/PUT](https://docs.tia.siemens.cloud/r/simatic_s7_1200_manual_collection_enus_20/communication/s7-communication/get-and-put-read-and-write-from-a-remote-cpu) | 절대 주소, 표준 DB, 접근 허용 조건. V21 자료와 구분하여 설정 참고로 사용 |
| ISO-on-TCP | [IETF RFC 1006 §6](https://www.rfc-editor.org/rfc/rfc1006.html#section-6), May 1987 | TPKT version 3, 예약값, 전체 패킷 길이의 바이트 순서. Siemens 고유 S7 본문 명세의 대체 자료가 아님 |

자료의 발행 연도가 오래됐다는 이유로 임의의 최신 규격으로 바꾸지 않는다. 확인일 현재 제공된 판본과 특정 기종이 지원하는 판본은 별개다. PDF를 재배포하지 않고 원문 링크와 검증한 절을 기록한다.

## Mitsubishi MC 3E

수정한 위치: `McBinaryCommandBuilder`, `McAsciiCommandBuilder`, `McBinaryResponseParser`, `McAsciiResponseParser`.

| 문제 | 교정 | 회귀 검사 |
| --- | --- | --- |
| 저수준 응답 파서가 서브헤더·선언 길이를 무시 | 3E 응답 서브헤더와 실제 길이를 검사 | `McStandardsBinaryResponseRequiresHeaderAndExactLengthAsync`, `McStandardsAsciiResponseRequiresHeaderAndExactLengthAsync` |
| ASCII 종료 코드가 잘못되면 `FormatException` 노출 | 잘못된 16진수 필드를 실패 결과로 반환 | `McStandardsAsciiMalformedFieldsReturnFailureAsync` |
| 홀수 바이트 워드 쓰기가 불완전한 워드를 직렬화 | 2바이트 단위를 검사 | `McStandardsWordWriteRejectsHalfWordAsync` |
| 큰 요청 길이가 wire 길이 필드에서 잘림 | 타이머를 포함한 16비트 길이의 초과 입력 거부 | `McStandardsFrameLengthCannotWrapAsync` |
| 기본 일괄 명령이 0점·과다 점수를 직렬화 | 워드 1~960, binary bit 1~7168, ASCII bit 1~3584를 검사 | `McStandardsBatchRequestsRespectPointLimitsAsync` |

`McStandardsReadMatchesPublishedExampleAsync`는 §5.2의 TN100~TN102 요청을 고정 벡터와 비교한다. 간편 MC 클라이언트에 이미 있던 경로/응답 길이/요청 수량/비트값 검사는 기존 MC 루프백 테스트로 유지한다.

호환성: 공개 시그니처는 유지된다. 저수준 기본 일괄 빌더의 null·빈 데이터·홀수 워드·범위 초과 입력은 이제 인수 예외가 된다. 간편 API의 정상 읽기·쓰기와 자료형 변환은 유지된다. 기종별 실제 점수 제한은 위 공통 최대보다 작을 수 있다.

남은 범위: 랜덤·확장·라벨 명령, 모든 디바이스 주소 상한, 1E/4E 및 Serial 파생 형식까지 전수 대조하지 않았다. KEYENCE MC도 공유하는 3E 외피·일괄 명령은 이 검사의 영향을 받지만, KEYENCE 전 모델의 네이티브 주소 매핑을 Mitsubishi 문서로 인증할 수는 없다.

## Modbus TCP

수정한 위치: `ModbusFrameRules`, `ModbusResponseParser`, `ModbusTcpExchange`, `ModbusTcpDriver`.

- PDU 최대 253바이트에서 Unit ID를 포함한 MBAP Length 허용 범위를 2~254로 제한한다. TCP ADU의 최대 크기는 260바이트다.
- 간편 PLC 및 로봇용 Modbus 교환이 같은 헤더 경계를 사용한다. 프로토콜 ID·길이가 잘못되면 6바이트 헤더를 읽은 시점에 `InvalidFrame`으로 실패한다. 잘못된 선언 길이만큼 본문을 기다려 시간 초과하는 이전 동작을 교정했다.
- FC01/02 응답에서 요청 비트 이후 마지막 바이트의 상위 패딩을 검사한다. 장치 예외 코드 및 정상 쓰기 echo 검사는 유지된다.

검사: `ModbusStandardsTcpBuilderEnforcesAduLimitAsync`, `ModbusStandardsRejectsInvalidMbapBeforeWaitingForBodyAsync`, `ModbusStandardsReadBitsRejectsNonzeroPaddingAsync`. 비트 정상 벡터는 공식 §6.1/6.2 예를 사용한다. 기존 `ModbusMbapLengthFieldIsCheckedExhaustivelyAsync`는 공개 프레임 규칙의 길이 전체 범위를 확인한다.

레지스터 내 16비트 순서와 여러 레지스터에 걸친 Int32/Float 워드 순서를 구분한다. 후자는 장치별 설정이다. RTU의 물리 문자 간격과 Serial 검증 한계는 [통신 기반 감사](transport-audit.md)에 기록했다.

## Siemens S7

이번에 확인한 범위에서는 새 제품 수정이 필요하지 않았다. `S7SessionCodec`의 PDU 협상, `S7ResponseValidation`의 TPKT 길이, STRING 최대/현재 길이 검사를 기존 코드 및 회귀 검사와 대조했다. 협상 PDU에서 28바이트를 뺀 보수적 분할 한계는 확인한 Siemens 단일 범위 읽기·쓰기 한도를 넘지 않는다.

- `S7OpenNegotiatesAndTypedReadWriteSharePduSequenceAsync`: 연결·협상·읽기·쓰기와 요청 번호.
- `S7NegotiatedPduSplitsArrayAndKeepsDeviceErrorAsync`, `S7NegotiatedPayloadNeverExceedsPduAsync`: 분할 및 장치 오류 유지.
- `S7StandardStringUsesDeclaredLengthAsync`, `S7StandardStringRejectsInvalidHeadersAsync`, `S7StringWritesRespectEncodedCapacityAsync`: 문자열 헤더·용량.
- `S7DeclaredLengthsAndTrailingDataAreCheckedAsync`, `S7TruncatedTcpResponsesNeverSucceedAsync`: 길이·잘림 거부.

**확인 한계:** 공개 Siemens 문서에서 전체 S7 wire 명세를 확보하지 못했다. 기존 골든 벡터와 회귀 테스트 통과를 Siemens 공식 바이트 명세에 대한 전수 검증으로 해석하지 않는다. COTP/S7 세션, 타이머·카운터, PPI/MPI, S7 Plus·Web API의 모든 변형 및 실제 CPU별 보안 설정은 별도 확인이 필요하다.
