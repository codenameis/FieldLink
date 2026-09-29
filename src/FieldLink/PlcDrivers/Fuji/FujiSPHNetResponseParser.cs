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
using static FieldLink.PlcDrivers.Fuji.FujiSPHNetCommandBuilder;

namespace FieldLink.PlcDrivers.Fuji
{
    /// <summary>FujiSPHNet 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class FujiSPHNetResponseParser
    {
        /// <summary>장치 오류 또는 상태 코드에 대응하는 설명을 반환합니다.</summary>
        /// <param name = "code">code에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string GetErrorDescription(byte code)
        {
            switch (code)
            {
                case 0x10:
                    return "Command cannot be executed because an error occurred in the CPU.";
                case 0x11:
                    return "Command cannot be executed because the CPU is running.";
                case 0x12:
                    return "Command cannot be executed due to the key switch condition of the CPU.";
                case 0x20:
                    return "CPU received undefined command or mode.";
                case 0x22:
                    return "Setting error was found in command header part.";
                case 0x23:
                    return "Transmission is interlocked by a command from another device.";
                case 0x28:
                    return "Requested command cannot be executed because another command is now being executed.";
                case 0x2B:
                    return "Requested command cannot be executed because the loader is now performing another processing( including program change).";
                case 0x2F:
                    return "Requested command cannot be executed because the system is now being initialized.";
                case 0x40:
                    return "Invalid data type or number was specified.";
                case 0x41:
                    return "Specified data cannot be found.";
                case 0x44:
                    return "Specified address exceeds the valid range.";
                case 0x45:
                    return "Address + the number of read/write words exceed the valid range.";
                case 0xA0:
                    return "No module exists at specified destination station No.";
                case 0xA2:
                    return "No response data is returned from the destination module.";
                case 0xA4:
                    return "Command cannot be communicated because an error occurred in the SX bus.";
                case 0xA5:
                    return "Command cannot be communicated because NAK occurred while sending data via the SX bus.";
                default:
                    return ProtocolMessages.UnknownError;
            }
        }

        /// <summary>프로토콜 응답에서 데이터와 상태 정보를 추출합니다.</summary>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> ExtractActualData(byte[] response)
        {
            try
            {
                if (response[4] != 0x00)
                    return new OperationResult<byte[]>(response[4], GetErrorDescription(response[4]));
                if (response.Length > 26)
                    return OperationResult.CreateSuccessResult(response.RemoveBegin(26));
                return OperationResult.CreateSuccessResult(new byte[0]);
            }
            catch (Exception ex)
            {
                return new OperationResult<byte[]>(ex.Message + " Source: " + response.ToHexString(' '));
            }
        }
    }
}
