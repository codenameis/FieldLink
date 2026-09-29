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
using static FieldLink.PlcDrivers.Yamatake.DigitronCPLCommandBuilder;

namespace FieldLink.PlcDrivers.Yamatake
{
    /// <summary>DigitronCPL 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class DigitronCPLResponseParser
    {
        /// <summary>장치 오류 또는 상태 코드에 대응하는 설명을 반환합니다.</summary>
        /// <param name = "err">err에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string GetErrorText(int err)
        {
            switch (err)
            {
                case 40:
                    return ProtocolMessages.YamatakeDigitronCPL40;
                case 41:
                    return ProtocolMessages.YamatakeDigitronCPL41;
                case 42:
                    return ProtocolMessages.YamatakeDigitronCPL42;
                case 43:
                    return ProtocolMessages.YamatakeDigitronCPL43;
                case 44:
                    return ProtocolMessages.YamatakeDigitronCPL44;
                case 45:
                    return ProtocolMessages.YamatakeDigitronCPL45;
                case 46:
                    return ProtocolMessages.YamatakeDigitronCPL46;
                case 47:
                    return ProtocolMessages.YamatakeDigitronCPL47;
                case 48:
                    return ProtocolMessages.YamatakeDigitronCPL48;
                case 99:
                    return ProtocolMessages.YamatakeDigitronCPL99;
                default:
                    return ProtocolMessages.UnknownError;
            }
        }

        /// <summary>프로토콜 응답에서 데이터와 상태 정보를 추출합니다.</summary>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> ExtraActualResponse(byte[] response)
        {
            try
            {
                int err = Convert.ToInt32(Encoding.ASCII.GetString(response, 6, 2));
                if (err > 0)
                    return new OperationResult<byte[]>(err, GetErrorText(err));
                int endIndex = 8;
                for (int i = 8; i < response.Length; i++)
                {
                    if (response[i] == AsciiControl.ETX)
                    {
                        endIndex = i;
                        break;
                    }
                }

                int startIndex = response[8] == 0x2C ? 9 : 8;
                if (endIndex - startIndex > 0)
                {
                    string[] splits = Encoding.ASCII.GetString(response, startIndex, endIndex - startIndex).Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    short[] values = splits.Select(m => short.Parse(m)).ToArray();
                    byte[] buffer = new byte[values.Length * 2];
                    for (int i = 0; i < values.Length; i++)
                    {
                        BitConverter.GetBytes(values[i]).CopyTo(buffer, i * 2);
                    }

                    return OperationResult.CreateSuccessResult(buffer);
                }

                return OperationResult.CreateSuccessResult(new byte[0]);
            }
            catch (Exception ex)
            {
                return new OperationResult<byte[]>("Data wrong: " + ex.Message + Environment.NewLine + "Source: " + response.ToHexString(' '));
            }
        }
    }
}
