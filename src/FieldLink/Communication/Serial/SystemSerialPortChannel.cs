using System.IO.Ports;

namespace FieldLink.Communication.Serial
{
    // BaseStream과 SerialPort의 서로 다른 수신 버퍼를 섞지 않고 바이트 API만 사용한다.
    internal sealed class SystemSerialPortChannel : ISerialPortChannel
    {
        private readonly SerialPort port;
        internal SystemSerialPortChannel(SerialPortSettings settings)
        {
            port = new SerialPort(settings.PortName, settings.BaudRate, settings.Parity, settings.DataBits, settings.StopBits)
            {
                Handshake = settings.Handshake, RtsEnable = settings.RtsEnable, DtrEnable = settings.DtrEnable
            };
        }
        public void Open() => port.Open();
        public int Read(byte[] buffer, int offset, int count, int timeoutMilliseconds)
        {
            port.ReadTimeout = timeoutMilliseconds;
            return port.Read(buffer, offset, count);
        }
        public void Write(byte[] buffer, int offset, int count, int timeoutMilliseconds)
        {
            port.WriteTimeout = timeoutMilliseconds;
            port.Write(buffer, offset, count);
        }
        public void DiscardInput() => port.DiscardInBuffer();
        public void Dispose() => port.Dispose();
    }
}
