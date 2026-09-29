using FieldLink.PlcDrivers.Common;
using System;
using System.Text;

namespace FieldLink.Robot.YASKAWA.Protocols
{
    /// <summary>YRC 고속 이더넷의 명령별 요청과 본문 파서를 구성합니다.</summary>
    public sealed partial class YrcEthernetRequestBuilder
    {
        private readonly IProtocolValueConverter byteTransform = new ProtocolValueConverter();
        private readonly Encoding encoding = Encoding.ASCII;
        /// <summary>모드·실행·알람·서보 상태 16비트 조회 요청을 구성합니다.</summary>
        public YrcEthernetRequest<bool[]> BuildReadStats() => Map(CreateRobotControlRequest(0x72, 1, 0, 0x01, null), data =>
        {
            // Yaskawa Controller Status Reading: two 4-byte status fields.
            if (data.Length != 8)
                throw new FormatException("YRC 상태 응답 본문은 8바이트여야 합니다.");
            return new byte[] { data[0], data[4] }.ToBoolArray();
        });
        /// <summary>작업 번호 1~16의 프로그램 이름·행·스텝·속도 조회 요청을 구성합니다.</summary>
        public YrcEthernetRequest<string[]> BuildReadJSeq(ushort task = 1)
        {
            return CreateRobotControlRequest(0x73, task, 0, 0x01, null).Then(read =>
            {
                if (!read.IsSuccess)
                    return OperationResult.CreateFailedResult<string[]>(read);
                string[] result = new string[4];
                result[0] = encoding.GetString(read.Content, 0, 32);
                result[1] = byteTransform.ReadInt32(read.Content, 32).ToString();
                result[2] = byteTransform.ReadInt32(read.Content, 36).ToString();
                result[3] = byteTransform.ReadInt32(read.Content, 40).ToString();
                return OperationResult.CreateSuccessResult(result);
            });
        }

        /// <summary>좌표 응답의 선행 20바이트를 제외한 위치 정수 조회 요청을 구성합니다.</summary>
        public YrcEthernetRequest<string[]> BuildReadPose()
        {
            return CreateRobotControlRequest(0x75, 101, 0, 0x01, null).Then(read =>
            {
                if (!read.IsSuccess)
                    return OperationResult.CreateFailedResult<string[]>(read);
                string[] result = new string[read.Content.Length / 4 - 5];
                for (int i = 0; i < result.Length; i++)
                {
                    result[i] = byteTransform.ReadInt32(read.Content, 20 + i * 4).ToString();
                }

                return OperationResult.CreateSuccessResult(result);
            });
        }

        /// <summary>축 토크 정수 조회 요청을 구성합니다.</summary>
        public YrcEthernetRequest<string[]> BuildReadTorqueData()
        {
            return CreateRobotControlRequest(0x77, 21, 0, 0x01, null).Then(read =>
            {
                if (!read.IsSuccess)
                    return OperationResult.CreateFailedResult<string[]>(read);
                string[] result = new string[read.Content.Length / 4];
                for (int i = 0; i < result.Length; i++)
                {
                    result[i] = byteTransform.ReadInt32(read.Content, i * 4).ToString();
                }

                return OperationResult.CreateSuccessResult(result);
            });
        }

        /// <summary>단일 또는 연속 I/O 조회. 연속 응답에서는 개수 접두어 4바이트를 제거 요청을 구성합니다.</summary>
        public YrcEthernetRequest<byte> BuildReadIO(ushort address)
        {
            return CreateRobotControlRequest(0x78, address, 1, 0x0E, null).Then(read =>
            {
                if (!read.IsSuccess)
                    return OperationResult.CreateFailedResult<byte>(read);
                return OperationResult.CreateSuccessResult(read.Content[0]);
            });
        }

        /// <summary>네트워크 입력 I/O 쓰기 요청을 구성합니다.</summary>
        public YrcEthernetRequest<byte[]> BuildWriteIO(ushort address, byte value)
        {
            return CreateRobotControlRequest(0x78, address, 1, 0x10, new byte[] { value });
        }

        /// <summary>단일 또는 연속 I/O 조회. 연속 응답에서는 개수 접두어 4바이트를 제거 요청을 구성합니다.</summary>
        public YrcEthernetRequest<byte[]> BuildReadIO(ushort address, int length)
        {
            return CreateRobotControlRequest(0x300, address, 0, 0x33, this.byteTransform.GetBytes(length)).Then(read =>
            {
                if (!read.IsSuccess)
                    return OperationResult.CreateFailedResult<byte[]>(read);
                int count = this.byteTransform.ReadInt32(read.Content, 0);
                return OperationResult.CreateSuccessResult(read.Content.SelectMiddle(4, count));
            });
        }

