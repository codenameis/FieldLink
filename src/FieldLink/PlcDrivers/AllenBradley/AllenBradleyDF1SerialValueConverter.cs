using FieldLink.PlcDrivers.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using static FieldLink.PlcDrivers.AllenBradley.AllenBradleyDF1SerialCommandBuilder;
using static FieldLink.PlcDrivers.AllenBradley.AllenBradleyDF1SerialResponseParser;

namespace FieldLink.PlcDrivers.AllenBradley
{
    /// <summary>AllenBradleyDF1Serial 프로토콜의 값과 바이트 표현을 변환합니다.</summary>
    public static class AllenBradleyDF1SerialValueConverter
    {
        /// <summary>AddLengthToMemoryStream 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "ms">ms에 사용할 입력값입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        internal static void AddLengthToMemoryStream(MemoryStream ms, ushort value)
        {
            if (value < 255)
                ms.WriteByte((byte)value); // 파일 번호
            else
            {
                ms.WriteByte(0xFF);
                ms.WriteByte(BitConverter.GetBytes(value)[0]);
                ms.WriteByte(BitConverter.GetBytes(value)[1]);
            }
        }

        /// <summary>CalculateCheckResult 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "command">포장할 명령 바이트입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] CalculateCheckResult(Df1FrameOptions context, byte station, byte[] command)
        {
            if (context.CheckType == CheckType.BCC)
            {
                int sum = station;
                for (int i = 0; i < command.Length; i++)
                {
                    sum += command[i];
                }

                sum = (byte)~sum;
                sum += 1;
                return new byte[]
                {
                    (byte)sum
                };
            }
            else
            {
                byte[] buffer = ProtocolBytes.SpliceArray(new byte[] { station }, new byte[] { 0x02 }, command, new byte[] { 0x03 });
                return Crc16.Append(buffer, 0xA001, 0).SelectLast(2);
            }
        }
    }
}
