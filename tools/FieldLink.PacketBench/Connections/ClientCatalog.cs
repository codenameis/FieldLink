using System.Collections.Generic;
using System.IO.Ports;
using FieldLink.Plc;
using FieldLink.Plc.Protocols.Delta;
using FieldLink.Plc.Protocols.Fatek;
using FieldLink.Plc.Protocols.FujiElectric;
using FieldLink.Plc.Protocols.Inovance;
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

namespace FieldLink.PacketBench.Connections;

/// <summary>화면에서 고를 수 있는 클라이언트 목록입니다. 항목을 추가하려면 여기에 한 줄만 넣으면 됩니다.</summary>
internal static class ClientCatalog
{
    private const int ModbusTcpPort = 502;

    public static IReadOnlyList<ClientDescriptor> All { get; } = CreateAll();

    private static List<ClientDescriptor> CreateAll() => new()
    {
        // ---- Modbus 계열 ----
        new ClientDescriptor(
            "modbus-tcp",
            "Modbus TCP",
            TransportKind.Tcp,
            ModbusTcpPort,
            usesStation: true,
            "40001 (보유) · 30001 (입력) · 00001 (코일) · 10001 (이산)",
            (transport, settings) => PlcClientFactory.CreateModbusTcp(
                transport,
                new ModbusTcpOptions(settings.Station),
                settings.CreateClientOptions())),

        new ClientDescriptor(
            "modbus-rtu",
            "Modbus RTU",
            TransportKind.Serial,
            0,
            usesStation: true,
            "40001 (보유) · 00001 (코일)",
            (transport, settings) => PlcClientFactory.CreateModbusRtu(
                transport,
                new ModbusRtuOptions(settings.Station),
                settings.CreateClientOptions()))
        {
            DefaultBaudRate = 9600,
            DefaultParity = Parity.Even,
            DefaultDataBits = 8,
        },

        new ClientDescriptor(
            "modbus-ascii",
            "Modbus ASCII",
            TransportKind.Serial,
            0,
            usesStation: true,
            "40001 (보유) · 00001 (코일)",
            (transport, settings) => PlcClientFactory.CreateModbusAscii(
                transport,
                new ModbusAsciiOptions(settings.Station),
                settings.CreateClientOptions()))
        {
            DefaultBaudRate = 9600,
            DefaultParity = Parity.Even,
            DefaultDataBits = 7,
        },

        // ---- LS ELECTRIC ----
        new ClientDescriptor(
            "ls-fenet",
            "LS ELECTRIC XGT FEnet",
            TransportKind.Tcp,
            2004,
            usesStation: false,
            "%DW100 (워드) · %MW100 · %MX0 (비트)",
            (transport, settings) => PlcClientFactory.CreateLsElectricFEnet(
                transport,
                new FEnetOptions { Port = settings.Port },
                settings.CreateClientOptions())),

        // ---- Mitsubishi ----
        new ClientDescriptor(
            "mc-binary",
            "Mitsubishi MC 3E (Binary)",
            TransportKind.Tcp,
            5000,
            usesStation: false,
            "D100 · M100 · X1F · ZR1000",
            (transport, settings) => PlcClientFactory.CreateMitsubishiMCBinary(
                transport,
                new MCOptions { Port = settings.Port },
                settings.CreateClientOptions())),

        new ClientDescriptor(
            "mc-ascii",
            "Mitsubishi MC 3E (ASCII)",
            TransportKind.Tcp,
            5000,
            usesStation: false,
            "D100 · M100",
            (transport, settings) => PlcClientFactory.CreateMitsubishiMCAscii(
                transport,
                new MCOptions { Port = settings.Port },
                settings.CreateClientOptions())),

        // ---- Siemens ----
        new ClientDescriptor(
            "s7comm",
            "Siemens S7comm",
            TransportKind.Tcp,
            102,
            usesStation: false,
            "DB1.DBW10 · M10.0 · IB0",
            (transport, settings) => PlcClientFactory.CreateSiemensS7Comm(
                transport,
                new S7CommOptions { Port = settings.Port },
                settings.CreateClientOptions())),

        // ---- Delta ----
        new ClientDescriptor(
            "delta-dvp-tcp",
            "Delta DVP (Modbus TCP)",
            TransportKind.Tcp,
            ModbusTcpPort,
            usesStation: true,
            "D100 · M100",
            (transport, settings) => PlcClientFactory.CreateDelta(
                transport,
                new DeltaOptions { Series = DeltaSeries.Dvp, Station = settings.Station },
                DeltaTransportEncoding.Tcp,
                settings.CreateClientOptions())),

        new ClientDescriptor(
            "delta-as-tcp",
            "Delta AS (Modbus TCP)",
            TransportKind.Tcp,
            ModbusTcpPort,
            usesStation: true,
            "D100 · M100",
            (transport, settings) => PlcClientFactory.CreateDelta(
                transport,
                new DeltaOptions { Series = DeltaSeries.AS, Station = settings.Station },
                DeltaTransportEncoding.Tcp,
                settings.CreateClientOptions())),

        new ClientDescriptor(
            "delta-dvp-rtu",
            "Delta DVP (Modbus RTU)",
            TransportKind.Serial,
            0,
            usesStation: true,
            "D100 · M100",
            (transport, settings) => PlcClientFactory.CreateDelta(
                transport,
                new DeltaOptions { Series = DeltaSeries.Dvp, Station = settings.Station },
                DeltaTransportEncoding.Rtu,
                settings.CreateClientOptions()))
        {
            DefaultBaudRate = 9600,
            DefaultParity = Parity.Even,
            DefaultDataBits = 8,
        },

        // ---- FATEK ----
        new ClientDescriptor(
            "fatek-serial",
            "FATEK Program Port",
            TransportKind.Serial,
            0,
            usesStation: true,
            "D100 · M100 · R100",
            (transport, settings) => PlcClientFactory.CreateFatek(
                transport,
                new FatekOptions { Station = settings.Station },
                settings.CreateClientOptions()))
        {
            DefaultBaudRate = 9600,
            DefaultParity = Parity.Even,
            DefaultDataBits = 7,
        },

        // ---- Fuji Electric ----
        new ClientDescriptor(
            "fuji-spb",
            "Fuji MICREX-F SPB",
            TransportKind.Serial,
            0,
            usesStation: true,
            "D100 · M100",
            (transport, settings) => PlcClientFactory.CreateFujiElectricSpb(
                transport,
                new FujiElectricSpbOptions { Station = settings.Station },
                settings.CreateClientOptions()))
        {
            DefaultBaudRate = 9600,
            DefaultParity = Parity.Even,
            DefaultDataBits = 8,
        },

        new ClientDescriptor(
            "fuji-sph",
            "Fuji MICREX-SX SPH (TCP)",
            TransportKind.Tcp,
            18245,
            usesStation: true,
            "D100 · M100",
            (transport, settings) => PlcClientFactory.CreateFujiElectricSph(
                transport,
                new FujiElectricSphOptions { ConnectionId = settings.Station },
                settings.CreateClientOptions())),

        // ---- GE ----
        new ClientDescriptor(
            "ge-srtp",
            "GE Fanuc SRTP",
            TransportKind.Tcp,
            18245,
            usesStation: false,
            "%R100 · %M10 · %I1",
            (transport, settings) => PlcClientFactory.CreateGESrtp(
                transport,
                null,
                settings.CreateClientOptions())),

        // ---- Inovance ----
        new ClientDescriptor(
            "inovance-tcp",
            "Inovance (Modbus TCP)",
            TransportKind.Tcp,
            ModbusTcpPort,
            usesStation: true,
            "D100 · M100",
            (transport, settings) => PlcClientFactory.CreateInovance(
                transport,
                new InovanceOptions { Station = settings.Station },
                InovanceTransportEncoding.Tcp,
                settings.CreateClientOptions())),

        new ClientDescriptor(
            "inovance-rtu",
            "Inovance (Modbus RTU)",
            TransportKind.Serial,
            0,
            usesStation: true,
            "D100 · M100",
            (transport, settings) => PlcClientFactory.CreateInovance(
                transport,
                new InovanceOptions { Station = settings.Station },
                InovanceTransportEncoding.Rtu,
                settings.CreateClientOptions()))
        {
            DefaultBaudRate = 9600,
            DefaultParity = Parity.Even,
            DefaultDataBits = 8,
        },

        // ---- Keyence ----
        new ClientDescriptor(
            "keyence-tcp",
            "Keyence KV Host Link (TCP)",
            TransportKind.Tcp,
            8501,
            usesStation: true,
            "DM100 · MR100",
            (transport, settings) => PlcClientFactory.CreateKeyenceHostLink(
                transport,
                new KeyenceHostLinkOptions { Station = settings.Station },
                settings.CreateClientOptions())),

        new ClientDescriptor(
            "keyence-serial",
            "Keyence KV Host Link (Serial)",
            TransportKind.Serial,
            0,
            usesStation: true,
            "DM100 · MR100",
            (transport, settings) => PlcClientFactory.CreateKeyenceHostLink(
                transport,
                new KeyenceHostLinkOptions { Station = settings.Station },
                settings.CreateClientOptions()))
        {
            DefaultBaudRate = 9600,
            DefaultParity = Parity.Even,
            DefaultDataBits = 8,
        },

        // ---- Megmeet ----
        new ClientDescriptor(
            "megmeet-tcp",
            "Megmeet (Modbus TCP)",
            TransportKind.Tcp,
            ModbusTcpPort,
            usesStation: true,
            "D100 · M100",
            (transport, settings) => PlcClientFactory.CreateMegmeet(
                transport,
                new MegmeetOptions { Station = settings.Station },
                MegmeetTransportEncoding.Tcp,
                settings.CreateClientOptions())),

        new ClientDescriptor(
            "megmeet-rtu",
            "Megmeet (Modbus RTU)",
            TransportKind.Serial,
            0,
            usesStation: true,
            "D100 · M100",
            (transport, settings) => PlcClientFactory.CreateMegmeet(
                transport,
                new MegmeetOptions { Station = settings.Station },
                MegmeetTransportEncoding.Rtu,
                settings.CreateClientOptions()))
        {
            DefaultBaudRate = 9600,
            DefaultParity = Parity.Even,
            DefaultDataBits = 8,
        },

        // ---- Panasonic ----
        new ClientDescriptor(
            "mewtocol-tcp",
            "Panasonic MEWTOCOL (TCP)",
            TransportKind.Tcp,
            9094,
            usesStation: true,
            "D100 · R100 · X1",
            (transport, settings) => PlcClientFactory.CreatePanasonicMewtocol(
                transport,
                new MewtocolOptions { Station = settings.Station },
                settings.CreateClientOptions())),

        new ClientDescriptor(
            "mewtocol-serial",
            "Panasonic MEWTOCOL (Serial)",
            TransportKind.Serial,
            0,
            usesStation: true,
            "D100 · R100 · X1",
            (transport, settings) => PlcClientFactory.CreatePanasonicMewtocol(
                transport,
                new MewtocolOptions { Station = settings.Station },
                settings.CreateClientOptions()))
        {
            DefaultBaudRate = 9600,
            DefaultParity = Parity.Odd,
            DefaultDataBits = 8,
        },

        // ---- JTEKT ----
        new ClientDescriptor(
            "toyopuc-tcp",
            "JTEKT TOYOPUC (TCP)",
            TransportKind.Tcp,
            10255,
            usesStation: false,
            "D100 · M100",
            (transport, settings) => PlcClientFactory.CreateJtektToyopuc(
                transport,
                null,
                settings.CreateClientOptions())),

        // ---- VIGOR ----
        new ClientDescriptor(
            "vigor-serial",
            "VIGOR VS (Serial)",
            TransportKind.Serial,
            0,
            usesStation: true,
            "D100 · M100",
            (transport, settings) => PlcClientFactory.CreateVigor(
                transport,
                new VigorOptions { StationNumber = settings.Station },
                settings.CreateClientOptions()))
        {
            DefaultBaudRate = 19200,
            DefaultParity = Parity.Even,
            DefaultDataBits = 7,
        },

        // ---- XINJE ----
        new ClientDescriptor(
            "xinje-tcp",
            "XINJE (Modbus TCP)",
            TransportKind.Tcp,
            ModbusTcpPort,
            usesStation: true,
            "D100 · M100 · HD100",
            (transport, settings) => PlcClientFactory.CreateXinje(
                transport,
                new XinjeOptions { Station = settings.Station },
                XinjeTransportEncoding.Tcp,
                settings.CreateClientOptions())),

        new ClientDescriptor(
            "xinje-rtu",
            "XINJE (Modbus RTU)",
            TransportKind.Serial,
            0,
            usesStation: true,
            "D100 · M100 · HD100",
            (transport, settings) => PlcClientFactory.CreateXinje(
                transport,
                new XinjeOptions { Station = settings.Station },
                XinjeTransportEncoding.Rtu,
                settings.CreateClientOptions()))
        {
            DefaultBaudRate = 19200,
            DefaultParity = Parity.Even,
            DefaultDataBits = 8,
        },

        // ---- Yokogawa ----
        new ClientDescriptor(
            "yokogawa-tcp",
            "Yokogawa FA-M3 (TCP)",
            TransportKind.Tcp,
            5001,
            usesStation: true,
            "D100 · X1 · Y1",
            (transport, settings) => PlcClientFactory.CreateYokogawa(
                transport,
                new YokogawaOptions { CpuNumber = System.Math.Clamp(settings.Station, (byte)1, (byte)4) },
                settings.CreateClientOptions())),
    };
}
