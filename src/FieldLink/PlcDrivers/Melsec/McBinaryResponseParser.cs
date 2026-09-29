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
using static FieldLink.PlcDrivers.Melsec.McBinaryCommandBuilder;

namespace FieldLink.PlcDrivers.Melsec
{
    /// <summary>McBinary 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class McBinaryResponseParser
    {
        /// <summary>입력 데이터가 프로토콜의 검증 조건을 만족하는지 확인합니다.</summary>
        /// <param name = "content">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult CheckResponseContentHelper(byte[] content)
        {
            if (content == null || content.Length < 11)
                return new OperationResult(ProtocolMessages.ReceiveDataLengthTooShort + "11, Content: " + content.ToHexString(' '));
            // SLMP SH-080956ENG-N §4.2: 3E response, length includes the end code.
            if (content[0] != 0xD0 || content[1] != 0x00)
                return new OperationResult("MC 3E 응답 서브헤더가 올바르지 않습니다.");
            int length = content[7] | content[8] << 8;
            if (length < 2 || content.Length != 9 + length)
                return new OperationResult("MC 응답의 선언 길이와 실제 길이가 다릅니다.");
            int errorCode = content[9] | content[10] << 8;
            if (errorCode != 0)
                return new OperationResult<byte[]>(errorCode, MelsecResponseParser.GetErrorDescription(errorCode));
            return OperationResult.CreateSuccessResult();
        }

        /// <summary>프로토콜 응답에서 데이터와 상태 정보를 추출합니다.</summary>
        /// <param name = "content">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> ExtraTagData(byte[] content)
        {
            try
            {
                int count = BitConverter.ToUInt16(content, 0);
                int index = 2;
                List<byte> array = new List<byte>(20);
                for (int i = 0; i < count; i++)
                {
                    int length = BitConverter.ToUInt16(content, index + 2);
                    array.AddRange(ProtocolBytes.ArraySelectMiddle(content, index + 4, length));
                    index += 4 + length;
                }

                return OperationResult.CreateSuccessResult(array.ToArray());
            }
            catch (Exception ex)
            {
                return new OperationResult<byte[]>(ex.Message + " Source:" + ProtocolBytes.ByteToHexString(content, ' '));
            }
        }

        /// <summary>프로토콜 응답에서 데이터와 상태 정보를 추출합니다.</summary>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <param name = "isBit">비트 단위 요청이면 true입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] ExtractActualDataHelper(byte[] response, bool isBit)
        {
            if (response == null || response.Length == 0)
                return response; // 데이터 직접 반환되지 않습니다
            if (isBit)
            {
                // 비트 읽기
                byte[] Content = new byte[response.Length * 2];
                for (int i = 0; i < response.Length; i++)
                {
                    if ((response[i] & 0x10) == 0x10)
                    {
                        Content[i * 2 + 0] = 0x01;
                    }

                    if ((response[i] & 0x01) == 0x01)
                    {
                        Content[i * 2 + 1] = 0x01;
                    }
                }

                return Content;
            }
            else
            {
                // 글 읽음
                return response;
            }
        }
    }
}
