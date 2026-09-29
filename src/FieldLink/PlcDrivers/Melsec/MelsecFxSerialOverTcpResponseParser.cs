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
using static FieldLink.PlcDrivers.Melsec.MelsecFxSerialOverTcpValueConverter;
using static FieldLink.PlcDrivers.Melsec.MelsecFxSerialOverTcpCommandBuilder;

namespace FieldLink.PlcDrivers.Melsec
{
    /// <summary>MelsecFxSerialOverTcp 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class MelsecFxSerialOverTcpResponseParser
    {
        /// <summary>UnpackResponseContent 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "send">send에 사용할 입력값입니다.</param>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> UnpackResponseContent(FxGotFrameOptions context, byte[] send, byte[] response)
        {
            if (context.useGot)
            {
                if (response.Length > 68)
                {
                    response = GetBytesReceive(context, response); // 0x10을 제거합니다.
                    int index = -1;
                    for (int i = 0; i < response.Length - 4; i++)
                    {
                        if (response[i] == 0x10 && response[i + 1] == 0x02)
                        {
                            index = i;
                            break;
                        }
                    }

                    if (index >= 0)
                        return OperationResult.CreateSuccessResult(response.RemoveDouble(64 + index, 4));
                }

                return new OperationResult<byte[]>("Got failed: " + response.ToHexString(' ', 16));
            }
            else
            {
                return OperationResult.CreateSuccessResult(response);
            }
        }
    }
}
