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
using static FieldLink.PlcDrivers.Omron.OmronConnectedCipNetValueConverter;
using static FieldLink.PlcDrivers.Omron.OmronConnectedCipNetCommandBuilder;

namespace FieldLink.PlcDrivers.Omron
{
    /// <summary>OmronConnectedCipNet 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class OmronConnectedCipNetResponseParser
    {
        /// <summary>프로토콜 응답에서 데이터와 상태 정보를 추출합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "content">해석할 응답 데이터입니다.</param>
        /// <param name = "encoding">문자열 변환에 사용할 인코딩입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<string> ExtraStringContent(OmronConnectedCipOptions context, byte[] content, Encoding encoding)
        {
            try
            {
                if (content.Length >= 2)
                {
                    int strLength = context.ValueConverter.ReadUInt16(content, 0);
                    return OperationResult.CreateSuccessResult(encoding.GetString(content, 2, strLength));
                }
                else
                {
                    return OperationResult.CreateSuccessResult(encoding.GetString(content));
                }
            }
            catch (Exception ex)
            {
                return new OperationResult<string>("Parse string failed: " + ex.Message + " Source: " + content.ToHexString(' '));
            }
        }
    }
}
