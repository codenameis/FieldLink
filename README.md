# FieldLink

> 고성능·무의존 산업용 PLC 통신 라이브러리 (.NET Standard 2.0 / .NET 8)  
> **MIT License** | **Cleanroom Architecture**

FieldLink는 제조사별 공식 통신 사양서와 검증된 패킷 구조를 기반으로 완전한 **클린룸(Cleanroom)** 방식으로 설계된 오픈소스 산업용 통신 라이브러리입니다. 외부 상용 라이브러리나 카피레프트(GPL/LGPL) 코드에 대한 의존성 없이, 단일화된 고수준 API(`PlcClient`, `PlcClientFactory`)를 통해 다양한 산업용 PLC와 즉시 통신할 수 있습니다.

---

## 1. 지원 제조사 및 프로토콜 일람

| 제조사 | 대상 PLC 제품군 | 지원 프로토콜 및 전송 | 팩토리 메서드 (`PlcClientFactory`) |
|---|---|---|---|
| **Mitsubishi** | MELSEC-Q / L / iQ-R / iQ-F / FX | MC Protocol (3E 프레임 Binary / ASCII, TCP/UDP) | `CreateMitsubishiMCBinary`, `CreateMitsubishiMCAscii` |
| **Siemens** | S7-200 / 300 / 400 / 1200 / 1500 | S7Comm (ISO-on-TCP 포트 102) | `CreateSiemensS7Comm` |
| **LS Electric** | XGT / XGB / XGK / XGI | FEnet (이더넷 전용 프로토콜 포트 2004) | `CreateLsElectricFEnet` |
| **Modbus** | 범용 표준 장치 | Modbus TCP (502) / RTU (Serial) / ASCII | `CreateModbusTcp`, `CreateModbusRtu`, `CreateModbusAscii` |
| **Delta** | DVP 시리즈 / AS300 시리즈 | Modbus RTU (Serial) / Modbus TCP (502) | `CreateDeltaDvpRtu`, `CreateDeltaDvpTcp`, `CreateDeltaAsTcp` |
| **FATEK** | FBs 시리즈 | FATEK 통신 프로토콜 (ASCII STX/ETX/LRC, Serial/TCP) | `CreateFatekSerial`, `CreateFatek` |
| **Fuji Electric** | MICREX-SX / SPB 시리즈 | SPB Loader (ASCII) / SPH SX-Bus Ethernet (포트 18245) | `CreateFujiElectricSpbSerial`, `CreateFujiElectricSphTcp` |
| **GE Fanuc** | PACSystems RX3i/7i, 90-30 | GE SRTP (바이너리 56바이트 헤더, 포트 18245) | `CreateGESrtpTcp` |
| **Inovance** | AM400/600 (IEC), H3U, H5U | Modbus RTU / Modbus TCP (오프셋 자동 매핑) | `CreateInovanceRtu`, `CreateInovanceTcp` |
| **Keyence** | KV-8000 / 7000 / 5000 / 3000 / Nano | KV Host Link (상위 링크 ASCII, TCP 포트 8501 / Serial) | `CreateKeyenceHostLinkTcp`, `CreateKeyenceHostLinkSerial` |
| **MegMeet** | MC200 / MC280 시리즈 | MC 시리즈 Modbus RTU / TCP (경계 분할 매핑) | `CreateMegmeetRtu`, `CreateMegmeetTcp` |
| **Panasonic** | FP-X / FP0 / FP2 / FP7 | MEWTOCOL-COM (ASCII `#RD`/`#WD`/`#RCS`/`#WCS`, BCC) | `CreatePanasonicMewtocolSerial`, `CreatePanasonicMewtocolTcp` |
| **Toyota / JTEKT** | TOYOPUC (2PORT-EFR) | TOYOPUC Computer Link (기본 및 확장 PRG, 포트 10255) | `CreateJtektToyopucTcp` |
| **VIGOR** | VS 시리즈 (丰炜) | VS Computer Link (DLE 이스케이프, 3바이트 BCD LE) | `CreateVigorSerial` |
| **XINJE** | XC 시리즈 / XD·XL·XG 시리즈 (信捷) | XD 24비트 확장 Modbus(`0x1E`~`0x21`) 및 표준 매핑 | `CreateXinjeRtu`, `CreateXinjeTcp` |
| **Yokogawa** | FA-M3 시리즈 | Higher-level Link Service (바이너리 링크, 포트 5001) | `CreateYokogawaTcp` |

