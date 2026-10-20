using System.IO.Ports;
using FieldLink.Plc.Protocols.Delta;
using FieldLink.Plc.Protocols.Fatek;
using FieldLink.Plc.Protocols.FujiElectric;
using FieldLink.Plc.Protocols.GE;
using FieldLink.Plc.Protocols.Inovance;
using FieldLink.Plc.Protocols.Jtekt;
using FieldLink.Plc.Protocols.Keyence;
using FieldLink.Plc.Protocols.LsElectric.FEnet;
using FieldLink.Plc.Protocols.Megmeet;
using FieldLink.Plc.Protocols.Mitsubishi.MC;
using FieldLink.Plc.Protocols.Modbus;
using FieldLink.Plc.Protocols.Panasonic;
using FieldLink.Plc.Protocols.Siemens.S7Comm;
using FieldLink.Plc.Protocols.Vigor;
using FieldLink.Plc.Protocols.Xinje;
using FieldLink.Plc.Protocols.Yokogawa;
using FieldLink.Transport;
using FieldLink.Transport.Serial;
using FieldLink.Transport.Tcp;

namespace FieldLink.Plc;

/// <summary>프로토콜과 전송 조합에 따른 <see cref="PlcClient"/> 인스턴스를 생성하는 팩토리입니다. (Creator 패턴)</summary>
public static class PlcClientFactory
{
    /// <summary>Modbus TCP 프로토콜을 사용하는 PLC 클라이언트를 생성합니다.</summary>
    public static PlcClient CreateModbusTcp(
        string host,
        int port = 502,
        byte unitId = 1,
        ModbusTcpOptions? modbusOptions = null,
        PlcClientOptions? clientOptions = null)
    {
        var transportOptions = new TcpTransportOptions(host, port);
        var transport = new TcpTransport(transportOptions);
        var options = modbusOptions ?? new ModbusTcpOptions(unitId);
        var protocol = new ModbusTcpProtocol(transport, options);
        return new PlcClient(transport, protocol, clientOptions);
    }

    /// <summary>사용자 정의 전송을 사용하는 Modbus TCP 클라이언트를 생성합니다.</summary>
    public static PlcClient CreateModbusTcp(
        ITransport transport,
        ModbusTcpOptions? modbusOptions = null,
        PlcClientOptions? clientOptions = null)
    {
        var protocol = new ModbusTcpProtocol(transport, modbusOptions);
        return new PlcClient(transport, protocol, clientOptions);
    }

    /// <summary>Modbus RTU 프로토콜을 사용하는 시리얼 PLC 클라이언트를 생성합니다.</summary>
    public static PlcClient CreateModbusRtu(
        string portName,
        int baudRate = 9600,
        Parity parity = Parity.Even,
        int dataBits = 8,
        StopBits stopBits = StopBits.One,
        byte slaveAddress = 1,
        ModbusRtuOptions? modbusOptions = null,
        PlcClientOptions? clientOptions = null)
    {
        var transportOptions = new SerialTransportOptions(portName, baudRate, parity, dataBits, stopBits);
        var transport = new SerialTransport(transportOptions);
        var options = modbusOptions ?? new ModbusRtuOptions(slaveAddress);
        var protocol = new ModbusRtuProtocol(transport, options);
        return new PlcClient(transport, protocol, clientOptions);
    }

    /// <summary>사용자 정의 전송을 사용하는 Modbus RTU 클라이언트를 생성합니다.</summary>
    public static PlcClient CreateModbusRtu(
        ITransport transport,
        ModbusRtuOptions? modbusOptions = null,
        PlcClientOptions? clientOptions = null)
    {
        var protocol = new ModbusRtuProtocol(transport, modbusOptions);
        return new PlcClient(transport, protocol, clientOptions);
    }

    /// <summary>Modbus ASCII 프로토콜을 사용하는 시리얼 PLC 클라이언트를 생성합니다.</summary>
    public static PlcClient CreateModbusAscii(
        string portName,
        int baudRate = 9600,
        Parity parity = Parity.Even,
        int dataBits = 7,
        StopBits stopBits = StopBits.One,
        byte slaveAddress = 1,
        ModbusAsciiOptions? modbusOptions = null,
        PlcClientOptions? clientOptions = null)
    {
        var transportOptions = new SerialTransportOptions(portName, baudRate, parity, dataBits, stopBits);
        var transport = new SerialTransport(transportOptions);
        var options = modbusOptions ?? new ModbusAsciiOptions(slaveAddress);
        var protocol = new ModbusAsciiProtocol(transport, options);
        return new PlcClient(transport, protocol, clientOptions);
    }

