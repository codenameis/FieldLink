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

namespace FieldLink.PlcDrivers.Fuji
{
    /// <summary>FujiSPBServer 프로토콜의 값과 바이트 표현을 변환합니다.</summary>
    public static class FujiSPBServerValueConverter
    {
        /// <summary>CreateResponseBack 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "err">err에 사용할 입력값입니다.</param>
        /// <param name = "command">포장할 명령 바이트입니다.</param>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "addLength">addLength에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] CreateResponseBack(FujiSpbResponseOptions context, byte err, string command, byte[] data, bool addLength = true)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(':');
            sb.Append(context.Station.ToString("X2"));
            sb.Append("00");
            sb.Append(command.Substring(9, 4));
            sb.Append(err.ToString("X2"));
            if (err == 0 && data != null)
            {
                if (addLength == true)
                    sb.Append(FujiSpbAddressParser.AnalysisIntegerAddress(data.Length / 2));
                sb.Append(data.ToHexString());
            }

            sb[3] = ((sb.Length - 5) / 2).ToString("X2")[0];
            sb[4] = ((sb.Length - 5) / 2).ToString("X2")[1];
            sb.Append("\r\n");
            return Encoding.ASCII.GetBytes(sb.ToString());
        }
    }
}
