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
using static FieldLink.PlcDrivers.Turck.ReaderNetResponseParser;
using static FieldLink.PlcDrivers.Turck.ReaderNetAddressParser;
using static FieldLink.PlcDrivers.Turck.ReaderNetCommandBuilder;

namespace FieldLink.PlcDrivers.Turck
{
    /// <summary>ReaderNet 프로토콜의 값과 바이트 표현을 변환합니다.</summary>
    public static class ReaderNetValueConverter
    {
        /// <summary>CalculateCRC 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "len">len에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] CalculateCRC(byte[] data, int len)
        {
            int crc = 0xFFFF;
            int poly = 0x8408;
            byte[] buffer = new byte[2];
            for (int i = 0; i < len; i++)
            {
                crc = crc ^ (int)data[i];
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 0x0001) == 1)
                    {
                        crc = (crc >> 1) ^ poly;
                    }
                    else
                    {
                        crc = (crc >> 1);
                    }
                }
            }

            crc = ~crc;
            buffer[0] = Convert.ToByte(crc & 0xff);
            buffer[1] = Convert.ToByte((crc >> 8) & 0xff);
            return buffer;
        }

        /// <summary>CalculateAndFillCRC 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "len">len에 사용할 입력값입니다.</param>
        public static void CalculateAndFillCRC(byte[] data, int len)
        {
            byte[] crc = CalculateCRC(data, len);
            data[len + 0] = crc[0];
            data[len + 1] = crc[1];
        }
    }
}
