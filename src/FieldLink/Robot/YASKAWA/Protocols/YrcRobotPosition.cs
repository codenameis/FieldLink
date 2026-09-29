using FieldLink.PlcDrivers.Common;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace FieldLink.Robot.YASKAWA.Protocols
{
    /// <summary>YRC 좌표·자세·공구·외부 축 데이터입니다. Re는 YRC100에서만 사용합니다.</summary>
    public class YrcRobotPosition
    {
        /// <summary>YRC 좌표·자세·공구·외부 축 데이터입니다. Re는 YRC100에서만 사용합니다.</summary>
        public YrcRobotPosition()
        {
            this.SpeedPercent = "100.0%";
            this.Status = new bool[6];
        }

        /// <summary>YRC 좌표·자세·공구·외부 축 데이터입니다. Re는 YRC100에서만 사용합니다.</summary>
        public YrcRobotPosition(YrcControllerModel type, string value) : this()
        {
            Parse(type, value);
        }

        /// <summary>
        /// 동작 속도(0.01 ～ 100.0%)
        /// </summary>
        /// <remarks>
        /// 읽기에는 아무런 의미가 없으며, 쓰기에만 유효합니다. 100.0%
        /// </remarks>
        public string SpeedPercent { get; set; }
        /// <summary>
        /// 참조 계, 0: 기지 좌표, 1: 로봇 좌표, 2-65 각각 사용자 좌표 1-64를 나타냅니다.
        /// </summary>
        public int Frame { get; set; }
        /// <summary>
        /// X 좌표값 ((단위 mm, 소수점 3자리 유효)
        /// </summary>
        public float X { get; set; }
        /// <summary>
        /// Y 좌표값 ((단위 mm, 소수점 3자리 유효)
        /// </summary>
        public float Y { get; set; }
        /// <summary>
        /// Z 좌표 값 ((단위 mm, 소수점 3자리 유효)
        /// </summary>
        public float Z { get; set; }
        /// <summary>
        /// 손목 자세 Rx 좌표 값 ((단위 °, 소수점 4자리 유효)
        /// </summary>
        public float Rx { get; set; }
        /// <summary>
        /// 손목 자세 Ry 좌표 값 ((단위 °, 소수점 4자리 유효)
        /// </summary>
        public float Ry { get; set; }
        /// <summary>
        /// 손목 자세 Rz 좌표 값 ((단위 °, 소수점 4 자리 유효)
        /// </summary>
        public float Rz { get; set; }
        /// <summary>7축 모델 YRC100의 추가 회전 좌표입니다.</summary>
        public float Re { get; set; }
        /// <summary>
        /// 형태적 데이터, 각 인덱스의 의미는 [0] 0: F lip,1:N o Flip [1] 0: 상부 팔꿈치,1: 하부 팔꿈치 [2] 0: 전면,1: 뒷면 [3] 0:R&lt;180, 1:R≥180 [4] 0:T&lt;180, 1:T≥180 [5] 0:S&lt;180, 1:S≥180
        /// </summary>
        public bool[] Status { get; set; }
        /// <summary>
        /// 도구 번호 ((0  63)
        /// </summary>
        public int ToolNumber { get; set; }
        /// <summary>
        /// 제 7 축 펄스 수 ((축 이동 시간, 단위 mm)
        /// </summary>
        public int Axis7PulseNumber { get; set; }
        /// <summary>
        /// 제 8 축 펄스 수 ((축 이동 시간, 단위 mm)
        /// </summary>
        public int Axis8PulseNumber { get; set; }
        /// <summary>
        /// 제9 축 펄스 수 ((행동축 시간, 단위 mm)
        /// </summary>
        public int Axis9PulseNumber { get; set; }
        /// <summary>
        /// 10축 펄스수
        /// </summary>
        public int Axis10PulseNumber { get; set; }
        /// <summary>
        /// 11축 펄스수
        /// </summary>
        public int Axis11PulseNumber { get; set; }
        /// <summary>
        /// 12축 펄스수
        /// </summary>
        public int Axis12PulseNumber { get; set; }

        /// <summary>속도·좌표계·위치·자세·공구·외부 축을 MOVJ 본문으로 변환합니다. 숫자는 소수점 마침표를 사용합니다.</summary>
        public string ToWriteString(YrcControllerModel type)
        {
            /* 이전 구현 — R-008
            if (type == YRCType.YRC100)
                return $"{SpeedPercent},{Frame},{X},{Y},{Z},{Rx},{Ry},{Rz},{Re},{ProtocolBytes.BoolArrayToByte(Status)[0]}," + $"{ToolNumber},{Axis7PulseNumber},{Axis8PulseNumber},{Axis9PulseNumber},{Axis10PulseNumber},{Axis11PulseNumber},{Axis12PulseNumber}";
            else
                return $"{SpeedPercent},{Frame},{X},{Y},{Z},{Rx},{Ry},{Rz},{ProtocolBytes.BoolArrayToByte(Status)[0]}," + $"{ToolNumber},{Axis7PulseNumber},{Axis8PulseNumber},{Axis9PulseNumber},{Axis10PulseNumber},{Axis11PulseNumber},{Axis12PulseNumber}";
            소수점 쉼표가 필드 구분자와 충돌하므로 송수신 모두 고정 문화권을 사용한다.
            */
            string position = FormattableString.Invariant($"{SpeedPercent},{Frame},{X},{Y},{Z},{Rx},{Ry},{Rz},");
            if (type == YrcControllerModel.YRC100)
                position += Re.ToString(CultureInfo.InvariantCulture) + ",";
            return position + FormattableString.Invariant($"{ProtocolBytes.BoolArrayToByte(Status)[0]},{ToolNumber},{Axis7PulseNumber},{Axis8PulseNumber},{Axis9PulseNumber},{Axis10PulseNumber},{Axis11PulseNumber},{Axis12PulseNumber}");
        }

        /// <summary>쉼표로 구분한 위치 응답을 고정 문화권으로 해석하며 외부 축이 있으면 6개를 모두 읽습니다.</summary>
        public void Parse(YrcControllerModel type, string value)
        {
            string[] datas = value.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            int index = 0;
            // 이전 구현: float.Parse(datas[index++]); R-008의 소수점 구분을 읽기에도 동일하게 적용한다.
            X = float.Parse(datas[index++], NumberStyles.Float, CultureInfo.InvariantCulture);
            Y = float.Parse(datas[index++], NumberStyles.Float, CultureInfo.InvariantCulture);
            Z = float.Parse(datas[index++], NumberStyles.Float, CultureInfo.InvariantCulture);
            Rx = float.Parse(datas[index++], NumberStyles.Float, CultureInfo.InvariantCulture);
            Ry = float.Parse(datas[index++], NumberStyles.Float, CultureInfo.InvariantCulture);
            Rz = float.Parse(datas[index++], NumberStyles.Float, CultureInfo.InvariantCulture);
            if (type == YrcControllerModel.YRC100)
                Re = float.Parse(datas[index++], NumberStyles.Float, CultureInfo.InvariantCulture);
            Status = new byte[]
            {
                byte.Parse(datas[index++])
            }.ToBoolArray().SelectBegin(6);
            ToolNumber = int.Parse(datas[index++]);
            if (datas.Length > index)
            {
                Axis7PulseNumber = int.Parse(datas[index++]);
                Axis8PulseNumber = int.Parse(datas[index++]);
                Axis9PulseNumber = int.Parse(datas[index++]);
                Axis10PulseNumber = int.Parse(datas[index++]);
                Axis11PulseNumber = int.Parse(datas[index++]);
                Axis12PulseNumber = int.Parse(datas[index++]);
            }
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"[{X},{Y},{Z},{Rx},{Ry},{Rz}]";
        }
    }
}
