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

namespace FieldLink.PlcDrivers.Panasonic
{
    /// <summary>PanasonicMcNet 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class PanasonicMcNetResponseParser
    {
        /// <summary>UnpackResponseContent 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "send">send에 사용할 입력값입니다.</param>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> UnpackResponseContent(byte[] send, byte[] response)
        {
            ushort errorCode = BitConverter.ToUInt16(response, 9);
            if (errorCode != 0)
                return new OperationResult<byte[]>(errorCode, PanasonicValueConverter.GetMcErrorDescription(errorCode));
            return OperationResult.CreateSuccessResult(response.RemoveBegin(11));
        }
    }
}
