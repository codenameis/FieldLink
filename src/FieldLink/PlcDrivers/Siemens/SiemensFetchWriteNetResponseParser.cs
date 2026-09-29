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
using static FieldLink.PlcDrivers.Siemens.SiemensFetchWriteNetAddressParser;
using static FieldLink.PlcDrivers.Siemens.SiemensFetchWriteNetCommandBuilder;

namespace FieldLink.PlcDrivers.Siemens
{
    /// <summary>SiemensFetchWriteNet 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class SiemensFetchWriteNetResponseParser
    {
        /// <summary>입력 데이터가 프로토콜의 검증 조건을 만족하는지 확인합니다.</summary>
        /// <param name = "content">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult CheckResponseContent(byte[] content)
        {
            if (content == null || content.Length < 9)
                return new OperationResult(ProtocolMessages.ReceiveDataLengthTooShort + "9, Content: " + content.ToHexString(' '));
            if (content[8] != 0x00)
                return new OperationResult(content[8], ProtocolMessages.SiemensWriteError + content[8]);
            return OperationResult.CreateSuccessResult();
        }
    }
}