        /// <summary>네트워크 입력 I/O 쓰기 요청을 구성합니다.</summary>
        public YrcEthernetRequest<byte[]> BuildWriteIO(ushort address, byte[] value)
        {
            return CreateRobotControlRequest(0x300, address, 0, 0x34, value);
        }

        /// <summary>16비트 부호 없는 레지스터 조회 요청을 구성합니다.</summary>
        public YrcEthernetRequest<ushort> BuildReadRegisterVariable(ushort address) => Map(CreateRobotControlRequest(0x79, address, 1, 0x0E, null), m => this.byteTransform.ReadUInt16(m, 0));
        /// <summary>16비트 부호 없는 레지스터 쓰기 요청을 구성합니다.</summary>
        public YrcEthernetRequest<byte[]> BuildWriteRegisterVariable(ushort address, ushort value) => CreateRobotControlRequest(0x79, address, 1, 0x10, this.byteTransform.GetBytes(value));
        /// <summary>16비트 부호 없는 레지스터 조회 요청을 구성합니다.</summary>
        public YrcEthernetRequest<ushort[]> BuildReadRegisterVariable(ushort address, int length) => Map(CreateRobotControlRequest(0x301, address, 0, 0x33, this.byteTransform.GetBytes(length)), m => this.byteTransform.ReadUInt16(m, 0, length));
        /// <summary>16비트 부호 없는 레지스터 쓰기 요청을 구성합니다.</summary>
        public YrcEthernetRequest<byte[]> BuildWriteRegisterVariable(ushort address, ushort[] value) => CreateRobotControlRequest(0x301, address, 0, 0x34, this.byteTransform.GetBytes(value));
        /// <summary>바이트 변수 조회 요청을 구성합니다.</summary>
        public YrcEthernetRequest<byte> BuildReadByteVariable(ushort address) => First(CreateRobotControlRequest(0x7A, address, 1, 0x0E, null));
        /// <summary>바이트 변수 쓰기 요청을 구성합니다.</summary>
        public YrcEthernetRequest<byte[]> BuildWriteByteVariable(ushort address, byte value) => CreateRobotControlRequest(0x7A, address, 1, 0x10, new byte[] { value });
        /// <summary>바이트 변수 조회 요청을 구성합니다.</summary>
        public YrcEthernetRequest<byte[]> BuildReadByteVariable(ushort address, int length) => CreateRobotControlRequest(0x302, address, 0, 0x33, this.byteTransform.GetBytes(length));
        /// <summary>바이트 변수 쓰기 요청을 구성합니다.</summary>
        public YrcEthernetRequest<byte[]> BuildWriteByteVariable(ushort address, byte[] vaule) => CreateRobotControlRequest(0x302, address, 0, 0x34, vaule);
        /// <summary>16비트 부호 있는 정수 변수 조회 요청을 구성합니다.</summary>
        public YrcEthernetRequest<short> BuildReadIntegerVariable(ushort address) => Map(CreateRobotControlRequest(0x7B, address, 1, 0x0E, null), m => this.byteTransform.ReadInt16(m, 0));
        /// <summary>16비트 부호 있는 정수 변수 쓰기 요청을 구성합니다.</summary>
        public YrcEthernetRequest<byte[]> BuildWriteIntegerVariable(ushort address, short value) => CreateRobotControlRequest(0x7B, address, 1, 0x10, this.byteTransform.GetBytes(value));
        /// <summary>16비트 부호 있는 정수 변수 조회 요청을 구성합니다.</summary>
        public YrcEthernetRequest<short[]> BuildReadIntegerVariable(ushort address, int length) => Map(CreateRobotControlRequest(0x303, address, 0, 0x33, this.byteTransform.GetBytes(length)), m => this.byteTransform.ReadInt16(m, 0, length));
        /// <summary>16비트 부호 있는 정수 변수 쓰기 요청을 구성합니다.</summary>
        public YrcEthernetRequest<byte[]> BuildWriteIntegerVariable(ushort address, short[] value) => CreateRobotControlRequest(0x303, address, 0, 0x34, this.byteTransform.GetBytes(value));
        /// <summary>32비트 정수 변수 조회 요청을 구성합니다.</summary>
        public YrcEthernetRequest<int> BuildReadDoubleIntegerVariable(ushort address) => Map(CreateRobotControlRequest(0x7C, address, 1, 0x0E, null), m => this.byteTransform.ReadInt32(m, 0));
        /// <summary>32비트 정수 변수 쓰기 요청을 구성합니다.</summary>
        public YrcEthernetRequest<byte[]> BuildWriteDoubleIntegerVariable(ushort address, int value) => CreateRobotControlRequest(0x7C, address, 1, 0x10, this.byteTransform.GetBytes(value));
        /// <summary>32비트 정수 변수 조회 요청을 구성합니다.</summary>
        public YrcEthernetRequest<int[]> BuildReadDoubleIntegerVariable(ushort address, int length) => Map(CreateRobotControlRequest(0x304, address, 0, 0x33, this.byteTransform.GetBytes(length)), m => this.byteTransform.ReadInt32(m, 0, length));
        /// <summary>32비트 정수 변수 쓰기 요청을 구성합니다.</summary>
        public YrcEthernetRequest<byte[]> BuildWriteDoubleIntegerVariable(ushort address, int[] value) => CreateRobotControlRequest(0x304, address, 0, 0x34, this.byteTransform.GetBytes(value));
        /// <summary>32비트 실수 변수 조회 요청을 구성합니다.</summary>
        public YrcEthernetRequest<float> BuildReadRealVariable(ushort address) => Map(CreateRobotControlRequest(0x7D, address, 1, 0x0E, null), m => this.byteTransform.ReadSingle(m, 0));
        /// <summary>32비트 실수 변수 쓰기 요청을 구성합니다.</summary>
        public YrcEthernetRequest<byte[]> BuildWriteRealVariable(ushort address, float value) => CreateRobotControlRequest(0x7D, address, 1, 0x10, this.byteTransform.GetBytes(value));
        /// <summary>32비트 실수 변수 조회 요청을 구성합니다.</summary>
        public YrcEthernetRequest<float[]> BuildReadRealVariable(ushort address, int length) => Map(CreateRobotControlRequest(0x305, address, 0, 0x33, this.byteTransform.GetBytes(length)), m => this.byteTransform.ReadSingle(m, 0, length));
        /// <summary>32비트 실수 변수 쓰기 요청을 구성합니다.</summary>
        public YrcEthernetRequest<byte[]> BuildWriteRealVariable(ushort address, float[] value) => CreateRobotControlRequest(0x305, address, 0, 0x34, this.byteTransform.GetBytes(value));
        /// <summary>ASCII 문자열 변수 조회. 연속 값은 16바이트씩 해석 요청을 구성합니다.</summary>
        public YrcEthernetRequest<string> BuildReadStringVariable(ushort address) => Map(CreateRobotControlRequest(0x7E, address, 1, 0x0E, null), m => this.byteTransform.ReadString(m, this.encoding));
        /// <summary>ASCII 문자열 변수 쓰기. 연속 값마다 16바이트 공간을 사용하며 초과 길이는 호출자가 제한 요청을 구성합니다.</summary>
        public YrcEthernetRequest<byte[]> BuildWriteStringVariable(ushort address, string value) => CreateRobotControlRequest(0x7E, address, 1, 0x10, ProtocolBytes.ArrayExpandToLength(this.encoding.GetBytes(value), 16));
        /// <summary>ASCII 문자열 변수 조회. 연속 값은 16바이트씩 해석 요청을 구성합니다.</summary>
        public YrcEthernetRequest<string[]> BuildReadStringVariable(ushort address, int length)
        {
            return CreateRobotControlRequest(0x306, address, 0, 0x33, this.byteTransform.GetBytes(length)).Then(read =>
            {
                if (!read.IsSuccess)
                    return OperationResult.CreateFailedResult<string[]>(read);
                string[] result = new string[length];
                for (int i = 0; i < length; i++)
                {
                    result[i] = this.encoding.GetString(read.Content, i * 16, 16);
                }

                return OperationResult.CreateSuccessResult(result);
            });
        }