---

## 2. 공통 사용 패턴

모든 제조사 PLC는 `PlcClient` 파사드를 통해 동일한 방식으로 제어됩니다.

```csharp
using System;
using System.Threading.Tasks;
using FieldLink.Plc;

// 1. 팩토리를 통한 클라이언트 생성
using var client = PlcClientFactory.CreateMitsubishiMCBinary("192.168.0.10");

// 2. 연결 수립
var openResult = await client.OpenAsync();
if (!openResult.IsSuccess)
{
    Console.WriteLine($"연결 실패: {openResult.Error?.Message}");
    return;
}

// 3. 비트 / 워드 / 기본 자료형 단위 읽기
var d100 = await client.ReadInt32Async("D100");
if (d100.IsSuccess)
{
    Console.WriteLine($"D100 값: {d100.Value}");
}

var m10 = await client.ReadBooleanAsync("M10");
if (m10.IsSuccess)
{
    Console.WriteLine($"M10 접점: {m10.Value}");
}

// 4. 쓰기 작업
await client.WriteInt32Async("D100", 123456);
await client.WriteBooleanAsync("M10", true);

// 5. 연속 블록 읽기
var block = await client.ReadInt16Async("D200", count: 10);
if (block.IsSuccess)
{
    Console.WriteLine($"D200~D209: {string.Join(", ", block.Value)}");
}

// 6. 연결 해제
await client.CloseAsync();
```

---

## 3. 제조사별 클라이언트 상세 사용법

### 3.1 Mitsubishi Electric (MELSEC MC 프로토콜)
MELSEC iQ-R, Q, L, FX 시리즈의 3E 프레임 통신을 지원합니다.
```csharp
// TCP 이더넷 클라이언트 (기본 포트 5007)
using var client = PlcClientFactory.CreateMitsubishiMCBinary("192.168.1.10");
await client.OpenAsync();

// 비트 읽기/쓰기 (M, X, Y, L, F, V, B)
await client.WriteBooleanAsync("M100", true);
var x0 = await client.ReadBooleanAsync("X0"); // 16진수 표기

// 워드 읽기/쓰기 (D, W, R, ZR)
await client.WriteInt16Async("D100", (short)100);
var zr1000 = await client.ReadInt32Async("ZR1000");
```

### 3.2 Siemens (S7Comm)
S7-200, 300, 400, 1200, 1500 기종의 데이터 블록(DB) 및 입출력 메모리에 접근합니다.
```csharp
// ISO-on-TCP 포트 102 연결 (S7-1200/1500: Rack=0, Slot=1 / S7-300: Rack=0, Slot=2)
using var client = PlcClientFactory.CreateSiemensS7Comm("192.168.0.1");
await client.OpenAsync();

// DB 블록 접근: DB{번호}.DB{타입}{오프셋}
var dbValue = await client.ReadInt16Async("DB1.DBW0");
var dbDint = await client.ReadInt32Async("DB1.DBD2");
var dbBit = await client.ReadBooleanAsync("DB1.DBX6.0");

// I/O 및 플래그 접근 (I, Q, M)
var m0 = await client.ReadBooleanAsync("M0.0");
var mw10 = await client.ReadInt16Async("MW10");
```

### 3.3 LS Electric (XGT FEnet)
LS ELECTRIC XGT / XGB 전용 이더넷 FEnet 통신을 지원합니다.
```csharp
// 기본 포트 2004 연결
using var client = PlcClientFactory.CreateLsElectricFEnet("192.168.1.5", port: 2004);
await client.OpenAsync();

// 비트 디바이스 (%MX, %PX, %QX)
await client.WriteBooleanAsync("%MX100", true);
var px0 = await client.ReadBooleanAsync("%PX0");

// 워드 디바이스 (%MW, %DW, %PW, %QW)
await client.WriteInt16Async("%DW100", (short)250);
var mw200 = await client.ReadInt32Async("%MD200");
```

