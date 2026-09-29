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
using static FieldLink.PlcDrivers.AllenBradley.AllenBradleyCommandBuilder;
using static FieldLink.PlcDrivers.AllenBradley.AllenBradleyAddressParser;
using static FieldLink.PlcDrivers.AllenBradley.AllenBradleyDefinitions;

namespace FieldLink.PlcDrivers.AllenBradley
{
    /// <summary>AllenBradley 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class AllenBradleyResponseParser
    {
        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "pathCommand">pathCommand에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string ParseRequestPathCommand(byte[] pathCommand)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < pathCommand.Length; i++)
            {
                if (pathCommand[i] == 0x91)
                {
                    string name = Encoding.UTF8.GetString(pathCommand, i + 2, pathCommand[i + 1]).TrimEnd('\0');
                    sb.Append(name);
                    int length = 2 + name.Length;
                    if (name.Length % 2 == 1)
                        length++;
                    if (pathCommand.Length > length + i)
                        if (pathCommand[i + length] == 0x28)
                        {
                            sb.Append($"[{pathCommand[i + length + 1]}]");
                        }
                        else if (pathCommand[i + length] == 0x29)
                        {
                            sb.Append($"[{BitConverter.ToUInt16(pathCommand, i + length + 2)}]");
                        }

                    sb.Append(".");
                }
            }

