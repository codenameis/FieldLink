using FieldLink.PlcDrivers.Common;

namespace FieldLink.Robot.Hyundai.Protocols
{
    /// <summary>현대 로봇의 64바이트 UDP 위치 보정 메시지를 변환합니다.</summary>
    public static class HyundaiProtocol
    {
        /// <summary>수신 패킷 크기를 검사한 뒤 좌표를 mm와 도 단위로 해석합니다.</summary>
        public static OperationResult<HyundaiPositionCorrection> Parse(byte[] packet)
        {
            if (packet.Length != 64)
                return new OperationResult<HyundaiPositionCorrection>("현대 로봇 패킷은 64바이트여야 합니다.");
            return OperationResult.CreateSuccessResult(new HyundaiPositionCorrection(packet));
        }

        /// <summary>S 시작 통지의 나머지 필드를 유지한 승인 응답을 만듭니다.</summary>
        public static byte[] BuildStartReply(byte[] packet)
        {
            var data = new HyundaiPositionCorrection(packet)
            {
                Command = 'S',
                Count = 0,
                State = 1
            };
            return data.ToBytes();
        }

        /// <summary>P 위치 보정 명령입니다. XYZ는 mm, WPR은 도 단위이며 순번은 호출자가 관리합니다.</summary>
        public static byte[] BuildIncrement(int count, double x, double y, double z, double w, double p, double r) => new HyundaiPositionCorrection
        {
            Command = 'P',
            State = 2,
            Count = count,
            Data = new[]
            {
                x,
                y,
                z,
                w,
                p,
                r
            }
        }.ToBytes();
    }
}