    /// <summary>사용자 정의 전송을 사용하는 Modbus ASCII 클라이언트를 생성합니다.</summary>
    public static PlcClient CreateModbusAscii(
        ITransport transport,
        ModbusAsciiOptions? modbusOptions = null,
        PlcClientOptions? clientOptions = null)
    {
        var protocol = new ModbusAsciiProtocol(transport, modbusOptions);
        return new PlcClient(transport, protocol, clientOptions);
    }

    /// <summary>LS ELECTRIC XGT FEnet 프로토콜을 사용하는 PLC 클라이언트를 생성합니다.</summary>
    public static PlcClient CreateLsElectricFEnet(
        string host,
        int port = 2004,
        byte cpuInfo = 0xA0,
        FEnetOptions? fenetOptions = null,
        PlcClientOptions? clientOptions = null)
    {
        var transportOptions = new TcpTransportOptions(host, port);
        var transport = new TcpTransport(transportOptions);
        var options = fenetOptions ?? new FEnetOptions(cpuInfo) { Port = port };
        var protocol = new FEnetProtocol(transport, options);
        return new PlcClient(transport, protocol, clientOptions);
    }

    /// <summary>사용자 정의 전송을 사용하는 LS ELECTRIC XGT FEnet 클라이언트를 생성합니다.</summary>
    public static PlcClient CreateLsElectricFEnet(
        ITransport transport,
        FEnetOptions? fenetOptions = null,
        PlcClientOptions? clientOptions = null)
    {
        var protocol = new FEnetProtocol(transport, fenetOptions);
        return new PlcClient(transport, protocol, clientOptions);
    }

    /// <summary>Mitsubishi MC 프로토콜(바이너리)을 TCP로 사용하는 PLC 클라이언트를 생성합니다.</summary>
    public static PlcClient CreateMitsubishiMCBinary(
        string host,
        MCOptions? mcOptions = null,
        PlcClientOptions? clientOptions = null)
    {
        MCOptions options = mcOptions ?? new MCOptions();
        var transport = new TcpTransport(new TcpTransportOptions(host, options.Port));
        var protocol = new MCBinaryProtocol(transport, options);
        return new PlcClient(transport, protocol, clientOptions);
    }

    /// <summary>사용자 정의 전송을 사용하는 Mitsubishi MC 바이너리 클라이언트를 생성합니다.</summary>
    public static PlcClient CreateMitsubishiMCBinary(
        ITransport transport,
        MCOptions? mcOptions = null,
        PlcClientOptions? clientOptions = null)
    {
        var protocol = new MCBinaryProtocol(transport, mcOptions);
        return new PlcClient(transport, protocol, clientOptions);
    }

    /// <summary>Mitsubishi MC 프로토콜(ASCII)을 TCP로 사용하는 PLC 클라이언트를 생성합니다.</summary>
    public static PlcClient CreateMitsubishiMCAscii(
        string host,
        MCOptions? mcOptions = null,
        PlcClientOptions? clientOptions = null)
    {
        MCOptions options = mcOptions ?? new MCOptions();
        var transport = new TcpTransport(new TcpTransportOptions(host, options.Port));
        var protocol = new MCAsciiProtocol(transport, options);
        return new PlcClient(transport, protocol, clientOptions);
    }

    /// <summary>사용자 정의 전송을 사용하는 Mitsubishi MC ASCII 클라이언트를 생성합니다.</summary>
    public static PlcClient CreateMitsubishiMCAscii(
        ITransport transport,
        MCOptions? mcOptions = null,
        PlcClientOptions? clientOptions = null)
    {
        var protocol = new MCAsciiProtocol(transport, mcOptions);
        return new PlcClient(transport, protocol, clientOptions);
    }

