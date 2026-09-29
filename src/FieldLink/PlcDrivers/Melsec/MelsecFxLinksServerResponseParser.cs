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
using static FieldLink.PlcDrivers.Melsec.MelsecFxLinksServerCommandBuilder;

namespace FieldLink.PlcDrivers.Melsec
{
    /// <summary>MelsecFxLinksServer 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class MelsecFxLinksServerResponseParser
    {
        /// <summary>프로토콜 응답에서 데이터와 상태 정보를 추출합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "command">포장할 명령 바이트입니다.</param>
        /// <param name = "format">format에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> ExtraMcCore(FxLinksResponseOptions context, byte[] command, int format)
        {
            byte station = Convert.ToByte(Encoding.ASCII.GetString(command, 1, 2), 16);
            if (context.Station != station)
                return new OperationResult<byte[]>($"Station Not Match, need: {context.Station}  but: {station}");
            // 형식 정보 및 검증 검사
            if (format == 1)
            {
                if (command[0] != AsciiControl.ENQ)
                    return new OperationResult<byte[]>("First Byte Must Start with ENQ(0x05)");
                if (context.SumCheck)
                {
                    if (!AdditiveChecksum.VerifyAscii(command, 1, command.Length - 2))
                        return new OperationResult<byte[]>(0x02, "Sum Check Failed!");
                    return OperationResult.CreateSuccessResult(command.SelectMiddle(5, command.Length - 7));
                }
                else
                {
                    return OperationResult.CreateSuccessResult(command.SelectMiddle(5, command.Length - 5));
                }
            }
            else if (format == 4)
            {
                if (command[command.Length - 1] == AsciiControl.LF && command[command.Length - 2] == AsciiControl.CR)
                    return ExtraMcCore(context, command.RemoveLast(2), 1);
                return new OperationResult<byte[]>("In format 4 case, last two char must be CR(0x0d) and LF(0x0a)");
            }

            return OperationResult.CreateSuccessResult(command);
        }
    }
}
