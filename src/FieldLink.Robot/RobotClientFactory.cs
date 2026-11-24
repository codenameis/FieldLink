using System;
using FieldLink.Robot.Protocols.Abb.Rws;
using FieldLink.Robot.Protocols.Efort.ER7BC10;
using FieldLink.Robot.Protocols.Estun.Modbus;
using FieldLink.Robot.Protocols.Fanuc.RobotInterface;
using FieldLink.Robot.Protocols.Hyundai.OnLTrack;
using FieldLink.Robot.Protocols.Kuka.SocketAscii;
using FieldLink.Robot.Protocols.Kuka.VarProxy;
using FieldLink.Robot.Protocols.Yamaha.Rcx;
using FieldLink.Robot.Protocols.Yaskawa.HighSpeedEthernet;
using FieldLink.Robot.Protocols.Yaskawa.YrcEthernet;
using FieldLink.Transport;
using FieldLink.Transport.Http;
using FieldLink.Transport.Tcp;
using FieldLink.Transport.Udp;

namespace FieldLink.Robot;

/// <summary>제조사·프로토콜과 전송 계층을 조합해 로봇 클라이언트를 생성하는 팩토리입니다.</summary>
public static class RobotClientFactory
{
    private const int DefaultAbbRwsPort = 80;
    private const int DefaultAbbRwsHttpsPort = 443;
    private const int DefaultEfortPort = 8008;
    private const int DefaultEstunPort = 502;
    private const int DefaultFanucPort = 60008;
    private const int DefaultHyundaiLocalPort = 7127;
    private const int DefaultKukaVarProxyPort = 7000;
    private const int DefaultKukaSocketAsciiPort = 9999;
    private const int DefaultYamahaRcxPort = 23;
    private const int DefaultYrcEthernetPort = 80;
    private const int DefaultYercPort = 10040;
    private const int DefaultTimeoutSeconds = 5;

    private static readonly TimeSpan DefaultFactoryTimeout = TimeSpan.FromSeconds(DefaultTimeoutSeconds);

    /// <summary>호스트·포트 설정으로 ABB Robot Web Services 클라이언트를 생성합니다.</summary>
    public static AbbRwsClient CreateAbbRws(string host, int? port = null, bool useHttps = false) =>
        new(new AbbRwsOptions(host, port ?? (useHttps ? DefaultAbbRwsHttpsPort : DefaultAbbRwsPort), useHttps));

    /// <summary>상세 옵션으로 ABB Robot Web Services 클라이언트를 생성합니다.</summary>
    public static AbbRwsClient CreateAbbRws(AbbRwsOptions options) =>
        new(options ?? throw new ArgumentNullException(nameof(options)));

    /// <summary>사용자 정의 HTTP 전송을 사용하는 ABB Robot Web Services 클라이언트를 생성합니다.</summary>
    public static AbbRwsClient CreateAbbRws(IHttpTransport transport, AbbRwsOptions? options = null) =>
        new(transport, options);

    /// <summary>EFORT ER7B-C10 v2(현행 정렬 버전) TCP 클라이언트를 생성합니다.</summary>
    public static EfortClient CreateEfortER7BC10V2(string host, int port = DefaultEfortPort, EfortOptions? options = null)
    {
        EfortOptions effective = new()
        {
            Version = EfortProtocolVersion.V2Aligned,
            Timeout = options?.Timeout ?? DefaultFactoryTimeout
        };
        var transport = new TcpTransport(CreateTcpOptions(host, port, effective.Timeout));
        return new EfortClient(transport, effective);
    }

    /// <summary>EFORT ER7B-C10 v1(구버전 비정렬) TCP 클라이언트를 생성합니다.</summary>
    public static EfortClient CreateEfortER7BC10V1(string host, int port = DefaultEfortPort, EfortOptions? options = null)
    {
        EfortOptions effective = new()
        {
            Version = EfortProtocolVersion.V1Unaligned,
            Timeout = options?.Timeout ?? DefaultFactoryTimeout
        };
        var transport = new TcpTransport(CreateTcpOptions(host, port, effective.Timeout));
        return new EfortClient(transport, effective);
    }

    /// <summary>사용자 정의 전송을 사용하는 EFORT ER7B-C10 클라이언트를 생성합니다.</summary>
    public static EfortClient CreateEfortER7BC10(ITransport transport, EfortOptions? options = null) =>
        new(transport, options);

    /// <summary>Estun Modbus TCP 로봇 클라이언트를 생성합니다.</summary>
    public static EstunClient CreateEstunModbus(string host, int port = DefaultEstunPort, EstunOptions? options = null)
    {
        EstunOptions effective = options ?? new EstunOptions();
        return new(new TcpTransport(CreateTcpOptions(host, port, effective.Timeout)), effective);
    }

    /// <summary>사용자 정의 전송을 사용하는 Estun Modbus TCP 클라이언트를 생성합니다.</summary>
    public static EstunClient CreateEstunModbus(ITransport transport, EstunOptions? options = null) =>
        new(transport, options);

    /// <summary>FANUC Robot Interface (SRTP/SNPX) TCP 클라이언트를 생성합니다.</summary>
    public static FanucClient CreateFanucRobotInterface(string host, int port = DefaultFanucPort, FanucOptions? options = null)
    {
        FanucOptions effective = options ?? new FanucOptions();
        return new(new TcpTransport(CreateTcpOptions(host, port, effective.Timeout)), effective);
    }

