# FieldLink

PLC·산업용 Robot·SECS/GEM 통신을 위한 **.NET Standard 2.0** 라이브러리입니다. 제조사별 프로토콜과 TCP·UDP·Serial·WebSocket 전송을 제공하며, 주요 PLC는 C# 자료형 그대로 읽고 쓸 수 있습니다.

[지원 PLC](#지원-plc) · [PLC 예제](#plc-예제) · [지원 Robot](#지원-robot) · [Robot 예제](#robot-예제) · [SECS/GEM](docs/secs-gem.md) · [빌드와 검증](#빌드와-검증) · [전체 문서](docs/README.md)

## 시작하기

사용할 프로젝트에서 [FieldLink.csproj](src/FieldLink/FieldLink.csproj)를 프로젝트 참조로 추가합니다.

```powershell
dotnet add <사용할-프로젝트.csproj> reference <FieldLink-경로>/src/FieldLink/FieldLink.csproj
```

아래 예제의 `using`은 파일 상단에, 실행 코드는 `CancellationToken cancellationToken`을 받는 `async Task` 메서드 본문에 넣습니다. 공통으로 다음 네임스페이스를 사용합니다.

```csharp
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.PlcDrivers;
```

IP·포트·주소·태그는 장치 설정에 맞춥니다. PLC 쓰기 예제에는 시험용 메모리를 사용하세요. 지원 범위는 현재 코드와 로컬 테스트 기준이며, 실제 장비 전체에 대한 호환성 인증을 의미하지 않습니다.

## 지원 PLC

### 연결부터 자료형 변환까지 제공하는 간편 API

| 제조사 / 규격 | 프로토콜 | 생성 API | 주소 예 |
| --- | --- | --- | --- |
| **Mitsubishi / 미쓰비시** | MC 3E Binary TCP | `Plc.MelsecMcTcp(ip, port)` | `D100`, `M100` |
| Siemens / 지멘스 | S7 TCP | `Plc.SiemensS7Tcp(ip)` | `DB1.0`, `M0.7` |
| OMRON / 오므론 | FINS/TCP | `Plc.OmronFinsTcp(ip)` | `D100`, `D100.15` |
| LS ELECTRIC | XGT FastEnet TCP | `Plc.LsFastEnetTcp(ip)` | `D100`, `DB200`, `MX2764` |
| Allen-Bradley / Rockwell | EtherNet/IP, 비연결형 CIP | `Plc.AllenBradleyTcp(ip)` | `ProductionCount`, `Values[0]` |
| KEYENCE / 키엔스 | MC 3E Binary TCP | `Plc.KeyenceMcTcp(ip, port)` | `DM100`, `MR100` |
| Modbus 지원 PLC·장치 | Modbus TCP | `Plc.ModbusTcp(ip)` | `0`, `s=2;0` |

공통으로 `OpenAsync → Read/Write → Dispose` 흐름을 사용합니다. `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `float`, `double`, `bool`, 문자열과 숫자 배열을 지원하며, 결과는 `IsSuccess`, `Content`, `Message`로 확인합니다. 연결을 유지한 채 여러 요청을 실행할 수 있습니다.

- 숫자 배열의 길이는 **C# 원소 수**입니다. MC에서 `int` 3개는 6워드를 사용합니다.
- 문자열의 `byteLength`는 **바이트 수**입니다. MC 2워드는 4바이트이며 기본 인코딩은 ASCII입니다.
- 지원 프로토콜의 전송 한도를 넘는 연속 요청은 자동 분할합니다. 여러 요청으로 나뉜 쓰기는 원자적 처리가 아니며 실패 시 앞부분이 이미 기록되었을 수 있습니다.
- Siemens STRING, CIP STRING, 워드 영역 문자열은 저장 형식이 다릅니다. Allen-Bradley의 packed BOOL 배열은 간편 API에서 지원하지 않습니다.
- 통신·장치 실패는 결과 객체로 전달합니다. 잘못된 인수·호출 상태는 예외, 취소는 `OperationCanceledException`으로 전달하며 자동 재접속·재전송하지 않습니다.

CPU·노드·슬롯은 아래 제조사별 예제와 각 Options 타입에서 설정합니다. 프로토콜별 공식 문서 대조 범위와 제한은 [검증 보고서](docs/standards/README.md)를 참고하세요.

### 추가 Serial 클라이언트

다음은 전송 객체를 직접 연결하는 API입니다. 위의 `Plc.*Tcp`와 지원 범위가 다릅니다.

| 제조사 / 규격 | Serial 프로토콜 |
| --- | --- |
| Mitsubishi | FX Links, A3C, FX 직접 연결 |
| Siemens | PPI, MPI |
| OMRON | Host Link FINS, Host Link C-Mode |
| LS ELECTRIC | Cnet, CPU 직접 연결 |
| Allen-Bradley | DF1 |
| KEYENCE | Nano |
| Delta | DVP / AS, Modbus RTU·ASCII |
| FATEK | Programming Port |
| Fuji | SPB |
| Inovance | 시리즈별 Modbus 주소 매핑 |
| MegMeet | Modbus 주소 매핑 |
| Panasonic | MEWTOCOL |
| Vigor | VS |
| XINJE | 시리즈별 Modbus 주소 매핑 |
| Yamatake | Digitron CPL |
| Modbus 장치 | RTU / ASCII |

Serial 타이밍과 검증 범위는 [통신 기반 문서](docs/standards/transport-audit.md)에 있습니다. Beckhoff ADS/AMS, GE SRTP, Toyota ToyoPuc, YASKAWA MEMOBUS, Yokogawa Link 등은 **명령 생성·주소 해석·응답 파싱 부품**을 제공합니다. 이 항목들에는 위와 같은 간편 TCP 연결 API가 없으며, 사용자가 전송·세션 처리를 조합해야 합니다. 제조사별 공식 자료와 미검증 범위는 [기타 PLC 문서](docs/standards/plc-secondary-audit.md)를 참고하세요.

## PLC 예제

### Mitsubishi — 숫자, 연속 읽기·쓰기, 2워드 문자열

PLC를 **MC 3E Binary / TCP**로 설정합니다. 아래의 5000은 설정 예시이며, 읽기·쓰기 쌍별로 사용하는 D 영역은 서로 겹치지 않습니다.

```csharp
using (var plc = Plc.MelsecMcTcp("192.168.0.10", 5000))
{
    var opened = await plc.OpenAsync(cancellationToken);
    if (!opened.IsSuccess)
        throw new InvalidOperationException(opened.Message);

    // int 한 개: D100~D101
    var written = await plc.WriteAsync("D100", 12345, cancellationToken);
    if (!written.IsSuccess)
        throw new InvalidOperationException(written.Message);
    var value = await plc.ReadInt32Async("D100", cancellationToken);
    Console.WriteLine(value.IsSuccess ? value.Content.ToString() : value.Message);

    // int 세 개를 연속 기록/조회: D110~D115
    written = await plc.WriteAsync("D110", new[] { 10, 20, 30 }, cancellationToken);
    if (!written.IsSuccess)
        throw new InvalidOperationException(written.Message);
    var values = await plc.ReadInt32Async("D110", 3, cancellationToken);
    Console.WriteLine(values.IsSuccess ? string.Join(", ", values.Content) : values.Message);

    // 문자열 2워드 = 4바이트: D120~D121, ASCII "ABCD"
    written = await plc.WriteStringAsync("D120", "ABCD", byteLength: 4,
        cancellationToken: cancellationToken);
    if (!written.IsSuccess)
        throw new InvalidOperationException(written.Message);
    var text = await plc.ReadStringAsync("D120", byteLength: 4,
        cancellationToken: cancellationToken);
    Console.WriteLine(text.IsSuccess ? text.Content : text.Message);
}
```

`ushort[]`로 미리 변환할 필요가 없습니다. 비트는 `ReadBoolAsync("M100")` / `WriteAsync("M100", true)`, 실수는 `ReadFloatAsync("D200")` / `WriteAsync("D200", 12.5f)`로 사용합니다. 쓰기의 반환 결과도 확인하세요. [MC 규격과 요청 한계](docs/standards/plc-primary-audit.md)

### Siemens — DB의 DINT 연속 읽기·쓰기

DB1의 바이트 0부터 DINT 3개(12바이트)를 사용합니다. 해당 CPU에서 원격 접근과 절대 주소 사용이 가능하도록 PUT/GET·DB 설정을 확인합니다. CPU·Rack·Slot은 옵션으로 지정할 수 있습니다.

```csharp
using (var plc = Plc.SiemensS7Tcp("192.168.0.11"))
{
    var opened = await plc.OpenAsync(cancellationToken);
    if (!opened.IsSuccess)
        throw new InvalidOperationException(opened.Message);
    var written = await plc.WriteAsync("DB1.0", new[] { 10, 20, 30 }, cancellationToken);
    if (!written.IsSuccess)
        throw new InvalidOperationException(written.Message);
    var read = await plc.ReadInt32Async("DB1.0", 3, cancellationToken);
    Console.WriteLine(read.IsSuccess ? string.Join(", ", read.Content) : read.Message);
}
```

<details>
<summary>OMRON · LS ELECTRIC · Allen-Bradley · KEYENCE · Modbus 예제</summary>

### OMRON — FINS/TCP의 D 영역

TCP 포트 기본값은 9600입니다. 연결 시 노드 협상을 수행합니다. 다중 워드 값의 기본 바이트 순서는 CDAB이므로 PLC 프로그램의 저장 순서에 맞춥니다.

```csharp
using (var plc = Plc.OmronFinsTcp("192.168.0.12"))
{
    var opened = await plc.OpenAsync(cancellationToken);
    if (!opened.IsSuccess)
        throw new InvalidOperationException(opened.Message);
    var written = await plc.WriteAsync("D100", new[] { 10, 20, 30 }, cancellationToken);
    if (!written.IsSuccess)
        throw new InvalidOperationException(written.Message);
    var read = await plc.ReadInt32Async("D100", 3, cancellationToken);
    Console.WriteLine(read.IsSuccess ? string.Join(", ", read.Content) : read.Message);
}
```

### LS ELECTRIC — XGT FastEnet

TCP 포트 기본값은 2004입니다. `D100`은 워드 주소이며 바이트 주소 `DB200`으로 변환됩니다. CPU·Base·Slot 옵션을 장치에 맞춥니다.

```csharp
using (var plc = Plc.LsFastEnetTcp("192.168.0.13"))
{
    var opened = await plc.OpenAsync(cancellationToken);
    if (!opened.IsSuccess)
        throw new InvalidOperationException(opened.Message);
    var written = await plc.WriteAsync("D100", new[] { 10, 20, 30 }, cancellationToken);
    if (!written.IsSuccess)
        throw new InvalidOperationException(written.Message);
    var read = await plc.ReadInt32Async("D100", 3, cancellationToken);
    Console.WriteLine(read.IsSuccess ? string.Join(", ", read.Content) : read.Message);
}
```

### Allen-Bradley — Logix DINT 배열 태그

컨트롤러에 외부 접근 가능한 `Values` DINT 배열을 3개 이상 선언합니다. TCP 포트 기본값은 44818이며 CPU 슬롯 또는 RoutePath를 실제 구성에 맞춥니다. `CloseAsync`는 CIP 세션을 정상 해제합니다.

```csharp
using (var plc = Plc.AllenBradleyTcp("192.168.0.14"))
{
    var opened = await plc.OpenAsync(cancellationToken);
    if (!opened.IsSuccess)
        throw new InvalidOperationException(opened.Message);
    var written = await plc.WriteAsync("Values[0]", new[] { 10, 20, 30 }, cancellationToken);
    if (!written.IsSuccess)
        throw new InvalidOperationException(written.Message);
    var read = await plc.ReadInt32Async("Values[0]", 3, cancellationToken);
    Console.WriteLine(read.IsSuccess ? string.Join(", ", read.Content) : read.Message);
    var closed = await plc.CloseAsync(cancellationToken);
    if (!closed.IsSuccess)
        throw new InvalidOperationException(closed.Message);
}
```

### KEYENCE — MC 호환 DM 영역

MC 3E Binary 통신을 사용할 수 있는 기종·설정에서 사용합니다. 포트 5000은 설정 예시입니다.

```csharp
using (var plc = Plc.KeyenceMcTcp("192.168.0.15", 5000))
{
    var opened = await plc.OpenAsync(cancellationToken);
    if (!opened.IsSuccess)
        throw new InvalidOperationException(opened.Message);
    var written = await plc.WriteAsync("DM100", new[] { 10, 20, 30 }, cancellationToken);
    if (!written.IsSuccess)
        throw new InvalidOperationException(written.Message);
    var read = await plc.ReadInt32Async("DM100", 3, cancellationToken);
    Console.WriteLine(read.IsSuccess ? string.Join(", ", read.Content) : read.Message);
}
```

### Modbus TCP — Holding Register

기본값은 TCP 502, Unit ID 1, 0부터 시작하는 주소입니다. 예제는 레지스터 0~2에 `ushort` 3개를 기록합니다. 매뉴얼의 40001 표기와 라이브러리 주소 `"0"`을 구분하세요.

```csharp
using (var plc = Plc.ModbusTcp("192.168.0.16"))
{
    var opened = await plc.OpenAsync(cancellationToken);
    if (!opened.IsSuccess)
        throw new InvalidOperationException(opened.Message);
    var written = await plc.WriteAsync("0", new ushort[] { 10, 20, 30 }, cancellationToken);
    if (!written.IsSuccess)
        throw new InvalidOperationException(written.Message);
    var read = await plc.ReadUInt16Async("0", 3, cancellationToken);
    Console.WriteLine(read.IsSuccess ? string.Join(", ", read.Content) : read.Message);
}
```

</details>

## 지원 Robot

| 제조사 | 통신 / 실행 API | 주요 기능 |
| --- | --- | --- |
| **YASKAWA / 야스카와** | High Speed Ethernet UDP `YrcHighEthernetUdpClient`, TCP `YrcTcpClient` | 상태·변수·작업 명령 |
| FANUC / 화낙 | TCP `FanucTcpClient` | I/O·레지스터·컨트롤러 상태·자세 |
| KUKA / 쿠카 | TCP `KukaVarProxyTcpClient`, `KukaTextTcpClient` | 프록시 변수 접근, 별도 텍스트 명령 |
| ABB | HTTP(S) `AbbHttpClient` | 지원 RWS 요청·상태·RAPID 조회 |
| YAMAHA / 야마하 | TCP `YamahaRcxTcpClient` | RCX 상태·좌표·명령 |
| EFORT | TCP `EfortTcpClient` | 현재/이전 응답 형식의 상태 조회 |
| Estun / 에스턴 | Modbus TCP `EstunTcpClient` | 상태·프로젝트·프로그램 명령 |
| Hyundai / 현대 | UDP `HyundaiUdpServer` | 로봇에서 보내는 위치 수신·위치 보정 응답 |

Robot API는 전송 객체와 제조사 클라이언트를 조합합니다. TCP·UDP는 호출자가 열고 해제하며, ABB는 `HttpClient`를 사용합니다. 장치 오류는 결과 객체로, 전송 실패는 예외로 전달됩니다. 확인한 컨트롤러 문서·프로토콜 범위와 미지원 사항은 [Robot 검증 문서](docs/standards/robot-audit.md)를 참고하세요.

## Robot 예제

### YASKAWA — High Speed Ethernet 상태 조회

고속 Ethernet 통신을 활성화한 컨트롤러에 UDP로 상태를 요청합니다. 아래는 UDP 10040을 사용하는 예제이며 결과는 상태 비트 배열입니다.

```csharp
using FieldLink.Robot.YASKAWA.Clients;
using FieldLink.Robot.YASKAWA.Protocols;
using FieldUdpClient = FieldLink.Communication.Udp.UdpClient;

using (var transport = new FieldUdpClient(new IPEndPoint(IPAddress.Parse("192.168.0.20"), 10040)))
{
    var robot = new YrcHighEthernetUdpClient(transport);
    var commands = new YrcEthernetRequestBuilder();
    await transport.OpenAsync(cancellationToken);
    var read = await robot.ExecuteAsync(commands.BuildReadStats(), cancellationToken);
    if (!read.IsSuccess)
        throw new InvalidOperationException(read.Message);
    Console.WriteLine(string.Join(", ", read.Content));
}
```

변수 조회도 `commands.BuildReadRegisterVariable(0)`처럼 요청만 바꾸어 실행합니다. TCP 명령 서비스는 별도 `YrcTcpClient`를 사용합니다.

<details>
<summary>FANUC · KUKA · ABB · YAMAHA · EFORT · Estun · Hyundai 예제</summary>

### FANUC — 그룹 입력 GI 읽기

컨트롤러의 인터페이스 설정이 필요합니다. 초기화에는 **메모리 매핑 할당 명령**이 포함됩니다. 포트 60008은 구성 예시입니다.

```csharp
using FieldLink.Communication.Framing;
using FieldLink.Robot.FANUC.Clients;
using FieldTcpClient = FieldLink.Communication.Tcp.TcpClient;

var frame = new HeaderLengthFrame(56, h =>
    56 + h.Array[h.Offset + 4] + h.Array[h.Offset + 5] * 256);
using (var transport = new FieldTcpClient(new IPEndPoint(IPAddress.Parse("192.168.0.21"), 60008), frame))
{
    var robot = new FanucTcpClient(transport);
    await transport.OpenAsync(cancellationToken);
    await robot.InitializeAsync(cancellationToken);
    var read = await robot.ReadUInt16Async("GI1", 2, cancellationToken);
    if (!read.IsSuccess)
        throw new InvalidOperationException(read.Message);
    Console.WriteLine(string.Join(", ", read.Content));
}
```

### KUKA — KUKAVARPROXY 변수 읽기

KUKAVARPROXY 서비스를 사용합니다. 포트 7000은 서비스 설정에 맞춥니다.

```csharp
using FieldLink.Communication.Framing;
using FieldLink.Robot.KUKA.Clients;
using FieldTcpClient = FieldLink.Communication.Tcp.TcpClient;

var frame = new HeaderLengthFrame(4, h => 4 + h.Array[h.Offset + 2] * 256 + h.Array[h.Offset + 3]);
using (var transport = new FieldTcpClient(new IPEndPoint(IPAddress.Parse("192.168.0.22"), 7000), frame))
{
    var robot = new KukaVarProxyTcpClient(transport);
    await transport.OpenAsync(cancellationToken);
    var read = await robot.ReadStringAsync("$OV_PRO", cancellationToken);
    Console.WriteLine(read.IsSuccess ? read.Content : read.Message);
}
```

### ABB — RWS 컨트롤러 상태

`ABB_USER`·`ABB_PASSWORD` 환경 변수에 계정을 설정합니다. 기본 URL과 인증 방식은 RWS 설정에 맞춥니다. 현재 요청 빌더는 지원 경로의 XHTML 응답을 해석합니다.

```csharp
using System.Net.Http;
using FieldLink.Robot.ABB.Clients;
using FieldLink.Robot.ABB.Protocols;

string user = Environment.GetEnvironmentVariable("ABB_USER");
string password = Environment.GetEnvironmentVariable("ABB_PASSWORD");
if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(password))
    throw new InvalidOperationException("ABB_USER와 ABB_PASSWORD를 설정하세요.");
