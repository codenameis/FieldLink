using System;
using System.Text;
using FieldLink.PlcDrivers.Common;

namespace FieldLink.Robot.YASKAWA.Protocols
{
    /// <summary>YRC TCP 명령별 요청을 구성합니다. 메서드를 호출해도 로봇과 통신하지 않습니다.</summary>
    public static class YrcTcpRequestBuilder
    {
        /// <summary>사용자 정의 명령과 선택적 본문을 구성합니다.</summary>
        public static YrcTcpRequest<string> BuildCommand(string command, string data = null) => new YrcTcpRequest<string>(command, data, YrcTcpProtocol.ParseResponse);
        /// <summary>점·콜론·세미콜론으로 구분한 명령과 본문을 구성합니다. 원본처럼 처음 두 항목만 사용합니다.</summary>
        public static YrcTcpRequest<string> BuildReadString(string address)
        {
            string[] parts = address.Split(new[] { '.', ':', ';' }, StringSplitOptions.None);
            return parts.Length > 1 ? BuildCommand(parts[0], parts[1]) : BuildCommand(address);
        }

        /// <summary>현재 알람 조회 명령입니다.</summary>
        public static YrcTcpRequest<string> BuildReadAlarm() => BuildCommand("RALARM");
        /// <summary>관절 좌표 조회 명령입니다.</summary>
        public static YrcTcpRequest<string> BuildReadPosJ() => BuildCommand("RPOSJ");
        /// <summary>지정 좌표계의 위치 조회입니다. 0: 베이스, 1: 로봇, 2~65: 사용자 좌표계입니다.</summary>
        public static YrcTcpRequest<YrcRobotPosition> BuildReadPosC(int coordinate, bool hasExternalAxis, YrcControllerModel type = YrcControllerModel.YRC1000) => BuildCommand("RPOSC", $"{coordinate},{(hasExternalAxis ? "1" : "0")}").Map(text => new YrcRobotPosition(type, text));
        /// <summary>모드·실행·HOLD·알람·서보 상태 비트를 조회합니다.</summary>
        public static YrcTcpRequest<bool[]> BuildReadStats() => BuildCommand("RSTATS").Map(text => text.ToStringArray<byte>().ToBoolArray());
        /// <summary>현재 프로그램과 실행 행·스텝 조회입니다.</summary>
        public static YrcTcpRequest<string> BuildReadJSeq() => BuildCommand("RJSEQ");
        /// <summary>사용자 좌표계 1~64의 좌표 조회입니다.</summary>
        public static YrcTcpRequest<string> BuildReadUFrame(int frame) => BuildCommand("RUFRAME", $"{frame}");
        /// <summary>바이트 변수 조회입니다.</summary>
        public static YrcTcpRequest<string> BuildReadByteVariable(string address) => BuildCommand("SAVEV", $"0,{address}");
        /// <summary>16비트 정수 변수 조회입니다.</summary>
        public static YrcTcpRequest<string> BuildReadIntegerVariable(string address) => BuildCommand("SAVEV", $"1,{address}");
        /// <summary>32비트 정수 변수 조회입니다.</summary>
        public static YrcTcpRequest<string> BuildReadDoubleIntegerVariable(string address) => BuildCommand("SAVEV", $"2,{address}");
        /// <summary>실수 변수 조회입니다.</summary>
        public static YrcTcpRequest<string> BuildReadRealVariable(string address) => BuildCommand("SAVEV", $"3,{address}");
        /// <summary>문자열 변수 조회입니다.</summary>
        public static YrcTcpRequest<string> BuildReadStringVariable(string address) => BuildCommand("SAVEV", $"7,{address}");
        /// <summary>HOLD 설정 명령입니다. true는 ON입니다.</summary>
        public static YrcTcpRequest<string> BuildHold(bool status) => BuildCommand("HOLD", status ? "1" : "0");
        /// <summary>알람 초기화 명령입니다.</summary>
        public static YrcTcpRequest<string> BuildReset() => BuildCommand("RESET");
        /// <summary>오류 취소 명령입니다.</summary>
        public static YrcTcpRequest<string> BuildCancel() => BuildCommand("CANCEL");
        /// <summary>모드 선택입니다. 1: 티칭, 2: 재생입니다.</summary>
        public static YrcTcpRequest<string> BuildMode(int number) => BuildCommand("MODE", number.ToString());
        /// <summary>사이클 선택입니다. 1: 스텝, 2: 1회, 3: 연속입니다.</summary>
        public static YrcTcpRequest<string> BuildCycle(int number) => BuildCommand("CYCLE", number.ToString());
        /// <summary>서보 전원 명령입니다. true는 ON입니다.</summary>
        public static YrcTcpRequest<string> BuildSvon(bool status) => BuildCommand("SVON", status ? "1" : "0");
        /// <summary>펜던트와 I/O 조작의 인터록 설정입니다.</summary>
        public static YrcTcpRequest<string> BuildHLock(bool status) => BuildCommand("HLOCK", status ? "1" : "0");
        /// <summary>펜던트 표시 메시지 명령입니다. 전송 명령은 MDSP입니다.</summary>
        public static YrcTcpRequest<string> BuildDisplayMessage(string message) => BuildCommand("MDSP", message);
        /// <summary>프로그램 실행 명령입니다. 이름 생략 시 현재 위치에서 실행합니다.</summary>
        public static YrcTcpRequest<string> BuildStart(string programName = null) => BuildCommand("START", programName);
        /// <summary>프로그램 삭제 명령입니다. 별표는 전체 삭제를 뜻합니다.</summary>
        public static YrcTcpRequest<string> BuildDelete(string programName = null) => BuildCommand("DELETE", programName);
        /// <summary>주 프로그램 지정 명령입니다.</summary>
        public static YrcTcpRequest<string> BuildSetMainJob(string programName = null) => BuildCommand("SETMJ", programName);
        /// <summary>프로그램 이름과 실행 행을 지정합니다.</summary>
        public static YrcTcpRequest<string> BuildJSeq(string programName, int line) => BuildCommand("JSEQ", $"{programName},{line}");
        /// <summary>관절 이동 명령입니다. 좌표 값은 원본의 현재 문화권 형식으로 변환됩니다.</summary>
        public static YrcTcpRequest<string> BuildMoveJ(YrcRobotPosition data, YrcControllerModel type = YrcControllerModel.YRC1000) => BuildCommand("MOVJ", data.ToWriteString(type));
        /// <summary>8점 단위 I/O 조회입니다. 응답의 모든 비트를 반환합니다.</summary>
        public static YrcTcpRequest<bool[]> BuildIORead(int address, int length) => BuildCommand("IOREAD", $"{address},{length}").Map(text => text.ToStringArray<byte>().ToBoolArray());
        /// <summary>8의 배수인 접점 배열을 I/O 쓰기 명령으로 변환합니다. 유효하지 않으면 요청을 만들지 않습니다.</summary>
        public static OperationResult<YrcTcpRequest<string>> BuildIOWrite(int address, bool[] value)
        {
            if (value == null || value.Length % 8 != 0)
                return new OperationResult<YrcTcpRequest<string>>("접점 배열은 null이 아니며 길이가 8의 배수여야 합니다.");
            var text = new StringBuilder($"{address},{value.Length}");
            foreach (byte item in value.ToByteArray())
                text.Append(",").Append(item.ToString());
            return OperationResult.CreateSuccessResult(BuildCommand("IOWRITE", text.ToString()));
        }
    }
}