    /// <summary>Siemens S7 통신(S7comm)을 TCP로 사용하는 PLC 클라이언트를 생성합니다. 실장비 검증 전입니다.</summary>
    public static PlcClient CreateSiemensS7Comm(
        string host,
        S7CommOptions? s7Options = null,
        PlcClientOptions? clientOptions = null)
    {
        S7CommOptions options = s7Options ?? new S7CommOptions();
        var transport = new TcpTransport(new TcpTransportOptions(host, options.Port));
        var protocol = new S7CommProtocol(transport, options);
        return new PlcClient(transport, protocol, clientOptions);
    }

    /// <summary>사용자 정의 전송을 사용하는 Siemens S7 통신 클라이언트를 생성합니다.</summary>
    public static PlcClient CreateSiemensS7Comm(
        ITransport transport,
        S7CommOptions? s7Options = null,
        PlcClientOptions? clientOptions = null)
    {
        var protocol = new S7CommProtocol(transport, s7Options);
        return new PlcClient(transport, protocol, clientOptions);
    }

    /// <summary>사용자 정의 전송을 사용하는 Delta PLC 클라이언트를 생성합니다.</summary>
    public static PlcClient CreateDelta(
        ITransport transport,
        DeltaOptions? options = null,
        DeltaTransportEncoding encoding = DeltaTransportEncoding.Rtu,
        PlcClientOptions? clientOptions = null)
    {
        var protocol = new DeltaProtocol(transport, options, encoding);
        return new PlcClient(transport, protocol, clientOptions);
    }

    /// <summary>Modbus RTU를 사용하는 Delta DVP 시리얼 클라이언트를 생성합니다.</summary>
    public static PlcClient CreateDeltaDvpRtu(
        string portName,
        int baudRate = 9600,
        Parity parity = Parity.Even,
        int dataBits = 8,
        StopBits stopBits = StopBits.One,
        byte station = 1,
        DeltaOptions? deltaOptions = null,
        PlcClientOptions? clientOptions = null)
    {
        var transportOptions = new SerialTransportOptions(portName, baudRate, parity, dataBits, stopBits);
        var transport = new SerialTransport(transportOptions);
        var options = deltaOptions ?? new DeltaOptions { Series = DeltaSeries.Dvp, Station = station };
        var protocol = new DeltaProtocol(transport, options, DeltaTransportEncoding.Rtu);
        return new PlcClient(transport, protocol, clientOptions);
    }

    /// <summary>Modbus TCP를 사용하는 Delta DVP 이더넷 클라이언트를 생성합니다.</summary>
    public static PlcClient CreateDeltaDvpTcp(
        string host,
        int port = 502,
        byte station = 1,
        DeltaOptions? deltaOptions = null,
        PlcClientOptions? clientOptions = null)
    {
        var transportOptions = new TcpTransportOptions(host, port);
        var transport = new TcpTransport(transportOptions);
        var options = deltaOptions ?? new DeltaOptions { Series = DeltaSeries.Dvp, Station = station };
        var protocol = new DeltaProtocol(transport, options, DeltaTransportEncoding.Tcp);
        return new PlcClient(transport, protocol, clientOptions);
    }

    /// <summary>Modbus TCP를 사용하는 Delta AS 시리즈 이더넷 클라이언트를 생성합니다.</summary>
    public static PlcClient CreateDeltaAsTcp(
        string host,
        int port = 502,
        byte station = 1,
        DeltaOptions? deltaOptions = null,
        PlcClientOptions? clientOptions = null)
    {
        var transportOptions = new TcpTransportOptions(host, port);
        var transport = new TcpTransport(transportOptions);
        var options = deltaOptions ?? new DeltaOptions { Series = DeltaSeries.AS, Station = station };
        var protocol = new DeltaProtocol(transport, options, DeltaTransportEncoding.Tcp);
        return new PlcClient(transport, protocol, clientOptions);
    }

    /// <summary>사용자 정의 전송을 사용하는 FATEK PLC 클라이언트를 생성합니다.</summary>
    public static PlcClient CreateFatek(
        ITransport transport,
        FatekOptions? options = null,
        PlcClientOptions? clientOptions = null)
    {
        var protocol = new FatekProtocol(transport, options);
        return new PlcClient(transport, protocol, clientOptions);
    }

