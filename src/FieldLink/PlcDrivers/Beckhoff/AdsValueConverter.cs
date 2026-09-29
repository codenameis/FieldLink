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
using static FieldLink.PlcDrivers.Beckhoff.AdsCommandBuilder;
using static FieldLink.PlcDrivers.Beckhoff.AdsResponseParser;
using static FieldLink.PlcDrivers.Beckhoff.AdsAddressParser;

namespace FieldLink.PlcDrivers.Beckhoff
{
    /// <summary>Ads 프로토콜의 값과 바이트 표현을 변환합니다.</summary>
    public static class AdsValueConverter
    {
        /// <summary>StrToAdsBytes 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] StrToAdsBytes(string value)
        {
            return ProtocolBytes.SpliceArray(Encoding.ASCII.GetBytes(value), new byte[1]);
        }

        /// <summary>StrToAMSNetId 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "amsNetId">amsNetId에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] StrToAMSNetId(string amsNetId)
        {
            byte[] buffer;
            string ip = amsNetId;
            if (amsNetId.IndexOf(':') > 0)
            {
                buffer = new byte[8];
                string[] ipPort = amsNetId.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                ip = ipPort[0];
                buffer[6] = BitConverter.GetBytes(int.Parse(ipPort[1]))[0];
                buffer[7] = BitConverter.GetBytes(int.Parse(ipPort[1]))[1];
            }
            else
            {
                buffer = new byte[6];
            }

            string[] ips = ip.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < ips.Length; i++)
            {
                buffer[i] = byte.Parse(ips[i]);
            }

            return buffer;
        }

        /// <summary>GetAmsNetIdString 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "index">데이터를 읽거나 기록할 위치입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string GetAmsNetIdString(byte[] data, int index)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(data[index]);
            sb.Append(".");
            sb.Append(data[index + 1]);
            sb.Append(".");
            sb.Append(data[index + 2]);
            sb.Append(".");
            sb.Append(data[index + 3]);
            sb.Append(".");
            sb.Append(data[index + 4]);
            sb.Append(".");
            sb.Append(data[index + 5]);
            sb.Append(":");
            sb.Append(BitConverter.ToUInt16(data, index + 6));
            return sb.ToString();
        }
    }
}
