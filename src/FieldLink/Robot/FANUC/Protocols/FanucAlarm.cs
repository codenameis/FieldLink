using FieldLink.PlcDrivers.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FieldLink.Robot.FANUC.Protocols
{
    /// <summary>FANUC의 200바이트 알람 항목입니다.</summary>
    public class FanucAlarm
    {
        /// <summary>알람 분류 ID입니다.</summary>
        public short AlarmID { get; set; }
        /// <summary>알람 번호입니다.</summary>
        public short AlarmNumber { get; set; }
        /// <summary>원인 알람의 분류 ID입니다.</summary>
        public short CauseAlarmID { get; set; }
        /// <summary>원인 알람 번호입니다.</summary>
        public short CauseAlarmNumber { get; set; }
        /// <summary>
        /// 심각도
        /// </summary>
        public short Severity { get; set; }
        /// <summary>알람 발생 시각입니다. 연도가 0이면 기본값을 유지합니다.</summary>
        public DateTime Time { get; set; }
        /// <summary>알람 설명 문자열입니다.</summary>
        public string AlarmMessage { get; set; }
        /// <summary>원인 알람 설명 문자열입니다.</summary>
        public string CauseAlarmMessage { get; set; }
        /// <summary>심각도 설명 문자열입니다.</summary>
        public string SeverityMessage { get; set; }

        /// <summary>지정 위치의 200바이트 알람 항목을 해석합니다. 문자열 영역은 지정한 인코딩을 사용합니다.</summary>
        public void LoadByContent(IProtocolValueConverter byteTransform, byte[] content, int index, Encoding encoding)
        {
            AlarmID = BitConverter.ToInt16(content, index);
            AlarmNumber = BitConverter.ToInt16(content, index + 2);
            CauseAlarmID = BitConverter.ToInt16(content, index + 4);
            CauseAlarmNumber = BitConverter.ToInt16(content, index + 6);
            Severity = BitConverter.ToInt16(content, index + 8);
            if (BitConverter.ToInt16(content, index + 10) > 0)
            {
                Time = new DateTime(BitConverter.ToInt16(content, index + 10), BitConverter.ToInt16(content, index + 12), BitConverter.ToInt16(content, index + 14), BitConverter.ToInt16(content, index + 16), BitConverter.ToInt16(content, index + 18), BitConverter.ToInt16(content, index + 20));
            }

            AlarmMessage = encoding.GetString(content, index + 22, 80).Trim('\u0000');
            CauseAlarmMessage = encoding.GetString(content, index + 102, 80).Trim('\u0000');
            SeverityMessage = encoding.GetString(content, index + 182, 18).Trim('\u0000');
        }

        /// <inheritdoc/>
        public override string ToString() => $"FanucAlarm ID[{AlarmID},{AlarmNumber},{CauseAlarmID},{CauseAlarmNumber},{Severity}]{Environment.NewLine}{AlarmMessage}{Environment.NewLine}{CauseAlarmMessage}{Environment.NewLine}{SeverityMessage}";
        /// <summary>지정 위치의 알람 항목을 새 객체로 해석합니다.</summary>
        public static FanucAlarm ParseFrom(IProtocolValueConverter byteTransform, byte[] content, int index, Encoding encoding)
        {
            FanucAlarm fanucAlarm = new FanucAlarm();
            fanucAlarm.LoadByContent(byteTransform, content, index, encoding);
            return fanucAlarm;
        }
    }
}