using (var handler = new HttpClientHandler { Credentials = new NetworkCredential(user, password) })
using (var http = new HttpClient(handler))
{
    var robot = new AbbHttpClient(http, new Uri("http://192.168.0.23/"));
    var read = await robot.ExecuteAsync(AbbRequestBuilder.GetCtrlState(), cancellationToken);
    Console.WriteLine(read.IsSuccess ? read.Content : read.Message);
}
```

### YAMAHA — RCX 모터 상태

아래 YAMAHA·EFORT 예제에서는 메서드 인수 `int robotPort`로 컨트롤러에 설정된 서비스 포트를 전달합니다. YAMAHA 상태 조회는 명령을 한 번 전송하고 값·OK 응답을 차례로 수신합니다. `JogXYAsync`는 지속 신호와 RUN/END 처리가 구현되지 않아 전송 없이 미지원 실패를 반환합니다.

```csharp
using FieldLink.Communication.Framing;
using FieldLink.Robot.YAMAHA.Clients;
using FieldTcpClient = FieldLink.Communication.Tcp.TcpClient;

using (var transport = new FieldTcpClient(new IPEndPoint(IPAddress.Parse("192.168.0.24"), robotPort),
    new DelimitedFrame(new byte[] { 13, 10 })))
{
    var robot = new YamahaRcxTcpClient(transport);
    await transport.OpenAsync(cancellationToken);
    var read = await robot.ReadMotorStatusAsync(cancellationToken);
    Console.WriteLine(read.IsSuccess ? read.Content.ToString() : read.Message);
}
```

### EFORT — 컨트롤러 상태

아래는 현재 응답 형식입니다. 이전 형식의 컨트롤러는 클라이언트의 `previousFormat`과 프레임 헤더 길이(17바이트)를 함께 맞춥니다.

```csharp
using FieldLink.Communication.Framing;
using FieldLink.Robot.EFORT.Clients;
using FieldTcpClient = FieldLink.Communication.Tcp.TcpClient;

