using FieldLink.PlcDrivers.Modbus.Clients;
using FieldLink.Communication;
using FieldLink.PlcDrivers.Common.Clients;
using FieldLink.PlcDrivers.Common;
using FieldLink.PlcDrivers.XINJE;
using System;

namespace FieldLink.PlcDrivers.XINJE.Clients
{
    /// <summary>XC·XD·XL 주소를 Modbus RTU로 교환합니다.</summary>
    public sealed class XinJESerialClient : ModbusSerialClient
    {
        /// <summary>연결된 포트와 PLC 시리즈를 지정합니다.</summary>
        public XinJESerialClient(ISerialClient transport, XinJESeries series = XinJESeries.XC, byte station = 1, TimeSpan? timeout = null)
            : base(transport, station: station, timeout: timeout, addressMapping: (address, function) => XinjeAddressParser.PraseXinJEAddress(series, address, function))
        {
            if (!Enum.IsDefined(typeof(XinJESeries), series))
                throw new ArgumentOutOfRangeException(nameof(series));
            Series = series;
        }
        /// <summary>주소 변환에 사용하는 시리즈입니다.</summary>
        public XinJESeries Series { get; }
    }
}