    /// <summary>FATEK Program Port ASCII 시리얼 클라이언트를 생성합니다.</summary>
    public static PlcClient CreateFatekSerial(
        string portName,
        int baudRate = 9600,
        Parity parity = Parity.Even,
        int dataBits = 7,
        StopBits stopBits = StopBits.One,
        byte station = 1,
        FatekOptions? fatekOptions = null,
        PlcClientOptions? clientOptions = null)
    {
        var transportOptions = new SerialTransportOptions(portName, baudRate, parity, dataBits, stopBits);
        var transport = new SerialTransport(transportOptions);
        var options = fatekOptions ?? new FatekOptions { Station = station };
        var protocol = new FatekProtocol(transport, options);
        return new PlcClient(transport, protocol, clientOptions);
    }

    /// <summary>사용자 정의 전송을 사용하는 Fuji SPB 클라이언트를 생성합니다.</summary>
    public static PlcClient CreateFujiElectricSpb(
        ITransport transport,
        FujiElectricSpbOptions? options = null,
        PlcClientOptions? clientOptions = null)
    {
        var protocol = new FujiElectricSpbProtocol(transport, options);
        return new PlcClient(transport, protocol, clientOptions);
    }

    /// <summary>Fuji SPB 시리얼 클라이언트를 생성합니다.</summary>
    public static PlcClient CreateFujiElectricSpbSerial(
        string portName,
        int baudRate = 9600,
        Parity parity = Parity.Even,
        int dataBits = 8,
        StopBits stopBits = StopBits.One,
        byte station = 1,
        FujiElectricSpbOptions? fujiOptions = null,
        PlcClientOptions? clientOptions = null)
    {
        var transportOptions = new SerialTransportOptions(portName, baudRate, parity, dataBits, stopBits);
        var transport = new SerialTransport(transportOptions);
        var options = fujiOptions ?? new FujiElectricSpbOptions { Station = station };
        var protocol = new FujiElectricSpbProtocol(transport, options);
        return new PlcClient(transport, protocol, clientOptions);
    }

    /// <summary>사용자 정의 전송을 사용하는 Fuji MICREX-SX SPH 클라이언트를 생성합니다.</summary>
    public static PlcClient CreateFujiElectricSph(
        ITransport transport,
        FujiElectricSphOptions? options = null,
        PlcClientOptions? clientOptions = null)
    {
        var protocol = new FujiElectricSphProtocol(transport, options);
        return new PlcClient(transport, protocol, clientOptions);
    }

    /// <summary>MICREX-SX SPH Ethernet TCP 클라이언트를 생성합니다.</summary>
    public static PlcClient CreateFujiElectricSphTcp(
        string host,
        int port = 18245,
        byte connectionId = 1,
        FujiElectricSphOptions? fujiOptions = null,
        PlcClientOptions? clientOptions = null)
    {
        var transportOptions = new TcpTransportOptions(host, port);
        var transport = new TcpTransport(transportOptions);
        var options = fujiOptions ?? new FujiElectricSphOptions { ConnectionId = connectionId };
        var protocol = new FujiElectricSphProtocol(transport, options);
        return new PlcClient(transport, protocol, clientOptions);
    }

    /// <summary>사용자 정의 전송을 사용하는 GE Fanuc / Emerson SRTP 클라이언트를 생성합니다.</summary>
    public static PlcClient CreateGESrtp(
        ITransport transport,
        GESrtpOptions? options = null,
        PlcClientOptions? clientOptions = null)
    {
        var protocol = new GESrtpProtocol(transport, options);
        return new PlcClient(transport, protocol, clientOptions);
    }

    /// <summary>GE SRTP TCP 클라이언트를 생성합니다. (기본 포트 18245)</summary>
    public static PlcClient CreateGESrtpTcp(
        string host,
        int port = 18245,
        GESrtpOptions? gesrtpOptions = null,
        PlcClientOptions? clientOptions = null)
    {
        var transportOptions = new TcpTransportOptions(host, port);
        var transport = new TcpTransport(transportOptions);
        var protocol = new GESrtpProtocol(transport, gesrtpOptions);
        return new PlcClient(transport, protocol, clientOptions);
    }

    /// <summary>사용자 정의 전송을 사용하는 Inovance PLC 클라이언트를 생성합니다.</summary>
    public static PlcClient CreateInovance(
        ITransport transport,
        InovanceOptions? options = null,
        InovanceTransportEncoding encoding = InovanceTransportEncoding.Tcp,
        PlcClientOptions? clientOptions = null)
    {
        var protocol = new InovanceProtocol(transport, options, encoding);
        return new PlcClient(transport, protocol, clientOptions);
    }

