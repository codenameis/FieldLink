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
using static FieldLink.PlcDrivers.Yokogawa.YokogawaLinkTcpCommandBuilder;

namespace FieldLink.PlcDrivers.Yokogawa
{
    /// <summary>YokogawaLinkTcp 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class YokogawaLinkTcpResponseParser
    {
        /// <summary>입력 데이터가 프로토콜의 검증 조건을 만족하는지 확인합니다.</summary>
        /// <param name = "content">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> CheckContent(byte[] content)
        {
            // IM34M06H24-08E §5.3.2: response subheader, exit code, big-endian data length.
            if (content == null || content.Length < 4)
                return new OperationResult<byte[]>(ProtocolMessages.ReceiveDataLengthTooShort + " 4");
            if ((content[0] & 0x80) == 0 || ((content[2] << 8) | content[3]) != content.Length - 4)
                return new OperationResult<byte[]>("Invalid Yokogawa response subheader or data length.");
            if (content[1] != 0x00)
                return new OperationResult<byte[]>(YokogawaLinkResponseParser.GetErrorMsg(content[1]));
            if (content.Length > 4)
                return OperationResult.CreateSuccessResult(content.RemoveBegin(4));
            else
                return OperationResult.CreateSuccessResult(new byte[0]);
        }
    }
}
