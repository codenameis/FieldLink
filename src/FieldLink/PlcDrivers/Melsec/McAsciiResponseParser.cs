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
using static FieldLink.PlcDrivers.Melsec.McAsciiCommandBuilder;

namespace FieldLink.PlcDrivers.Melsec
{
    /// <summary>McAscii 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class McAsciiResponseParser
    {
        /// <summary>프로토콜 응답에서 데이터와 상태 정보를 추출합니다.</summary>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <param name = "isBit">비트 단위 요청이면 true입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] ExtractActualDataHelper(byte[] response, bool isBit)
        {
            if (isBit)
                return response.Select(m => m == 0x30 ? (byte)0x00 : (byte)0x01).ToArray();
            else
                return MelsecValueConverter.TransAsciiByteArrayToByteArray(response);
        }

        /// <summary>입력 데이터가 프로토콜의 검증 조건을 만족하는지 확인합니다.</summary>
        /// <param name = "content">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult CheckResponseContent(byte[] content)
        {
            if (content == null || content.Length < 22)
                return new OperationResult(ProtocolMessages.ReceiveDataLengthTooShort + "22, Content: " + ProtocolBytes.GetAsciiStringRender(content));
            // SLMP SH-080956ENG-N §4.2: ASCII length counts bytes, including 4 end-code characters.
            if (content[0] != 'D' || content[1] != '0' || content[2] != '0' || content[3] != '0')
                return new OperationResult("MC 3E ASCII 응답 서브헤더가 올바르지 않습니다.");
            if (!ushort.TryParse(Encoding.ASCII.GetString(content, 14, 4), NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture, out ushort length) || length < 4 || content.Length != 18 + length)
                return new OperationResult("MC ASCII 응답의 선언 길이와 실제 길이가 다릅니다.");
            if (!ushort.TryParse(Encoding.ASCII.GetString(content, 18, 4), NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture, out ushort errorCode))
                return new OperationResult("MC ASCII 응답의 종료 코드가 16진수가 아닙니다.");
            if (errorCode != 0)
                return new OperationResult(errorCode, MelsecResponseParser.GetErrorDescription(errorCode));
            return OperationResult.CreateSuccessResult();
        }
    }
}
