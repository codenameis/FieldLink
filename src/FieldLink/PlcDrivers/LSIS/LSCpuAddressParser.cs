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
using static FieldLink.PlcDrivers.LSIS.LSCpuResponseParser;
using static FieldLink.PlcDrivers.LSIS.LSCpuValueConverter;
using static FieldLink.PlcDrivers.LSIS.LSCpuCommandBuilder;
using static FieldLink.PlcDrivers.LSIS.LSCpuDefinitions;

namespace FieldLink.PlcDrivers.LSIS
{
    /// <summary>LSCpu 주소를 프로토콜의 영역 코드와 위치로 변환합니다.</summary>
    public static class LSCpuAddressParser
    {
        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "transBit">transBit에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<string> AnalysisAddress(string address, bool transBit = false)
        {
            // Bit: P,M,L,K,F,T
            // Continus P,M,L,K,F,T,C,D,S
            StringBuilder sb = new StringBuilder();
            try
            {
                if (!CpuTypes.Contains(address[0]))
                    return new OperationResult<string>(ProtocolMessages.NotSupportedDataType);
                sb.Append(address[0]);
                if (address[0] == 'M')
                {
                    sb.Append("X");
                    if (transBit & address.IndexOf('.') > 0)
                    {
                        int bitIndex = AddressParameters.GetBitIndexInformation(ref address);
                        sb.Append(address.Substring(2));
                        sb.Append(bitIndex.ToString("X1"));
                    }
                    else
                    {
                        sb.Append(address.Substring(2, address.Length - 2));
                    }
                }
                else
                {
                    sb.Append("W");
                    sb.Append(Convert.ToInt32(address.Substring(2, address.Length - 2)));
                }
            }
            catch (Exception ex)
            {
                return new OperationResult<string>(ex.Message);
            }

            return OperationResult.CreateSuccessResult(sb.ToString());
        }
    }
}
