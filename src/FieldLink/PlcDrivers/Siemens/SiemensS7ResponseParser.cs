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
using static FieldLink.PlcDrivers.Siemens.SiemensS7ValueConverter;

namespace FieldLink.PlcDrivers.Siemens
{
    /// <summary>SiemensS7 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class SiemensS7ResponseParser
    {
        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "content">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> AnalysisReadBit(byte[] content)
        {
            // 이 공개 API는 PPI에서도 사용된다. ISO/TCP에는 검증된 S7 항목 파서를 적용한다.
            if (!IsPpiFrame(content))
                return S7TcpReadResponseParser.Parse(content, true);
            try
            {
                int receiveCount = 1;
                if (content.Length >= 21 && content[20] == 1)
                {
                    byte[] buffer = new byte[receiveCount];
                    if (22 < content.Length)
                    {
                        if (content[21] == 0xFF && content[22] == 0x03)
                        {
                            buffer[0] = content[25];
                        }
                        else if (content[21] == 0x05 && content[22] == 0x00)
                        {
                            return new OperationResult<byte[]>(content[21], ProtocolMessages.SiemensReadLengthOverPlcAssign);
                        }
                        else if (content[21] == 0x06 && content[22] == 0x00)
                        {
                            return new OperationResult<byte[]>(content[21], ProtocolMessages.SiemensError0006);
                        }
                        else if (content[21] == 0x0A && content[22] == 0x00)
                        {
                            return new OperationResult<byte[]>(content[21], ProtocolMessages.SiemensError000A);
                        }
                        else
                        {
                            return new OperationResult<byte[]>(content[21], ProtocolMessages.UnknownError + " Source: " + content.ToHexString(' '));
                        }
                    }

                    return OperationResult.CreateSuccessResult(buffer);
                }
                else
                {
                    return new OperationResult<byte[]>(ProtocolMessages.SiemensDataLengthCheckFailed);
                }
            }
            catch (Exception ex)
            {
                return new OperationResult<byte[]>("AnalysisReadBit failed: " + ex.Message + Environment.NewLine + " Msg:" + ProtocolBytes.ByteToHexString(content, ' '));
            }
        }

        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "content">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> AnalysisReadByte(byte[] content)
        {
            if (!IsPpiFrame(content))
                return S7TcpReadResponseParser.Parse(content, false);
            try
            {
                List<byte> list = new List<byte>();
                if (content.Length >= 21)
                {
                    for (int i = 21; i < content.Length - 1; i++)
                    {
                        if (content[i] == 0xFF && content[i + 1] == 0x04)
                        {
                            int count = (content[i + 2] * 256 + content[i + 3]) / 8;
                            list.AddRange(content.SelectMiddle(i + 4, count));
                            i += count + 3;
                        }
                        else if (content[i] == 0xFF && content[i + 1] == 0x09)
                        {
                            int count = content[i + 2] * 256 + content[i + 3];
                            if (count % 3 == 0)
                            {
                                for (int j = 0; j < count / 3; j++)
                                {
                                    list.AddRange(content.SelectMiddle(i + 5 + 3 * j, 2));
                                }
                            }
                            else
                            {
                                for (int j = 0; j < count / 5; j++)
                                {
                                    list.AddRange(content.SelectMiddle(i + 7 + 5 * j, 2));
                                }
                            }

                            i += count + 4;
                        }
                        else if (content[i] == 0x05 && content[i + 1] == 0x00)
                            return new OperationResult<byte[]>(content[i], ProtocolMessages.SiemensReadLengthOverPlcAssign);
                        else if (content[i] == 0x06 && content[i + 1] == 0x00)
                            return new OperationResult<byte[]>(content[i], ProtocolMessages.SiemensError0006);
                        else if (content[i] == 0x0A && content[i + 1] == 0x00)
                            return new OperationResult<byte[]>(content[i], ProtocolMessages.SiemensError000A);
                    }

                    return OperationResult.CreateSuccessResult(list.ToArray());
                }
                else
                {
                    return new OperationResult<byte[]>(ProtocolMessages.SiemensDataLengthCheckFailed + " Msg: " + ProtocolBytes.ByteToHexString(content, ' '));
                }
            }
            catch (Exception ex)
            {
                return new OperationResult<byte[]>("AnalysisReadByte failed: " + ex.Message + Environment.NewLine + " Msg:" + ProtocolBytes.ByteToHexString(content, ' '));
            }
        }
        private static bool IsPpiFrame(byte[] content) => content != null && content.Length >= 6 &&
            content[0] == 0x68 && content[3] == 0x68 && content[1] == content[2] &&
            content.Length == content[1] + 6 && content[content.Length - 1] == 0x16;
    }
}
