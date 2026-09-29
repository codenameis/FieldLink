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
using static FieldLink.PlcDrivers.OpenProtocol.OpenProtocolNetCommandBuilder;
using static FieldLink.PlcDrivers.OpenProtocol.OpenProtocolNetValueConverter;

namespace FieldLink.PlcDrivers.OpenProtocol
{
    /// <summary>OpenProtocolNet 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class OpenProtocolNetResponseParser
    {
        /// <summary>장치 오류 또는 상태 코드에 대응하는 설명을 반환합니다.</summary>
        /// <param name = "code">code에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string GetErrorText(int code)
        {
            switch (code)
            {
                case 1:
                    return "Invalid data";
                case 2:
                    return "Parameter set ID not present";
                case 3:
                    return "Parameter set can not be set.";
                case 4:
                    return "Parameter set not running";
                case 06:
                    return "VIN upload subscription already exists";
                case 07:
                    return "VIN upload subscription does not exists";
                case 08:
                    return "VIN input source not granted";
                case 09:
                    return "Last tightening result subscription already exists";
                case 10:
                    return "Last tightening result subscription does not exist";
                case 11:
                    return "Alarm subscription already exists";
                case 12:
                    return "Alarm subscription does not exist";
                case 13:
                    return "Parameter set selection subscription already exists";
                case 14:
                    return "Parameter set selection subscription does not exist";
                case 15:
                    return "Tightening ID requested not found";
                case 16:
                    return "Connection rejected protocol busy";
                case 17:
                    return "Job ID not present";
                case 18:
                    return "Job info subscription already exists";
                case 19:
                    return "Job info subscription does not exist";
                case 20:
                    return "Job can not be set";
                case 21:
                    return "Job not running";
                case 22:
                    return "Not possible to execute dynamic Job request";
                case 23:
                    return "Job batch decrement failed";
                case 30:
                    return "Controller is not a sync Master/station controller";
                case 31:
                    return "Multi-spindle status subscription already exists";
                case 32:
                    return "Multi-spindle status subscription does not exist";
                case 33:
                    return "Multi-spindle result subscription already exists";
                case 34:
                    return "Multi-spindle result subscription does not exist";
                case 40:
                    return "Job line control info subscription already exists";
                case 41:
                    return "Job line control info subscription does not exist";
                case 42:
                    return "Identifier input source not granted";
                case 43:
                    return "Multiple identifiers work order subscription already exists";
                case 44:
                    return "Multiple identifiers work order subscription does not exist";
                case 50:
                    return "Status external monitored inputs subscription already exists";
                case 51:
                    return "Status external monitored inputs subscription does not exist";
                case 52:
                    return "IO device not connected";
                case 53:
                    return "Faulty IO device ID";
                case 58:
                    return "No alarm present";
                case 59:
                    return "Tool currently in use";
                case 60:
                    return "No histogram available";
                case 70:
                    return "Calibration failed";
                case 79:
                    return "Command failed";
                case 80:
                    return "Audi emergency status subscription exists";
                case 81:
                    return "Audi emergency status subscription does not exist";
                case 82:
                    return "Automatic/Manual mode subscribe already exist";
                case 83:
                    return "Automatic/Manual mode subscribe does not exist";
                case 84:
                    return "The relay function subscription already exists";
                case 85:
                    return "The relay function subscription does not exist";
                case 86:
                    return "The selector socket info subscription already exist";
                case 87:
                    return "The selector socket info subscription does not exist";
                case 88:
                    return "The digin info subscription already exist";
                case 89:
                    return "The digin info subscription does not exist";
                case 90:
                    return "Lock at bach done subscription already exist";
                case 91:
                    return "Lock at bach done subscription does not exist";
                case 92:
                    return "Open protocol commands disabled";
                case 93:
                    return "Open protocol commands disabled subscription already exists";
                case 94:
                    return "Open protocol commands disabled subscription does not exist";
                case 95:
                    return "Reject request, PowerMACS is in manual mode";
                case 96:
                    return "Client already connected";
                case 97:
                    return "MID revision unsupported";
                case 98:
                    return "Controller internal request timeout";
                case 99:
                    return "Unknown MID";
                default:
                    return ProtocolMessages.UnknownError;
            }
        }

        /// <summary>입력 데이터가 프로토콜의 검증 조건을 만족하는지 확인합니다.</summary>
        /// <param name = "reply">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult CheckRequestReplyMessages(byte[] reply)
        {
            try
            {
                if (Encoding.ASCII.GetString(reply, 4, 4) == "0004")
                {
                    string mid = Encoding.ASCII.GetString(reply, 20, 4);
                    int code = Convert.ToInt32(Encoding.ASCII.GetString(reply, 24, 2));
                    if (code == 0)
                        return OperationResult.CreateSuccessResult();
                    return new OperationResult(code, $"The request MID {mid} Select parameter set failed: " + GetErrorText(code));
                }

                return OperationResult.CreateSuccessResult();
            }
            catch (Exception e)
            {
                return new OperationResult(e.Message);
            }
        }
    }
}
