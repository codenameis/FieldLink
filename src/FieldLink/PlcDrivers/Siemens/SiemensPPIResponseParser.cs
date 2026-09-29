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
using static FieldLink.PlcDrivers.Siemens.SiemensPPIAddressParser;
using static FieldLink.PlcDrivers.Siemens.SiemensPPICommandBuilder;

namespace FieldLink.PlcDrivers.Siemens
{
    /// <summary>SiemensPPI 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class SiemensPPIResponseParser
    {
        /// <summary>장치 오류 또는 상태 코드에 대응하는 설명을 반환합니다.</summary>
        /// <param name = "code">code에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string GetMsgFromStatus(byte code)
        {
            switch (code)
            {
                case 0xFF:
                    return "No error";
                case 0x01:
                    return "Hardware fault";
                case 0x03:
                    return "Illegal object access";
                case 0x05:
                    return "Invalid address(incorrent variable address)";
                case 0x06:
                    return "Data type is not supported";
                case 0x0A:
                    return "Object does not exist or length error";
                default:
                    return ProtocolMessages.UnknownError;
            }
        }

        /// <summary>장치 오류 또는 상태 코드에 대응하는 설명을 반환합니다.</summary>
        /// <param name = "errorClass">errorClass에 사용할 입력값입니다.</param>
        /// <param name = "errorCode">errorCode에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string GetMsgFromStatus(byte errorClass, byte errorCode)
        {
            if (errorClass == 0x80 && errorCode == 0x01)
            {
                return "Switch in wrong position for requested operation";
            }
            else if (errorClass == 0x81 && errorCode == 0x04)
            {
                return "Miscellaneous structure error in command.  Command is not supportedby CPU";
            }
            else if (errorClass == 0x84 && errorCode == 0x04)
            {
                return "CPU is busy processing an upload or download CPU cannot process command because of system fault condition";
            }
            else if (errorClass == 0x85 && errorCode == 0x00)
            {
                return "Length fields are not correct or do not agree with the amount of data received";
            }
            else if (errorClass == 0xD2)
            {
                return "Error in upload or download command";
            }
            else if (errorClass == 0xD6)
            {
                return "Protection error(password)";
            }
            else if (errorClass == 0xDC && errorCode == 0x01)
            {
                return "Error in time-of-day clock data";
            }
            else
            {
                return ProtocolMessages.UnknownError;
            }
        }

        /// <summary>누적된 바이트가 완전한 프로토콜 프레임인지 판별합니다.</summary>
        /// <param name = "ms">ms에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static bool CheckReceiveDataComplete(MemoryStream ms)
        {
            byte[] buffer = ms.ToArray();
            if (buffer.Length <= 0)
                return false;
            if (buffer.Length == 1 && buffer[0] == 0xE5)
                return true;
            if (buffer.Length > 6 && buffer[0] == 0x68 && (buffer[1] + 6 == buffer.Length) && buffer[buffer.Length - 1] == AsciiControl.SYN)
                return true;
            if (buffer.Length > 6 && buffer[0] == 0x03 && (buffer[3] == buffer.Length))
                return true;
            return false;
        }

        /// <summary>입력 데이터가 프로토콜의 검증 조건을 만족하는지 확인합니다.</summary>
        /// <param name = "content">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult CheckResponse(byte[] content)
        {
            if (content.Length < 21)
                return new OperationResult(10000, "Failed, data too short:" + ProtocolBytes.ByteToHexString(content, ' '));
            if (content[17] != 0x00 || content[18] != 0x00)
                return new OperationResult(content[19], GetMsgFromStatus(content[18], content[19]));
            if (content.Length < 22)
                return new OperationResult(10000, "Failed, data too short:" + ProtocolBytes.ByteToHexString(content, ' '));
            if (content[21] != 0xFF)
                return new OperationResult(content[21], GetMsgFromStatus(content[21]));
            return OperationResult.CreateSuccessResult();
        }
    }
}
