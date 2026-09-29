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
using static FieldLink.PlcDrivers.GE.GeCommandBuilder;

namespace FieldLink.PlcDrivers.GE
{
    /// <summary>Ge 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class GeResponseParser
    {
        /// <summary>프로토콜 응답에서 데이터와 상태 정보를 추출합니다.</summary>
        /// <param name = "content">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> ExtraResponseContent(byte[] content)
        {
            try
            {
                if (content[0] != 0x03)
                    return new OperationResult<byte[]>(content[0], ProtocolMessages.UnknownError + " Source:" + content.ToHexString(' '));
                if (content[31] == 0xD4)
                {
                    ushort status = BitConverter.ToUInt16(content, 42);
                    if (status != 0)
                        return new OperationResult<byte[]>(status, ProtocolMessages.UnknownError);
                    return OperationResult.CreateSuccessResult(content.SelectMiddle(44, 6));
                }

                if (content[31] == 0x94)
                    return OperationResult.CreateSuccessResult(content.RemoveBegin(56));
                return new OperationResult<byte[]>("Extra Wrong:" + ProtocolMessages.UnknownError + " Source:" + content.ToHexString(' '));
            }
            catch (Exception ex)
            {
                return new OperationResult<byte[]>("Extra Wrong:" + ex.Message + " Source:" + content.ToHexString(' '));
            }
        }

        /// <summary>프로토콜 응답에서 데이터와 상태 정보를 추출합니다.</summary>
        /// <param name = "content">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<DateTime> ExtraDateTime(byte[] content)
        {
            try
            {
                return OperationResult.CreateSuccessResult(new DateTime(int.Parse(content[5].ToString("X2")) + 2000, int.Parse(content[4].ToString("X2")), int.Parse(content[3].ToString("X2")), int.Parse(content[2].ToString("X2")), int.Parse(content[1].ToString("X2")), int.Parse(content[0].ToString("X2"))));
            }
            catch (Exception ex)
            {
                return new OperationResult<DateTime>(ex.Message + " Source:" + content.ToHexString(' '));
            }
        }

        /// <summary>프로토콜 응답에서 데이터와 상태 정보를 추출합니다.</summary>
        /// <param name = "content">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<string> ExtraProgramName(byte[] content)
        {
            try
            {
                return OperationResult.CreateSuccessResult(Encoding.UTF8.GetString(content, 18, 16).Trim('\0'));
            }
            catch (Exception ex)
            {
                return new OperationResult<string>(ex.Message + " Source:" + content.ToHexString(' '));
            }
        }
    }
}
