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
using static FieldLink.PlcDrivers.Siemens.SiemensMPICommandBuilder;

namespace FieldLink.PlcDrivers.Siemens
{
    /// <summary>SiemensMPI 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class SiemensMPIResponseParser
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
    }
}