    /// <summary>Modbus TCP를 사용하는 Inovance 이더넷 클라이언트를 생성합니다.</summary>
    public static PlcClient CreateInovanceTcp(
        string host,
        int port = 502,
        byte station = 1,
        InovanceSeries series = InovanceSeries.H3u,
        InovanceOptions? inovanceOptions = null,
        PlcClientOptions? clientOptions = null)
    {
        var transportOptions = new TcpTransportOptions(host, port);
        var transport = new TcpTransport(transportOptions);
        var options = inovanceOptions ?? new InovanceOptions { Series = series, Station = station };
        var protocol = new InovanceProtocol(transport, options, InovanceTransportEncoding.Tcp);
        return new PlcClient(transport, protocol, clientOptions);
    }

    /// <summary>Modbus RTU를 사용하는 Inovance 시리얼 클라이언트를 생성합니다.</summary>
    public static PlcClient CreateInovanceRtu(
        string portName,
        int baudRate = 9600,
        Parity parity = Parity.Even,
        int dataBits = 8,
        StopBits stopBits = StopBits.One,
        byte station = 1,
        InovanceSeries series = InovanceSeries.H3u,
        InovanceOptions? inovanceOptions = null,
        PlcClientOptions? clientOptions = null)
    {
        var transportOptions = new SerialTransportOptions(portName, baudRate, parity, dataBits, stopBits);
        var transport = new SerialTransport(transportOptions);
        var options = inovanceOptions ?? new InovanceOptions { Series = series, Station = station };
        var protocol = new InovanceProtocol(transport, options, InovanceTransportEncoding.Rtu);
        return new PlcClient(transport, protocol, clientOptions);
    }

    /// <summary>사용자 정의 전송을 사용하는 Keyence KV 상위 링크(Host Link) 클라이언트를 생성합니다.</summary>
    public static PlcClient CreateKeyenceHostLink(
        ITransport transport,
        KeyenceHostLinkOptions? options = null,
        PlcClientOptions? clientOptions = null)
    {
        var protocol = new KeyenceHostLinkProtocol(transport, options);
        return new PlcClient(transport, protocol, clientOptions);
    }

    /// <summary>Keyence KV 상위 링크 TCP 클라이언트를 생성합니다. (기본 포트 8501)</summary>
    public static PlcClient CreateKeyenceHostLinkTcp(
        string host,
        int port = 8501,
        KeyenceHostLinkOptions? options = null,
        PlcClientOptions? clientOptions = null)
    {
        var transportOptions = new TcpTransportOptions(host, port);
        var transport = new TcpTransport(transportOptions);
        var protocol = new KeyenceHostLinkProtocol(transport, options);
        return new PlcClient(transport, protocol, clientOptions);
    }

    /// <summary>Keyence KV 상위 링크 시리얼 클라이언트를 생성합니다.</summary>
    public static PlcClient CreateKeyenceHostLinkSerial(
        string portName,
        int baudRate = 9600,
        Parity parity = Parity.Even,
        int dataBits = 8,
        StopBits stopBits = StopBits.One,
        byte? station = null,
        KeyenceHostLinkOptions? hostLinkOptions = null,
        PlcClientOptions? clientOptions = null)
    {
        var transportOptions = new SerialTransportOptions(portName, baudRate, parity, dataBits, stopBits);
        var transport = new SerialTransport(transportOptions);
        var options = hostLinkOptions ?? new KeyenceHostLinkOptions { Station = station };
        var protocol = new KeyenceHostLinkProtocol(transport, options);
        return new PlcClient(transport, protocol, clientOptions);
    }

    /// <summary>사용자 정의 전송을 사용하는 Megmeet PLC 클라이언트를 생성합니다.</summary>
    public static PlcClient CreateMegmeet(
        ITransport transport,
        MegmeetOptions? options = null,
        MegmeetTransportEncoding encoding = MegmeetTransportEncoding.Rtu,
        PlcClientOptions? clientOptions = null)
    {
        var protocol = new MegmeetProtocol(transport, options, encoding);
        return new PlcClient(transport, protocol, clientOptions);
    }

