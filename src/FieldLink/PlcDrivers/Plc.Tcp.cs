using FieldLink.PlcDrivers.Common;
using FieldLink.PlcDrivers.Omron;
using FieldLink.PlcDrivers.Omron.Clients;
using FieldLink.PlcDrivers.Siemens;
using FieldLink.PlcDrivers.Siemens.Clients;
using FieldLink.PlcDrivers.LSIS;
using FieldLink.PlcDrivers.LSIS.Clients;
using FieldLink.PlcDrivers.AllenBradley;
using FieldLink.PlcDrivers.AllenBradley.Clients;

namespace FieldLink.PlcDrivers
{
    public static partial class Plc
    {
        /// <summary>Logix 태그를 읽고 쓰는 EtherNet/IP 비연결 CIP 클라이언트를 만듭니다. Open에서 세션을 등록합니다.</summary>
        public static PlcClient AllenBradleyTcp(string ipAddress, int port = 44818, AllenBradleyTcpClientOptions options = null)
        {
            options = options ?? new AllenBradleyTcpClientOptions();
            return Create(ipAddress, port, AllenBradleyCipDriver.Frame, t => new AllenBradleyCipDriver(t, options), options, ByteOrder.LittleEndian);
        }

        /// <summary>LS ELECTRIC XGT Fast Enet TCP의 자료형·연속 전송을 처리하는 클라이언트를 만듭니다.</summary>
        public static PlcClient LsFastEnetTcp(string ipAddress, int port = 2004, LsFastEnetClientOptions options = null)
        {
            options = options ?? new LsFastEnetClientOptions();
            return Create(ipAddress, port, LsFastEnetDriver.Frame, t => new LsFastEnetDriver(t, options), options, ByteOrder.LittleEndian);
        }

        /// <summary>노드 협상·SID·주소 변환을 처리하는 OMRON FINS/TCP 클라이언트를 만듭니다.</summary>
        public static PlcClient OmronFinsTcp(string ipAddress, int port = 9600, FinsTcpClientOptions options = null)
        {
            options = options ?? new FinsTcpClientOptions();
            return Create(ipAddress, port, new FinsTcpFrame(), t => new FinsTcpDriver(t, options), options, ByteOrder.LittleEndianWithByteSwap);
        }

        /// <summary>COTP·PDU 협상과 DB/M/I/Q 자료형 접근을 처리하는 Siemens S7 TCP 클라이언트를 만듭니다.</summary>
        public static PlcClient SiemensS7Tcp(string ipAddress, int port = 102, SiemensS7ClientOptions options = null)
        {
            options = options ?? new SiemensS7ClientOptions();
            return Create(ipAddress, port, SiemensS7Driver.Frame, t => new SiemensS7Driver(t, options), options, ByteOrder.BigEndian);
        }
    }
}
