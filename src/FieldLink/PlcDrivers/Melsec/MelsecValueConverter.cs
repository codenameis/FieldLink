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
using static FieldLink.PlcDrivers.Melsec.MelsecAddressParser;
using static FieldLink.PlcDrivers.Melsec.MelsecResponseParser;
using static FieldLink.PlcDrivers.Melsec.MelsecCommandBuilder;

namespace FieldLink.PlcDrivers.Melsec
{
    /// <summary>Melsec 프로토콜의 값과 바이트 표현을 변환합니다.</summary>
    public static class MelsecValueConverter
    {
        /// <summary>TransBoolArrayToByteData 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static byte[] TransBoolArrayToByteData(byte[] value) => TransBoolArrayToByteData(value.Select(m => m != 0x00).ToArray());
        /// <summary>TransByteArrayToBoolData 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "offset">offset에 사용할 입력값입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static bool[] TransByteArrayToBoolData(byte[] value, int offset, int length)
        {
            bool[] result = new bool[length > (value.Length - offset) * 2 ? (value.Length - offset) * 2 : length];
            for (int i = 0; i < result.Length; i++)
            {
                if (i % 2 == 0)
                    result[i] = (value[offset + i / 2] & 0x10) == 0x10;
                else
                    result[i] = (value[offset + i / 2] & 0x01) == 0x01;
            }

            return result;
        }

        /// <summary>TransBoolArrayToByteData 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static byte[] TransBoolArrayToByteData(bool[] value)
        {
            int length = (value.Length + 1) / 2;
            byte[] buffer = new byte[length];
            for (int i = 0; i < length; i++)
            {
                if (value[i * 2 + 0])
                    buffer[i] += 0x10;
                if ((i * 2 + 1) < value.Length)
                {
                    if (value[i * 2 + 1])
                        buffer[i] += 0x01;
                }
            }

            return buffer;
        }

        /// <summary>TransByteArrayToAsciiByteArray 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static byte[] TransByteArrayToAsciiByteArray(byte[] value)
        {
            if (value == null)
                return new byte[0];
            byte[] buffer = new byte[value.Length * 2];
            for (int i = 0; i < value.Length / 2; i++)
            {
                ProtocolBytes.BuildAsciiBytesFrom(BitConverter.ToUInt16(value, i * 2)).CopyTo(buffer, 4 * i);
            }

            return buffer;
        }

        /// <summary>TransAsciiByteArrayToByteArray 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static byte[] TransAsciiByteArrayToByteArray(byte[] value)
        {
            byte[] Content = new byte[value.Length / 2];
            for (int i = 0; i < Content.Length / 2; i++)
            {
                ushort tmp = Convert.ToUInt16(Encoding.ASCII.GetString(value, i * 4, 4), 16);
                BitConverter.GetBytes(tmp).CopyTo(Content, i * 2);
            }

            return Content;
        }

        /// <summary>FxCalculateCRC 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "start">start에 사용할 입력값입니다.</param>
        /// <param name = "tail">tail에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static byte[] FxCalculateCRC(byte[] data, int start = 1, int tail = 2)
        {
            int sum = 0;
            for (int i = start; i < data.Length - tail; i++)
            {
                sum += data[i];
            }

            return ProtocolBytes.BuildAsciiBytesFrom((byte)sum);
        }
    }
}
