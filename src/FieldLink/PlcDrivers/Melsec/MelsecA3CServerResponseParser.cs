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
using static FieldLink.PlcDrivers.Melsec.MelsecA3CServerValueConverter;
using static FieldLink.PlcDrivers.Melsec.MelsecA3CServerCommandBuilder;

namespace FieldLink.PlcDrivers.Melsec
{
    /// <summary>MelsecA3CServer 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class MelsecA3CServerResponseParser
    {
        /// <summary>프로토콜 응답에서 데이터와 상태 정보를 추출합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "command">포장할 명령 바이트입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> ExtraMcCore(A3CResponseOptions context, byte[] command)
        {
            // 국번은 명령 빌더와 동일한 두 자리 16진 ASCII로 해석한다.
            byte station = Convert.ToByte(Encoding.ASCII.GetString(command, context.Format == 2 ? 5 : 3, 2), 16);
            if (context.Station != station)
                return new OperationResult<byte[]>($"Station Not Match, need: {context.Station}  but: {station}");
            // 형식 정보 및 검증 검사
            if (context.Format == 1)
            {
                if (command[0] != 0x05)
                    return new OperationResult<byte[]>("First Byte Must Start with 0x05");
                if (context.SumCheck)
                {
                    if (!CalculatSumCheck(context, command, 1, 2))
                        return new OperationResult<byte[]>("Sum Check Failed!");
                    return OperationResult.CreateSuccessResult(command.SelectMiddle(11, command.Length - 13));
                }
                else
                {
                    return OperationResult.CreateSuccessResult(command.SelectMiddle(11, command.Length - 11));
                }
            }
            else if (context.Format == 2)
            {
                if (command[0] != 0x05)
                    return new OperationResult<byte[]>("First Byte Must Start with 0x05");
                if (context.SumCheck)
                {
                    if (!CalculatSumCheck(context, command, 1, 2))
                        return new OperationResult<byte[]>("Sum Check Failed!");
                    return OperationResult.CreateSuccessResult(command.SelectMiddle(13, command.Length - 15));
                }
                else
                {
                    return OperationResult.CreateSuccessResult(command.SelectMiddle(13, command.Length - 13));
                }
            }
            else if (context.Format == 3)
            {
                if (command[0] != 0x02)
                    return new OperationResult<byte[]>("First Byte Must Start with 0x02");
                if (context.SumCheck)
                {
                    if (command[command.Length - 3] != 0x03)
                        return new OperationResult<byte[]>("The last three Byte Must be 0x03");
                    if (!CalculatSumCheck(context, command, 1, 2))
                        return new OperationResult<byte[]>("Sum Check Failed!");
                    return OperationResult.CreateSuccessResult(command.SelectMiddle(11, command.Length - 14));
                }
                else
                {
                    if (command[command.Length - 1] != 0x03)
                        return new OperationResult<byte[]>("The last Byte Must be 0x03");
                    return OperationResult.CreateSuccessResult(command.SelectMiddle(11, command.Length - 12));
                }
            }
            else if (context.Format == 4)
            {
                if (command[0] != 0x05)
                    return new OperationResult<byte[]>("First Byte Must Start with 0x05");
                if (command[command.Length - 1] != 0x0A)
                    return new OperationResult<byte[]>("The last Byte must be 0x0D,0x0A");
                if (command[command.Length - 2] != 0x0D)
                    return new OperationResult<byte[]>("The last Byte must be 0x0D,0x0A");
                if (context.SumCheck)
                {
                    if (!CalculatSumCheck(context, command, 1, 4))
                        return new OperationResult<byte[]>("Sum Check Failed!");
                    return OperationResult.CreateSuccessResult(command.SelectMiddle(11, command.Length - 15));
                }
                else
                {
                    return OperationResult.CreateSuccessResult(command.SelectMiddle(11, command.Length - 13));
                }
            }
            else
                return new OperationResult<byte[]>("Not Support Format:" + context.Format);
        }
    }
}