    /// <summary>Modbus RTU를 사용하는 Megmeet 시리얼 클라이언트를 생성합니다.</summary>
    public static PlcClient CreateMegmeetRtu(
        string portName,
        int baudRate = 9600,
        Parity parity = Parity.Even,
        int dataBits = 8,
        StopBits stopBits = StopBits.One,
        byte station = 1,
        MegmeetOptions? megmeetOptions = null,
        PlcClientOptions? clientOptions = null)
    {
        var transportOptions = new SerialTransportOptions(portName, baudRate, parity, dataBits, stopBits);
        var transport = new SerialTransport(transportOptions);
        var options = megmeetOptions ?? new MegmeetOptions { Station = station };
        var protocol = new MegmeetProtocol(transport, options, MegmeetTransportEncoding.Rtu);
        return new PlcClient(transport, protocol, clientOptions);
    }

    /// <summary>Modbus TCP를 사용하는 Megmeet 이더넷 클라이언트를 생성합니다.</summary>
    public static PlcClient CreateMegmeetTcp(
        string host,
        int port = 502,
        byte station = 1,
        MegmeetOptions? megmeetOptions = null,
        PlcClientOptions? clientOptions = null)
    {
        var transportOptions = new TcpTransportOptions(host, port);
        var transport = new TcpTransport(transportOptions);
        var options = megmeetOptions ?? new MegmeetOptions { Station = station };
        var protocol = new MegmeetProtocol(transport, options, MegmeetTransportEncoding.Tcp);
        return new PlcClient(transport, protocol, clientOptions);
    }

    /// <summary>사용자 정의 전송을 사용하는 Panasonic MEWTOCOL-COM 클라이언트를 생성합니다.</summary>
    public static PlcClient CreatePanasonicMewtocol(
        ITransport transport,
        MewtocolOptions? options = null,
        PlcClientOptions? clientOptions = null)
    {
        var protocol = new MewtocolProtocol(transport, options);
        return new PlcClient(transport, protocol, clientOptions);
    }

    /// <summary>Panasonic MEWTOCOL-COM 시리얼 클라이언트를 생성합니다.</summary>
    public static PlcClient CreatePanasonicMewtocolSerial(
        string portName,
        int baudRate = 9600,
        Parity parity = Parity.Odd,
        int dataBits = 8,
        StopBits stopBits = StopBits.One,
        byte station = 0xEE,
        MewtocolOptions? mewtocolOptions = null,
        PlcClientOptions? clientOptions = null)
    {
        var transportOptions = new SerialTransportOptions(portName, baudRate, parity, dataBits, stopBits);
        var transport = new SerialTransport(transportOptions);
        var options = mewtocolOptions ?? new MewtocolOptions { Station = station };
        var protocol = new MewtocolProtocol(transport, options);
        return new PlcClient(transport, protocol, clientOptions);
    }

    /// <summary>Panasonic MEWTOCOL-COM TCP 클라이언트를 생성합니다.</summary>
    public static PlcClient CreatePanasonicMewtocolTcp(
        string host,
        int port = 9094,
        byte station = 0xEE,
        MewtocolOptions? mewtocolOptions = null,
        PlcClientOptions? clientOptions = null)
    {
        var transportOptions = new TcpTransportOptions(host, port);
        var transport = new TcpTransport(transportOptions);
        var options = mewtocolOptions ?? new MewtocolOptions { Station = station };
        var protocol = new MewtocolProtocol(transport, options);
        return new PlcClient(transport, protocol, clientOptions);
    }

    /// <summary>사용자 정의 전송을 사용하는 Toyota / JTEKT TOYOPUC 클라이언트를 생성합니다.</summary>
    public static PlcClient CreateJtektToyopuc(
        ITransport transport,
        ToyopucOptions? options = null,
        PlcClientOptions? clientOptions = null)
    {
        var protocol = new ToyopucProtocol(transport, options);
        return new PlcClient(transport, protocol, clientOptions);
    }

    /// <summary>Toyota / JTEKT TOYOPUC 2PORT-EFR TCP 클라이언트를 생성합니다. (기본 포트 10255)</summary>
    public static PlcClient CreateJtektToyopucTcp(
        string host,
        int port = 10255,
        ToyopucOptions? options = null,
        PlcClientOptions? clientOptions = null)
    {
        var transportOptions = new TcpTransportOptions(host, port);
        var transport = new TcpTransport(transportOptions);
        var protocol = new ToyopucProtocol(transport, options);
        return new PlcClient(transport, protocol, clientOptions);
    }

