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
using static FieldLink.PlcDrivers.Melsec.MelsecFxSerialOverTcpCommandBuilder;
using static FieldLink.PlcDrivers.Melsec.MelsecFxSerialOverTcpResponseParser;

namespace FieldLink.PlcDrivers.Melsec
{
    /// <summary>MelsecFxSerialOverTcp 프로토콜의 값과 바이트 표현을 변환합니다.</summary>
    public static class MelsecFxSerialOverTcpValueConverter
    {
        /// <summary>GetBytesSend 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "command">포장할 명령 바이트입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] GetBytesSend(FxGotFrameOptions context, byte[] command)
        {
            List<byte> array = new List<byte>();
            for (int i = 0; i < command.Length; i++)
            {
                if (i < 2)
                {
                    array.Add(command[i]);
                }
                else if (i < command.Length - 4)
                {
                    if (command[i] == 0x10)
                        array.Add(command[i]);
                    array.Add(command[i]);
                }
                else
                {
                    array.Add(command[i]);
                }
            }

            return array.ToArray();
        }

        /// <summary>GetBytesReceive 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] GetBytesReceive(FxGotFrameOptions context, byte[] response)
        {
            List<byte> array = new List<byte>();
            for (int i = 0; i < response.Length; i++)
            {
                if (i < 2)
                {
                    array.Add(response[i]);
                }
                else if (i < response.Length - 4)
                {
                    if (response[i] == 0x10 && response[i + 1] == 0x10)
                    {
                        array.Add(response[i]);
                        i++;
                    }
                    else
                    {
                        array.Add(response[i]);
                    }
                }
                else
                {
                    array.Add(response[i]);
                }
            }

            return array.ToArray();
        }
    }
}
