# PLC 공식 문서 대조: FINS, XGT, CIP 및 기타 제조사

확인일: **2026-09-29**. 아래 버전은 이 날짜에 접근한 제조사 공개본이다. 제조사가 최신판임을 보장하지 않는 자료는 전 제품군의 최신 규격이라고 부르지 않는다. 문서 대조와 합성 응답·로컬 통신 테스트는 실기기 호환성 인증을 대신하지 않는다.

검토 대상은 `src/FieldLink/PlcDrivers` 아래의 코드다. 외부 통신 라이브러리 소스를 규격의 근거로 사용하지 않았다. 제조사 PDF 원문을 저장소에 재배포하는 대신 출처, 판본, 절, 검토 범위를 남긴다. 기존에 삭제된 문서는 복원하지 않았다.

## 대조 결과와 범위

| 프로토콜 | 이번에 실제로 대조한 경로 | 결과 | 범위 밖 |
| --- | --- | --- | --- |
| Omron FINS/TCP | `Omron/Clients/FinsTcpSession.cs`, `FinsTcpDriver.cs`, `OmronFinsNetCommandBuilder.cs`, `OmronFinsNetResponseParser.cs` | 간편 드라이버의 노드 협상, 프레임 길이, SID·명령·노드 대응, end code 및 읽기/쓰기 길이를 문서와 대조 | 모든 FINS 명령, Host Link, 연결형 CIP, 공개 저수준 파서를 단독 호출하는 모든 경로 |
| LS XGT FastEnet | `LSIS/LSFastEnetCommandBuilder.cs`, `LSFastEnetResponseParser.cs`, `Clients/LsFastEnetDriver.cs` | 응답 헤더·블록·ACK 검증, 다중 블록 반환, 16비트 오류 보존, CPU 표시 오류 수정 | Cnet/Computer Link, 전체 CPU 기종·메모리 주소표, 이중화 전환, 저수준 빌더의 모든 입력 한계 |
| Allen-Bradley EtherNet/IP CIP | `AllenBradley/Clients/AllenBradleyCipDriver.cs`, `.Wire.cs`, `.Transfer.cs` | RegisterSession, encapsulation/CPF, sender context·session, CIP 상태·타입, 0x52/0x53 분할 전송 경로 검토 | 유료 ODVA 규격 전문, DF1/SLC, Forward Open·연결형 CIP 전체, UDT/전체 타입, 모든 공개 저수준 파서 |
| Beckhoff AMS/TCP ADS | `Beckhoff/AdsResponseParser.cs`, `BeckhoffAdsNetResponseParser.cs` | 짧은 응답의 성공 처리 제거. 두 헤더 길이와 응답 플래그, 결과·명령별 본문 길이, 요청 invoke ID·명령·주소 쌍 검증 추가 | Secure ADS, 라우터 등록, 비동기 notification, 기기별 index group/offset 전체 |
| Yokogawa FA-M3 binary | `Yokogawa/YokogawaLinkTcpResponseParser.cs` | 최소 4바이트 헤더, 응답 subheader, big-endian 본문 길이 검증 추가 | 확장 ID 서비스, ASCII, 전체 명령·주소표, 요청 명령과 응답의 상관관계 |
| YASKAWA MEMOBUS | `YASKAWA/MemobusResponseParser.cs`, `MemobusCommandBuilder.cs`, `MemobusFrameRules.cs` | 218 헤더/본문 길이, 응답 종류, ID·채널·CPU·SFC 대응 및 오류 응답 길이 검증 추가 | 모든 SFC별 데이터 형식·개수, MP 제품군 전체, Serial MEMOBUS/Modbus와의 호환성 |

### Omron

