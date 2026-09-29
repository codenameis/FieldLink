using System.Collections.Generic;
using System.Text;
using FieldLink.PlcDrivers.Common;

namespace FieldLink.Robot.Estun.Protocols
{
    /// <summary>Estun의 Modbus 레지스터 배치와 명령 교환 순서입니다. Modbus 전송 계층은 포함하지 않습니다.</summary>
    public static class EstunProtocol
    {
        /// <summary>프로그램 시작 명령 값입니다.</summary>
        public const short StartProgram = 0x04;
        /// <summary>프로그램 정지 명령 값입니다.</summary>
        public const short StopProgram = 0x08;
        /// <summary>오류 초기화 명령 값입니다.</summary>
        public const short ResetError = 0x10;
        /// <summary>프로젝트 로드 명령 값입니다.</summary>
        public const short LoadProject = 0x80;
        /// <summary>프로젝트 등록 해제 명령 값입니다.</summary>
        public const short UnregisterProject = 0x100;
        /// <summary>전역 속도 설정 명령 값입니다.</summary>
        public const short SetGlobalSpeed = 0x200;
        /// <summary>명령 상태 초기화 값입니다.</summary>
        public const short RestartCommandStatus = 0x400;
        /// <summary>주소 0에서 100워드를 읽은 응답 본문을 해석합니다.</summary>
        public static EstunControllerState ParseData(byte[] data) => new EstunControllerState(data, new ProtocolValueConverter(ByteOrder.LittleEndianWithByteSwap));
        /// <summary>두 상태 값을 모두 읽은 뒤 명령 실행 전의 유휴 상태를 확인합니다.</summary>
        public static OperationResult CheckIdle(short register99, short register51)
        {
            if (register99 != 0)
                return new OperationResult("1단계: 레지스터 99가 0이 아닙니다. 실제: " + register99);
            if (register51 != 0)
                return new OperationResult("1단계: 레지스터 51이 0이 아닙니다. 실제: " + register51);
            return OperationResult.CreateSuccessResult();
        }

        /// <summary>명령의 순서와 검사 조건을 반환합니다. 각 단계 실패 시 중단하며 자동 정리·재전송은 하지 않습니다.</summary>
        public static EstunProtocolStep[] BuildCommand(short command) => new[]
        {
            new EstunProtocolStep(EstunStepKind.Read, 99),
            new EstunProtocolStep(EstunStepKind.Read, 51),
            new EstunProtocolStep(EstunStepKind.CheckIdle),
            Write(99, 0x11),
            new EstunProtocolStep(EstunStepKind.Poll, 18, expectedValue: 0x801, maxAttempts: 20, delayMilliseconds: 100),
            Write(51, command),
            new EstunProtocolStep(EstunStepKind.Read, 18, delayMilliseconds: 100),
            Write(99, 0),
            Write(51, 0)
        };
        /// <summary>주소 53에 ASCII 20바이트 이름을 기록한 뒤 로드 명령을 실행하는 순서입니다.</summary>
        public static EstunProtocolStep[] BuildLoadProject(string name)
        {
            var steps = new List<EstunProtocolStep>
            {
                new EstunProtocolStep(EstunStepKind.Write, 53, ProtocolBytes.ArrayExpandToLength(Encoding.ASCII.GetBytes(name), 20))
            };
            steps.AddRange(BuildCommand(LoadProject));
            return steps.ToArray();
        }

        /// <summary>주소 52에 속도를 기록한 뒤 적용 명령을 실행하는 순서입니다.</summary>
        public static EstunProtocolStep[] BuildSetGlobalSpeed(short value)
        {
            var steps = new List<EstunProtocolStep>
            {
                Write(52, value)
            };
            steps.AddRange(BuildCommand(SetGlobalSpeed));
            return steps.ToArray();
        }

        private static EstunProtocolStep Write(ushort address, short value) => new EstunProtocolStep(EstunStepKind.Write, address, new ProtocolValueConverter(ByteOrder.LittleEndianWithByteSwap).GetBytes(value));
    }

    /// <summary>Estun 명령 교환에서 호출자가 실행할 단계 종류입니다.</summary>
    public enum EstunStepKind
    {
        /// <summary>한 레지스터를 읽습니다. DelayMilliseconds가 있으면 읽기 전에 기다립니다.</summary>
        Read,
        /// <summary>Data를 지정 주소에 씁니다.</summary>
        Write,
        /// <summary>앞서 읽은 주소 99와 51을 CheckIdle에 전달해 검사합니다.</summary>
        CheckIdle,
        /// <summary>ExpectedValue까지 반복 조회합니다. 첫 조회는 즉시, 불일치 사이에만 기다립니다.</summary>
        Poll
    }

    /// <summary>레지스터 작업 한 단계입니다. 데이터 배열은 반환받은 호출자가 소유합니다.</summary>
    public sealed class EstunProtocolStep
    {
        internal EstunProtocolStep(EstunStepKind kind, ushort address = 0, byte[] data = null, short? expectedValue = null, int maxAttempts = 1, int delayMilliseconds = 0)
        {
            Kind = kind;
            Address = address;
            Data = data;
            ExpectedValue = expectedValue;
            MaxAttempts = maxAttempts;
            DelayMilliseconds = delayMilliseconds;
        }

        /// <summary>실행할 작업 종류입니다.</summary>
        public EstunStepKind Kind { get; }
        /// <summary>0부터 시작하는 Modbus 레지스터 주소입니다.</summary>
        public ushort Address { get; }
        /// <summary>쓰기 바이트입니다. 정수는 빅 엔디언이며 이름 문자열은 원본 ASCII 배열입니다.</summary>
        public byte[] Data { get; }
        /// <summary>반복 조회에서 요구하는 값입니다. 마지막 상태 읽기에는 추가 값 검사가 없습니다.</summary>
        public short? ExpectedValue { get; }
        /// <summary>반복 조회의 최대 횟수입니다.</summary>
        public int MaxAttempts { get; }
        /// <summary>Read 전 대기 또는 Poll 불일치 사이의 대기 시간입니다.</summary>
        public int DelayMilliseconds { get; }
    }
}
