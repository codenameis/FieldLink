using FieldLink.PlcDrivers.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace FieldLink.Robot.FANUC.Protocols
{
    /// <summary>FANUC 기본 할당 영역의 상태입니다. FanucSnapshotParser.Parse에는 D1부터 6130워드의 본문이 필요합니다.</summary>
    public sealed class FanucControllerSnapshot
    {
        internal FanucControllerSnapshot() { }
        /// <summary>최근 알람 5개</summary>
        public FanucAlarm[] AlarmList { get; internal set; }
        /// <summary>현재 알람</summary>
        public FanucAlarm AlarmCurrent { get; internal set; }
        /// <summary>암호 관련 알람</summary>
        public FanucAlarm AlarmPassword { get; internal set; }
        /// <summary>그룹 1의 현재 자세</summary>
        public FanucPose CurrentPose { get; internal set; }
        /// <summary>사용자 좌표계의 현재 자세</summary>
        public FanucPose CurrentUserFramePose { get; internal set; }
        /// <summary>그룹 2의 현재 자세</summary>
        public FanucPose CurrentPose2 { get; internal set; }
        /// <summary>그룹 3의 현재 자세</summary>
        public FanucPose CurrentPose3 { get; internal set; }
        /// <summary>그룹 4의 현재 자세</summary>
        public FanucPose CurrentPose4 { get; internal set; }
        /// <summary>그룹 5의 현재 자세</summary>
        public FanucPose CurrentPose5 { get; internal set; }
        /// <summary>현재 프로그램 작업</summary>
        public FanucTask Task { get; internal set; }
        /// <summary>매크로를 제외한 작업</summary>
        public FanucTask TaskIgnoreMacro { get; internal set; }
        /// <summary>KAREL을 제외한 작업</summary>
        public FanucTask TaskIgnoreKarel { get; internal set; }
        /// <summary>매크로와 KAREL을 제외한 작업</summary>
        public FanucTask TaskIgnoreMacroKarel { get; internal set; }
        /// <summary>그룹 1의 위치 레지스터 배열</summary>
        public FanucPose[] Group1PositionRegisters { get; internal set; }
        /// <summary>그룹 2의 위치 레지스터 배열</summary>
        public FanucPose[] Group2PositionRegisters { get; internal set; }
        /// <summary>그룹 3의 위치 레지스터 배열</summary>
        public FanucPose[] Group3PositionRegisters { get; internal set; }
        /// <summary>그룹 4의 위치 레지스터 배열</summary>
        public FanucPose[] Group4PositionRegisters { get; internal set; }
        /// <summary>그룹 5의 위치 레지스터 배열</summary>
        public FanucPose[] Group5PositionRegisters { get; internal set; }
        /// <summary>고속 시계 값</summary>
        public int FastClock { get; internal set; }
        /// <summary>타이머 10의 값</summary>
        public int Timer10Value { get; internal set; }
        /// <summary>그룹의 현재 각도</summary>
        public float CurrentGroupAngle { get; internal set; }
        /// <summary>동작 온도 값</summary>
        public float DutyTemperature { get; internal set; }
        /// <summary>타이머 10의 설명</summary>
        public string Timer10Comment { get; internal set; }
        /// <summary>타이머 2의 설명</summary>
        public string Timer2Comment { get; internal set; }
        /// <summary>그룹 1의 공구 1 자세</summary>
        public FanucPose Group1Tool1Pose { get; internal set; }
        /// <summary>할당된 KCL 명령 문자열</summary>
        public string KclCommands { get; internal set; }
        /// <summary>정수 레지스터 R1~R5</summary>
        public int[] IntegerRegisters { get; internal set; }
        /// <summary>실수 레지스터 R6~R10</summary>
        public float[] RealRegisters { get; internal set; }
        /// <summary>분할 할당 영역에서 조합한 위치 레지스터</summary>
        public FanucPose[] CombinedPositionRegisters { get; internal set; }
        /// <summary>DI 신호의 설명 문자열</summary>
        public string[] DigitalInputComments { get; internal set; }
        /// <summary>DO 신호의 설명 문자열</summary>
        public string[] DigitalOutputComments { get; internal set; }
        /// <summary>RI 신호의 설명 문자열</summary>
        public string[] RobotInputComments { get; internal set; }
        /// <summary>RO 신호의 설명 문자열</summary>
        public string[] RobotOutputComments { get; internal set; }
        /// <summary>UI 신호의 설명 문자열</summary>
        public string[] UserInputComments { get; internal set; }
        /// <summary>UO 신호의 설명 문자열</summary>
        public string[] UserOutputComments { get; internal set; }
        /// <summary>SI 신호의 설명 문자열</summary>
        public string[] SystemInputComments { get; internal set; }
        /// <summary>SO 신호의 설명 문자열</summary>
        public string[] SystemOutputComments { get; internal set; }
        /// <summary>WI 신호의 설명 문자열</summary>
        public string[] WeldInputComments { get; internal set; }
        /// <summary>WO 신호의 설명 문자열</summary>
        public string[] WeldOutputComments { get; internal set; }
        /// <summary>WSI 신호의 설명 문자열</summary>
        public string[] WeldSystemInputComments { get; internal set; }
        /// <summary>AI 신호의 설명 문자열</summary>
        public string[] AnalogInputComments { get; internal set; }
        /// <summary>AO 신호의 설명 문자열</summary>
        public string[] AnalogOutputComments { get; internal set; }
        /// <summary>GI 신호의 설명 문자열</summary>
        public string[] GroupInputComments { get; internal set; }
        /// <summary>GO 신호의 설명 문자열</summary>
        public string[] GroupOutputComments { get; internal set; }
        /// <summary>문자열 레지스터 값</summary>
        public string[] StringRegisterValues { get; internal set; }
        /// <summary>문자열 레지스터 설명</summary>
        public string[] StringRegisterComments { get; internal set; }



        /// <inheritdoc/>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            AppendStringBuilder(sb, "AlarmList", AlarmList.Select(m => m.ToString()).ToArray());
            AppendStringBuilder(sb, "AlarmCurrent", AlarmCurrent.ToString());
            AppendStringBuilder(sb, "AlarmPassword", AlarmPassword.ToString());
            AppendStringBuilder(sb, "CurrentPose", CurrentPose.ToString());
            AppendStringBuilder(sb, "CurrentUserFramePose", CurrentUserFramePose.ToString());
            AppendStringBuilder(sb, "CurrentPose2", CurrentPose2.ToString());
            AppendStringBuilder(sb, "CurrentPose3", CurrentPose3.ToString());
            AppendStringBuilder(sb, "CurrentPose4", CurrentPose4.ToString());
            AppendStringBuilder(sb, "CurrentPose5", CurrentPose5.ToString());
            AppendStringBuilder(sb, "Task", Task.ToString());
            AppendStringBuilder(sb, "TaskIgnoreMacro", TaskIgnoreMacro.ToString());
            AppendStringBuilder(sb, "TaskIgnoreKarel", TaskIgnoreKarel.ToString());
            AppendStringBuilder(sb, "TaskIgnoreMacroKarel", TaskIgnoreMacroKarel.ToString());
            AppendStringBuilder(sb, "Group1PositionRegisters", Group1PositionRegisters.Select(m => m.ToString()).ToArray());
            AppendStringBuilder(sb, "Group2PositionRegisters", Group2PositionRegisters.Select(m => m.ToString()).ToArray());
            AppendStringBuilder(sb, "Group3PositionRegisters", Group3PositionRegisters.Select(m => m.ToString()).ToArray());
            AppendStringBuilder(sb, "Group4PositionRegisters", Group4PositionRegisters.Select(m => m.ToString()).ToArray());
            AppendStringBuilder(sb, "Group5PositionRegisters", Group5PositionRegisters.Select(m => m.ToString()).ToArray());
            AppendStringBuilder(sb, "FastClock", FastClock.ToString());
            AppendStringBuilder(sb, "Timer10Value", Timer10Value.ToString());
            AppendStringBuilder(sb, "CurrentGroupAngle", CurrentGroupAngle.ToString());
            AppendStringBuilder(sb, "DutyTemperature", DutyTemperature.ToString());
            AppendStringBuilder(sb, "Timer10Comment", Timer10Comment.ToString());
            AppendStringBuilder(sb, "Timer2Comment", Timer2Comment.ToString());
            AppendStringBuilder(sb, "Group1Tool1Pose", Group1Tool1Pose.ToString());
            AppendStringBuilder(sb, "KclCommands", KclCommands.ToString());
            AppendStringBuilder(sb, "IntegerRegisters", ProtocolBytes.ArrayFormat(IntegerRegisters));
            AppendStringBuilder(sb, "RealRegisters", ProtocolBytes.ArrayFormat(RealRegisters));
            AppendStringBuilder(sb, "CombinedPositionRegisters", CombinedPositionRegisters.Select(m => m.ToString()).ToArray());
            AppendStringBuilder(sb, "DigitalInputComments", ProtocolBytes.ArrayFormat(DigitalInputComments));
            AppendStringBuilder(sb, "DigitalOutputComments", ProtocolBytes.ArrayFormat(DigitalOutputComments));
            AppendStringBuilder(sb, "RobotInputComments", ProtocolBytes.ArrayFormat(RobotInputComments));
            AppendStringBuilder(sb, "RobotOutputComments", ProtocolBytes.ArrayFormat(RobotOutputComments));
            AppendStringBuilder(sb, "UserInputComments", ProtocolBytes.ArrayFormat(UserInputComments));
            AppendStringBuilder(sb, "UserOutputComments", ProtocolBytes.ArrayFormat(UserOutputComments));
            AppendStringBuilder(sb, "SystemInputComments", ProtocolBytes.ArrayFormat(SystemInputComments));
            AppendStringBuilder(sb, "SystemOutputComments", ProtocolBytes.ArrayFormat(SystemOutputComments));
            AppendStringBuilder(sb, "WeldInputComments", ProtocolBytes.ArrayFormat(WeldInputComments));
            AppendStringBuilder(sb, "WeldOutputComments", ProtocolBytes.ArrayFormat(WeldOutputComments));
            AppendStringBuilder(sb, "WeldSystemInputComments", ProtocolBytes.ArrayFormat(WeldSystemInputComments));
            AppendStringBuilder(sb, "AnalogInputComments", ProtocolBytes.ArrayFormat(AnalogInputComments));
            AppendStringBuilder(sb, "AnalogOutputComments", ProtocolBytes.ArrayFormat(AnalogOutputComments));
            AppendStringBuilder(sb, "GroupInputComments", ProtocolBytes.ArrayFormat(GroupInputComments));
            AppendStringBuilder(sb, "GroupOutputComments", ProtocolBytes.ArrayFormat(GroupOutputComments));
            AppendStringBuilder(sb, "StringRegisterValues", ProtocolBytes.ArrayFormat(StringRegisterValues));
            AppendStringBuilder(sb, "StringRegisterComments", ProtocolBytes.ArrayFormat(StringRegisterComments));
            return sb.ToString();
        }




        private static void AppendStringBuilder(StringBuilder sb, string name, string value)
        {
            AppendStringBuilder(sb, name, new string[] { value });
        }

        private static void AppendStringBuilder(StringBuilder sb, string name, string[] values)
        {
            sb.Append(name);
            sb.Append(":");
            if (values.Length > 1)
                sb.Append(Environment.NewLine);
            for (int i = 0; i < values.Length; i++)
            {
                sb.Append(values[i]);
                sb.Append(Environment.NewLine);
            }

            if (values.Length > 1)
                sb.Append(Environment.NewLine);
        }

    }
}
