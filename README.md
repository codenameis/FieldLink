# FieldLink

> **High-Performance, Zero-Dependency Industrial PLC Communication Library for .NET (.NET Standard 2.0 / .NET 8+)**  
> **고성능·무의존 산업용 PLC 통신 라이브러리 (.NET Standard 2.0 / .NET 8+)**  
> **MIT License** | **Cleanroom Architecture**

[**English**](#english) | [**한국어**](#한국어)

---

<a id="english"></a>

# English

## 1. Supported Vendors & Protocols

| Vendor | Target PLC Series | Supported Protocol & Transport | Factory Method (`PlcClientFactory`) |
|---|---|---|---|
| **Mitsubishi Electric** | MELSEC-Q / L / iQ-R / iQ-F / FX | MC Protocol (3E Frame Binary / ASCII over TCP/UDP) | `CreateMitsubishiMCBinary`, `CreateMitsubishiMCAscii` |
| **Siemens** | S7-200 / 300 / 400 / 1200 / 1500 | S7Comm (ISO-on-TCP, Port 102) | `CreateSiemensS7Comm` |
| **LS ELECTRIC** | XGT / XGB / XGK / XGI | FEnet (Dedicated Ethernet Protocol, Port 2004) | `CreateLsElectricFEnet` |
| **Modbus** | Standard Modbus Devices | Modbus TCP (Port 502) / RTU (Serial) / ASCII (Serial) | `CreateModbusTcp`, `CreateModbusRtu`, `CreateModbusAscii` |
| **Delta Electronics** | DVP Series / AS300 Series | Modbus RTU (Serial) / Modbus TCP (Port 502) | `CreateDeltaDvpRtu`, `CreateDeltaDvpTcp`, `CreateDeltaAsTcp` |
| **FATEK** | FBs Series | FATEK Program Port ASCII (`STX`/`ETX`/LRC, Serial/TCP) | `CreateFatekSerial`, `CreateFatek` |
| **Fuji Electric** | MICREX-SX SPH / SPB Series | SPB Loader (ASCII) / SPH SX-Bus Ethernet (Port 18245) | `CreateFujiElectricSpbSerial`, `CreateFujiElectricSphTcp` |
| **GE Fanuc / Emerson** | PACSystems RX3i/RX7i, Series 90-30 | GE SRTP (56-byte Binary Header, Port 18245) | `CreateGESrtpTcp`, `CreateGESrtp` |
| **Inovance** | AM400/600 (IEC), H3U, H5U | Modbus RTU / Modbus TCP (Automatic Offset Mapping) | `CreateInovanceRtu`, `CreateInovanceTcp` |
| **Keyence** | KV-8000 / 7000 / 5000 / 3000 / Nano | KV Host Link (Upper Link ASCII, TCP Port 8501 / Serial) | `CreateKeyenceHostLinkTcp`, `CreateKeyenceHostLinkSerial` |
| **Megmeet** | MC200 / MC280 Series | MC Series Modbus RTU / TCP (Boundary-Aware Mapping) | `CreateMegmeetRtu`, `CreateMegmeetTcp` |
| **Panasonic** | FP-X / FP0R / FP2SH / FP7 | MEWTOCOL-COM (ASCII `%` / `<` Frame, `#RD`/`#WD`/`#RCS`/`#WCS`, BCC) | `CreatePanasonicMewtocolSerial`, `CreatePanasonicMewtocolTcp` |
| **Toyota / JTEKT** | TOYOPUC (2PORT-EFR) | TOYOPUC Computer Link (Basic & Extended PRG, Port 10255) | `CreateJtektToyopucTcp`, `CreateJtektToyopuc` |
| **VIGOR** | M / VB Series | Computer Link ASCII (`:01...` LRC Frame, Function `01`~`08`) | `CreateVigorSerial`, `CreateVigor` |
| **XINJE** | XC Series / XD · XL Series | Modbus RTU / Modbus TCP (Series-Specific Device Mapping) | `CreateXinjeRtu`, `CreateXinjeTcp` |
| **Yokogawa** | FA-M3 Series | Higher-Level Link Service (PC Link Binary, Port 5001) | `CreateYokogawaTcp`, `CreateYokogawa` |

---

## 2. Common Usage Pattern

Every PLC vendor shares the same unified `PlcClient` facade and `PlcResult<T>` error-handling model:

```csharp
using System;
using System.Threading.Tasks;
using FieldLink.Plc;

// 1. Create a client instance via PlcClientFactory
using var client = PlcClientFactory.CreateMitsubishiMCBinary("192.168.0.10");

// 2. Open the connection
var openResult = await client.OpenAsync();
if (!openResult.IsSuccess)
{
    Console.WriteLine($"Connection failed: {openResult.Error?.Message}");
    return;
}

// 3. Read scalar values (Bit / Word / DWord / Float / String)
var d100 = await client.ReadInt32Async("D100");
if (d100.IsSuccess)
{
    Console.WriteLine($"D100: {d100.Value}");
}

var m10 = await client.ReadBooleanAsync("M10");
if (m10.IsSuccess)
{
    Console.WriteLine($"M10: {m10.Value}");
}

// 4. Write values
await client.WriteInt32Async("D100", 123456);
await client.WriteBooleanAsync("M10", true);

// 5. Read contiguous arrays (automatic multi-frame chunking when exceeding frame limits)
var block = await client.ReadInt16Async("D200", count: 10);
if (block.IsSuccess)
{
    Console.WriteLine($"D200..D209: {string.Join(", ", block.Value)}");
}

// 6. Close the connection
await client.CloseAsync();
```

---

## 3. Vendor-Specific Usage Examples

### 3.1 Mitsubishi Electric (MELSEC MC Protocol)
Supports 3E frame (Binary and ASCII) communication for MELSEC iQ-R, Q, L, and FX series.
```csharp
using var client = PlcClientFactory.CreateMitsubishiMCBinary("192.168.1.10");
await client.OpenAsync();

// Bit devices (M, X, Y, L, F, V, B)
await client.WriteBooleanAsync("M100", true);
var x1A = await client.ReadBooleanAsync("X1A"); // Hexadecimal bit address

// Word devices (D, W, R, ZR)
await client.WriteInt16Async("D100", (short)100);
var zr1000 = await client.ReadInt32Async("ZR1000");
```

### 3.2 Siemens (S7Comm)
Communicates with S7-200, 300, 400, 1200, and 1500 Data Blocks (DB), Inputs (I), Outputs (Q), and Merkers (M) over ISO-on-TCP (Port 102).
```csharp
using var client = PlcClientFactory.CreateSiemensS7Comm("192.168.0.1");
await client.OpenAsync();

// Data Block addressing: DB{number}.DB{X|B|W|D}{offset}
var dbWord = await client.ReadInt16Async("DB1.DBW0");
var dbDInt = await client.ReadInt32Async("DB1.DBD2");
var dbBit = await client.ReadBooleanAsync("DB1.DBX6.0");

// I/O and Merker addressing (I, Q, M)
var m0 = await client.ReadBooleanAsync("M0.0");
var mw10 = await client.ReadInt16Async("MW10");
```

### 3.3 LS ELECTRIC (XGT FEnet)
Supports dedicated FEnet Ethernet communication (Port 2004) for XGT / XGB series.
```csharp
using var client = PlcClientFactory.CreateLsElectricFEnet("192.168.1.5", port: 2004);
await client.OpenAsync();

// Bit devices (%MX, %PX, %KX, %FX, %TX, %CX)
await client.WriteBooleanAsync("%MX100", true);
var px0 = await client.ReadBooleanAsync("%PX0");

// Word / DWord devices (%MW, %DW, %MD, %DD)
await client.WriteInt16Async("%DW100", (short)250);
var md200 = await client.ReadInt32Async("%MD200");
```

### 3.4 Modbus (TCP / RTU / ASCII)
Standard Modbus communication over Ethernet or Serial ports.
```csharp
// Modbus TCP (default port 502, unitId = 1)
using var tcpClient = PlcClientFactory.CreateModbusTcp("192.168.1.20", port: 502, unitId: 1);

// Modbus RTU Serial
using var rtuClient = PlcClientFactory.CreateModbusRtu("COM1", baudRate: 19200, slaveAddress: 1);

await tcpClient.OpenAsync();
var holdingReg = await tcpClient.ReadInt16Async("40001");
var coil = await tcpClient.ReadBooleanAsync("00001");
```

### 3.5 Delta Electronics (DVP & AS Series)
Automatically resolves Delta DVP and AS300 Modbus device mappings and non-contiguous boundary splits.
```csharp
using var client = PlcClientFactory.CreateDeltaDvpTcp("192.168.1.30", station: 1);
await client.OpenAsync();

// Octal I/O (X, Y) and Decimal devices (M, S, T, C, D)
var d100 = await client.ReadInt16Async("D100");
var y0 = await client.ReadBooleanAsync("Y0");
var c200 = await client.ReadInt32Async("C200"); // 32-bit high-speed counter (C200..C255)
```

### 3.6 FATEK (FBs Series)
Supports FATEK Program Port ASCII protocol (`STX`/`ETX` + LRC).
```csharp
using var client = PlcClientFactory.CreateFatekSerial("COM2", baudRate: 9600, station: 1);
await client.OpenAsync();

// Discrete relays: X, Y, M, S, T, C (4-digit decimal)
await client.WriteBooleanAsync("Y0", true);
var m10 = await client.ReadBooleanAsync("M10");

// 16-bit registers (R, D, RT, RC) and 32-bit registers (DRC)
var r100 = await client.ReadInt16Async("R100");
var drc0 = await client.ReadInt32Async("DRC0"); // 8-character ASCII Hex 32-bit register
```

### 3.7 Fuji Electric (SPB & MICREX-SX SPH)
Supports Fuji SPB Serial Loader protocol and MICREX-SX SPH Ethernet (SX-Bus, Port 18245).
```csharp
// Fuji SPB Serial Loader
using var spbClient = PlcClientFactory.CreateFujiElectricSpbSerial("COM3", station: 1);
await spbClient.OpenAsync();
var spbD100 = await spbClient.ReadInt16Async("D100");

// Fuji MICREX-SX SPH Ethernet (Port 18245)
using var sphClient = PlcClientFactory.CreateFujiElectricSphTcp("192.168.1.40", port: 18245);
await sphClient.OpenAsync();
var mw0 = await sphClient.ReadInt16Async("%MW1.0");
var mx0 = await sphClient.ReadBooleanAsync("%MX1.0.0");
```

### 3.8 GE Fanuc / Emerson (SRTP)
Supports GE SRTP 56-byte binary header protocol (Port 18245) for PACSystems RX3i/RX7i and Series 90-30.
```csharp
using var client = PlcClientFactory.CreateGESrtpTcp("192.168.1.50");
await client.OpenAsync();

// Word registers: %R, %AI, %AQ (1-based addressing)
var r1 = await client.ReadInt16Async("%R1");
await client.WriteInt16Async("%R100", (short)999);

// Bit references: %I, %Q, %M, %T, %S, %SA, %SB, %SC, %G (byte-aligned read/write)
var q1 = await client.ReadBooleanAsync("%Q1");
```

### 3.9 Inovance (AM / H3U / H5U Series)
Supports Inovance AM400/600 (IEC `%MW`, `%MD`, `%MX`, `%IX`, `%QX`) and H3U/H5U (`D`, `R`, `M`, `S`, `T`, `C`, octal `X`/`Y`) over Modbus TCP/RTU.
```csharp
using var client = PlcClientFactory.CreateInovanceTcp("192.168.1.60", series: InovanceSeries.H3u);
await client.OpenAsync();

var x0 = await client.ReadBooleanAsync("X0");
var d100 = await client.ReadInt16Async("D100");
var c200 = await client.ReadInt32Async("C200"); // 32-bit counter (2 PDU words per element)
```

### 3.10 Keyence (KV Host Link)
Supports Keyence KV-8000/7000/5000/3000/Nano Upper Link (Host Link) ASCII protocol (TCP Port 8501 / Serial).
```csharp
using var client = PlcClientFactory.CreateKeyenceHostLinkTcp("192.168.1.70", port: 8501);
await client.OpenAsync();

// Word memory (DM, EM, FM, ZF, W, TM, Z, T, C)
var dm100 = await client.ReadInt16Async("DM100");
await client.WriteInt32Async("DM200", 50000);

// Relay channels (R, B, MR, LR, CR, VB)
var mr100 = await client.ReadBooleanAsync("MR100");
var r0 = await client.ReadBooleanAsync("R0");
```

### 3.11 Megmeet (MC200 / MC280 Series)
Supports Megmeet MC series Modbus mapping with automatic range splitting across PDU boundaries.
```csharp
using var client = PlcClientFactory.CreateMegmeetTcp("192.168.1.80");
await client.OpenAsync();

var d100 = await client.ReadInt16Async("D100");
var sd0 = await client.ReadInt16Async("SD0");
var c200 = await client.ReadInt32Async("C200"); // 32-bit counter (C200..C255)
var y0 = await client.ReadBooleanAsync("Y0");     // Octal I/O
```

### 3.12 Panasonic (MEWTOCOL-COM)
Supports Panasonic FP series MEWTOCOL-COM ASCII protocol (`%` standard frame and `<` expanded frame up to 509 words).
```csharp
using var client = PlcClientFactory.CreatePanasonicMewtocolTcp("192.168.1.90", station: 1);
await client.OpenAsync();

// Word registers: DT, LD, FL, SV, EV, IX, IY, WX, WY, WR, WL
var dt100 = await client.ReadInt16Async("DT100");

// Bit contacts (word + hexadecimal bit 0..F): R, X, Y, L, T, C
var r0 = await client.ReadBooleanAsync("R0");
var x1A = await client.ReadBooleanAsync("X1A"); // Word 1, Bit 0xA
```

### 3.13 Toyota / JTEKT (TOYOPUC Computer Link)
Supports TOYOPUC 2PORT-EFR Computer Link binary protocol (Basic Commands `1C`/`1D`/`20`/`21` and Extended Program/Area Commands `94`/`95`/`98`/`99`).
```csharp
using var client = PlcClientFactory.CreateJtektToyopucTcp("192.168.1.100", port: 10255);
await client.OpenAsync();

// Basic area (Hexadecimal address): D, S, N, R, B, K, V, T, C, L, X, Y, M
var d100 = await client.ReadInt16Async("D0100");
var m10 = await client.ReadBooleanAsync("M0010");

// Extended area & Program-prefixed area: EK, EV, ET, EC, EL, EX, EY, EM, ES, EN, EB, P1-D0100
var ek10 = await client.ReadInt16Async("EK0010");
```

### 3.14 VIGOR (M / VB Series)
Supports VIGOR M / VB series Computer Link ASCII protocol (`:01...` LRC frame, Function Codes `01`~`08`).
```csharp
using var client = PlcClientFactory.CreateVigorSerial("COM4", station: 1);
await client.OpenAsync();

// Word registers (D, SD, R, T, C)
var d100 = await client.ReadInt16Async("D100");
var c200 = await client.ReadInt32Async("C200"); // 32-bit counter (C200..C255, Device Code 0x56)

// Bit relays (X, Y: octal; M, S, SM, T, C: decimal)
var m10 = await client.ReadBooleanAsync("M10");
var s100 = await client.ReadBooleanAsync("S100");
```

### 3.15 XINJE (XC / XD / XL Series)
Supports XINJE XC and XD/XL series Modbus RTU/TCP mapping.
```csharp
using var client = PlcClientFactory.CreateXinjeTcp("192.168.1.110", station: 1);
await client.OpenAsync();

// Word registers (D, HD, TD, CD, SD, FD, ETD, ECD, HSCD, HSD)
var d100 = await client.ReadInt16Async("D100");
var hd0 = await client.ReadInt16Async("HD0");

// Bit contacts (X, Y: octal; M, HM, S, T, C, SM, ET, EC, HSC)
var m10 = await client.ReadBooleanAsync("M10");
var x10 = await client.ReadBooleanAsync("X10");
```

### 3.16 Yokogawa (FA-M3 Series)
Supports Yokogawa FA-M3 Higher-Level Link Service (PC Link binary protocol, Port 5001).
```csharp
using var client = PlcClientFactory.CreateYokogawaTcp("192.168.1.120", port: 5001, cpuNumber: 1);
await client.OpenAsync();

// Word registers: D, B, F, R, V, Z, W, T, C
var d100 = await client.ReadInt16Async("D100");
var t1 = await client.ReadInt16Async("T1");

// Bit relays: X (read-only input), Y, I, E, M, T, C, L
var y1 = await client.ReadBooleanAsync("Y1");
var i10 = await client.ReadBooleanAsync("I10");
```

---

<a id="한국어"></a>

# 한국어

## 1. 지원 제조사 및 프로토콜 일람

| 제조사 | 대상 PLC 제품군 | 지원 프로토콜 및 전송 | 팩토리 메서드 (`PlcClientFactory`) |
|---|---|---|---|
| **Mitsubishi Electric** | MELSEC-Q / L / iQ-R / iQ-F / FX | MC Protocol (3E 프레임 Binary / ASCII, TCP/UDP) | `CreateMitsubishiMCBinary`, `CreateMitsubishiMCAscii` |
| **Siemens** | S7-200 / 300 / 400 / 1200 / 1500 | S7Comm (ISO-on-TCP, 포트 102) | `CreateSiemensS7Comm` |
| **LS ELECTRIC** | XGT / XGB / XGK / XGI | FEnet (이더넷 전용 프로토콜, 포트 2004) | `CreateLsElectricFEnet` |
| **Modbus** | 범용 표준 장치 | Modbus TCP (포트 502) / RTU (Serial) / ASCII (Serial) | `CreateModbusTcp`, `CreateModbusRtu`, `CreateModbusAscii` |
| **Delta Electronics** | DVP 시리즈 / AS300 시리즈 | Modbus RTU (Serial) / Modbus TCP (포트 502) | `CreateDeltaDvpRtu`, `CreateDeltaDvpTcp`, `CreateDeltaAsTcp` |
| **FATEK** | FBs 시리즈 | FATEK 통신 프로토콜 (ASCII `STX`/`ETX`/LRC, Serial/TCP) | `CreateFatekSerial`, `CreateFatek` |
| **Fuji Electric** | MICREX-SX SPH / SPB 시리즈 | SPB Loader (ASCII) / SPH SX-Bus Ethernet (포트 18245) | `CreateFujiElectricSpbSerial`, `CreateFujiElectricSphTcp` |
| **GE Fanuc / Emerson** | PACSystems RX3i/RX7i, Series 90-30 | GE SRTP (바이너리 56바이트 헤더, 포트 18245) | `CreateGESrtpTcp`, `CreateGESrtp` |
| **Inovance** | AM400/600 (IEC), H3U, H5U | Modbus RTU / Modbus TCP (시리즈별 오프셋 자동 매핑) | `CreateInovanceRtu`, `CreateInovanceTcp` |
| **Keyence** | KV-8000 / 7000 / 5000 / 3000 / Nano | KV Host Link (상위 링크 ASCII, TCP 포트 8501 / Serial) | `CreateKeyenceHostLinkTcp`, `CreateKeyenceHostLinkSerial` |
| **Megmeet** | MC200 / MC280 시리즈 | MC 시리즈 Modbus RTU / TCP (불연속 경계 분할 매핑) | `CreateMegmeetRtu`, `CreateMegmeetTcp` |
| **Panasonic** | FP-X / FP0R / FP2SH / FP7 | MEWTOCOL-COM (ASCII `%` / `<` 프레임, `#RD`/`#WD`/`#RCS`/`#WCS`, BCC) | `CreatePanasonicMewtocolSerial`, `CreatePanasonicMewtocolTcp` |
| **Toyota / JTEKT** | TOYOPUC (2PORT-EFR) | TOYOPUC Computer Link (기본 및 확장 PRG, 포트 10255) | `CreateJtektToyopucTcp`, `CreateJtektToyopuc` |
| **VIGOR** | M / VB 시리즈 (丰炜) | Computer Link ASCII (`:01...` LRC 프레임, 기능 코드 `01`~`08`) | `CreateVigorSerial`, `CreateVigor` |
| **XINJE** | XC 시리즈 / XD · XL 시리즈 (信捷) | Modbus RTU / Modbus TCP (시리즈별 주소 매핑) | `CreateXinjeRtu`, `CreateXinjeTcp` |
| **Yokogawa** | FA-M3 시리즈 | Higher-Level Link Service (PC Link 바이너리, 포트 5001) | `CreateYokogawaTcp`, `CreateYokogawa` |

---

## 2. 공통 사용 패턴

모든 제조사 PLC는 `PlcClient` 파사드와 `PlcResult<T>` 결과 타입을 통해 동일한 방식으로 제어됩니다.

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

// 5. 연속 배열 읽기 (프레임 한도 초과 시 자동 분할 송수신)
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
MELSEC iQ-R, Q, L, FX 시리즈의 3E 프레임(Binary / ASCII) 통신을 지원합니다.
```csharp
// TCP 이더넷 클라이언트 (기본 포트 5007)
using var client = PlcClientFactory.CreateMitsubishiMCBinary("192.168.1.10");
await client.OpenAsync();

// 비트 읽기/쓰기 (M, X, Y, L, F, V, B)
await client.WriteBooleanAsync("M100", true);
var x1A = await client.ReadBooleanAsync("X1A"); // 16진수 비트 주소

// 워드 읽기/쓰기 (D, W, R, ZR)
await client.WriteInt16Async("D100", (short)100);
var zr1000 = await client.ReadInt32Async("ZR1000");
```

### 3.2 Siemens (S7Comm)
S7-200, 300, 400, 1200, 1500 기종의 데이터 블록(DB) 및 입출력 메모리에 접근합니다.
```csharp
// ISO-on-TCP 포트 102 연결 (기본 Rack=0, Slot=1)
using var client = PlcClientFactory.CreateSiemensS7Comm("192.168.0.1");
await client.OpenAsync();

// DB 블록 접근: DB{번호}.DB{X|B|W|D}{오프셋}
var dbValue = await client.ReadInt16Async("DB1.DBW0");
var dbDint = await client.ReadInt32Async("DB1.DBD2");
var dbBit = await client.ReadBooleanAsync("DB1.DBX6.0");

// I/O 및 플래그 접근 (I, Q, M)
var m0 = await client.ReadBooleanAsync("M0.0");
var mw10 = await client.ReadInt16Async("MW10");
```

### 3.3 LS ELECTRIC (XGT FEnet)
LS ELECTRIC XGT / XGB 전용 이더넷 FEnet 통신(기본 포트 2004)을 지원합니다.
```csharp
using var client = PlcClientFactory.CreateLsElectricFEnet("192.168.1.5", port: 2004);
await client.OpenAsync();

// 비트 디바이스 (%MX, %PX, %KX, %FX, %TX, %CX)
await client.WriteBooleanAsync("%MX100", true);
var px0 = await client.ReadBooleanAsync("%PX0");

// 워드 / 더블워드 디바이스 (%MW, %DW, %MD, %DD)
await client.WriteInt16Async("%DW100", (short)250);
var md200 = await client.ReadInt32Async("%MD200");
```

### 3.4 Modbus (TCP / RTU / ASCII)
표준 Modbus 디바이스와 이더넷 및 시리얼 포트로 통신합니다.
```csharp
// Modbus TCP (기본 포트 502, UnitId=1)
using var tcpClient = PlcClientFactory.CreateModbusTcp("192.168.1.20", port: 502, unitId: 1);

// Modbus RTU 시리얼
using var rtuClient = PlcClientFactory.CreateModbusRtu("COM1", baudRate: 19200, slaveAddress: 1);

await tcpClient.OpenAsync();
var holdingVal = await tcpClient.ReadInt16Async("40001");
var coilVal = await tcpClient.ReadBooleanAsync("00001");
```

### 3.5 Delta Electronics (DVP & AS 시리즈)
Delta DVP 계열(시리얼/이더넷) 및 AS300 계열의 Modbus 메모리 맵과 불연속 구간 분할을 자동으로 처리합니다.
```csharp
using var client = PlcClientFactory.CreateDeltaDvpTcp("192.168.1.30", station: 1);
await client.OpenAsync();

// 8진수 X, Y 및 10진수 M, S, T, C, D
var d100 = await client.ReadInt16Async("D100");
var y0 = await client.ReadBooleanAsync("Y0");     // 8진수 주소
var c200 = await client.ReadInt32Async("C200"); // 32비트 고속 카운터(C200~C255)
```

### 3.6 FATEK (FBs 시리즈)
FATEK FBs 전용 Program Port ASCII 통신 프로토콜(`STX`/`ETX` + LRC)을 사용합니다.
```csharp
using var client = PlcClientFactory.CreateFatekSerial("COM2", baudRate: 9600, station: 1);
await client.OpenAsync();

// 이산 릴레이: X, Y, M, S, T, C (4자리 10진 표기)
await client.WriteBooleanAsync("Y0", true);
var m10 = await client.ReadBooleanAsync("M10");

// 16비트 레지스터(R, D, RT, RC) 및 32비트 레지스터(DRC)
var r100 = await client.ReadInt16Async("R100");
var drc0 = await client.ReadInt32Async("DRC0"); // 8자리 ASCII Hex 32비트 카운터 레지스터
```

### 3.7 Fuji Electric (SPB & MICREX-SX SPH)
Fuji Electric SPB 계열(시리얼 로더) 및 MICREX-SX SPH 계열(이더넷 SX-Bus, 포트 18245)을 지원합니다.
```csharp
// Fuji SPB 시리얼 로더
using var spbClient = PlcClientFactory.CreateFujiElectricSpbSerial("COM3", station: 1);
await spbClient.OpenAsync();
var spbD100 = await spbClient.ReadInt16Async("D100");

// Fuji MICREX-SX SPH 이더넷 (포트 18245)
using var sphClient = PlcClientFactory.CreateFujiElectricSphTcp("192.168.1.40", port: 18245);
await sphClient.OpenAsync();
var mw0 = await sphClient.ReadInt16Async("%MW1.0");
var mx0 = await sphClient.ReadBooleanAsync("%MX1.0.0");
```

### 3.8 GE Fanuc / Emerson (SRTP)
PACSystems RX3i/RX7i 및 Series 90-30의 SRTP 56바이트 바이너리 프로토콜(포트 18245)을 지원합니다.
```csharp
using var client = PlcClientFactory.CreateGESrtpTcp("192.168.1.50");
await client.OpenAsync();

// 워드 레지스터: %R, %AI, %AQ (1부터 시작)
var r1 = await client.ReadInt16Async("%R1");
await client.WriteInt16Async("%R100", (short)999);

// 비트 레퍼런스: %I, %Q, %M, %T, %S, %SA, %SB, %SC, %G (바이트 정렬 단위 접근)
var q1 = await client.ReadBooleanAsync("%Q1");
```

### 3.9 Inovance (AM / H3U / H5U 시리즈)
Inovance AM400/600 (`%MW`, `%MD`, `%MX`, `%IX`, `%QX`) 및 H3U, H5U (`D`, `R`, `M`, `S`, `T`, `C`, 8진수 `X`/`Y`) 계열을 지원합니다.
```csharp
using var client = PlcClientFactory.CreateInovanceTcp("192.168.1.60", series: InovanceSeries.H3u);
await client.OpenAsync();

var x0 = await client.ReadBooleanAsync("X0");
var d100 = await client.ReadInt16Async("D100");
var c200 = await client.ReadInt32Async("C200"); // 32비트 카운터 (원소당 2 PDU 워드 자동 증가)
```

### 3.10 Keyence (KV 상위 링크)
KV-8000/7000/5000/3000/Nano 계열의 KV 상위 링크(Host Link) ASCII 프로토콜(기본 포트 8501)을 지원합니다.
```csharp
using var client = PlcClientFactory.CreateKeyenceHostLinkTcp("192.168.1.70", port: 8501);
await client.OpenAsync();

// 워드 메모리 (DM, EM, FM, ZF, W, TM, Z, T, C)
var dm100 = await client.ReadInt16Async("DM100");
await client.WriteInt32Async("DM200", 50000);

// 릴레이 접점 (R, B, MR, LR, CR, VB)
var mr100 = await client.ReadBooleanAsync("MR100");
var r0 = await client.ReadBooleanAsync("R0");
```

### 3.11 Megmeet (MC200 / MC280 시리즈)
Megmeet MC200/MC280 시리즈의 Modbus 매핑 테이블과 PDU 불연속 경계 분할 읽기를 지원합니다.
```csharp
using var client = PlcClientFactory.CreateMegmeetTcp("192.168.1.80");
await client.OpenAsync();

var d100 = await client.ReadInt16Async("D100");
var sd0 = await client.ReadInt16Async("SD0");
var c200 = await client.ReadInt32Async("C200"); // 32비트 카운터(C200~C255)
var y0 = await client.ReadBooleanAsync("Y0");     // 8진수 입출력
```

### 3.12 Panasonic (MEWTOCOL-COM)
Panasonic FP 계열의 MEWTOCOL-COM 프로토콜(`%` 표준 프레임 및 `<` 확장 프레임, `#RD`, `#WD`, `#RCS`, `#WCS`)을 지원합니다.
```csharp
using var client = PlcClientFactory.CreatePanasonicMewtocolTcp("192.168.1.90", station: 1);
await client.OpenAsync();

// 워드 레지스터: DT, LD, FL, SV, EV, IX, IY, WX, WY, WR, WL
var dt100 = await client.ReadInt16Async("DT100");

// 릴레이 접점 (워드 번호 + 16진수 비트 0..F): R, X, Y, L, T, C
var r0 = await client.ReadBooleanAsync("R0");
var x1A = await client.ReadBooleanAsync("X1A"); // 워드 1, 비트 0xA
```

### 3.13 Toyota / JTEKT (TOYOPUC Computer Link)
TOYOPUC 2PORT-EFR 이더넷 컴퓨터 링크 바이너리 프로토콜(기본 명령 `1C`/`1D`/`20`/`21` 및 확장 프로그램·영역 명령 `94`/`95`/`98`/`99`)을 지원합니다.
```csharp
using var client = PlcClientFactory.CreateJtektToyopucTcp("192.168.1.100", port: 10255);
await client.OpenAsync();

// 기본 영역 (16진수 주소): D, S, N, R, B, K, V, T, C, L, X, Y, M
var d100 = await client.ReadInt16Async("D0100");
var m10 = await client.ReadBooleanAsync("M0010");

// 확장 영역 및 프로그램 지정: EK, EV, ET, EC, EL, EX, EY, EM, ES, EN, EB, P1-D0100
var ek10 = await client.ReadInt16Async("EK0010");
```

### 3.14 VIGOR (丰炜 M / VB 시리즈)
VIGOR M / VB 계열 Computer Link ASCII 프로토콜(`:01...` LRC 프레임, 기능 코드 `01`~`08`)을 지원합니다.
```csharp
using var client = PlcClientFactory.CreateVigorSerial("COM4", station: 1);
await client.OpenAsync();

// 워드 레지스터 (D, SD, R, T, C)
var d100 = await client.ReadInt16Async("D100");
var c200 = await client.ReadInt32Async("C200"); // C200~C255는 32비트 카운터(0x56) 자동 적용

// 릴레이 접점 (X, Y: 8진수 / M, S, SM, T, C: 10진수)
var m10 = await client.ReadBooleanAsync("M10");
var s100 = await client.ReadBooleanAsync("S100");
```

### 3.15 XINJE (信捷 XC / XD / XL 시리즈)
XINJE XC 및 XD/XL 계열의 시리즈별 Modbus RTU/TCP 주소 매핑을 지원합니다.
```csharp
using var client = PlcClientFactory.CreateXinjeTcp("192.168.1.110", station: 1);
await client.OpenAsync();

// 워드 레지스터 (D, HD, TD, CD, SD, FD, ETD, ECD, HSCD, HSD)
var d100 = await client.ReadInt16Async("D100");
var hd0 = await client.ReadInt16Async("HD0");

// 비트 접점 (X, Y: 8진수 / M, HM, S, T, C, SM, ET, EC, HSC)
var m10 = await client.ReadBooleanAsync("M10");
var x10 = await client.ReadBooleanAsync("X10");
```

### 3.16 Yokogawa (FA-M3 시리즈)
Yokogawa FA-M3 Higher-Level Link Service (PC Link 바이너리 프로토콜, 포트 5001)를 지원합니다.
```csharp
using var client = PlcClientFactory.CreateYokogawaTcp("192.168.1.120", port: 5001, cpuNumber: 1);
await client.OpenAsync();

// 워드 레지스터: D, B, F, R, V, Z, W, T, C
var d100 = await client.ReadInt16Async("D100");
var t1 = await client.ReadInt16Async("T1");

// 릴레이 접점: X (입력 전용), Y, I, E, M, T, C, L
var y1 = await client.ReadBooleanAsync("Y1");
var i10 = await client.ReadBooleanAsync("I10");
```
