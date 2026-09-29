using FieldLink.PlcDrivers.Common;
using System;
using System.Text;

namespace FieldLink.PlcDrivers.Modbus
{
    /// <summary>Modbus 시리얼 전송 외피입니다.</summary>
    public enum ModbusSerialEncoding
    {
        /// <summary>이진 데이터와 CRC16을 사용합니다.</summary>
        Rtu,
        /// <summary>ASCII 16진수와 LRC를 사용합니다.</summary>
        Ascii
    }

    /// <summary>Modbus 코어에 시리얼 외피를 붙이거나 검증하여 제거합니다.</summary>
    public static class ModbusSerialCodec
    {
        // Modbus Serial Line V1.02 §2.5.1.1, p.13: 19200 이하 t1.5, 초과는 750us 권고값.
        // 100ns 단위로 올림하여 표현 오차 때문에 정상 간격을 거부하지 않는다.
        internal static TimeSpan GetMaximumRtuInterCharacterInterval(int baudRate, double bitsPerCharacter) =>
            TimeSpan.FromTicks((long)Math.Ceiling((baudRate > 19200 ? 0.00075 : 1.5 * bitsPerCharacter / baudRate) * TimeSpan.TicksPerSecond));

        // Inovance AM의 시스템 영역은 표준 기능에 0x30을 더한 같은 외형의 응답을 사용합니다.
        internal static byte ResponseLayout(byte function) => function >= 0x31 && function <= 0x47 ? (byte)(function - 0x30) : function;

        /// <summary>국번부터 시작하는 코어를 전송 프레임으로 만듭니다.</summary>
        public static byte[] Encode(byte[] core, ModbusSerialEncoding encoding)
        {
            if (core == null)
                throw new ArgumentNullException(nameof(core));
            if (encoding == ModbusSerialEncoding.Rtu)
                return Crc16.Append(core);
            if (encoding != ModbusSerialEncoding.Ascii)
                throw new ArgumentOutOfRangeException(nameof(encoding));
            return Encoding.ASCII.GetBytes(":" + BitConverter.ToString(LrcChecksum.Append(core)).Replace("-", "") + "\r\n");
        }

        /// <summary>CRC16 또는 LRC와 ASCII 시작·종료 기호를 확인합니다.</summary>
        /// <remarks>바이트만으로 물리 수신 간격을 알 수 없습니다. RTU t1.5 검사는 시간 정보를 제공하는 전송 어댑터에서 별도로 수행합니다.</remarks>
        public static OperationResult<byte[]> Decode(byte[] frame, ModbusSerialEncoding encoding)
        {
            if (frame == null)
                throw new ArgumentNullException(nameof(frame));
            if (encoding == ModbusSerialEncoding.Rtu)
                return frame.Length >= 5 && Crc16.Verify(frame)
                    ? OperationResult.CreateSuccessResult(frame.SelectBegin(frame.Length - 2))
                    : new OperationResult<byte[]>("Modbus RTU 응답의 길이 또는 CRC16이 잘못되었습니다.");
            if (encoding != ModbusSerialEncoding.Ascii)
                throw new ArgumentOutOfRangeException(nameof(encoding));
            if (frame.Length < 11 || frame[0] != ':' || frame[frame.Length - 2] != 13 || frame[frame.Length - 1] != 10 || frame.Length % 2 != 1)
                return new OperationResult<byte[]>("Modbus ASCII 응답의 외피가 잘못되었습니다.");
            var core = new byte[(frame.Length - 3) / 2];
            for (int i = 0; i < core.Length; i++)
            {
                int high = Hex(frame[1 + i * 2]), low = Hex(frame[2 + i * 2]);
                if (high < 0 || low < 0)
                    return new OperationResult<byte[]>("Modbus ASCII 응답에 16진수가 아닌 문자가 있습니다.");
                core[i] = (byte)(high * 16 + low);
            }
            return LrcChecksum.Verify(core) ? OperationResult.CreateSuccessResult(core.SelectBegin(core.Length - 1))
                : new OperationResult<byte[]>("Modbus ASCII 응답의 LRC가 잘못되었습니다.");
        }

        private static int Hex(byte value) => value >= '0' && value <= '9' ? value - '0' :
            value >= 'A' && value <= 'F' ? value - 'A' + 10 : value >= 'a' && value <= 'f' ? value - 'a' + 10 : -1;
    }
}
