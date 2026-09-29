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
using static FieldLink.PlcDrivers.FATEK.FatekProgramValueConverter;
using static FieldLink.PlcDrivers.FATEK.FatekProgramCommandBuilder;
using static FieldLink.PlcDrivers.FATEK.FatekControlCommandBuilder;
using static FieldLink.PlcDrivers.FATEK.FatekProgramControlCommandBuilder;
using static FieldLink.PlcDrivers.FATEK.FatekProgramControlResponseParser;

namespace FieldLink.PlcDrivers.FATEK
{
    /// <summary>FatekProgram 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class FatekProgramResponseParser
    {
        /// <summary>누적된 바이트가 완전한 프로토콜 프레임인지 판별합니다.</summary>
        /// <param name = "ms">ms에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static bool CheckReceiveDataComplete(MemoryStream ms)
        {
            byte[] buffer = ms.ToArray();
            if (buffer.Length < 5)
                return false;
            return buffer[buffer.Length - 1] == AsciiControl.ETX;
        }

        /// <summary>프로토콜 응답에서 데이터와 상태 정보를 추출합니다.</summary>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] ExtraResponse(byte[] response, ushort length)
        {
            byte[] Content = new byte[length * 2];
            for (int i = 0; i < Content.Length / 2; i++)
            {
                ushort tmp = Convert.ToUInt16(Encoding.ASCII.GetString(response, i * 4 + 6, 4), 16);
                BitConverter.GetBytes(tmp).CopyTo(Content, i * 2);
            }

            return Content;
        }

        /// <summary>입력 데이터가 프로토콜의 검증 조건을 만족하는지 확인합니다.</summary>
        /// <param name = "content">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult CheckResponse(byte[] content)
        {
            try
            {
                if (content[0] != AsciiControl.STX)
                    return new OperationResult(content[0], "Write Faild:" + ProtocolBytes.ByteToHexString(content, ' '));
                if (content[5] != 0x30)
                    return new OperationResult(content[5], GetErrorDescriptionFromCode((char)content[5]));
                return OperationResult.CreateSuccessResult();
            }
            catch (Exception ex)
            {
                return new OperationResult("CheckResponse failed: " + ex.Message + Environment.NewLine + "Source: " + content.ToHexString(' '));
            }
        }

        /// <summary>장치 오류 또는 상태 코드에 대응하는 설명을 반환합니다.</summary>
        /// <param name = "code">code에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string GetErrorDescriptionFromCode(char code)
        {
            switch (code)
            {
                case '2':
                    return ProtocolMessages.FatekStatus02;
                case '3':
                    return ProtocolMessages.FatekStatus03;
                case '4':
                    return ProtocolMessages.FatekStatus04;
                case '5':
                    return ProtocolMessages.FatekStatus05;
                case '6':
                    return ProtocolMessages.FatekStatus06;
                case '7':
                    return ProtocolMessages.FatekStatus07;
                case '9':
                    return ProtocolMessages.FatekStatus09;
                case 'A':
                    return ProtocolMessages.FatekStatus10;
                default:
                    return ProtocolMessages.UnknownError;
            }
        }
    }
}
