using FieldLink.PlcDrivers.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FieldLink.Robot.FANUC.Protocols
{
    /// <summary>FANUC의 100바이트 자세 레코드입니다.</summary>
    public class FanucPose
    {
        /// <summary>직교 자세 및 확장 축 좌표 9개입니다.</summary>
        public float[] Xyzwpr { get; set; }
        /// <summary>F/N, L/R, U/D, T/B와 회전 수 3개로 구성된 자세 설정입니다.</summary>
        public string[] Config { get; set; }
        /// <summary>관절 좌표 및 확장 축 값 9개입니다.</summary>
        public float[] Joint { get; set; }
        /// <summary>사용자 좌표계 번호입니다.</summary>
        public short UF { get; set; }
        /// <summary>공구 번호입니다.</summary>
        public short UT { get; set; }
        /// <summary>직교 좌표의 유효 플래그입니다.</summary>
        public short ValidC { get; set; }
        /// <summary>관절 좌표의 유효 플래그입니다.</summary>
        public short ValidJ { get; set; }

        /// <summary>지정 위치에서 자세·구성·유효 플래그·좌표계·공구를 읽습니다.</summary>
        public void LoadByContent(IProtocolValueConverter byteTransform, byte[] content, int index)
        {
            Xyzwpr = new float[9];
            for (int i = 0; i < Xyzwpr.Length; i++)
            {
                Xyzwpr[i] = BitConverter.ToSingle(content, index + 4 * i);
            }

            Config = TransConfigStringArray(byteTransform.ReadInt16(content, index + 36, 7));
            Joint = new float[9];
            for (int i = 0; i < Joint.Length; i++)
            {
                Joint[i] = BitConverter.ToSingle(content, index + 52 + 4 * i);
            }

            ValidC = BitConverter.ToInt16(content, index + 50);
            ValidJ = BitConverter.ToInt16(content, index + 88);
            UF = BitConverter.ToInt16(content, index + 90);
            UT = BitConverter.ToInt16(content, index + 92);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder($"FanucPose UF={UF} UT={UT}");
            if (ValidC != 0)
            {
                sb.Append($"\r\nXyzwpr={ProtocolBytes.ArrayFormat(Xyzwpr)}\r\nConfig={ProtocolBytes.ArrayFormat(Config)}");
            }

            if (ValidJ != 0)
            {
                sb.Append($"\r\nJOINT={ProtocolBytes.ArrayFormat(Joint)}");
            }

            return sb.ToString();
        }

        /// <summary>지정 위치의 자세 레코드를 새 객체로 해석합니다.</summary>
        public static FanucPose ParseFrom(IProtocolValueConverter byteTransform, byte[] content, int index)
        {
            FanucPose fanucPose = new FanucPose();
            fanucPose.LoadByContent(byteTransform, content, index);
            return fanucPose;
        }

        /// <summary>7개의 구성 값을 네 개의 자세 표식과 세 개의 회전 수 문자열로 변환합니다.</summary>
        public static string[] TransConfigStringArray(short[] value)
        {
            string[] array = new string[7];
            array[0] = value[0] != 0 ? "F" : "N";
            array[1] = value[1] != 0 ? "L" : "R";
            array[2] = value[2] != 0 ? "U" : "D";
            array[3] = value[3] != 0 ? "T" : "B";
            array[4] = value[4].ToString();
            array[5] = value[5].ToString();
            array[6] = value[6].ToString();
            return array;
        }
    }
}