### 3.4 Modbus (TCP / RTU / ASCII)
표준 Modbus 디바이스와 범용으로 통신합니다.
```csharp
// Modbus TCP (기본 포트 502, UnitId=1)
using var tcpClient = PlcClientFactory.CreateModbusTcp("192.168.1.20", port: 502, unitId: 1);

// Modbus RTU 시리얼
using var rtuClient = PlcClientFactory.CreateModbusRtu("COM1", baudRate: 19200, unitId: 1);

await tcpClient.OpenAsync();
var holdingVal = await tcpClient.ReadInt16Async("40001");
var coilVal = await tcpClient.ReadBooleanAsync("00001");
```

### 3.5 Delta Electronics (DVP & AS 시리즈)
Delta DVP 계열(시리얼/이더넷) 및 AS300 계열의 Modbus 메모리 맵을 자동으로 해석합니다.
```csharp
// Delta DVP Modbus TCP 클라이언트
using var client = PlcClientFactory.CreateDeltaDvpTcp("192.168.1.30", station: 1);
await client.OpenAsync();

// 8진수 X, Y 및 10진수 M, S, T, C, D
var d100 = await client.ReadInt16Async("D100");
var y0 = await client.ReadBooleanAsync("Y0"); // 8진수 주소
var c200 = await client.ReadInt32Async("C200"); // 32비트 고속 카운터 자동 처리
```

### 3.6 FATEK (FBs 시리즈)
FATEK FBs 전용 ASCII 통신 프로토콜을 사용합니다.
```csharp
// FATEK 시리얼 포트 클라이언트 (기본 9600 7E1, 국번 1)
using var client = PlcClientFactory.CreateFatekSerial("COM2", baudRate: 9600, station: 1);
await client.OpenAsync();

// 이산 릴레이: X, Y (4자리 8진), M, S, T, C
await client.WriteBooleanAsync("Y0", true);
var m10 = await client.ReadBooleanAsync("M10");

// 레지스터: R (데이터 레지스터 5자리), D (데이터 레지스터 5자리)
var r100 = await client.ReadInt16Async("R100");
var d0 = await client.ReadInt32Async("D0");
```

### 3.7 Fuji Electric (SPB & SPH)
Fuji Electric SPB 계열(시리얼 로더) 및 MICREX-SX SPH 계열(이더넷 SX-Bus)을 지원합니다.
```csharp
// Fuji SPB 시리얼 로더 (기본 19200 8E1, 국번 1)
using var spbClient = PlcClientFactory.CreateFujiElectricSpbSerial("COM3", station: 1);

// Fuji SPH 이더넷 (SX-Bus 헤더, 포트 18245)
using var sphClient = PlcClientFactory.CreateFujiElectricSphTcp("192.168.1.40", port: 18245);
await sphClient.OpenAsync();

// SPB: D, R, W, X, Y, M / SPH: SX-Bus M, I, Q
var d100 = await sphClient.ReadInt16Async("D100");
var m10 = await sphClient.ReadBooleanAsync("M10");
```

### 3.8 GE Fanuc / Emerson (SRTP)
PACSystems RX3i/RX7i 및 Series 90-30의 SRTP 56바이트 바이너리 프로토콜을 지원합니다.
```csharp
// GE SRTP TCP 클라이언트 (기본 포트 18245)
using var client = PlcClientFactory.CreateGESrtpTcp("192.168.1.50");
await client.OpenAsync();

// 워드 레지스터: %R, %AI, %AQ
var r1 = await client.ReadInt16Async("%R1");
await client.WriteInt16Async("%R100", (short)999);

// 비트 릴레이: %I, %Q, %M, %T, %S, %SA, %SB, %SC, %G
var q1 = await client.ReadBooleanAsync("%Q1");
var m1 = await client.ReadBooleanAsync("%M1");
```