var frame = new HeaderLengthFrame(18, h => h.Array[h.Offset + 16] + h.Array[h.Offset + 17] * 256);
using (var transport = new FieldTcpClient(new IPEndPoint(IPAddress.Parse("192.168.0.25"), robotPort), frame))
{
    var robot = new EfortTcpClient(transport);
    await transport.OpenAsync(cancellationToken);
    var read = await robot.ReadDataAsync(cancellationToken);
    if (!read.IsSuccess)
        throw new InvalidOperationException(read.Message);
    Console.WriteLine($"Project={read.Content.ProjectName}, Error={read.Content.ErrorStatus}");
}
```

### Estun — 프로그램 실행 상태

컨트롤러의 Modbus TCP 서비스에 연결합니다. 예제는 포트 502·국번 1이며 상태 레지스터를 조회합니다.

```csharp
using FieldLink.Communication.Framing;
using FieldLink.Robot.Estun.Clients;
using FieldTcpClient = FieldLink.Communication.Tcp.TcpClient;

var frame = new HeaderLengthFrame(6, h => 6 + h.Array[h.Offset + 4] * 256 + h.Array[h.Offset + 5]);
using (var transport = new FieldTcpClient(new IPEndPoint(IPAddress.Parse("192.168.0.26"), 502), frame))
{
    var robot = new EstunTcpClient(transport, station: 1);
    await transport.OpenAsync(cancellationToken);
    var read = await robot.ReadDataAsync(cancellationToken);
    if (!read.IsSuccess)
        throw new InvalidOperationException(read.Message);
    Console.WriteLine($"Running={read.Content.ProgramRunStatus}, Error={read.Content.ErrorStatus}");
}
```

### Hyundai — PC에서 위치 데이터 수신

PC가 UDP 서버입니다. 로봇의 송신 대상을 PC 주소·포트로 설정합니다. 아래의 10000은 PC 수신 포트 예시이며 취소 토큰으로 수신 대기를 종료합니다.

```csharp
using FieldLink.Robot.Hyundai.Servers;

