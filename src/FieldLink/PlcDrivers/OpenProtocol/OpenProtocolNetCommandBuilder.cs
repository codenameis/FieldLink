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
using static FieldLink.PlcDrivers.OpenProtocol.OpenProtocolNetResponseParser;
using static FieldLink.PlcDrivers.OpenProtocol.OpenProtocolNetValueConverter;

namespace FieldLink.PlcDrivers.OpenProtocol
{
    /// <summary>OpenProtocolNet 요청 프레임을 생성합니다.</summary>
    public static class OpenProtocolNetCommandBuilder
    {
        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "mid">mid에 사용할 입력값입니다.</param>
        /// <param name = "revison">revison에 사용할 입력값입니다.</param>
        /// <param name = "stationId">stationId에 사용할 입력값입니다.</param>
        /// <param name = "spindleId">spindleId에 사용할 입력값입니다.</param>
        /// <param name = "parameters">parameters에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildReadCommand(int mid, int revison, int stationId, int spindleId, List<string> parameters)
        {
            if (mid < 0 || mid > 9999)
                return new OperationResult<byte[]>("Mid must be between 0 - 9999");
            if (revison < 0 || revison > 999)
                return new OperationResult<byte[]>("revison must be between 0 - 999");
            if (stationId > 9)
                return new OperationResult<byte[]>("stationId must be between 0 - 9");
            if (spindleId > 99)
                return new OperationResult<byte[]>("spindleId must be between 0 - 99");
            int count = 0;
            if (parameters != null)
                parameters.ForEach(m => count += m.Length);
            StringBuilder sb = new StringBuilder();
            sb.Append((20 + count).ToString("D4"));
            sb.Append(mid.ToString("D4"));
            sb.Append(revison.ToString("D3"));
            sb.Append('0'); // ack 플래그 없음: 0
            sb.Append(stationId < 0 ? " " : stationId.ToString("D1"));
            sb.Append(spindleId < 0 ? "  " : spindleId.ToString("D2"));
            sb.Append(' ');
            sb.Append(' ');
            sb.Append(' ');
            sb.Append(' ');
            sb.Append(' ');
            if (parameters != null)
                for (int i = 0; i < parameters.Count; i++)
                {
                    sb.Append(parameters[i]);
                }

            sb.Append('\0');
            return OperationResult.CreateSuccessResult(Encoding.ASCII.GetBytes(sb.ToString()));
        }
    }
}
