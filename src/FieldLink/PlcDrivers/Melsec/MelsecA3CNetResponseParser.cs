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
using static FieldLink.PlcDrivers.Melsec.MelsecA3CNetCommandBuilder;

namespace FieldLink.PlcDrivers.Melsec
{
    /// <summary>MelsecA3CNet 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class MelsecA3CNetResponseParser
    {
        /// <summary>장치 오류 또는 상태 코드에 대응하는 설명을 반환합니다.</summary>
        /// <param name = "plc">plc에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static int GetErrorCodeOrDataStartIndex(A3CFrameOptions plc)
        {
            int start = 11;
            switch (plc.Format)
            {
                case 1:
                    start = 11;
                    break;
                case 2:
                    start = 13;
                    break;
                case 3:
                    start = 15;
                    break;
                case 4:
                    start = 11;
                    break;
            }

            return start;
        }

        /// <summary>프로토콜 응답에서 데이터와 상태 정보를 추출합니다.</summary>
        /// <param name = "plc">plc에 사용할 입력값입니다.</param>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> ExtraReadActualResponse(A3CFrameOptions plc, byte[] response)
        {
            try
            {
                int start = GetErrorCodeOrDataStartIndex(plc);
                // 결과 검증
                if (plc.Format == 1 || plc.Format == 2 || plc.Format == 4)
                {
                    if (response[0] == AsciiControl.NAK)
                    {
                        int errorCode = Convert.ToInt32(Encoding.ASCII.GetString(response, start, 4), 16);
                        return new OperationResult<byte[]>(errorCode, MelsecResponseParser.GetErrorDescription(errorCode));
                    }

                    if (response[0] != AsciiControl.STX)
                        return new OperationResult<byte[]>(response[0], "Read Faild:" + ProtocolBytes.GetAsciiStringRender(response));
                }
                else if (plc.Format == 3)
                {
                    string ending = Encoding.ASCII.GetString(response, 11, 4);
                    if (ending == "QNAK")
                    {
                        int errorCode = Convert.ToInt32(Encoding.ASCII.GetString(response, start, 4), 16);
                        return new OperationResult<byte[]>(errorCode, MelsecResponseParser.GetErrorDescription(errorCode));
                    }

                    if (ending != "QACK")
                        return new OperationResult<byte[]>(response[0], "Read Faild:" + ProtocolBytes.GetAsciiStringRender(response));
                }

                int end = -1;
                for (int i = start; i < response.Length; i++)
                {
                    if (response[i] == AsciiControl.ETX)
                    {
                        end = i;
                        break;
                    }
                }

                if (end == -1)
                    end = response.Length;
                return OperationResult.CreateSuccessResult(response.SelectMiddle(start, end - start));
            }
            catch (Exception ex)
            {
                return new OperationResult<byte[]>("ExtraReadActualResponse Wrong:" + ex.Message + Environment.NewLine + "Source: " + response.ToHexString(' '));
            }
        }

        /// <summary>입력 데이터가 프로토콜의 검증 조건을 만족하는지 확인합니다.</summary>
        /// <param name = "plc">plc에 사용할 입력값입니다.</param>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult CheckWriteResponse(A3CFrameOptions plc, byte[] response)
        {
            int start = GetErrorCodeOrDataStartIndex(plc);
            try
            {
                // 결과 검증
                if (plc.Format == 1 || plc.Format == 2)
                {
                    if (response[0] == AsciiControl.NAK)
                    {
                        int errorCode = Convert.ToInt32(Encoding.ASCII.GetString(response, start, 4), 16);
                        return new OperationResult<byte[]>(errorCode, MelsecResponseParser.GetErrorDescription(errorCode));
                    }

                    if (response[0] != AsciiControl.ACK)
                        return new OperationResult<byte[]>(response[0], "Write Faild:" + ProtocolBytes.GetAsciiStringRender(response));
                }
                else if (plc.Format == 3)
                {
                    if (response[0] != AsciiControl.STX)
                        return new OperationResult<byte[]>(response[0], "Write Faild:" + ProtocolBytes.GetAsciiStringRender(response));
                    string ending = Encoding.ASCII.GetString(response, 11, 4);
                    if (ending == "QNAK")
                    {
                        int errorCode = Convert.ToInt32(Encoding.ASCII.GetString(response, start, 4), 16);
                        return new OperationResult<byte[]>(errorCode, MelsecResponseParser.GetErrorDescription(errorCode));
                    }

                    if (ending != "QACK")
                        return new OperationResult<byte[]>(response[0], "Write Faild:" + ProtocolBytes.GetAsciiStringRender(response));
                }
                else if (plc.Format == 4)
                {
                    if (response[0] == AsciiControl.NAK)
                    {
                        int errorCode = Convert.ToInt32(Encoding.ASCII.GetString(response, start, 4), 16);
                        return new OperationResult<byte[]>(errorCode, MelsecResponseParser.GetErrorDescription(errorCode));
                    }

                    if (response[0] != AsciiControl.ACK)
                        return new OperationResult<byte[]>(response[0], "Write Faild:" + ProtocolBytes.GetAsciiStringRender(response));
                }

                return OperationResult.CreateSuccessResult();
            }
            catch (Exception ex)
            {
                return new OperationResult<byte[]>("CheckWriteResponse failed: " + ex.Message + Environment.NewLine + "Content: " + ProtocolBytes.GetAsciiStringRender(response));
            }
        }
    }
}
