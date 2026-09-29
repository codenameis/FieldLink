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
using static FieldLink.PlcDrivers.Omron.OmronFinsNetCommandBuilder;
using static FieldLink.PlcDrivers.Omron.FinsControlCommandBuilder;

namespace FieldLink.PlcDrivers.Omron
{
    /// <summary>OmronFinsNet 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class OmronFinsNetResponseParser
    {
        /// <summary>ResponseValidAnalysis 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> ResponseValidAnalysis(byte[] response)
        {
            if (response.Length >= 16)
            {
                // 오류 코드를 추출 -> Extracting error Codes
                byte[] buffer = new byte[4];
                buffer[0] = response[15];
                buffer[1] = response[14];
                buffer[2] = response[13];
                buffer[3] = response[12];
                int err = BitConverter.ToInt32(buffer, 0);
                if (err > 0)
                    return new OperationResult<byte[]>(err, GetStatusDescription(err));
                return UdpResponseValidAnalysis(response.RemoveBegin(16));
            }

            return new OperationResult<byte[]>(ProtocolMessages.OmronReceiveDataError);
        }

        /// <summary>UdpResponseValidAnalysis 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> UdpResponseValidAnalysis(byte[] response)
        {
            if (response.Length >= 14)
            {
                int err = response[12] * 256 + response[13];
                /* 이전 구현 보존 — R-001
                if (response[12].GetBoolByIndex(7))
                {
                    int mainCode = response[12] & 0x7F;
                    int subCode = response[13] & 0x3F;
                    return new OperationResult<byte[]>(err, GetEndCodeDescription(mainCode, subCode));
                }
                릴레이 오류만 검사하여 2101(읽기 전용 영역 쓰기)도 아래 성공 분기로 흘렀다.
                W342 §5-1-3에 따라 CPU 상태 비트(6, 7)를 제외한 명령 결과도 검사한다.
                0040처럼 명령이 정상 완료된 CPU 경고는 허용하며 원래 코드는 ErrorCode에 보존한다.
                근거: https://assets.omron.eu/downloads/latest/manual/en/w342_cs_cj_cp_nsj_communications_commands_reference_manual_en.pdf
                */
                int mainCode = response[12] & 0x7F;
                int subCode = response[13] & 0x3F;
                bool relayError = (response[12] & 0x80) != 0;
                if (relayError || mainCode != 0 || subCode != 0)
                    return new OperationResult<byte[]>(err, GetEndCodeDescription(mainCode, subCode));

                if ((response[10] == 0x01 & response[11] == 0x01) || (response[10] == 0x01 & response[11] == 0x04) || (response[10] == 0x02 & response[11] == 0x01) || (response[10] == 0x03 & response[11] == 0x06) || (response[10] == 0x05 & response[11] == 0x01) || (response[10] == 0x05 & response[11] == 0x02) || (response[10] == 0x06 & response[11] == 0x01) || (response[10] == 0x06 & response[11] == 0x20) || (response[10] == 0x07 & response[11] == 0x01) || (response[10] == 0x09 & response[11] == 0x20) || (response[10] == 0x21 & response[11] == 0x02) || (response[10] == 0x22 & response[11] == 0x02))
                {
                    // C0 00 02 00 00 00 00 01 00 00 01 01 21 08
                    // 읽기 작업
                    try
                    {
                        byte[] content = new byte[response.Length - 14];
                        if (content.Length > 0)
                            Array.Copy(response, 14, content, 0, content.Length);
                        OperationResult<byte[]> success = OperationResult.CreateSuccessResult(content);
                        if (content.Length == 0)
                            success.IsSuccess = false;
                        success.ErrorCode = err;
                        success.Message = GetStatusDescription(err) + " Received:" + ProtocolBytes.ByteToHexString(response, ' ');
                        if (response[10] == 0x01 & response[11] == 0x04) // 여러 개의 데이터 블록을 읽으면, 2차 분석이 필요합니다.
                        {
                            byte[] buffer = content.Length > 0 ? new byte[content.Length * 2 / 3] : new byte[0];
                            for (int i = 0; i < content.Length / 3; i++)
                            {
                                buffer[i * 2 + 0] = content[i * 3 + 1];
                                buffer[i * 2 + 1] = content[i * 3 + 2];
                            }

                            success.Content = buffer;
                        }

                        return success;
                    }
                    catch (Exception ex)
                    {
                        return new OperationResult<byte[]>("UdpResponseValidAnalysis failed: " + ex.Message + Environment.NewLine + "Content: " + response.ToHexString(' '));
                    }
                }
                else
                {
                    // 쓰기 동작
                    OperationResult<byte[]> success = OperationResult.CreateSuccessResult(new byte[0]);
                    success.ErrorCode = err;
                    success.Message = GetStatusDescription(err) + " Received:" + ProtocolBytes.ByteToHexString(response, ' ');
                    return success;
                }
            }

            return new OperationResult<byte[]>(ProtocolMessages.OmronReceiveDataError);
        }

        /// <summary>장치 오류 또는 상태 코드에 대응하는 설명을 반환합니다.</summary>
        /// <param name = "mainCode">mainCode에 사용할 입력값입니다.</param>
        /// <param name = "subCode">subCode에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string GetEndCodeDescription(int mainCode, int subCode)
        {
            if (mainCode == 0)
            {
                if (subCode == 0)
                    return "Normal completion";
                if (subCode == 1)
                    return "Data link status: Service was canceled";
            }
            else if (mainCode == 0x01)
            {
                if (subCode == 1)
                    return "Local node is not participating in the network.";
                if (subCode == 2)
                    return "Token does not arrive. [Set the local node to within the maximum node address.]";
                if (subCode == 3)
                    return "Send was not possible during the specified number of retries.";
                if (subCode == 4)
                    return "Cannot send because maximum number of event frames exceeded.";
                if (subCode == 5)
                    return "Node address setting error occurred";
                if (subCode == 6)
                    return "The same node address has been set twice in the same network";
            }
            else if (mainCode == 0x02)
            {
                if (subCode == 1)
                    return "The destination node is not in the network";
                if (subCode == 2)
                    return "There is no Unit with the specified unit address.";
                if (subCode == 3)
                    return "The third node does not exist";
                if (subCode == 4)
                    return "The destination node is busy";
                if (subCode == 5)
                    return "The message was destroyed by noise.";
            }
            else if (mainCode == 0x03)
            {
                if (subCode == 1)
                    return "An error occurred in the communications controller";
                if (subCode == 2)
                    return "A CPU error occurred in the destination CPU Unit.";
                if (subCode == 3)
                    return "A response was not returned because an error occurred in the Board";
                if (subCode == 4)
                    return "The unit number was set incorrectly";
            }
            else if (mainCode == 0x04)
            {
                if (subCode == 1)
                    return "The Unit/Board does not support the specified command code";
                if (subCode == 2)
                    return "The command cannot be executed because the model or version is incorrect";
            }
            else if (mainCode == 0x05)
            {
                if (subCode == 1)
                    return "The destination network or node address is not set in the routing tables";
                if (subCode == 2)
                    return "Relaying is not possible because there are no routing tables";
                if (subCode == 3)
                    return "There is an error in the routing tables";
                if (subCode == 4)
                    return "An attempt was made to send to a network that was over 3 networks away";
            }
            else if (mainCode == 0x10)
            {
                if (subCode == 1)
                    return "The command is longer than the maximum permissible length";
                if (subCode == 2)
                    return "The command is shorter than the minimum permissible length";
                if (subCode == 3)
                    return "The designated number of elements differs from the number of write data items";
                if (subCode == 4)
                    return "An incorrect format was used";
                if (subCode == 5)
                    return "Either the relay table in the local node or the local network table in the relay node is incorrect.";
            }
            else if (mainCode == 0x11)
            {
                if (subCode == 1)
                    return "The specified word does not exist in the memory area or there is no EM Area";
                if (subCode == 2)
                    return "The access size specification is incorrect or an odd word address is specified";
                if (subCode == 3)
                    return "The start address in command process is beyond the accessible area";
                if (subCode == 4)
                    return "The end address in command process is beyond the accessible area";
                if (subCode == 0x0b)
                    return "The response format is longer than the maximum permissible length.";
            }
            else if (mainCode == 0x20)
            {
                if (subCode == 2)
                    return "The program area is protected";
                if (subCode == 4)
                    return "The search data does not exist.";
                if (subCode == 5)
                    return "A non-existing program number has been specified";
            }
            else if (mainCode == 0x21)
            {
                if (subCode == 1)
                    return "The specified area is read-only.";
                if (subCode == 2)
                    return "The program area is protected.";
            }

            return ProtocolMessages.UnknownError;
        }

        /// <summary>장치 오류 또는 상태 코드에 대응하는 설명을 반환합니다.</summary>
        /// <param name = "err">err에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string GetStatusDescription(int err)
        {
            switch (err)
            {
                case 0x00:
                    return ProtocolMessages.OmronStatus0;
                case 0x01:
                    return ProtocolMessages.OmronStatus1;
                case 0x02:
                    return ProtocolMessages.OmronStatus2;
                case 0x03:
                    return ProtocolMessages.OmronStatus3;
                case 0x20:
                    return ProtocolMessages.OmronStatus20;
                case 0x21:
                    return ProtocolMessages.OmronStatus21;
                case 0x22:
                    return ProtocolMessages.OmronStatus22;
                case 0x23:
                    return ProtocolMessages.OmronStatus23;
                case 0x24:
                    return ProtocolMessages.OmronStatus24;
                case 0x25:
                    return ProtocolMessages.OmronStatus25;
                default:
                    return ProtocolMessages.UnknownError;
            }
        }

        /// <summary>CreatePlcTime 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "buffer">프레임 데이터가 담긴 버퍼입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<DateTime> CreatePlcTime(byte[] buffer)
        {
            try
            {
                string hex = buffer.ToHexString();
                DateTime current = DateTime.Now;
                int year = Convert.ToInt32(current.Year.ToString().Substring(0, 2) + hex.Substring(0, 2));
                return OperationResult.CreateSuccessResult(new DateTime(year, Convert.ToInt32(hex.Substring(2, 2)), Convert.ToInt32(hex.Substring(4, 2)), Convert.ToInt32(hex.Substring(6, 2)), Convert.ToInt32(hex.Substring(8, 2)), Convert.ToInt32(hex.Substring(10, 2))));
            }
            catch (Exception ex)
            {
                return new OperationResult<DateTime>("Prase Time failed: " + ex.Message + Environment.NewLine + "Source: " + buffer.ToHexString(' '));
            }
        }
    }
}