### 3.9 Inovance (汇川技术)
Inovance AM400/600 (IEC 61131-3) 및 H3U, H5U 계열을 지원합니다.
```csharp
// H3U / H5U Modbus TCP 클라이언트
using var client = PlcClientFactory.CreateInovanceTcp("192.168.1.60", InovanceSeries.H3u);
await client.OpenAsync();

// 8진수 X, Y (H3U: X=0xF800, Y=0xFC00 매핑 자동 계산)
var x0 = await client.ReadBooleanAsync("X0");
var y10 = await client.ReadBooleanAsync("Y10");

// 레지스터 및 IEC 주소
var d100 = await client.ReadInt16Async("D100");
// AM 시리즈: %MW100, %MD0 등 지원
```

### 3.10 Keyence (KV 상위 링크)
KV-8000/7000/5000/3000/Nano 계열의 KV 상위 링크(Host Link) ASCII 프로토콜을 지원합니다.
```csharp
// KV Host Link TCP 클라이언트 (기본 포트 8501)
using var client = PlcClientFactory.CreateKeyenceHostLinkTcp("192.168.1.70", port: 8501);
await client.OpenAsync();

// 데이터 메모리 (DM, EM, FM, ZF, W)
var dm100 = await client.ReadInt16Async("DM100");
await client.WriteInt32Async("DM200", 50000);

// 내부 릴레이 및 접점 (MR, LR, CR, VB, R, B)
var mr100 = await client.ReadBooleanAsync("MR100");
var r0 = await client.ReadBooleanAsync("R0");
```

### 3.11 MegMeet (麦格米特)
Megmeet MC200/MC280 시리즈의 고유 Modbus 매핑 테이블과 경계 분할 읽기를 지원합니다.
```csharp
using var client = PlcClientFactory.CreateMegmeetTcp("192.168.1.80");
await client.OpenAsync();

// 데이터 레지스터 (D), 특수 레지스터 (SD)
var d100 = await client.ReadInt16Async("D100");
var sd0 = await client.ReadInt16Async("SD0");

// 보조 릴레이 (M, SM, S) 및 8진수 입출력 (X, Y)
var m10 = await client.ReadBooleanAsync("M10");
var y0 = await client.ReadBooleanAsync("Y0");
```

