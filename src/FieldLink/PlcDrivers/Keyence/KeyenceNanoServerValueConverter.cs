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
using static FieldLink.PlcDrivers.Keyence.KeyenceNanoServerResponseParser;
using static FieldLink.PlcDrivers.Keyence.KeyenceNanoServerDefinitions;

namespace FieldLink.PlcDrivers.Keyence
{
    /// <summary>KeyenceNanoServer 프로토콜의 값과 바이트 표현을 변환합니다.</summary>
    public static class KeyenceNanoServerValueConverter
    {
        /// <summary>GetBoolResponseData 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] GetBoolResponseData(byte[] data)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < data.Length; i++)
            {
                sb.Append(data[i]);
                if (i != data.Length - 1)
                    sb.Append(" ");
            }

            sb.Append("\r\n");
            return Encoding.ASCII.GetBytes(sb.ToString());
        }

        /// <summary>GetWordResponseData 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] GetWordResponseData(byte[] data)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < data.Length / 2; i++)
            {
                sb.Append(BitConverter.ToUInt16(data, i * 2));
                if (i != data.Length / 2 - 1)
                    sb.Append(" ");
            }

            sb.Append("\r\n");
            return Encoding.ASCII.GetBytes(sb.ToString());
        }

        /// <summary>GetDoubleWordResponseData 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] GetDoubleWordResponseData(byte[] data)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < data.Length / 4; i++)
            {
                sb.Append(BitConverter.ToUInt32(data, i * 4));
                if (i != data.Length / 4 - 1)
                    sb.Append(" ");
            }

            sb.Append("\r\n");
            return Encoding.ASCII.GetBytes(sb.ToString());
        }
    }
}
