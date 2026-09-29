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
using static FieldLink.PlcDrivers.Omron.OmronHostLinkCModeAddressParser;
using static FieldLink.PlcDrivers.Omron.OmronHostLinkCModeCommandBuilder;
using static FieldLink.PlcDrivers.Omron.OmronHostLinkCModeControlCommandBuilder;
using static FieldLink.PlcDrivers.Omron.OmronHostLinkCModeControlResponseParser;

namespace FieldLink.PlcDrivers.Omron
{
    /// <summary>OmronHostLinkCMode 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class OmronHostLinkCModeResponseParser
    {
        /// <summary>ResponseValidAnalysis 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <param name = "isRead">isRead에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> ResponseValidAnalysis(byte[] response, bool isRead)
        {
            // 데이터 유효성 분석
            if (response.Length >= 11)
            {
                try
                {
                    // 오류 코드를 추출합니다.
                    int err = Convert.ToInt32(Encoding.ASCII.GetString(response, 5, 2), 16);
                    byte[] Content = null;
                    if (response.Length > 11)
                        Content = Encoding.ASCII.GetString(response, 7, response.Length - 11).ToHexBytes();
                    if (err > 0)
                    {
                        return new OperationResult<byte[]>()
                        {
                            ErrorCode = err,
                            Message = GetErrorMessage(err),
                            Content = Content
                        };
                    }
                    else
                    {
                        return OperationResult.CreateSuccessResult(Content);
                    }
                }
                catch (Exception ex)
                {
                    return new OperationResult<byte[]>("ResponseValidAnalysis failed: " + ex.Message + " Source: " + response.ToHexString(' '));
                }
            }

            return new OperationResult<byte[]>(ProtocolMessages.OmronReceiveDataError);
        }

        /// <summary>GetModelText 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "model">문자열 메모리 형식을 결정하는 Siemens PLC 기종입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<string> GetModelText(string model)
        {
            switch (model)
            {
                case "30":
                    return OperationResult.CreateSuccessResult("CS/CJ");
                case "01":
                    return OperationResult.CreateSuccessResult("C250");
                case "02":
                    return OperationResult.CreateSuccessResult("C500");
                case "03":
                    return OperationResult.CreateSuccessResult("C120/C50");
                case "09":
                    return OperationResult.CreateSuccessResult("C250F");
                case "0A":
                    return OperationResult.CreateSuccessResult("C500F");
                case "0B":
                    return OperationResult.CreateSuccessResult("C120F");
                case "0E":
                    return OperationResult.CreateSuccessResult("C2000");
                case "10":
                    return OperationResult.CreateSuccessResult("C1000H");
                case "11":
                    return OperationResult.CreateSuccessResult("C2000H/CQM1/CPM1");
                case "12":
                    return OperationResult.CreateSuccessResult("C20H/C28H/C40H, C200H, C200HS, C200HX/HG/HE (-ZE)");
                case "20":
                    return OperationResult.CreateSuccessResult("CV500");
                case "21":
                    return OperationResult.CreateSuccessResult("CV1000");
                case "22":
                    return OperationResult.CreateSuccessResult("CV2000");
                case "40":
                    return OperationResult.CreateSuccessResult("CVM1-CPU01-E");
                case "41":
                    return OperationResult.CreateSuccessResult("CVM1-CPU11-E");
                case "42":
                    return OperationResult.CreateSuccessResult("CVM1-CPU21-E");
                default:
                    return new OperationResult<string>("Unknown model, model code:" + model);
            }
        }

        /// <summary>장치 오류 또는 상태 코드에 대응하는 설명을 반환합니다.</summary>
        /// <param name = "err">err에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string GetErrorMessage(int err)
        {
            switch (err)
            {
                case 0x01:
                    return "Not executable in RUN mode";
                case 0x02:
                    return "Not executable in MONITOR mode";
                case 0x03:
                    return "UM write-protected";
                case 0x04:
                    return "Address over: The program address setting in an read or write command is above the highest program address.";
                case 0x0B:
                    return "Not executable in PROGRAM mode";
                case 0x13:
                    return "The FCS is wrong.";
                case 0x14:
                    return "The command format is wrong, or a command that cannot be divided has been divided, or the frame length is smaller than the minimum length for the applicable command.";
                case 0x15:
                    return "1. The data is outside of the specified range or too long. 2.Hexadecimal data has not been specified.";
                case 0x16:
                    return "Command not supported: The operand specified in an SV Read or SV Change command does not exist in the program.";
                case 0x18:
                    return "Frame length error: The maximum frame length of 131 bytes was exceeded.";
                case 0x19:
                    return "Not executable: The read SV exceeded 9,999, or an I/O memory batch read was executed when items to read were not registered for composite command, or access right was not obtained.";
                case 0x20:
                    return "Could not create I/O table";
                case 0x21:
                    return "Not executable due to CPU Unit CPU error( See note.)";
                case 0x23:
                    return "User memory protected, The UM is read-protected or writeprotected.";
                case 0xA3:
                    return "Aborted due to FCS error in transmission data";
                case 0xA4:
                    return "Aborted due to format error in transmission data";
                case 0xA5:
                    return "Aborted due to entry number data error in transmission data";
                case 0xA8:
                    return "Aborted due to frame length error in transmission data";
                default:
                    return ProtocolMessages.UnknownError;
            }
        }
    }
}