### 3.12 Panasonic (MEWTOCOL-COM)
Panasonic FP 계열의 MEWTOCOL-COM 프로토콜(#RD, #WD, #RCS, #WCS)을 지원합니다.
```csharp
// Panasonic MEWTOCOL TCP/시리얼 클라이언트
using var client = PlcClientFactory.CreatePanasonicMewtocolTcp("192.168.1.90", station: 1);
await client.OpenAsync();

// 데이터 레지스터: DT, LD, FL
var dt100 = await client.ReadInt16Async("DT100");

// 릴레이 접점: R, X, Y, L
var r0 = await client.ReadBooleanAsync("R0");
var x0 = await client.ReadBooleanAsync("X0");
```

### 3.13 Toyota / JTEKT (TOYOPUC)
TOYOPUC 2PORT-EFR 이더넷 컴퓨터 링크 프로토콜(기본 및 확장 PRG)을 지원합니다.
```csharp
// TOYOPUC 2PORT-EFR TCP 클라이언트 (기본 포트 10255)
using var client = PlcClientFactory.CreateJtektToyopucTcp("192.168.1.100", port: 10255);
await client.OpenAsync();

// 기본 영역: 16진수 주소 체계 (D, S, N, R, B, K, V, T, C, L, X, Y, M)
var d100 = await client.ReadInt16Async("D100"); // 16진수 0x0100번지
var m10 = await client.ReadBooleanAsync("M10");

// 확장 영역: EK, EV, ET, EC, EL, EX, EY, EM, ES, EN, EB (확장 번호 자동 처리)
var ek10 = await client.ReadInt16Async("EK10");
```

### 3.14 VIGOR (丰炜 VS 시리즈)
VIGOR VS 계열 Computer Link 바이너리 프로토콜(DLE 0x10 바이트 스터핑 및 3바이트 BCD LE)을 지원합니다.
```csharp
// VIGOR VS 계열 시리얼 클라이언트 (기본 19200 7E1, 국번 1)
using var client = PlcClientFactory.CreateVigorSerial("COM4", station: 1);
await client.OpenAsync();

// 데이터 레지스터 (D, SD, R, T, C)
var d100 = await client.ReadInt16Async("D100");
var c200 = await client.ReadInt32Async("C200"); // C200 이상은 32비트 카운터(0xAD) 자동 적용

// 릴레이 (X, Y, M, S, SM, TC, TS, CC, CS)
var m10 = await client.ReadBooleanAsync("M10");
var sm0 = await client.ReadBooleanAsync("SM0"); // M9000~M9255는 SM0~SM255로 자동 매핑
```

### 3.15 XINJE (信捷 XC / XD / XL 시리즈)
XINJE XD/XL 계열의 24비트 확장 Modbus(`0x1E`~`0x21`) 및 XC 계열의 표준 Modbus 매핑을 지원합니다.
```csharp
// XD/XL 시리즈 Modbus TCP 클라이언트 (포트 502)
using var client = PlcClientFactory.CreateXinjeTcp("192.168.1.110", station: 1);
await client.OpenAsync();

// 24비트 대용량 주소 공간 직접 접근 (D, HD, TD, CD, SD, ID, QD, FD, SFD)
var d100 = await client.ReadInt16Async("D100");
var hd0 = await client.ReadInt16Async("HD0");

// 비트 접점 (X, Y: 8진수, M, HM, S, HS, T, HT, C, HC, SM)
var m10 = await client.ReadBooleanAsync("M10");
var x10000 = await client.ReadBooleanAsync("X10000"); // 확장 모듈 I/O 자동 처리
```

### 3.16 Yokogawa (FA-M3 시리즈)
Yokogawa FA-M3 Higher-level Link Service 바이너리 링크 프로토콜을 지원합니다.
```csharp
// Yokogawa FA-M3 TCP 클라이언트 (기본 포트 5001, CPU 번호 1~4)
using var client = PlcClientFactory.CreateYokogawaTcp("192.168.1.120", port: 5001, cpuNumber: 1);
await client.OpenAsync();

// 데이터 레지스터: D, B, F, R, V, Z, W, TS, TP, TI, CS, CP, CI
var d100 = await client.ReadInt16Async("D100");
var ts1 = await client.ReadInt16Async("TS1");

// 릴레이 접점: X (입력 읽기전용), Y, I, E, M, T, C, L
var y1 = await client.ReadBooleanAsync("Y1");
var i10 = await client.ReadBooleanAsync("I10");
```

---

## 4. 아키텍처 및 품질 보증

```mermaid
graph TD
    App[사용자 응용 프로그램] --> Factory[PlcClientFactory]
    Factory --> Client[PlcClient (Facade)]
    Client --> Protocol[IPlcProtocol (16개 제조사 구현체)]
    Protocol --> Transport[ITransport (TcpTransport / SerialTransport / UdpTransport)]
    Transport --> Physical[물리 PLC / 통신 모듈]
```

- **Cleanroom Implementation**: 레거시/GPL 오염 코드를 일절 참조하지 않고 공식 제조사 사양서만으로 독립 개발
- **No Warnings**: `TreatWarningsAsErrors=true`로 모든 프로젝트 컴파일 경고 0건 유지
- **Strict Code Style**: `EnforceCodeStyleInBuild=true`, `dotnet format --verify-no-changes` 전수 통과
- **High Test Coverage**: 582개 단위 테스트 전수 통과 (100% 녹색불)
- **Zero Alloc Framing**: 프레임 버퍼 관리 및 청크 분할(`RequestChunker`)로 고속 데이터 수집 최적화
