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
using static FieldLink.PlcDrivers.Omron.OmronHostLinkCommandBuilder;

namespace FieldLink.PlcDrivers.Omron
{
    /// <summary>OmronHostLink 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class OmronHostLinkResponseParser
    {
        /// <summary>ResponseValidAnalysis 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "send">send에 사용할 입력값입니다.</param>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> ResponseValidAnalysis(byte[] send, byte[] response)
        {
            // 데이터 유효성 분석
            // @00FA00400000000102000040*\cr
            if (response.Length >= 27)
            {
                try
                {
                    string commandSend = Encoding.ASCII.GetString(send, 14, 4);
                    string commandReceive = Encoding.ASCII.GetString(response, 15, 4);
                    if (commandReceive != commandSend)
                        return new OperationResult<byte[]>($"Send Command [{commandSend}] not the same as receive command [{commandReceive}] source:[{ProtocolBytes.GetAsciiStringRender(response)}]");
                    int err;
                    // 오류 코드를 추출합니다.
                    try
                    {
                        err = Convert.ToInt32(Encoding.ASCII.GetString(response, 19, 4), 16);
                    }
                    catch (Exception ex)
                    {
                        return new OperationResult<byte[]>("Get error code failed: " + ex.Message + Environment.NewLine + "Source Data: " + ProtocolBytes.GetAsciiStringRender(response));
                    }

                    byte[] content = new byte[0];
                    if (response.Length > 27)
                        content = ProtocolBytes.HexStringToBytes(Encoding.ASCII.GetString(response, 23, response.Length - 27));
                    if (err > 0)
                        return new OperationResult<byte[]>()
                        {
                            ErrorCode = err,
                            Content = content,
                            Message = GetErrorText(err)
                        };
                    else
                    {
                        // 여러 개의 데이터 블록을 읽으면, 2차 분석이 필요합니다.
                        if (Encoding.ASCII.GetString(response, 15, 4) == "0104")
                        {
                            byte[] buffer = content.Length > 0 ? new byte[content.Length * 2 / 3] : new byte[0];
                            for (int i = 0; i < content.Length / 3; i++)
                            {
                                buffer[i * 2 + 0] = content[i * 3 + 1];
                                buffer[i * 2 + 1] = content[i * 3 + 2];
                            }

                            content = buffer;
                        }

                        return OperationResult.CreateSuccessResult(content);
                    }
                }
                catch (Exception ex)
                {
                    return new OperationResult<byte[]>("ResponseValidAnalysis failed: " + ex.Message + " Source: " + response.ToHexString(' '));
                }
            }

            return new OperationResult<byte[]>(ProtocolMessages.OmronReceiveDataError + " Source Data: " + response.ToHexString(' '));
        }

        /// <summary>장치 오류 또는 상태 코드에 대응하는 설명을 반환합니다.</summary>
        /// <param name = "error">error에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string GetErrorText(int error)
        {
            switch (error)
            {
                case 0x0001:
                    return "Service was canceled.";
                case 0x0101:
                    return "Local node is not participating in the network.";
                case 0x0102:
                    return "Token does not arrive.";
                case 0x0103:
                    return "Send was not possible during the specified number of retries.";
                case 0x0104:
                    return "Cannot send because maximum number of event frames exceeded.";
                case 0x0105:
                    return "Node address setting error occurred.";
                case 0x0106:
                    return "The same node address has been set twice in the same network.";
                case 0x0201:
                    return "The destination node is not in the network.";
                case 0x0202:
                    return "There is no Unit with the specified unit address.";
                case 0x0203:
                    return "The third node does not exist.";
                case 0x0204:
                    return "The destination node is busy.";
                case 0x0205:
                    return "The message was destroyed by noise";
                case 0x0301:
                    return "An error occurred in the communications controller.";
                case 0x0302:
                    return "A CPU error occurred in the destination CPU Unit.";
                case 0x0303:
                    return "A response was not returned because an error occurred in the Board.";
                case 0x0304:
                    return "The unit number was set incorrectly";
                case 0x0401:
                    return "The Unit/Board does not support the specified command code.";
                case 0x0402:
                    return "The command cannot be executed because the model or version is incorrect";
                case 0x0501:
                    return "The destination network or node address is not set in the routing tables.";
                case 0x0502:
                    return "Relaying is not possible because there are no routing tables";
                case 0x0503:
                    return "There is an error in the routing tables.";
                case 0x0504:
                    return "An attempt was made to send to a network that was over 3 networks away";
                // 명령 형식 오류
                case 0x1001:
                    return "The command is longer than the maximum permissible length.";
                case 0x1002:
                    return "The command is shorter than the minimum permissible length.";
                case 0x1003:
                    return "The designated number of elements differs from the number of write data items.";
                case 0x1004:
                    return "An incorrect format was used.";
                case 0x1005:
                    return "Either the relay table in the local node or the local network table in the relay node is incorrect.";
                // 매개 변수 오류
                case 0x1101:
                    return "The specified word does not exist in the memory area or there is no EM Area.";
                case 0x1102:
                    return "The access size specification is incorrect or an odd word address is specified.";
                case 0x1103:
                    return "The start address in command process is beyond the accessible area";
                case 0x1104:
                    return "The end address in command process is beyond the accessible area.";
                case 0x1106:
                    return "FFFF hex was not specified.";
                case 0x1109:
                    return "A large–small relationship in the elements in the command data is incorrect.";
                case 0x110B:
                    return "The response format is longer than the maximum permissible length.";
                case 0x110C:
                    return "There is an error in one of the parameter settings.";
                // 읽기 불가능
                case 0x2002:
                    return "The program area is protected.";
                case 0x2003:
                    return "A table has not been registered.";
                case 0x2004:
                    return "The search data does not exist.";
                case 0x2005:
                    return "A non-existing program number has been specified.";
                case 0x2006:
                    return "The file does not exist at the specified file device.";
                case 0x2007:
                    return "A data being compared is not the same.";
                // 쓰기 불가
                case 0x2101:
                    return "The specified area is read-only.";
                case 0x2102:
                    return "The program area is protected.";
                case 0x2103:
                    return "The file cannot be created because the limit has been exceeded.";
                case 0x2105:
                    return "A non-existing program number has been specified.";
                case 0x2106:
                    return "The file does not exist at the specified file device.";
                case 0x2107:
                    return "A file with the same name already exists in the specified file device.";
                case 0x2108:
                    return "The change cannot be made because doing so would create a problem.";
                // 현재 모드에서 실행할 수 없습니다.
                case 0x2201:
                case 0x2202:
                case 0x2208:
                    return "The mode is incorrect.";
                case 0x2203:
                    return "The PLC is in PROGRAM mode.";
                case 0x2204:
                    return "The PLC is in DEBUG mode.";
                case 0x2205:
                    return "The PLC is in MONITOR mode.";
                case 0x2206:
                    return "The PLC is in RUN mode.";
                case 0x2207:
                    return "The specified node is not the polling node.";
                //  그런 장치가 없습니다.
                case 0x2301:
                    return "The specified memory does not exist as a file device.";
                case 0x2302:
                    return "There is no file memory.";
                case 0x2303:
                    return "There is no clock.";
                case 0x2401:
                    return "The data link tables have not been registered or they contain an error.";
                default:
                    return ProtocolMessages.UnknownError;
            }
        }
    }
}
