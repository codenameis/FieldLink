using FieldLink.PlcDrivers.Common;
using System;
using System.Text;

namespace FieldLink.PlcDrivers.Modbus
{
    /// <summary>Modbus 국번·기능과 메모리 주소를 보관합니다.</summary>
    public class ModbusAddress : DeviceAddress
    {
        /// <summary>기본 옵션 또는 지정한 국번·기능으로 주소를 해석합니다.</summary>
        public ModbusAddress()
        {
            Station = -1;
            Function = -1;
            WriteFunction = -1;
            AddressStart = 0;
        }

        /// <summary>기본 옵션 또는 지정한 국번·기능으로 주소를 해석합니다.</summary>
        public ModbusAddress(string address)
        {
            Station = -1;
            Function = -1;
            WriteFunction = -1;
            AddressStart = 0;
            Parse(address, 1);
        }

        /// <summary>기본 옵션 또는 지정한 국번·기능으로 주소를 해석합니다.</summary>
        public ModbusAddress(string address, byte function)
        {
            Station = -1;
            WriteFunction = -1;
            Function = function;
            AddressStart = 0;
            Parse(address, 1);
        }

        /// <summary>기본 옵션 또는 지정한 국번·기능으로 주소를 해석합니다.</summary>
        public ModbusAddress(string address, byte station, byte function)
        {
            WriteFunction = -1;
            Function = function;
            Station = station;
            AddressStart = 0;
            Parse(address, 1);
        }

        /// <summary>주소에 지정된 국번입니다. 지정하지 않으면 -1입니다.</summary>
        public int Station { get; set; }
        /// <summary>주소에 지정된 읽기 기능 코드입니다.</summary>
        public int Function { get; set; }
        /// <summary>주소에 지정된 쓰기 기능 코드입니다.</summary>
        public int WriteFunction { get; set; }

        /// <summary>s=국번;x=기능;w=쓰기기능;주소 형식을 해석합니다.</summary>
        public override void Parse(string address, ushort length)
        {
            this.Length = length;
            if (address.IndexOf(';') < 0)
                this.AddressStart = ushort.Parse(address); // 읽기bool은 기본 기능 코드 01, 다른 타입을 읽기, 기능 코드 03입니다.
            else
            {
                string[] list = address.Split(';');
                for (int i = 0; i < list.Length; i++)
                {
                    if (list[i].StartsWith("s=", StringComparison.OrdinalIgnoreCase))
                        this.Station = byte.Parse(list[i].Substring(2)); // 국번 정보
                    else if (list[i].StartsWith("x=", StringComparison.OrdinalIgnoreCase))
                        this.Function = byte.Parse(list[i].Substring(2));
                    else if (list[i].StartsWith("w=", StringComparison.OrdinalIgnoreCase))
                        this.WriteFunction = byte.Parse(list[i].Substring(2));
                    else
                        this.AddressStart = ushort.Parse(list[i]);
                }
            }
        }

        /// <summary>지정한 오프셋만큼 이동한 새 주소를 반환합니다.</summary>
        public ModbusAddress AddressAdd(int value)
        {
            return new ModbusAddress()
            {
                Station = this.Station,
                Function = this.Function,
                WriteFunction = this.WriteFunction,
                AddressStart = this.AddressStart + value,
            };
        }

        /// <summary>지정한 오프셋만큼 이동한 새 주소를 반환합니다.</summary>
        public ModbusAddress AddressAdd() => AddressAdd(1);
        /// <summary>주소 옵션을 포함한 문자열을 반환합니다.</summary>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            if (Station >= 0)
                sb.Append("s=" + Station + ";");
            if (Function == ModbusFrameRules.ReadDiscrete || Function == ModbusFrameRules.ReadInputRegister || Function > 6)
                sb.Append("x=" + Function + ";");
            if (WriteFunction > 0)
                sb.Append("w=" + WriteFunction + ";");
            sb.Append(AddressStart.ToString());
            return sb.ToString();
        }
    }
}