    /// <summary>사용자 정의 전송을 사용하는 VIGOR VS 계열 클라이언트를 생성합니다.</summary>
    public static PlcClient CreateVigor(
        ITransport transport,
        VigorOptions? options = null,
        PlcClientOptions? clientOptions = null)
    {
        var protocol = new VigorProtocol(transport, options);
        return new PlcClient(transport, protocol, clientOptions);
    }

    /// <summary>시리얼 포트를 사용하는 VIGOR VS 계열 클라이언트를 생성합니다.</summary>
    public static PlcClient CreateVigorSerial(
        string portName,
        int baudRate = 19200,
        byte station = 1,
        VigorOptions? vigorOptions = null,
        PlcClientOptions? clientOptions = null)
    {
        var serialOptions = new SerialTransportOptions(portName, baudRate, Parity.Even, 7, StopBits.One);
        var transport = new SerialTransport(serialOptions);
        var options = vigorOptions ?? new VigorOptions { StationNumber = station };
        var protocol = new VigorProtocol(transport, options);
        return new PlcClient(transport, protocol, clientOptions);
    }

    /// <summary>사용자 정의 전송을 사용하는 XINJE PLC 클라이언트를 생성합니다.</summary>
    public static PlcClient CreateXinje(
        ITransport transport,
        XinjeOptions? options = null,
        XinjeTransportEncoding encoding = XinjeTransportEncoding.Rtu,
        PlcClientOptions? clientOptions = null)
    {
        var protocol = new XinjeProtocol(transport, options, encoding);
        return new PlcClient(transport, protocol, clientOptions);
    }

    /// <summary>Modbus RTU 시리얼 통신을 사용하는 XINJE PLC 클라이언트를 생성합니다.</summary>
    public static PlcClient CreateXinjeRtu(
        string portName,
        int baudRate = 19200,
        byte station = 1,
        XinjeOptions? xinjeOptions = null,
        PlcClientOptions? clientOptions = null)
    {
        var serialOptions = new SerialTransportOptions(portName, baudRate, Parity.Even, 8, StopBits.One);
        var transport = new SerialTransport(serialOptions);
        var options = xinjeOptions ?? new XinjeOptions { Station = station };
        var protocol = new XinjeProtocol(transport, options, XinjeTransportEncoding.Rtu);
        return new PlcClient(transport, protocol, clientOptions);
    }

    /// <summary>Modbus TCP 이더넷 통신을 사용하는 XINJE PLC 클라이언트를 생성합니다. (포트 502)</summary>
    public static PlcClient CreateXinjeTcp(
        string host,
        int port = 502,
        byte station = 1,
        XinjeOptions? xinjeOptions = null,
        PlcClientOptions? clientOptions = null)
    {
        var transportOptions = new TcpTransportOptions(host, port);
        var transport = new TcpTransport(transportOptions);
        var options = xinjeOptions ?? new XinjeOptions { Station = station };
        var protocol = new XinjeProtocol(transport, options, XinjeTransportEncoding.Tcp);
        return new PlcClient(transport, protocol, clientOptions);
    }

    /// <summary>사용자 정의 전송을 사용하는 Yokogawa FA-M3 클라이언트를 생성합니다.</summary>
    public static PlcClient CreateYokogawa(
        ITransport transport,
        YokogawaOptions? options = null,
        PlcClientOptions? clientOptions = null)
    {
        var protocol = new YokogawaProtocol(transport, options);
        return new PlcClient(transport, protocol, clientOptions);
    }

    /// <summary>TCP 이더넷 통신을 사용하는 Yokogawa FA-M3 클라이언트를 생성합니다. (기본 포트 5001)</summary>
    public static PlcClient CreateYokogawaTcp(
        string host,
        int port = 5001,
        byte cpuNumber = 1,
        YokogawaOptions? yokogawaOptions = null,
        PlcClientOptions? clientOptions = null)
    {
        var transportOptions = new TcpTransportOptions(host, port);
        var transport = new TcpTransport(transportOptions);
        var options = yokogawaOptions ?? new YokogawaOptions { CpuNumber = cpuNumber };
        var protocol = new YokogawaProtocol(transport, options);
        return new PlcClient(transport, protocol, clientOptions);
    }
}