        /// <summary>ASCII 문자열 변수 쓰기. 연속 값마다 16바이트 공간을 사용하며 초과 길이는 호출자가 제한 요청을 구성합니다.</summary>
        public YrcEthernetRequest<byte[]> BuildWriteStringVariable(ushort address, string[] value)
        {
            byte[] buffer = new byte[value.Length * 16];
            for (int i = 0; i < value.Length; i++)
            {
                this.encoding.GetBytes(value[i]).CopyTo(buffer, i * 16);
            }

            return CreateRobotControlRequest(0x306, address, 0, 0x34, buffer);
        }

        /// <summary>HOLD 설정. true는 값 1, false는 값 2 요청을 구성합니다.</summary>
        public YrcEthernetRequest<byte[]> BuildHold(bool status) => CreateRobotControlRequest(0x83, 1, 1, 0x10, status ? this.byteTransform.GetBytes(1) : this.byteTransform.GetBytes(2));
        /// <summary>알람 초기화 요청을 구성합니다.</summary>
        public YrcEthernetRequest<byte[]> BuildReset() => CreateRobotControlRequest(0x82, 1, 1, 0x10, this.byteTransform.GetBytes(1));
        /// <summary>오류 취소 요청을 구성합니다.</summary>
        public YrcEthernetRequest<byte[]> BuildCancel() => CreateRobotControlRequest(0x82, 2, 1, 0x10, this.byteTransform.GetBytes(1));
        /// <summary>서보 전원 설정. true는 값 1, false는 값 2 요청을 구성합니다.</summary>
        public YrcEthernetRequest<byte[]> BuildSvon(bool status) => CreateRobotControlRequest(0x83, 2, 1, 0x10, status ? this.byteTransform.GetBytes(1) : this.byteTransform.GetBytes(2));
        /// <summary>조작 인터록 설정. true는 값 1, false는 값 2 요청을 구성합니다.</summary>
        public YrcEthernetRequest<byte[]> BuildHLock(bool status) => CreateRobotControlRequest(0x83, 3, 1, 0x10, status ? this.byteTransform.GetBytes(1) : this.byteTransform.GetBytes(2));
        /// <summary>사이클 설정. 1: 스텝, 2: 1회, 3: 연속 요청을 구성합니다.</summary>
        public YrcEthernetRequest<byte[]> BuildCycle(int number) => CreateRobotControlRequest(0x84, 2, 1, 0x10, this.byteTransform.GetBytes(number));
        /// <summary>펜던트 표시 메시지 설정 요청을 구성합니다.</summary>
        public YrcEthernetRequest<byte[]> BuildMSDP(string message) => CreateRobotControlRequest(0x85, 1, 1, 0x10, string.IsNullOrEmpty(message) ? new byte[0] : this.encoding.GetBytes(message));
        /// <summary>프로그램 시작 요청을 구성합니다.</summary>
        public YrcEthernetRequest<byte[]> BuildStart() => CreateRobotControlRequest(0x86, 1, 1, 0x10, this.byteTransform.GetBytes(1));
        /// <summary>관리 시각 조회. ASCII 16바이트를 현재 문화권의 시각으로 변환 요청을 구성합니다.</summary>
        public YrcEthernetRequest<DateTime> BuildReadManagementTime(ushort address)
        {
            return CreateRobotControlRequest(0x88, address, 1, 0x0E, null).Then(read =>
            {
                if (!read.IsSuccess)
                    return OperationResult.CreateFailedResult<DateTime>(read);
                return OperationResult.CreateSuccessResult(Convert.ToDateTime(Encoding.ASCII.GetString(read.Content, 0, 16)));
            });
        }

