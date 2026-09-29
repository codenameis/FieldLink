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
using static FieldLink.PlcDrivers.AllenBradley.NetworkConnectedCipCommandBuilder;
using static FieldLink.PlcDrivers.AllenBradley.NetworkConnectedCipValueConverter;

namespace FieldLink.PlcDrivers.AllenBradley
{
    /// <summary>ConnectedCip 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class ConnectedCipResponseParser
    {
        /// <summary>프로토콜 응답에서 데이터와 상태 정보를 추출합니다.</summary>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <param name = "isRead">isRead에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[], ushort, bool> ExtractActualData(byte[] response, bool isRead)
        {
            List<byte> data = new List<byte>();
            try
            {
                int offset = 42;
                bool hasMoreData = false;
                ushort dataType = 0;
                ushort count = BitConverter.ToUInt16(response, offset); // 나머지 전체 바이트의 길이는 1개의 데이터일 수도 있고, 여러 개의 데이터일 수도 있습니다.
                if (BitConverter.ToInt32(response, 46) == 0x8A)
                {
                    // 다중 데이터
                    offset = 50;
                    int dataCount = BitConverter.ToUInt16(response, offset);
                    for (int i = 0; i < dataCount; i++)
                    {
                        int offsetStart = BitConverter.ToUInt16(response, offset + 2 + i * 2) + offset;
                        int offsetEnd = (i == dataCount - 1) ? response.Length : (BitConverter.ToUInt16(response, (offset + 4 + i * 2)) + offset);
                        ushort err = BitConverter.ToUInt16(response, offsetStart + 2);
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
                            for (int j = offsetStart + 6; j < offsetEnd; j++)
                            {
                                data.Add(response[j]);
                            }
                        }
                    }
                }
                else
                {
                    byte err = response[offset + 6];
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

                    if (response[offset + 4] == 0xCD || response[offset + 4] == 0xD3)
                        return OperationResult.CreateSuccessResult(data.ToArray(), dataType, hasMoreData);
                    if (response[offset + 4] == 0xCC || response[offset + 4] == 0xD2)
                    {
                        for (int i = offset + 10; i < offset + 2 + count; i++)
                        {
                            data.Add(response[i]);
                        }

                        dataType = BitConverter.ToUInt16(response, offset + 8);
                    }
                    else if (response[offset + 4] == 0xD5)
                    {
                        for (int i = offset + 8; i < offset + 2 + count; i++)
                        {
                            data.Add(response[i]);
                        }
                    }
                    else if (response[offset + 4] == 0xCB)
                    {
                        // PCCC의 형식이 반환됩니다.
                        if (response[58] != 0x00)
                            return new OperationResult<byte[], ushort, bool>(response[58], AllenBradleyDF1SerialResponseParser.GetExtStatusDescription(response[58]) + Environment.NewLine + "Source: " + response.RemoveBegin(57).ToHexString(' '));
                        if (!isRead)
                            return OperationResult.CreateSuccessResult(data.ToArray(), dataType, hasMoreData);
                        return OperationResult.CreateSuccessResult(response.RemoveBegin(61), dataType, hasMoreData);
                    }
                }

                return OperationResult.CreateSuccessResult(data.ToArray(), dataType, hasMoreData);
            }
            catch (Exception ex)
            {
                return new OperationResult<byte[], ushort, bool>("ExtractActualData failed: " + ex.Message + Environment.NewLine + "Source: " + response.ToHexString(' '));
            }
        }
    }
}
