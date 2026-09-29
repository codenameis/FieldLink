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

namespace FieldLink.PlcDrivers.Omron
{
    /// <summary>FinsSession 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class FinsSessionResponseParser
    {
        /// <summary>FINS/TCP 노드 주소 응답을 확인하고 context의 SA1·DA1에 반영합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult ParseNodeAddresses(FinsTcpFrameOptions context, byte[] response)
        {
            int status = BitConverter.ToInt32(new byte[] { response[15], response[14], response[13], response[12] }, 0);
            if (status != 0)
                return new OperationResult(status, OmronFinsNetResponseParser.GetStatusDescription(status));
            if (response.Length >= 20)
                context.SA1 = response[19];
            if (response.Length >= 24)
                context.DA1 = response[23];
            return OperationResult.CreateSuccessResult();
        }
    }
}