        /// <summary>관리 누적 시간 조회. ASCII 12바이트를 그대로 반환 요청을 구성합니다.</summary>
        public YrcEthernetRequest<string> BuildReadManagementTimeSpan(ushort address)
        {
            return CreateRobotControlRequest(0x88, address, 2, 0x0E, null).Then(read =>
            {
                if (!read.IsSuccess)
                    return OperationResult.CreateFailedResult<string>(read);
                return OperationResult.CreateSuccessResult(Encoding.ASCII.GetString(read.Content, 0, 12));
            });
        }

        /// <summary>시스템 버전 24바이트·기종 16바이트·매개변수 버전 8바이트 조회 요청을 구성합니다.</summary>
        public YrcEthernetRequest<string[]> BuildReadSystemInfo(ushort system)
        {
            return CreateRobotControlRequest(0x89, system, 0x00, 0x01, null).Then(read =>
            {
                if (!read.IsSuccess)
                    return OperationResult.CreateFailedResult<string[]>(read);
                string[] result = new string[3];
                result[0] = encoding.GetString(read.Content, 0, 24);
                result[1] = encoding.GetString(read.Content, 24, 16);
                result[2] = encoding.GetString(read.Content, 40, 8);
                return OperationResult.CreateSuccessResult(result);
            });
        }

        /// <summary>실행할 프로그램과 시작 행을 지정하는 Job 선택 요청을 구성합니다.</summary>
        public YrcEthernetRequest<byte[]> BuildJSeq(string programName, int line)
        {
            byte[] buffer = new byte[36];
            this.encoding.GetBytes(programName).CopyTo(buffer, 0);
            this.byteTransform.GetBytes(line).CopyTo(buffer, 32);
            // 이전 구현: return CreateRobotControlRequest(0x84, 2, 1, 0x10, buffer);
            // R-006: 0x84는 운전 사이클 변경이다. Job 선택은 공식 HSES 표의 0x87/1/0/2를 사용한다.
            return CreateRobotControlRequest(0x87, 1, 0, 0x02, buffer);
        }
    }
}
