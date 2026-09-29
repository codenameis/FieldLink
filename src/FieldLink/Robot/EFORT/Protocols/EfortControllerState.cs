using FieldLink.PlcDrivers.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FieldLink.Robot.EFORT.Protocols
{
    /// <summary>EFORT 상태 패킷의 필드를 보관합니다. 현재 형식은 788바이트, 이전 형식은 784바이트입니다.</summary>
    public class EfortControllerState
    {
        /// <summary>EFORT 상태 패킷의 필드를 보관합니다. 현재 형식은 788바이트, 이전 형식은 784바이트입니다.</summary>
        public EfortControllerState()
        {
            IoDOut = new byte[32];
            IoDIn = new byte[32];
            IoIOut = new int[32];
            IoIIn = new int[32];
            DbAxisPos = new float[7];
            DbCartPos = new float[6];
            DbAxisSpeed = new float[7];
            DbAxisAcc = new float[7];
            DbAxisAccAcc = new float[7];
            DbAxisTorque = new float[7];
            DbAxisDirCnt = new int[7];
            DbAxisTime = new int[7];
        }

        /// <summary>패킷 시작 문자열입니다.</summary>
        public string PacketStart { get; set; }
        /// <summary>명령 번호입니다.</summary>
        public ushort PacketOrders { get; set; }
        /// <summary>하트비트 순번입니다.</summary>
        public ushort PacketHeartbeat { get; set; }
        /// <summary>장치의 알람 상태 원시 값입니다.</summary>
        public byte ErrorStatus { get; set; }
        /// <summary>비상 정지 상태입니다. 1: 정지 없음, 0: 정지입니다.</summary>
        public byte HstopStatus { get; set; }
        /// <summary>조작 권한 상태입니다. 1이면 권한이 있습니다.</summary>
        public byte AuthorityStatus { get; set; }
        /// <summary>서보 허용 상태입니다. 1이면 허용됩니다.</summary>
        public byte ServoStatus { get; set; }
        /// <summary>축 동작 상태입니다. 1이면 동작 중입니다.</summary>
        public byte AxisMoveStatus { get; set; }
        /// <summary>프로그램 실행 상태입니다.</summary>
        public byte ProgMoveStatus { get; set; }
        /// <summary>프로그램 로드 상태입니다.</summary>
        public byte ProgLoadStatus { get; set; }
        /// <summary>프로그램 정지 상태입니다.</summary>
        public byte ProgHoldStatus { get; set; }
        /// <summary>운전 모드입니다. 1: 수동, 2: 자동, 3: 원격입니다.</summary>
        public ushort ModeStatus { get; set; }
        /// <summary>속도 설정의 백분율 값입니다.</summary>
        public ushort SpeedStatus { get; set; }
        /// <summary>디지털 출력 32바이트입니다.</summary>
        public byte[] IoDOut { get; set; }
        /// <summary>디지털 입력 32바이트입니다.</summary>
        public byte[] IoDIn { get; set; }
        /// <summary>정수 출력 32개입니다.</summary>
        public int[] IoIOut { get; set; }
        /// <summary>정수 입력 32개입니다.</summary>
        public int[] IoIIn { get; set; }
        /// <summary>로드된 프로젝트 이름입니다.</summary>
        public string ProjectName { get; set; }
        /// <summary>로드된 프로그램 이름입니다.</summary>
        public string ProgramName { get; set; }
        /// <summary>장치가 반환한 오류 문자열입니다.</summary>
        public string ErrorText { get; set; }
        /// <summary>1~7축의 위치 값입니다.</summary>
        public float[] DbAxisPos { get; set; }
        /// <summary>직교 좌표 X, Y, Z, A, B, C입니다.</summary>
        public float[] DbCartPos { get; set; }
        /// <summary>1~7축의 속도입니다.</summary>
        public float[] DbAxisSpeed { get; set; }
        /// <summary>1~7축의 가속도입니다.</summary>
        public float[] DbAxisAcc { get; set; }
        /// <summary>1~7축의 가속도 변화율입니다.</summary>
        public float[] DbAxisAccAcc { get; set; }
        /// <summary>1~7축의 토크입니다.</summary>
        public float[] DbAxisTorque { get; set; }
        /// <summary>1~7축의 방향 반전 횟수입니다.</summary>
        public int[] DbAxisDirCnt { get; set; }
        /// <summary>1~7축의 누적 동작 시간입니다.</summary>
        public int[] DbAxisTime { get; set; }
        /// <summary>장치의 누적 가동 시간입니다.</summary>
        public int DbDeviceTime { get; set; }
        /// <summary>패킷 종료 문자열입니다.</summary>
        public string PacketEnd { get; set; }

        /// <summary>최소 784바이트인 이전 형식의 상태 패킷을 해석합니다. 부족한 길이는 실패 결과를 반환합니다.</summary>
        public static OperationResult<EfortControllerState> ParseFromPrevious(byte[] data)
        {
            if (data.Length < 784)
                return new OperationResult<EfortControllerState>(string.Format("데이터 길이가 부족합니다. 필요: {0}, 실제: {1}", 784, data.Length));
            // 데이터 분석 시작
            EfortControllerState efortData = new EfortControllerState();
            efortData.PacketStart = Encoding.ASCII.GetString(data, 0, 15).Trim();
            efortData.PacketOrders = BitConverter.ToUInt16(data, 17);
            efortData.PacketHeartbeat = BitConverter.ToUInt16(data, 19);
            efortData.ErrorStatus = data[21];
            efortData.HstopStatus = data[22];
            efortData.AuthorityStatus = data[23];
            efortData.ServoStatus = data[24];
            efortData.AxisMoveStatus = data[25];
            efortData.ProgMoveStatus = data[26];
            efortData.ProgLoadStatus = data[27];
            efortData.ProgHoldStatus = data[28];
            efortData.ModeStatus = BitConverter.ToUInt16(data, 29);
            efortData.SpeedStatus = BitConverter.ToUInt16(data, 31);
            for (int i = 0; i < 32; i++)
            {
                efortData.IoDOut[i] = data[33 + i];
            }

            for (int i = 0; i < 32; i++)
            {
                efortData.IoDIn[i] = data[65 + i];
            }

            for (int i = 0; i < 32; i++)
            {
                efortData.IoIOut[i] = BitConverter.ToInt32(data, 97 + 4 * i);
            }

            for (int i = 0; i < 32; i++)
            {
                efortData.IoIIn[i] = BitConverter.ToInt32(data, 225 + 4 * i);
            }

            efortData.ProjectName = Encoding.ASCII.GetString(data, 353, 32).Trim('\u0000');
            efortData.ProgramName = Encoding.ASCII.GetString(data, 385, 32).Trim('\u0000');
            efortData.ErrorText = Encoding.ASCII.GetString(data, 417, 128).Trim('\u0000');
            for (int i = 0; i < 7; i++)
            {
                efortData.DbAxisPos[i] = BitConverter.ToSingle(data, 545 + 4 * i);
            }

            for (int i = 0; i < 6; i++)
            {
                efortData.DbCartPos[i] = BitConverter.ToSingle(data, 573 + 4 * i);
            }

            for (int i = 0; i < 7; i++)
            {
                efortData.DbAxisSpeed[i] = BitConverter.ToSingle(data, 597 + 4 * i);
            }

            for (int i = 0; i < 7; i++)
            {
                efortData.DbAxisAcc[i] = BitConverter.ToSingle(data, 625 + 4 * i);
            }

            for (int i = 0; i < 7; i++)
            {
                efortData.DbAxisAccAcc[i] = BitConverter.ToSingle(data, 653 + 4 * i);
            }

            for (int i = 0; i < 7; i++)
            {
                efortData.DbAxisTorque[i] = BitConverter.ToSingle(data, 681 + 4 * i);
            }

            for (int i = 0; i < 7; i++)
            {
                efortData.DbAxisDirCnt[i] = BitConverter.ToInt32(data, 709 + 4 * i);
            }

            for (int i = 0; i < 7; i++)
            {
                efortData.DbAxisTime[i] = BitConverter.ToInt32(data, 737 + 4 * i);
            }

            efortData.DbDeviceTime = BitConverter.ToInt32(data, 765);
            efortData.PacketEnd = Encoding.ASCII.GetString(data, 769, 15).Trim();
            return OperationResult.CreateSuccessResult(efortData);
        }

        /// <summary>최소 788바이트인 현재 형식의 상태 패킷을 해석합니다. 부족한 길이는 실패 결과를 반환합니다.</summary>
        public static OperationResult<EfortControllerState> ParseFrom(byte[] data)
        {
            if (data.Length < 788)
                return new OperationResult<EfortControllerState>(string.Format("데이터 길이가 부족합니다. 필요: {0}, 실제: {1}", 788, data.Length));
            // 데이터 분석 시작
            EfortControllerState efortData = new EfortControllerState();
            efortData.PacketStart = Encoding.ASCII.GetString(data, 0, 16).Trim();
            efortData.PacketOrders = BitConverter.ToUInt16(data, 18);
            efortData.PacketHeartbeat = BitConverter.ToUInt16(data, 20);
            efortData.ErrorStatus = data[22];
            efortData.HstopStatus = data[23];
            efortData.AuthorityStatus = data[24];
            efortData.ServoStatus = data[25];
            efortData.AxisMoveStatus = data[26];
            efortData.ProgMoveStatus = data[27];
            efortData.ProgLoadStatus = data[28];
            efortData.ProgHoldStatus = data[29];
            efortData.ModeStatus = BitConverter.ToUInt16(data, 30);
            efortData.SpeedStatus = BitConverter.ToUInt16(data, 32);
            for (int i = 0; i < 32; i++)
            {
                efortData.IoDOut[i] = data[34 + i];
            }

            for (int i = 0; i < 32; i++)
            {
                efortData.IoDIn[i] = data[66 + i];
            }

            for (int i = 0; i < 32; i++)
            {
                efortData.IoIOut[i] = BitConverter.ToInt32(data, 100 + 4 * i);
            }

            for (int i = 0; i < 32; i++)
            {
                efortData.IoIIn[i] = BitConverter.ToInt32(data, 228 + 4 * i);
            }

            efortData.ProjectName = Encoding.ASCII.GetString(data, 356, 32).Trim('\u0000');
            efortData.ProgramName = Encoding.ASCII.GetString(data, 388, 32).Trim('\u0000');
            efortData.ErrorText = Encoding.ASCII.GetString(data, 420, 128).Trim('\u0000');
            for (int i = 0; i < 7; i++)
            {
                efortData.DbAxisPos[i] = BitConverter.ToSingle(data, 548 + 4 * i);
            }

            for (int i = 0; i < 6; i++)
            {
                efortData.DbCartPos[i] = BitConverter.ToSingle(data, 576 + 4 * i);
            }

            for (int i = 0; i < 7; i++)
            {
                efortData.DbAxisSpeed[i] = BitConverter.ToSingle(data, 600 + 4 * i);
            }

            for (int i = 0; i < 7; i++)
            {
                efortData.DbAxisAcc[i] = BitConverter.ToSingle(data, 628 + 4 * i);
            }

            for (int i = 0; i < 7; i++)
            {
                efortData.DbAxisAccAcc[i] = BitConverter.ToSingle(data, 656 + 4 * i);
            }

            for (int i = 0; i < 7; i++)
            {
                efortData.DbAxisTorque[i] = BitConverter.ToSingle(data, 684 + 4 * i);
            }

            for (int i = 0; i < 7; i++)
            {
                efortData.DbAxisDirCnt[i] = BitConverter.ToInt32(data, 712 + 4 * i);
            }

            for (int i = 0; i < 7; i++)
            {
                efortData.DbAxisTime[i] = BitConverter.ToInt32(data, 740 + 4 * i);
            }

            efortData.DbDeviceTime = BitConverter.ToInt32(data, 768);
            efortData.PacketEnd = Encoding.ASCII.GetString(data, 772, 16).Trim();
            return OperationResult.CreateSuccessResult(efortData);
        }
    }
}
