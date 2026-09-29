using FieldLink.PlcDrivers.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FieldLink.Robot.Hyundai.Protocols
{
    /// <summary>현대 로봇의 64바이트 위치 보정 데이터입니다.</summary>
    public class HyundaiPositionCorrection
    {
        /// <summary>현대 로봇의 64바이트 위치 보정 데이터입니다.</summary>
        public HyundaiPositionCorrection()
        {
            Data = new double[6];
        }

        /// <summary>현대 로봇의 64바이트 위치 보정 데이터입니다.</summary>
        public HyundaiPositionCorrection(byte[] buffer)
        {
            LoadBy(buffer);
        }

        /// <summary>S: 시작, P: 위치, F: 종료를 나타내는 명령 문자입니다.</summary>
        public char Command { get; set; }
        /// <summary>3바이트 문자 예약 영역입니다. 직렬화 전에 길이를 지켜야 합니다.</summary>
        public string CharDummy { get; set; }
        /// <summary>
        /// 상태 코드
        /// </summary>
        public int State { get; set; }
        /// <summary>교환 순번입니다.</summary>
        public int Count { get; set; }
        /// <summary>32비트 정수 예약 영역입니다.</summary>
        public int IntDummy { get; set; }
        /// <summary>X, Y, Z, W, P, R 순서의 6개 값입니다. 위치는 mm, 각도는 도 단위입니다.</summary>
        public double[] Data { get; set; }

        /// <summary>64바이트 프레임을 해석하고 m를 mm로, rad를 도로 변환합니다.</summary>
        public void LoadBy(byte[] buffer, int index = 0)
        {
            Command = (char)buffer[index];
            CharDummy = Encoding.ASCII.GetString(buffer, index + 1, 3);
            State = BitConverter.ToInt32(buffer, index + 4);
            Count = BitConverter.ToInt32(buffer, index + 8);
            IntDummy = BitConverter.ToInt32(buffer, index + 12);
            Data = new double[6];
            for (int i = 0; i < Data.Length; i++)
            {
                if (i < 3)
                    Data[i] = BitConverter.ToDouble(buffer, index + 16 + 8 * i) * 1000d;
                else
                    Data[i] = BitConverter.ToDouble(buffer, index + 16 + 8 * i) * 180d / Math.PI;
            }
        }

        /// <summary>위치와 각도를 m·rad로 변환한 64바이트 프레임을 구성합니다. Data는 6개, CharDummy는 최대 3바이트여야 합니다.</summary>
        public byte[] ToBytes()
        {
            byte[] buffer = new byte[64];
            buffer[0] = (byte)Command;
            if (!string.IsNullOrEmpty(CharDummy))
                Encoding.ASCII.GetBytes(CharDummy).CopyTo(buffer, 1);
            BitConverter.GetBytes(State).CopyTo(buffer, 4);
            BitConverter.GetBytes(Count).CopyTo(buffer, 8);
            BitConverter.GetBytes(IntDummy).CopyTo(buffer, 12);
            for (int i = 0; i < Data.Length; i++)
            {
                if (i < 3)
                    BitConverter.GetBytes(Data[i] / 1000d).CopyTo(buffer, 16 + 8 * i);
                else
                    BitConverter.GetBytes(Data[i] * Math.PI / 180d).CopyTo(buffer, 16 + 8 * i);
            }

            return buffer;
        }

        /// <inheritdoc/>
        public override string ToString() => $"HyundaiData:Cmd[{Command},{CharDummy},{State},{Count},{IntDummy}] Data:{ProtocolBytes.ArrayFormat(Data)}";
    }
}