    /// <summary>사용자 정의 전송을 사용하는 FANUC Robot Interface 클라이언트를 생성합니다.</summary>
    public static FanucClient CreateFanucRobotInterface(ITransport transport, FanucOptions? options = null) =>
        new(transport, options);

    /// <summary>Hyundai OnLTrack UDP 수신 서버 클라이언트를 생성합니다.</summary>
    public static HyundaiOnLTrackClient CreateHyundaiOnLTrack(int localPort = DefaultHyundaiLocalPort, HyundaiOnLTrackOptions? options = null)
    {
        HyundaiOnLTrackOptions effective = new()
        {
            LocalPort = localPort,
            LocalAddress = options?.LocalAddress ?? System.Net.IPAddress.Any,
            Timeout = options?.Timeout ?? DefaultFactoryTimeout
        };
        return new HyundaiOnLTrackClient(effective);
    }

    /// <summary>사용자 정의 UDP 리스너 전송을 사용하는 Hyundai OnLTrack 클라이언트를 생성합니다.</summary>
    public static HyundaiOnLTrackClient CreateHyundaiOnLTrack(IUdpListenerTransport transport, HyundaiOnLTrackOptions? options = null) =>
        new(transport, options);

    /// <summary>KUKA KUKAVARPROXY Binary TCP 클라이언트를 생성합니다.</summary>
    public static KukaVarProxyClient CreateKukaVarProxy(string host, int port = DefaultKukaVarProxyPort, KukaVarProxyOptions? options = null)
    {
        KukaVarProxyOptions effective = options ?? new KukaVarProxyOptions();
        return new(new TcpTransport(CreateTcpOptions(host, port, effective.Timeout)), effective);
    }

    /// <summary>사용자 정의 전송을 사용하는 KUKAVARPROXY 클라이언트를 생성합니다.</summary>
    public static KukaVarProxyClient CreateKukaVarProxy(ITransport transport, KukaVarProxyOptions? options = null) =>
        new(transport, options);

    /// <summary>KUKA Socket ASCII TCP 클라이언트를 생성합니다.</summary>
    public static KukaSocketAsciiClient CreateKukaSocketAscii(string host, int port = DefaultKukaSocketAsciiPort, KukaSocketAsciiOptions? options = null)
    {
        KukaSocketAsciiOptions effective = options ?? new KukaSocketAsciiOptions();
        return new(new TcpTransport(CreateTcpOptions(host, port, effective.Timeout)), effective);
    }

    /// <summary>사용자 정의 전송을 사용하는 KUKA Socket ASCII 클라이언트를 생성합니다.</summary>
    public static KukaSocketAsciiClient CreateKukaSocketAscii(ITransport transport, KukaSocketAsciiOptions? options = null) =>
        new(transport, options);

    /// <summary>YAMAHA RCX TCP ASCII 클라이언트를 생성합니다.</summary>
    public static YamahaRcxClient CreateYamahaRcx(string host, int port = DefaultYamahaRcxPort, YamahaRcxOptions? options = null)
    {
        YamahaRcxOptions effective = options ?? new YamahaRcxOptions();
        return new(new TcpTransport(CreateTcpOptions(host, port, effective.Timeout)), effective);
    }

    /// <summary>사용자 정의 전송을 사용하는 YAMAHA RCX 클라이언트를 생성합니다.</summary>
    public static YamahaRcxClient CreateYamahaRcx(ITransport transport, YamahaRcxOptions? options = null) =>
        new(transport, options);

    /// <summary>YASKAWA YRC1000/DX200 Ethernet Server TCP 클라이언트를 생성합니다.</summary>
    public static YrcEthernetClient CreateYaskawaYrcEthernet(string host, int port = DefaultYrcEthernetPort, YrcEthernetOptions? options = null)
    {
        YrcEthernetOptions effective = options ?? new YrcEthernetOptions();
        return new(new TcpTransport(CreateTcpOptions(host, port, effective.Timeout)), effective);
    }

    /// <summary>사용자 정의 전송을 사용하는 YASKAWA YRC Ethernet Server 클라이언트를 생성합니다.</summary>
    public static YrcEthernetClient CreateYaskawaYrcEthernet(ITransport transport, YrcEthernetOptions? options = null) =>
        new(transport, options);

    /// <summary>YASKAWA High-Speed Ethernet (<c>YERC</c>) UDP 클라이언트를 생성합니다.</summary>
    public static YercUdpClient CreateYaskawaHighSpeedEthernet(string host, int port = DefaultYercPort, YercUdpOptions? options = null)
    {
        YercUdpOptions effective = options ?? new YercUdpOptions();
        return new(new UdpTransport(new UdpTransportOptions(host, port) { ReceiveTimeout = effective.Timeout }), effective);
    }

    /// <summary>사용자 정의 전송을 사용하는 YASKAWA High-Speed Ethernet 클라이언트를 생성합니다.</summary>
    public static YercUdpClient CreateYaskawaHighSpeedEthernet(ITransport transport, YercUdpOptions? options = null) =>
        new(transport, options);

    private static TcpTransportOptions CreateTcpOptions(string host, int port, TimeSpan timeout) =>
        new(host, port) { ConnectTimeout = timeout };
}
