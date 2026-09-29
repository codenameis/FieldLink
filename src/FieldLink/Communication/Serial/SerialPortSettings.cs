using System;
using System.Globalization;
using System.IO.Ports;

namespace FieldLink.Communication.Serial
{
    /// <summary>열기 전에 지정하는 불변 시리얼 포트 설정입니다.</summary>
    public sealed class SerialPortSettings
    {
        /// <summary>포트 이름과 통신 조건을 지정합니다. 기본값은 9600 baud, 8-N-1입니다.</summary>
        public SerialPortSettings(string portName, int baudRate = 9600, int dataBits = 8,
            Parity parity = Parity.None, StopBits stopBits = StopBits.One,
            Handshake handshake = Handshake.None, bool rtsEnable = false, bool dtrEnable = false)
        {
            if (string.IsNullOrWhiteSpace(portName))
                throw new ArgumentException("포트 이름이 필요합니다.", nameof(portName));
            if (baudRate <= 0)
                throw new ArgumentOutOfRangeException(nameof(baudRate));
            if (dataBits < 5 || dataBits > 8)
                throw new ArgumentOutOfRangeException(nameof(dataBits));
            if (!Enum.IsDefined(typeof(Parity), parity))
                throw new ArgumentOutOfRangeException(nameof(parity));
            if (!Enum.IsDefined(typeof(StopBits), stopBits) || stopBits == StopBits.None)
                throw new ArgumentOutOfRangeException(nameof(stopBits));
            if (!Enum.IsDefined(typeof(Handshake), handshake))
                throw new ArgumentOutOfRangeException(nameof(handshake));
            PortName = portName; BaudRate = baudRate; DataBits = dataBits; Parity = parity;
            StopBits = stopBits; Handshake = handshake; RtsEnable = rtsEnable; DtrEnable = dtrEnable;
        }

        /// <summary>운영체제 포트 이름입니다.</summary>
        public string PortName { get; }
        /// <summary>초당 전송 심벌 수입니다.</summary>
        public int BaudRate { get; }
        /// <summary>데이터 비트 수입니다.</summary>
        public int DataBits { get; }
        /// <summary>패리티 검사 방식입니다.</summary>
        public Parity Parity { get; }
        /// <summary>정지 비트 수입니다.</summary>
        public StopBits StopBits { get; }
        /// <summary>흐름 제어 방식입니다.</summary>
        public Handshake Handshake { get; }
        /// <summary>수동 RTS 신호 설정입니다.</summary>
        public bool RtsEnable { get; }
        /// <summary>DTR 신호 설정입니다.</summary>
        public bool DtrEnable { get; }

        /// <summary>COM3 또는 COM5-19200-7-E-2 형식을 해석합니다. 구분자로 세미콜론도 허용합니다.</summary>
        public static SerialPortSettings Parse(string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));
            string[] parts = text.Split(new[] { '-', ';' });
            if (parts.Length == 1)
                return new SerialPortSettings(text);
            if (parts.Length != 5)
                throw new ArgumentException("포트-속도-데이터비트-패리티-정지비트 형식이 필요합니다.", nameof(text));
            Parity parity;
            switch (parts[3].ToUpperInvariant())
            {
                case "N": parity = Parity.None; break;
                case "O": parity = Parity.Odd; break;
                case "E": parity = Parity.Even; break;
                case "M": parity = Parity.Mark; break;
                case "S": parity = Parity.Space; break;
                default: throw new ArgumentException("패리티는 N/O/E/M/S 중 하나여야 합니다.", nameof(text));
            }
            StopBits stopBits;
            switch (parts[4])
            {
                case "1": stopBits = StopBits.One; break;
                case "1.5": stopBits = StopBits.OnePointFive; break;
                case "2": stopBits = StopBits.Two; break;
                default: throw new ArgumentException("정지 비트는 1, 1.5, 2 중 하나여야 합니다.", nameof(text));
            }
            if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int baud) ||
                !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int bits))
                throw new ArgumentException("전송 속도와 데이터 비트는 정수여야 합니다.", nameof(text));
            return new SerialPortSettings(parts[0], baud, bits, parity, stopBits);
        }
    }
}