using (var server = new HyundaiUdpServer(new IPEndPoint(IPAddress.Any, 10000)))
{
    server.PositionReceived += position => Console.WriteLine(string.Join(", ", position.Data));
    await server.StartAsync(cancellationToken);
    try
    {
        await Task.Delay(Timeout.Infinite, cancellationToken);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
        // 호출자의 종료 요청
    }
    finally
    {
        await server.StopAsync();
    }
}
```

</details>

## 통신과 프로젝트 구성

직접 프로토콜을 연결할 때는 `ITcpClient`, `IUdpClient`, `ISerialClient`를 사용합니다. TCP·UDP 서버, WebSocket 클라이언트·서버, 토픽 Push와 HTTP(S) 파일 전송도 제공합니다.

| 위치 | 내용 |
| --- | --- |
| [src/FieldLink/PlcDrivers](src/FieldLink/PlcDrivers) | PLC 간편 API, 제조사별 클라이언트·명령·응답 |
| [src/FieldLink/Robot](src/FieldLink/Robot) | Robot 클라이언트·서버·프로토콜 |
| [src/FieldLink/Communication](src/FieldLink/Communication) | 전송·프레임·연결 수명 관리 |
| [samples](samples/FieldLink.Communication) | PLC·통신 예제 코드 |
| [tests](tests) | 로컬 서버·가상 Serial·고정 패킷 회귀 검증 |
| [docs](docs/README.md) | 공식 문서 출처·검증 결과·제한 및 테스트 안내 |

## 빌드와 검증

저장소 루트에서 실행합니다. 빌드에는 C# 12와 `.slnx`를 지원하는 .NET SDK 및 .NET Framework 4.8 개발자 팩이 필요합니다. 제품·테스트 라이브러리는 `netstandard2.0`, 테스트 실행기는 `net48`이며 실행 환경은 Windows의 .NET Framework 4.8 이상입니다.

```powershell
dotnet build FieldLink.slnx -c Release
dotnet run --project tests/FieldLink.Communication.TestRunner/FieldLink.Communication.TestRunner.csproj -c Release
```

테스트는 별도 콘솔 러너를 사용합니다. `dotnet test` 대신 위 명령으로 실행하며 실패하거나 필터와 일치하는 테스트가 없으면 종료 코드 1을 반환합니다. 일반 빌드·회귀 테스트에 외부 참고 프로젝트나 실제 장비는 필요하지 않습니다.

자세한 필터·검증 범위는 [테스트 가이드](docs/testing.md), 공식 문서와 수정 내역은 [검증 보고서](docs/standards/README.md)를 참고하세요.
