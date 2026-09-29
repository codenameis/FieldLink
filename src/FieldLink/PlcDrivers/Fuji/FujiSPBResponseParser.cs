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
using static FieldLink.PlcDrivers.Fuji.FujiSpbAddressParser;
using static FieldLink.PlcDrivers.Fuji.FujiSPBValueConverter;
using static FieldLink.PlcDrivers.Fuji.FujiSPBCommandBuilder;

namespace FieldLink.PlcDrivers.Fuji
{
    /// <summary>FujiSPB 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class FujiSPBResponseParser
    {
        /// <summary>입력 데이터가 프로토콜의 검증 조건을 만족하는지 확인합니다.</summary>
        /// <param name = "content">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> CheckResponseData(byte[] content)
        {
            try
            {
                if (content[0] != ':')
                    return new OperationResult<byte[]>(content[0], "Read Faild:" + ProtocolBytes.ByteToHexString(content, ' '));
                string code = Encoding.ASCII.GetString(content, 9, 2);
                if (code != "00")
                    return new OperationResult<byte[]>(Convert.ToInt32(code, 16), GetErrorDescriptionFromCode(code));
                if (content[content.Length - 2] == 0x0D && content[content.Length - 1] == 0x0A)
                    content = content.RemoveLast(2);
                return OperationResult.CreateSuccessResult(content.RemoveBegin(11));
            }
            catch (Exception ex)
            {
                return new OperationResult<byte[]>("CheckResponseData failed: " + ex.Message + Environment.NewLine + "Source: " + content.ToHexString(' '));
            }
        }

        /// <summary>장치 오류 또는 상태 코드에 대응하는 설명을 반환합니다.</summary>
        /// <param name = "code">code에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string GetErrorDescriptionFromCode(string code)
        {
            switch (code)
            {
                case "01":
                    return ProtocolMessages.FujiSpbStatus01;
                case "02":
                    return ProtocolMessages.FujiSpbStatus02;
                case "03":
                    return ProtocolMessages.FujiSpbStatus03;
                case "04":
                    return ProtocolMessages.FujiSpbStatus04;
                case "05":
                    return ProtocolMessages.FujiSpbStatus05;
                case "06":
                    return ProtocolMessages.FujiSpbStatus06;
                case "07":
                    return ProtocolMessages.FujiSpbStatus07;
                case "09":
                    return ProtocolMessages.FujiSpbStatus09;
                case "0C":
                    return ProtocolMessages.FujiSpbStatus0C;
                default:
                    return ProtocolMessages.UnknownError;
            }
        }
    }
}
