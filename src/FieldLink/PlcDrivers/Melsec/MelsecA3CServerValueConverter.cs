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
using static FieldLink.PlcDrivers.Melsec.MelsecA3CServerResponseParser;
using static FieldLink.PlcDrivers.Melsec.MelsecA3CServerCommandBuilder;

namespace FieldLink.PlcDrivers.Melsec
{
    /// <summary>MelsecA3CServer 프로토콜의 값과 바이트 표현을 변환합니다.</summary>
    public static class MelsecA3CServerValueConverter
    {
        /// <summary>SetSumCheck 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "command">포장할 명령 바이트입니다.</param>
        /// <param name = "startLength">startLength에 사용할 입력값입니다.</param>
        /// <param name = "endLength">endLength에 사용할 입력값입니다.</param>
        public static void SetSumCheck(A3CResponseOptions context, byte[] command, int startLength, int endLength)
        {
            int sum = 0;
            for (int i = startLength; i < command.Length - endLength; i++)
            {
                sum += command[i];
            }

            byte[] check = ProtocolBytes.BuildAsciiBytesFrom((byte)sum);
            command[command.Length - endLength] = check[0];
            command[command.Length - endLength + 1] = check[1];
        }

        /// <summary>CalculatSumCheck 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "command">포장할 명령 바이트입니다.</param>
        /// <param name = "startLength">startLength에 사용할 입력값입니다.</param>
        /// <param name = "endLength">endLength에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static bool CalculatSumCheck(A3CResponseOptions context, byte[] command, int startLength, int endLength)
        {
            int sum = 0;
            for (int i = startLength; i < command.Length - endLength; i++)
            {
                sum += command[i];
            }

            byte[] check = ProtocolBytes.BuildAsciiBytesFrom((byte)sum);
            if (command[command.Length - endLength] != check[0] || command[command.Length - endLength + 1] != check[1])
                return false;
            return true;
        }
    }
}