- [W421-E1-04: Ethernet Units — Construction of Applications](https://assets.omron.eu/downloads/latest/manual/en/w421_cj1w-etn21_cs1w-etn21_ethernet_units_-_construction_of_applications_operation_manual_en.pdf), **2009-04**, §§7-2, 7-4, 7-4-2. 공식 `latest` 주소에서 제공된 실제 판본을 기록했다.
- [W342-E1-18: Communications Commands Reference](https://assets.omron.eu/downloads/latest/manual/en/w342_cs_cj_cp_nsj_communications_commands_reference_manual_en.pdf?v=9), **2023-07**, §5-1-3 및 §§5-3-1/5-3-2.

`FinsTcpSession`은 `FINS` signature와 선언 길이를 검사하고 노드 협상 응답의 command/size/node를 확인한다. 요청 SID와 역방향 노드 주소, MRC/SRC를 대조하며 command 6 keep-alive와 command 3 연결 오류를 구별한다. `FinsTcpDriver`는 예상 데이터 길이와 비트 값 범위를 확인한다. end code 해석에서 CPU 상태 비트와 실제 오류 부분을 구분하는 처리도 대조했다.

이 결과는 **간편 TCP 드라이버 경로**에 대한 검토다. `OmronFinsNetResponseParser`를 단독 호출하면 세션 계층의 signature·길이·SID 검증이 자동 적용되지 않는다. 저수준 함수 전체의 독립 입력 검증은 이번에 완료하지 않았다.

### LS ELECTRIC

- [XGL-EFMTB/T8 User's Manual V3.5](https://ssqbloblocal.blob.core.windows.net/ssqblob/largefile/document/17064909300010/XGL-EFMTB_T8_Manual_V3.5_202401_EN.pdf), **2024-01**, §7.1.1 pp.7-1–7-4, §7.1.2 pp.7-5–7-10. LS 제조사 명의 문서의 공개 첨부 파일이며, 문서 내부 개정 이력으로 판본을 확인했다.
- [XGT FEnet V2.30](https://www.ls-electric.com/upload/customer/download/f8a96bfb-6212-4a0f-8d07-a1ba8868240f/User%27s%20Manual_XGT%20FEnet_V2.30.pdf), §§8.1.2, 8.2.1–8.2.4. 이전 장치의 1바이트 NAK 표현을 확인하는 호환성 자료로만 사용했다.
- [LS ELECTRIC 제품 문서 탐색 경로](https://sol.ls-electric.com/ww/en/product/category/491). 동적 다운로드 목록만으로 전 지역·기종에 대해 V3.5보다 새 판본이 없는지 확정하지 않았다.

20바이트 헤더의 회사 ID, 응답 source `0x11`, application 길이, BCC를 확인한다. BCC `0`은 reserved 사용과 호환되며, 0이 아니면 헤더 byte sum을 검사한다. invoke ID의 요청 대응은 `LsFastEnetDriver`가 담당한다.

기존 저수준 파서는 첫 블록만 반환하고 불완전한 write ACK를 성공 처리했다. 이제 블록 수·각 길이·마지막 바이트까지 확인하고 모든 읽기 블록을 순서대로 합친다. continuous 응답은 한 블록만 허용한다. V3.5의 2바이트 오류 코드를 보존하면서 이전 1바이트 NAK도 처리한다. CPU 정보의 6비트 필드를 사용하고 값 `5`의 명칭을 `XGK/I-CPUU`로 수정했다. 전체 CPU 명칭 표를 새로 구현한 것은 아니다.

### Allen-Bradley / ODVA

- [Rockwell Logix 5000 Controllers Data Access, 1756-PM020I-EN-P](https://literature.rockwellautomation.com/idc/groups/literature/documents/pm/1756-pm020_-en-p.pdf), **2025-09**, Chapter 1의 Data Types, Request/Response, Read Tag, Read Tag Fragmented, Write Tag, Write Tag Fragmented 절.
- [ODVA specifications](https://www.odva.org/subscriptions-services/specifications/): 조회 시 **2026-04** 판 목록은 Volume 1 **3.40**, Volume 2 **1.36**. 전문은 구독 자료여서 이번 검토에서 읽지 못했다.
- [ODVA EtherNet/IP Developer's Guide, PUB00213R0](https://www.odva.org/wp-content/uploads/2020/05/PUB00213R0_EtherNetIP_Developers_Guide.pdf), **2008**, 공개 개요 자료. 최신 규범 문서의 대체 근거로 사용하지 않았다.

간편 드라이버는 session handle, sender context, encapsulation length, CPF item 길이, CIP reply service·status·additional status·type을 검사한다. 분할 읽기/쓰기는 service `0x52`/`0x53`, 바이트 단위 offset, 읽기의 partial transfer 상태 `6`과 진행량을 다룬다. Rockwell 문서의 태그 서비스 형식과 이 경로를 대조했다.

encapsulation/CPF의 구조 검토와 기존 테스트 확인은 수행했지만, **2026년 ODVA 규격 전문 대조 및 적합성 시험을 완료했다는 의미는 아니다**. 공개 저수준 `AllenBradleyResponseParser`에 간편 드라이버와 같은 상관관계 검사가 모두 들어 있는 것도 아니다. 문자열 DATA/LEN을 나누어 쓰는 작업은 하나의 원자적 쓰기로 보장되지 않는다.

### Beckhoff

판 번호와 발행일이 없는 **현재 공개 Infosys HTML**을 사용했다. 페이지 footer의 연도를 개정일로 간주하지 않았다.

- [AMS/TCP packet](https://infosys.beckhoff.com/content/1033/tcadscommon/12440280843.html), [AMS/TCP header](https://infosys.beckhoff.com/content/1033/tcadscommon/12440282379.html), [AMS header](https://infosys.beckhoff.com/content/1033/tcadscommon/12440283915.html).
- [ADS command 목록](https://infosys.beckhoff.com/content/1033/tcadscommon/12440286859.html), [Read Device Info](https://infosys.beckhoff.com/content/1033/tcadscommon/12440288395.html), [Read](https://infosys.beckhoff.com/content/1033/tcadscommon/12440289931.html), [Write](https://infosys.beckhoff.com/content/1033/tcadscommon/12440291467.html), [ReadWrite](https://infosys.beckhoff.com/content/1033/tcadscommon/12440300683.html).

6바이트 TCP 헤더와 32바이트 AMS 헤더, response flag `0x0005`, invoke ID와 역방향 endpoint, 명령별 result/data length를 대조했다. 기존에는 38바이트 미만 입력을 성공으로 반환하고 요청 인자를 쓰지 않았다. 수정 후 잘림·불일치·지원하지 않는 응답 명령을 실패로 처리한다. `send`를 비워 사용하는 기존 API는 유지되지만 이 경우 요청과의 상관관계는 확인할 수 없다.

### Yokogawa

- [F3LE12-1T Ethernet Interface Module, IM34M06H24-08E](https://web-material3.yokogawa.com/IM34M06H24-08E.pdf), **1st Edition, 2015-03**, §5.3.2 binary format, §5.3.3 extended service 구분, §5.4 error codes.

기본 binary 응답의 4바이트 헤더와 실제 본문 길이를 맞추었다. 이전 파서는 2~3바이트만 있어도 성공할 수 있었다. 확장 서비스의 추가 ID를 기본 프레임에 임의로 섞지 않았다. 명령 상세 문서 **IM34M06P41-01E** 및 다른 모듈/최신 전체 제품군 대조는 미완료다.

### YASKAWA MEMOBUS

- [MP2000 Communication Module User's Manual, SIEP C88070004Q](https://www.yaskawa.com/delegate/getAttachment?cmd=documents&documentId=SIEPC88070004&documentName=siepc88070004q_21_3.pdf), **Rev.21 / Web Rev.3, 2025-07**. Appendix B.1 pp.A-14–A-15, B.2 pp.A-16–A-23, E.1의 PC 통신 예제. 날짜는 PDF 끝 개정 이력으로 확인했다.

218 헤더의 response `0x19`, ID, source/destination channel, 전체 길이와 MEMOBUS 본문의 길이·MFC·SFC·CPU 정보를 대조했다. 잘린 프레임이나 다른 요청의 응답을 성공 반환하지 않도록 수정했다. 여기서 다룬 218-header MEMOBUS는 일반 Modbus/TCP MBAP 프레임과 구별한다.

## 회귀 검사 근거

새 검사는 제품 빌더가 만든 응답을 다시 제품 파서에 넣는 방식이 아니라 문서 필드로 구성한 고정 응답과 변조 응답을 사용한다.

| 테스트 파일 | 재현한 결함 | 수정 전 / 후 |
| --- | --- | --- |
| `tests/FieldLink.Communication.Tests/ProtocolResponseTests.SecondaryStandards.cs` | LS envelope, write ACK, 다중 블록, CPU명, 독립 16비트 오류 검사 | 5개 RED 확인 후 GREEN. `0x0190`이 `0x90`으로 잘리던 결과도 독립 재현 |
| `tests/FieldLink.Communication.Tests/ProtocolResponseTests.AdditionalPlcStandards.cs` | ADS 잘림/길이와 요청 식별자 2개, Yokogawa 헤더 1개, MEMOBUS 길이/식별자 1개 | 4개 RED 확인 후 GREEN |

기존 `ProtocolResponseTests.Plc.cs`의 LS fixture는 source/length가 0인 합성 헤더를 사용했고, read 본문을 남긴 채 명령 바이트만 write로 바꾸었다. 명세의 정상 read/ACK/NAK 프레임으로 각각 교정했다. MEMOBUS fixture도 요청용 `0x11`을 응답에 쓰고 길이/MFC를 비워 두어, 공식 응답 헤더 및 오류 응답 형식으로 교정했다. 기존의 잘못된 bytes를 새 정상 규격으로 간주하지 않았다.

위 9개는 통합 Standards 검사에서 통과했다. 전체 솔루션의 최종 실행 결과·환경과 제외 사항은 상위 감사 보고서를 따른다. 실기기나 실제 Serial 포트로 시행한 검사는 없다.

## 기타 제조사: 자료 목록과 미완료 범위

아래 표는 문서 탐색 및 대표 코드 경로 검토 목록이다. **공식 wire-format 대조 완료 목록이 아니다.** 제조사 이름 하나가 같은 회사의 모든 CPU·firmware·통신 모듈을 뜻하지 않는다.

| 제조사 / 코드 | 공식 자료·확인 판본 | 접근 상태와 남은 확인 |
| --- | --- | --- |
| GE/Emerson / `GE/GeResponseParser.cs`, `GeSrtpFrameRules.cs` | [Ethernet Communications User Manual, GFK-2224AG, 2025-01](https://emerson-mas.my.site.com/communities/sfc/servlet.shepherd/document/download/069QA00000UvFWeYAN) | 공개 자료 식별. SRTP 사용 설명과 binary wire packet 규격은 구별해야 한다. 전체 응답 길이·sequence 검증의 공식 근거 대조 미완료 |
| Delta / `Delta/DeltaDvpAddressParser.cs`, `DeltaAsAddressParser.cs` | [DVP ES2/EX2/EC5/SS2/SA2/SX2/SE/SE2/TP Programming Manual, 파일명 2024-10-31](https://filecenter.deltaww.com/Products/download/06/060302/Manual/DELTA_IA-PLC_DVP-ES2-EX2-EC5-SS2-SA2-SX2-SE-SE2-TP_PM_EN_20241031.pdf) | 모델별 공식 공개 PDF 확인. 모든 DVP/AS의 주소 변환·범위·진법을 대조하지 않음. 공통 Modbus 검사만으로 제조사 주소표가 검증되지는 않음 |
| Fuji / `Fuji/FujiSPHNetResponseParser.cs`, `FujiSPBResponseParser.cs`, `FujiCommandSettingTypeResponseParser.cs` | [공식 Computer Communication 매뉴얼 목록: FEH259 등](https://www.fujielectric.com/products/drives_inverters/plc/product_series/sph_product_information_computercom_manual.html), [loader-port FAQ](https://faq.fujielectric.com/faq/show/1379?site_domain=english) | 목록 확인, 일부 문서는 회원 접근. 해당 SPH/SPB/loader별 packet 본문 및 최신 revision 미확인. FAQ를 packet 규격으로 대신하지 않음 |
| FATEK / `FATEK/FatekProgramResponseParser.cs`, `FatekProgramReadResponseParser.cs` | [공식 FBs/B1 매뉴얼 진입 목록](https://www.fatek.com/en/download.php?act=list&cid=59) | FBs ASCII 통신 부록의 직접 본문·판본 확보 미완료. M-series binary 문서를 이 구현의 근거로 사용하지 않음. 대표 저수준 파서에서 SUM/ETX/station/echo와 bit 문자 범위 검증을 추가 대조해야 함 |
| Panasonic / `Panasonic/PanasonicResponseParser.cs`, `Clients/PanasonicMewtocolSerialClient.cs` | [MEWTOCOL Communication Protocol, WUME-MEWCP-03, 2024-04](https://industry.panasonic.eu/storage/custom-upload/Factory%20%26%20Automation/PLC/Manuals/mn_all_plcs_mewtocol_user_pid_en.pdf) | 공개 본문의 판본·COM framing/BCC를 확인. parser/client 전체 경로와 연속 프레임/분할 응답의 대조 미완료. 저수준 `ExtraActualData`/`ExtraActualBool`은 자체 BCC·station·echo 검사 없이 내용을 추출함 |
| Inovance / `Inovance/InovanceAddressParser.cs` | [H3U User Guide, 19010494-SC/A05, 파일명 2020-11-24](https://portal-file.inovance.com/owfile/ProdDoc/SC/19010494-SC/A05/19010494-SC_A05%E3%80%8AH3U%20PLC%20User%20Guide%E3%80%8B-EN-20201124.pdf?response-content-disposition=attachment), [H3U 제품 페이지](https://www.inovance.eu/south-korea/products/plcs-hmis/h3u-plc) | 공개 자료 확인. AM/H3U/H5U 등의 모든 Modbus 주소표와 현행 firmware 차이 대조 미완료 |
| MegMeet / `MegMeet/MegMeetAddressParser.cs` | [MC280/MC200E 공식 자료 목록](https://megmeet.com/products/info.html?id=67): MODBUS 주소표 목록 날짜 **2023-08-01** | 목록 확인. 주소표 파일 본문·revision 및 계열별 주소 검증 미완료 |
| Toyota/JTEKT / `Toyota/ToyoPucResponseParser.cs`, `ToyoPucFrameRules.cs` | [공식 매뉴얼 검색](https://toyoda.jtekt.co.jp/e/support/OfcTpTorisetuList.php?gengo=1&q=&searchBtn=%E6%A4%9C%E7%B4%A2&series_id=), [T-756 Rev.11 2PORT-EFR PDF 경로](https://toyoda.jtekt.co.jp/data/Tp/Torisetu/t-756-11-n_2PORT%2BEFR.pdf) | PDF 요청이 제조사 인증 화면으로 이동. 본문을 읽지 못했으므로 현재 구현의 성공 응답/길이 규칙을 공식 확인했다고 표시하지 않음 |
| Vigor / `Vigor/VigorVsResponseParser.cs`, `Clients/VigorSerialClient.cs` | [공식 protocol 목록](https://www.vigorplc.com/en/download-c5043/Protocol.html), [VS Protocol §7-4, pp.408–415](https://www.vigorplc.com/v_comm/inc/download_file.asp?fid=37575&re_id=2853) | 목록 날짜 **2019-08-30**, PDF 내부 revision 미표시. 응답 DLE/ACK 및 SUM 관련 본문 확인. low-level/parser/Serial 프레임 전체 조합 대조와 정정은 미완료; 현재 합성 fixture의 DLE/STX 및 SUM값을 공식 응답 표본으로 사용하면 안 됨 |
| XINJE / `XINJE/XinjeAddressParser.cs` | [공식 XD 문서 목록](https://en.xinje.com/web/search/searchData?val=XD): XD/XL/XG Software Manual **V3.8.0, 2026-07-20** | 목록에서 현행 문서 식별. 다운로드 본문 확인 실패. XD/XL private TCP와 XC/Modbus 주소표를 혼동하지 않도록 모델별 대조 필요 |
| Azbil/Yamatake / `Yamatake/DigitronCPLResponseParser.cs` | [CPL SDC40A/40G, CP-UM-1583E 목록](https://aa-industrial.azbil.com/en/download/document/manual/CP-UM-1583E), [C45/C46 CP-SP-1218E-12](https://www.azbil.com/products/factory/download/manual/CP-SP-1218E-12.pdf), Chapter 9 | 관련 공개 자료 식별. SDC/DCP 모델 차이, checksum, station·command 대응 및 데이터 범위의 전체 대조 미완료 |

나머지 저수준/Serial 구현은 기존 회귀 테스트 통과와 제조사 규격 적합성을 구분해야 한다. 우선 후속 범위는 FATEK/MEWTOCOL/Vigor/CPL의 checksum·종결자·station·echo 검증, 모델별 주소표, 그리고 FINS/CIP 저수준 단독 호출 경계다. Toyota/Fuji/SRTP는 정확한 모델의 공식 packet 본문부터 확보해야 한다.

## 내려받아 대조한 파일의 식별값

실제 파일은 감사 중 임시 `obj/standards-audit`에만 두었다. 아래 hash는 출처 URL이 나중에 같은 이름으로 갱신될 때 이번 검토본을 구별하기 위한 값이다.

| 문서 | SHA-256 |
| --- | --- |
| LS XGL-EFMTB/T8 V3.5 | `AEAF9A88E840BD5DB02E3060093DC087FBA567ADF315A70025F3BBC59E9995E1` |
| Yokogawa IM34M06H24-08E | `E57938D3CBFE40916F0B2C02162257041E3CE3AF677902CFB2C20A30C27F6BE6` |
