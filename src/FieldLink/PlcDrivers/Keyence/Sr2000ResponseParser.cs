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

namespace FieldLink.PlcDrivers.Keyence
{
    /// <summary>Sr2000 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class Sr2000ResponseParser
    {
        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "command">포장할 명령 바이트입니다.</param>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<string> Parse(string command, byte[] response)
        {
            string cmd = command;
            if (command.IndexOf(',') > 0)
                cmd = command.Substring(0, command.IndexOf(','));
            string result = Encoding.ASCII.GetString(response).Trim('\r');
            if (result.StartsWith($"ER,{cmd},"))
                return new OperationResult<string>(KeyenceSR2000ResponseParser.GetErrorDescription(result.Substring(4 + cmd.Length)));
            if (result.StartsWith($"OK,{cmd}"))
            {
                if (result.Length > (4 + cmd.Length))
                {
                    return OperationResult.CreateSuccessResult(result.Substring(4 + cmd.Length));
                }
            }

            return OperationResult.CreateSuccessResult(result);
        }

        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<bool> ParseInput(string value)
        {
            if (value == "ON")
                return OperationResult.CreateSuccessResult(true);
            if (value == "OFF")
                return OperationResult.CreateSuccessResult(false);
            return new OperationResult<bool>(value);
        }

        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static int[] ParseRecord(string value) => value.Split(',').Select(int.Parse).ToArray();
    }
}
