using FieldLink.PlcDrivers.Common;
using System;

namespace FieldLink.Robot.ABB.Protocols
{
    /// <summary>ABB의 HTTP 경로와 해당 응답 파서를 결합합니다. 모든 메서드는 요청을 구성하며 통신하지 않습니다.</summary>
    public static partial class AbbRequestBuilder
    {
        private static AbbRequest<string> Create(string path) => new AbbRequest<string>(path, OperationResult.CreateSuccessResult);
        /// <summary>제어 상태 조회 요청입니다.</summary>
        public static AbbRequest<string> GetCtrlState() => Create("/rw/panel/ctrlstate").Then(m => AbbResponseParser.ParseSpanByClass(m, "ctrlstate"));
        /// <summary>모션 오류 상태 조회 요청입니다.</summary>
        public static AbbRequest<string> GetErrorState() => Create("/rw/motionsystem/errorstate").Then(m => AbbResponseParser.ParseSpanByClass(m, "err-state"));
        /// <summary>지정 기구의 관절 및 외부 축 좌표를 JSON 배열로 조회하는 요청입니다.</summary>
        public static AbbRequest<string> GetJointTarget(string mechunit = "ROB_1") => Create($"/rw/motionsystem/mechunits/{mechunit}/jointtarget").Then(m => AbbResponseParser.ParseListSpanByClass(m, "((rax_[0-9]+)|(eax_[a-z]))", n => Convert.ToDouble(n)));
        /// <summary>속도 비율 조회 요청입니다.</summary>
        public static AbbRequest<string> GetSpeedRatio() => Create("/rw/panel/speedratio").Then(m => AbbResponseParser.ParseSpanByClass(m, "speedratio"));
        /// <summary>운전 모드 조회 요청입니다.</summary>
        public static AbbRequest<string> GetOperationMode() => Create("/rw/panel/opmode").Then(m => AbbResponseParser.ParseSpanByClass(m, "opmode"));
        /// <summary>D652_10 장치의 입력 값 조회 요청입니다.</summary>
        public static AbbRequest<string> GetIOIn() => Create("/rw/iosystem/devices/D652_10").Then(m => AbbResponseParser.ParseSpanByClass(m, "indata"));
        /// <summary>D652_10 장치의 출력 값 조회 요청입니다.</summary>
        public static AbbRequest<string> GetIOOut() => Create("/rw/iosystem/devices/D652_10").Then(m => AbbResponseParser.ParseSpanByClass(m, "outdata"));
        /// <summary>BK5250 장치의 입력 값 조회 요청입니다.</summary>
        public static AbbRequest<string> GetIO2In() => Create("/rw/iosystem/devices/BK5250").Then(m => AbbResponseParser.ParseSpanByClass(m, "indata"));
        /// <summary>BK5250 장치의 출력 값 조회 요청입니다.</summary>
        public static AbbRequest<string> GetIO2Out() => Create("/rw/iosystem/devices/BK5250").Then(m => AbbResponseParser.ParseSpanByClass(m, "outdata"));
        /// <summary>로그 항목을 지정 개수까지 JSON 배열로 조회합니다. 기존 lang=zh 설정을 유지합니다.</summary>
        // 이전 경로: /rw/elog/0?lang=zh&amp;resource=title
        // R-009: HTTP URI는 XML을 해제하지 않으므로 매개변수 구분자 &를 직접 사용한다.
        public static AbbRequest<string> GetLog(int logCount = 10) => Create("/rw/elog/0?lang=zh&resource=title").Then(m => AbbResponseParser.ParseJArrayByClass(m, "elog-message-li", logCount));
        /// <summary>시스템 이름·버전·식별자 조회 요청입니다.</summary>
        public static AbbRequest<string> GetSystem() => Create("/rw/system").Then(m => AbbResponseParser.ParseJObjectByClass(m, "sys-system-li"));
        /// <summary>ROB_1의 목표 자세 조회 요청입니다.</summary>
        public static AbbRequest<string> GetRobotTarget() => Create("/rw/motionsystem/mechunits/ROB_1/robtarget").Then(m => AbbResponseParser.ParseJObjectByClass(m, "ms-robtargets"));
        /// <summary>DRV1K1 서보 신호 조회 요청입니다.</summary>
        public static AbbRequest<string> GetServoEnable() => Create("/rw/iosystem/signals/Local/DRV_1/DRV1K1").Then(m => AbbResponseParser.ParseJObjectByClass(m, "ios-signal"));
        /// <summary>RAPID 프로그램 실행 상태 조회 요청입니다.</summary>
        public static AbbRequest<string> GetRapidExecution() => Create("/rw/rapid/execution").Then(m => AbbResponseParser.ParseJObjectByClass(m, "rap-execution"));
        /// <summary>RAPID 작업 목록 조회 요청입니다.</summary>
        public static AbbRequest<string> GetRapidTasks() => Create("/rw/rapid/tasks").Then(m => AbbResponseParser.ParseJArrayByClass(m, "rap-task-li"));
        /// <summary>사용자 심벌의 value 항목을 실수 배열로 조회합니다. url= 접두어로 경로를 직접 지정할 수 있습니다.</summary>
        public static AbbRequest<double[]> GetUserValue(string name) => Create(name.StartsWith("url=", StringComparison.OrdinalIgnoreCase) ? name.Substring(4) : "/rw/rapid/symbol/data/RAPID/T_ROB1/user/" + name).Then(m => AbbResponseParser.ParseDoubleListSpanByClass(m, "value"));
        /// <summary>지정 네트워크·유닛·신호의 조회 요청입니다.</summary>
        public static AbbRequest<string> GetAnIOSignal(string network = "Local", string unit = "DRV_1", string signal = "DRV1K1") => Create($"/rw/iosystem/signals/{network}/{unit}/{signal}").Then(m => AbbResponseParser.ParseJObjectByClass(m, "ios-signal"));
    }
}
