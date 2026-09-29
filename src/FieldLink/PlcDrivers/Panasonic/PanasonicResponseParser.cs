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
using static FieldLink.PlcDrivers.Panasonic.PanasonicValueConverter;
using static FieldLink.PlcDrivers.Panasonic.PanasonicAddressParser;
using static FieldLink.PlcDrivers.Panasonic.PanasonicCommandBuilder;

namespace FieldLink.PlcDrivers.Panasonic
{
    /// <summary>Panasonic 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class PanasonicResponseParser
    {
        /// <summary>프로토콜 응답에서 데이터와 상태 정보를 추출합니다.</summary>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <param name = "parseData">parseData에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> ExtraActualData(byte[] response, bool parseData = true)
        {
            if (response.Length < 9)
                return new OperationResult<byte[]>(ProtocolMessages.PanasonicReceiveLengthMustLargerThan9);
            try
            {
                if (response[3] == '$')
                {
                    byte[] data = new byte[response.Length - 9];
                    if (data.Length > 0)
                    {
                        Array.Copy(response, 6, data, 0, data.Length);
                        if (parseData)
                            data = ProtocolBytes.HexStringToBytes(Encoding.ASCII.GetString(data));
                    }

                    return OperationResult.CreateSuccessResult(data);
                }
                else if (response[3] == '!')
                {
                    int err = int.Parse(Encoding.ASCII.GetString(response, 4, 2));
                    return new OperationResult<byte[]>(err, GetErrorDescription(err));
                }
                else
                {
                    return new OperationResult<byte[]>(ProtocolMessages.UnknownError + " Source Data: " + ProtocolBytes.GetAsciiStringRender(response));
                }
            }
            catch (Exception ex)
            {
                return new OperationResult<byte[]>("ExtraActualData failed: " + ex.Message + Environment.NewLine + "Source: " + ProtocolBytes.GetAsciiStringRender(response));
            }
        }

        /// <summary>프로토콜 응답에서 데이터와 상태 정보를 추출합니다.</summary>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<bool[]> ExtraActualBool(byte[] response)
        {
            if (response.Length < 9)
                return new OperationResult<bool[]>(ProtocolMessages.PanasonicReceiveLengthMustLargerThan9 + " Source: " + ProtocolBytes.GetAsciiStringRender(response));
            if (response[3] == '$')
            {
                byte[] value = response.SelectMiddle(6, response.Length - 9);
                return OperationResult.CreateSuccessResult(value.Select(m => m == 0x31).ToArray());
            }
            else if (response[3] == '!')
            {
                int err = int.Parse(Encoding.ASCII.GetString(response, 4, 2));
                return new OperationResult<bool[]>(err, GetErrorDescription(err));
            }
            else
            {
                return new OperationResult<bool[]>(ProtocolMessages.UnknownError + " Source: " + ProtocolBytes.GetAsciiStringRender(response));
            }
        }

        /// <summary>장치 오류 또는 상태 코드에 대응하는 설명을 반환합니다.</summary>
        /// <param name = "err">err에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string GetErrorDescription(int err)
        {
            switch (err)
            {
                case 20:
                    return ProtocolMessages.PanasonicMewStatus20;
                case 21:
                    return ProtocolMessages.PanasonicMewStatus21;
                case 22:
                    return ProtocolMessages.PanasonicMewStatus22;
                case 23:
                    return ProtocolMessages.PanasonicMewStatus23;
                case 24:
                    return ProtocolMessages.PanasonicMewStatus24;
                case 25:
                    return ProtocolMessages.PanasonicMewStatus25;
                case 26:
                    return ProtocolMessages.PanasonicMewStatus26;
                case 27:
                    return ProtocolMessages.PanasonicMewStatus27;
                case 28:
                    return ProtocolMessages.PanasonicMewStatus28;
                case 29:
                    return ProtocolMessages.PanasonicMewStatus29;
                case 30:
                    return ProtocolMessages.PanasonicMewStatus30;
                case 40:
                    return ProtocolMessages.PanasonicMewStatus40;
                case 41:
                    return ProtocolMessages.PanasonicMewStatus41;
                case 42:
                    return ProtocolMessages.PanasonicMewStatus42;
                case 43:
                    return ProtocolMessages.PanasonicMewStatus43;
                case 50:
                    return ProtocolMessages.PanasonicMewStatus50;
                case 51:
                    return ProtocolMessages.PanasonicMewStatus51;
                case 52:
                    return ProtocolMessages.PanasonicMewStatus52;
                case 53:
                    return ProtocolMessages.PanasonicMewStatus53;
                case 60:
                    return ProtocolMessages.PanasonicMewStatus60;
                case 61:
                    return ProtocolMessages.PanasonicMewStatus61;
                case 62:
                    return ProtocolMessages.PanasonicMewStatus62;
                case 63:
                    return ProtocolMessages.PanasonicMewStatus63;
                case 64:
                    return ProtocolMessages.PanasonicMewStatus64;
                case 65:
                    return ProtocolMessages.PanasonicMewStatus65;
                case 66:
                    return ProtocolMessages.PanasonicMewStatus66;
                case 67:
                    return ProtocolMessages.PanasonicMewStatus67;
                case 68:
                    return ProtocolMessages.PanasonicMewStatus68;
                case 71:
                    return ProtocolMessages.PanasonicMewStatus71;
                case 78:
                    return ProtocolMessages.PanasonicMewStatus78;
                case 80:
                    return ProtocolMessages.PanasonicMewStatus80;
                case 81:
                    return ProtocolMessages.PanasonicMewStatus81;
                case 90:
                    return ProtocolMessages.PanasonicMewStatus90;
                case 92:
                    return ProtocolMessages.PanasonicMewStatus92;
                default:
                    return ProtocolMessages.UnknownError;
            }
        }
    }
}
