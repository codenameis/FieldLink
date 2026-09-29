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
using static FieldLink.PlcDrivers.Yamatake.DigitronCPLResponseParser;

namespace FieldLink.PlcDrivers.Yamatake
{
    /// <summary>DigitronCPL 요청 프레임을 생성합니다.</summary>
    public static class DigitronCPLCommandBuilder
    {
        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildReadCommand(byte station, string address, ushort length)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append('\u0002');
                sb.Append(station.ToString("X2"));
                sb.Append("00XRS,");
                sb.Append(ushort.Parse(address).ToString());
                sb.Append("W,");
                sb.Append(length.ToString());
                sb.Append('\u0003');
                int sum = 0;
                for (int i = 0; i < sb.Length; i++)
                {
                    sum += sb[i];
                }

                byte check = (byte)(256 - sum % 256);
                sb.Append(check.ToString("X2"));
                sb.Append("\u000D\u000A");
                return OperationResult.CreateSuccessResult(Encoding.ASCII.GetBytes(sb.ToString()));
            }
            catch (Exception ex)
            {
                return new OperationResult<byte[]>("Address wrong: " + ex.Message);
            }
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteCommand(byte station, string address, byte[] value)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append('\u0002');
                sb.Append(station.ToString("X2"));
                sb.Append("00XWS,");
                sb.Append(ushort.Parse(address).ToString());
                sb.Append("W");
                for (int i = 0; i < value.Length / 2; i++)
                {
                    short tmp = BitConverter.ToInt16(value, i * 2);
                    sb.Append(",");
                    sb.Append(tmp.ToString());
                }

                sb.Append('\u0003');
                int sum = 0;
                for (int i = 0; i < sb.Length; i++)
                {
                    sum += sb[i];
                }

                byte check = (byte)(256 - sum % 256);
                sb.Append(check.ToString("X2"));
                sb.Append("\u000D\u000A");
                return OperationResult.CreateSuccessResult(Encoding.ASCII.GetBytes(sb.ToString()));
            }
            catch (Exception ex)
            {
                return new OperationResult<byte[]>("Address wrong: " + ex.Message);
            }
        }

        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "err">err에 사용할 입력값입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "dataType">dataType에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackResponseContent(byte station, int err, byte[] value, byte dataType)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append('\u0002');
            sb.Append(station.ToString("X2"));
            sb.Append("00X");
            sb.Append(err.ToString("D2"));
            if (err == 0)
            {
                if (value != null)
                {
                    for (int i = 0; i < value.Length / 2; i++)
                    {
                        if (dataType == 0x57)
                        {
                            short tmp = BitConverter.ToInt16(value, i * 2);
                            sb.Append(",");
                            sb.Append(tmp.ToString());
                        }
                        else
                        {
                            ushort tmp = BitConverter.ToUInt16(value, i * 2);
                            sb.Append(",");
                            sb.Append(tmp.ToString());
                        }
                    }
                }
            }

            sb.Append('\u0003');
            int sum = 0;
            for (int i = 0; i < sb.Length; i++)
            {
                sum += sb[i];
            }

            byte check = (byte)(256 - sum % 256);
            sb.Append(check.ToString("X2"));
            sb.Append("\u000D\u000A");
            return Encoding.ASCII.GetBytes(sb.ToString());
        }
    }
}
