using System;
using System.Net;
using FieldLink.Communication.Tcp;
using FieldLink.Communication;
using FieldLink.Communication.Framing;
using FieldLink.PlcDrivers.Common;
using FieldLink.PlcDrivers.Common.Clients;
using FieldLink.PlcDrivers.Modbus;
using FieldLink.PlcDrivers.Modbus.Clients;
using FieldLink.PlcDrivers.Melsec;
using FieldLink.PlcDrivers.Melsec.Clients;

namespace FieldLink.PlcDrivers
{
    /// <summary>장치에 맞는 전송과 프로토콜을 조립합니다. 생성만으로 연결하거나 요청을 보내지 않습니다.</summary>
    public static partial class Plc
    {
        /// <summary>전송을 소유하는 MC 3E Binary TCP 클라이언트를 만듭니다. IP 리터럴과 PLC에 설정한 포트를 지정하세요.</summary>
        public static MelsecMcClient MelsecMcTcp(string ipAddress, int port, McClientOptions options = null)
            => CreateMc(ipAddress, port, options, McDeviceAddress.ParseMelsecFrom);

        /// <summary>KEYENCE 주소(DM100, MR100 등)를 사용하는 MC 3E Binary TCP 클라이언트를 만듭니다.</summary>
        public static MelsecMcClient KeyenceMcTcp(string ipAddress, int port, McClientOptions options = null)
            => CreateMc(ipAddress, port, options, McDeviceAddress.ParseKeyenceFrom);

        private static MelsecMcClient CreateMc(string ipAddress, int port, McClientOptions options,
            Func<string, ushort, bool, OperationResult<McDeviceAddress>> parseAddress)
        {
            if (ipAddress == null)
                throw new ArgumentNullException(nameof(ipAddress));
            if (!IPAddress.TryParse(ipAddress, out IPAddress address))
                throw new ArgumentException("IPv4 또는 IPv6 주소가 필요합니다.", nameof(ipAddress));
            if (port < 1 || port > 65535)
                throw new ArgumentOutOfRangeException(nameof(port));
            var transport = new TcpClient(new IPEndPoint(address, port), new MelsecMc3EBinaryFrame());
            try
            {
                return new MelsecMcClient(transport, options, true, parseAddress);
            }
            catch
            {
                transport.Dispose();
                throw;
            }
        }

        /// <summary>Modbus TCP의 레지스터·코일을 자료형 그대로 읽고 쓰는 클라이언트를 만듭니다.</summary>
        public static PlcClient ModbusTcp(string ipAddress, int port = 502, ModbusTcpClientOptions options = null)
        {
            options = options ?? new ModbusTcpClientOptions();
            return Create(ipAddress, port, ModbusTcpDriver.Frame, t => new ModbusTcpDriver(t, options), options, ByteOrder.BigEndian);
        }

        private static PlcClient Create(string ipAddress, int port, IFrameBoundary boundary,
            Func<ITcpClient, IPlcDriver> createDriver, PlcClientOptions options, ByteOrder defaultOrder)
        {
            if (ipAddress == null)
                throw new ArgumentNullException(nameof(ipAddress));
            if (!IPAddress.TryParse(ipAddress, out IPAddress address))
                throw new ArgumentException("IPv4 또는 IPv6 주소가 필요합니다.", nameof(ipAddress));
            if (port < 1 || port > 65535)
                throw new ArgumentOutOfRangeException(nameof(port));
            var transport = new TcpClient(new IPEndPoint(address, port), boundary);
            try { return new PlcClient(transport, createDriver(transport), options, defaultOrder); }
            catch { transport.Dispose(); throw; }
        }
    }
}