            if (sb[sb.Length - 1] == '.')
                sb.Remove(sb.Length - 1, 1);
            return sb.ToString();
        }

        /// <summary>입력 데이터가 프로토콜의 검증 조건을 만족하는지 확인합니다.</summary>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult CheckResponse(byte[] response)
        {
            try
            {
                int status = BitConverter.ToInt32(response, 8);
                if (status == 0)
                    return OperationResult.CreateSuccessResult();
                string msg = string.Empty;
                switch (status)
                {
                    case 0x01:
                        msg = ProtocolMessages.AllenBradleySessionStatus01;
                        break;
                    case 0x02:
                        msg = ProtocolMessages.AllenBradleySessionStatus02;
                        break;
                    case 0x03:
                        msg = ProtocolMessages.AllenBradleySessionStatus03;
                        break;
                    case 0x64:
                        msg = ProtocolMessages.AllenBradleySessionStatus64;
                        break;
                    case 0x65:
                        msg = ProtocolMessages.AllenBradleySessionStatus65;
                        break;
                    case 0x69:
                        msg = ProtocolMessages.AllenBradleySessionStatus69;
                        break;
                    default:
                        msg = ProtocolMessages.UnknownError;
                        break;
                }

                return new OperationResult(status, msg);
            }
            catch (Exception ex)
            {
                return new OperationResult("CheckResponse failed: " + ex.Message + Environment.NewLine + "Source: " + response.ToHexString(' '));
            }
        }

        /// <summary>프로토콜 응답에서 데이터와 상태 정보를 추출합니다.</summary>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <param name = "isRead">isRead에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[], ushort, bool> ExtractActualData(byte[] response, bool isRead)
        {
            List<byte> data = new List<byte>();
            try
            {
                int offset = 38;
                bool hasMoreData = false;
                ushort dataType = 0;
                ushort count = BitConverter.ToUInt16(response, 38); // 나머지 전체 바이트의 길이는 1개의 데이터일 수도 있고, 여러 개의 데이터일 수도 있습니다.
                if (BitConverter.ToInt32(response, 40) == 0x8A)
                {
                    // 00 01 02 03 04 05 06 07 08 09 10 11 12 13 14 15 16 17 18 19 20 21 22 23 24 25 26 27 28 29 30 31 32 33 34 35 36 37 38 39 40 41 42 43
                    // 6F 00 31 00 47 00 39 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 02 00 00 00 00 00 B2 00 21 00 8A 00 00 00
                    // 44 45 46 47 48 49 50 51 52 53 54 55 56 57 58 59 60 61 62 63 64 65 66 67 68 69 70 71 72
                    // 03 00 08 00 0F 00 16 00 CC 00 00 00 C1 00 01 CC 00 00 00 C1 00 01 CC 00 00 00 C1 00 01
                    // 다중 데이터
                    offset = 44;
                    int dataCount = BitConverter.ToUInt16(response, offset);
                    for (int i = 0; i < dataCount; i++)
                    {
                        int offectStart = BitConverter.ToUInt16(response, offset + 2 + i * 2) + offset;
                        int offectEnd = (i == dataCount - 1) ? response.Length : (BitConverter.ToUInt16(response, offset + 4 + i * 2) + offset);
                        ushort err = BitConverter.ToUInt16(response, offectStart + 2);
                        switch (err)
                        {
                            case 0x04:
                                return new OperationResult<byte[], ushort, bool>()
                                {
                                    ErrorCode = err,
                                    Message = ProtocolMessages.AllenBradley04
                                };
                            case 0x05:
                                return new OperationResult<byte[], ushort, bool>()
                                {
                                    ErrorCode = err,
                                    Message = ProtocolMessages.AllenBradley05
                                };
                            case 0x06:
                            {
                                // 06의 오류 코드는 보통 너무 긴 데이터입니다.
                                // CC는 기호 반환, D2는 기호 조각 반환, D5는 목록 데이터
                                if (response[offset + 2] == 0xD2 || response[offset + 2] == 0xCC)
                                    return new OperationResult<byte[], ushort, bool>()
                                    {
                                        ErrorCode = err,
                                        Message = ProtocolMessages.AllenBradley06
                                    };
                                break;
                            }

                            case 0x0A:
                                return new OperationResult<byte[], ushort, bool>()
                                {
                                    ErrorCode = err,
                                    Message = ProtocolMessages.AllenBradley0A
                                };
                            case 0x0C:
                                return new OperationResult<byte[], ushort, bool>()
                                {
                                    ErrorCode = err,
                                    Message = ProtocolMessages.AllenBradley0C
                                };
                            case 0x13:
                                return new OperationResult<byte[], ushort, bool>()
                                {
                                    ErrorCode = err,
                                    Message = ProtocolMessages.AllenBradley13
                                };
                            case 0x1C:
                                return new OperationResult<byte[], ushort, bool>()
                                {
                                    ErrorCode = err,
                                    Message = ProtocolMessages.AllenBradley1C
                                };
                            case 0x1E:
                                return new OperationResult<byte[], ushort, bool>()
                                {
                                    ErrorCode = err,
                                    Message = ProtocolMessages.AllenBradley1E
                                };
                            case 0x26:
                                return new OperationResult<byte[], ushort, bool>()
                                {
                                    ErrorCode = err,
                                    Message = ProtocolMessages.AllenBradley26
                                };
                            case 0x00:
                                break;
                            default:
                                return new OperationResult<byte[], ushort, bool>()
                                {
                                    ErrorCode = err,
                                    Message = ProtocolMessages.UnknownError
                                };
                        }

                        if (isRead)
                        {
                            for (int j = offectStart + 6; j < offectEnd; j++)
                            {
                                data.Add(response[j]);
                            }
                        }
                    }
                }
                else
                {
                    // 개별 데이터
                    byte err = response[offset + 4];
                    switch (err)
                    {
                        case 0x04:
                            return new OperationResult<byte[], ushort, bool>()
                            {
                                ErrorCode = err,
                                Message = ProtocolMessages.AllenBradley04
                            };
                        case 0x05:
                            return new OperationResult<byte[], ushort, bool>()
                            {
                                ErrorCode = err,
                                Message = ProtocolMessages.AllenBradley05
                            };
                        case 0x06:
                            hasMoreData = true;
                            break;
                        case 0x0A:
                            return new OperationResult<byte[], ushort, bool>()
                            {
                                ErrorCode = err,
                                Message = ProtocolMessages.AllenBradley0A
                            };
                        case 0x0C:
                            return new OperationResult<byte[], ushort, bool>()
                            {
                                ErrorCode = err,
                                Message = ProtocolMessages.AllenBradley0C
                            };
                        case 0x13:
                            return new OperationResult<byte[], ushort, bool>()
                            {
                                ErrorCode = err,
                                Message = ProtocolMessages.AllenBradley13
                            };
                        case 0x1C:
                            return new OperationResult<byte[], ushort, bool>()
                            {
                                ErrorCode = err,
                                Message = ProtocolMessages.AllenBradley1C
                            };
                        case 0x1E:
                            return new OperationResult<byte[], ushort, bool>()
                            {
                                ErrorCode = err,
                                Message = ProtocolMessages.AllenBradley1E
                            };
                        case 0x20:
                            return new OperationResult<byte[], ushort, bool>()
                            {
                                ErrorCode = err,
                                Message = ProtocolMessages.AllenBradley20
                            };
                        case 0x26:
                            return new OperationResult<byte[], ushort, bool>()
                            {
                                ErrorCode = err,
                                Message = ProtocolMessages.AllenBradley26
                            };
                        case 0x00:
                            break;
                        default:
                            return new OperationResult<byte[], ushort, bool>()
                            {
                                ErrorCode = err,
                                Message = ProtocolMessages.UnknownError
                            };
                    }

                    if (response[offset + 2] == 0xCD || response[offset + 2] == 0xD3)
                        return OperationResult.CreateSuccessResult(data.ToArray(), dataType, hasMoreData);
                    if (response[offset + 2] == 0xCC || response[offset + 2] == 0xD2)
                    {
                        for (int i = offset + 8; i < offset + 2 + count; i++)
                        {
                            data.Add(response[i]);
                        }

                        dataType = BitConverter.ToUInt16(response, offset + 6);
                    }
                    else if (response[offset + 2] == 0xD5) // 0x55입니다.
                    {
                        for (int i = offset + 6; i < offset + 2 + count; i++)
                        {
                            data.Add(response[i]);
                        }
                    }
                }

                return OperationResult.CreateSuccessResult(data.ToArray(), dataType, hasMoreData);
            }
            catch (Exception ex)
            {
                return new OperationResult<byte[], ushort, bool>("ExtractActualData failed: " + ex.Message + Environment.NewLine + response.ToHexString(' '));
            }
        }

        /// <summary>프로토콜 응답에서 데이터와 상태 정보를 추출합니다.</summary>
        /// <param name = "read">read에 사용할 입력값입니다.</param>
        /// <param name = "byteTransform">byteTransform에 사용할 입력값입니다.</param>
        /// <param name = "encoding">문자열 변환에 사용할 인코딩입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<string> ExtractActualString(OperationResult<byte[], ushort, bool> read, IProtocolValueConverter byteTransform, Encoding encoding)
        {
            if (!read.IsSuccess)
                return OperationResult.CreateFailedResult<string>(read);
            try
            {
                if (read.Content2 == 0xDA)
                {
                    if (read.Content1.Length >= 1)
                    {
                        if (read.Content1[0] == 0)
                            return OperationResult.CreateSuccessResult(string.Empty); // 빈 문자열의 경우
                        if (read.Content1[0] >= read.Content1.Length)
                            return OperationResult.CreateSuccessResult(encoding.GetString(read.Content1));
                        return OperationResult.CreateSuccessResult(encoding.GetString(read.Content1, 1, read.Content1[0]));
                    }
                    else
                    {
                        return OperationResult.CreateSuccessResult(encoding.GetString(read.Content1));
                    }
                }
                else
                {
                    if (read.Content1.Length >= 6)
                    {
                        int strLength = byteTransform.ReadInt32(read.Content1, 2);
                        if (strLength == 0)
                            return OperationResult.CreateSuccessResult(string.Empty); // 빈 문자열의 경우
                        return OperationResult.CreateSuccessResult(encoding.GetString(read.Content1, 6, strLength));
                    }
                    else
                    {
                        return OperationResult.CreateSuccessResult(encoding.GetString(read.Content1));
                    }
                }
            }
            catch (Exception ex)
            {
                return new OperationResult<string>(ex.Message + " Source: " + read.Content1.ToHexString(' '));
            }
        }
    }
}
