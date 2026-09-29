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
using static FieldLink.PlcDrivers.Melsec.MelsecFxLinksCommandBuilder;

namespace FieldLink.PlcDrivers.Melsec
{
    /// <summary>MelsecFxLinks 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class MelsecFxLinksResponseParser
    {
        /// <summary>GetPlcTypeFromCode 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "code">code에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<string> GetPlcTypeFromCode(string code)
        {
            switch (code)
            {
                case "F2":
                    return OperationResult.CreateSuccessResult("FX1S");
                case "8E":
                    return OperationResult.CreateSuccessResult("FX0N");
                case "8D":
                    return OperationResult.CreateSuccessResult("FX2/FX2C");
                case "9E":
                    return OperationResult.CreateSuccessResult("FX1N/FX1NC");
                case "9D":
                    return OperationResult.CreateSuccessResult("FX2N/FX2NC");
                case "F4":
                    return OperationResult.CreateSuccessResult("FX3G");
                case "F3":
                    return OperationResult.CreateSuccessResult("FX3U/FX3UC");
                case "98":
                    return OperationResult.CreateSuccessResult("A0J2HCPU");
                case "A1":
                    return OperationResult.CreateSuccessResult("A1CPU /A1NCPU");
                // case "98": return OperateResult.CreateSuccessResult( "A1SCPU/A1SJCPU" );
                case "A2":
                    return OperationResult.CreateSuccessResult("A2CPU/A2NCPU/A2SCPU");
                case "92":
                    return OperationResult.CreateSuccessResult("A2ACPU");
                case "93":
                    return OperationResult.CreateSuccessResult("A2ACPU-S1");
                case "9A":
                    return OperationResult.CreateSuccessResult("A2CCPU");
                case "82":
                    return OperationResult.CreateSuccessResult("A2USCPU");
                case "83":
                    return OperationResult.CreateSuccessResult("A2CPU-S1/A2USCPU-S1");
                case "A3":
                    return OperationResult.CreateSuccessResult("A3CPU/A3NCPU");
                case "94":
                    return OperationResult.CreateSuccessResult("A3ACPU");
                case "A4":
                    return OperationResult.CreateSuccessResult("A3HCPU/A3MCPU");
                case "84":
                    return OperationResult.CreateSuccessResult("A3UCPU");
                case "85":
                    return OperationResult.CreateSuccessResult("A4UCPU");
                // case "9A": return OperateResult.CreateSuccessResult( "A52GCPU" );
                // case "A3": return OperateResult.CreateSuccessResult( "A73CPU" );
                // case "A3": return OperateResult.CreateSuccessResult( "A7LMS-F" );
                case "AB":
                    return OperationResult.CreateSuccessResult("AJ72P25/R25");
                case "8B":
                    return OperationResult.CreateSuccessResult("AJ72LP25/BR15");
                default:
                    return new OperationResult<string>(ProtocolMessages.NotSupportedDataType + " Code:" + code);
            }
        }

        /// <summary>장치 오류 또는 상태 코드에 대응하는 설명을 반환합니다.</summary>
        /// <param name = "error">error에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string GetErrorText(int error)
        {
            switch (error)
            {
                case 0x02:
                    return ProtocolMessages.MelsecFxLinksError02;
                case 0x03:
                    return ProtocolMessages.MelsecFxLinksError03;
                case 0x06:
                    return ProtocolMessages.MelsecFxLinksError06;
                case 0x07:
                    return ProtocolMessages.MelsecFxLinksError07;
                case 0x0A:
                    return ProtocolMessages.MelsecFxLinksError0A;
                case 0x10:
                    return ProtocolMessages.MelsecFxLinksError10;
                case 0x18:
                    return ProtocolMessages.MelsecFxLinksError18;
                default:
                    return ProtocolMessages.UnknownError;
            }
        }

        /// <summary>입력 데이터가 프로토콜의 검증 조건을 만족하는지 확인합니다.</summary>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> CheckPlcResponse(byte[] response)
        {
            try
            {
                if (response[0] == AsciiControl.NAK)
                {
                    int err = Convert.ToInt32(Encoding.ASCII.GetString(response, 5, 2), 16);
                    return new OperationResult<byte[]>(err, GetErrorText(err));
                }

                if ((response[0] != AsciiControl.STX) && (response[0] != AsciiControl.ACK))
                    return new OperationResult<byte[]>(response[0], "Check command failed: " + ProtocolBytes.GetAsciiStringRender(response));
                if (response[0] == AsciiControl.ACK)
                    return OperationResult.CreateSuccessResult(new byte[0]);
                int etxIndex = -1;
                for (int i = 5; i < response.Length; i++)
                {
                    if (response[i] == AsciiControl.ETX)
                    {
                        etxIndex = i;
                        break;
                    }
                }

                if (etxIndex == -1)
                    etxIndex = response.Length;
                return OperationResult.CreateSuccessResult(response.SelectMiddle(5, etxIndex - 5));
            }
            catch (Exception ex)
            {
                return new OperationResult<byte[]>("Check Plc Response failed Error: " + ex.Message + " Source: " + ProtocolBytes.GetAsciiStringRender(response));
            }
        }

        /// <summary>프로토콜 응답에서 데이터와 상태 정보를 추출합니다.</summary>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> ExtraResponse(byte[] response)
        {
            try
            {
                byte[] content = new byte[response.Length / 2];
                for (int i = 0; i < content.Length / 2; i++)
                {
                    ushort tmp = Convert.ToUInt16(Encoding.ASCII.GetString(response, i * 4, 4), 16);
                    BitConverter.GetBytes(tmp).CopyTo(content, i * 2);
                }

                return OperationResult.CreateSuccessResult(content);
            }
            catch (Exception ex)
            {
                return new OperationResult<byte[]>("Extra source data failed: " + ex.Message + Environment.NewLine + "Source: " + response.ToHexString(' '));
            }
        }
    }
}
