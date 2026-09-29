using FieldLink.PlcDrivers.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FieldLink.Robot.YASKAWA.Protocols
{
    /// <summary>YRC 고속 이더넷 알람 항목입니다. 시각은 ASCII, 메시지는 지정 인코딩을 사용합니다.</summary>
    public class YrcAlarm
    {
        /// <summary>YRC 고속 이더넷 알람 항목입니다. 시각은 ASCII, 메시지는 지정 인코딩을 사용합니다.</summary>
        public YrcAlarm()
        {
        }

        /// <summary>YRC 고속 이더넷 알람 항목입니다. 시각은 ASCII, 메시지는 지정 인코딩을 사용합니다.</summary>
        public YrcAlarm(IProtocolValueConverter byteTransform, byte[] content, Encoding encoding)
        {
            AlarmCode = byteTransform.ReadInt32(content, 0);
            Time = Convert.ToDateTime(Encoding.ASCII.GetString(content, 16, 16));
            Message = encoding.GetString(content.RemoveBegin(32));
        }

        /// <summary>
        /// 알람 코드
        /// </summary>
        public int AlarmCode { get; set; }
        /// <summary>
        /// 경보 발생 시점
        /// </summary>
        public DateTime Time { get; set; }
        /// <summary>장치가 반환한 알람 메시지입니다.</summary>
        public string Message { get; set; }

        /// <inheritdoc/>
        public override string ToString() => $"[{AlarmCode}] Time:[{Time}] {Message}";
    }
}
