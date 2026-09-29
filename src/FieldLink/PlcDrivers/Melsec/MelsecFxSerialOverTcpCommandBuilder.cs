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
using static FieldLink.PlcDrivers.Melsec.MelsecFxSerialOverTcpResponseParser;

namespace FieldLink.PlcDrivers.Melsec
{
    /// <summary>MelsecFxSerialOverTcp 요청 프레임을 생성합니다.</summary>
    public static class MelsecFxSerialOverTcpCommandBuilder
    {
        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "command">포장할 명령 바이트입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackCommandWithHeader(FxGotFrameOptions context, byte[] command)
        {
            if (context.useGot)
            {
                byte[] buffer = new byte[32 + 34 + command.Length];
                buffer[0] = 0x10;
                buffer[1] = 0x02;
                buffer[2] = 0x5E;
                buffer[6] = 0xFC;
                buffer[12] = 0x12;
                buffer[13] = 0x12;
                buffer[17] = 0xFF;
                buffer[18] = 0xFF;
                buffer[19] = 0x03;
                buffer[22] = 0xFF;
                buffer[23] = 0x03;
                buffer[26] = BitConverter.GetBytes(34 + command.Length)[0]; // 메시지의 길이 정보 0x2D
                buffer[27] = BitConverter.GetBytes(34 + command.Length)[1];
                buffer[28] = 0x1C;
                buffer[29] = 0x09;
                buffer[30] = 0x1A;
                buffer[31] = 0x18;
                buffer[41] = 0xFC;
                buffer[44] = 0x12;
                buffer[45] = 0x12;
                buffer[46] = 0x04;
                buffer[47] = 0x14;
                buffer[49] = 0x01;
                buffer[50] = BitConverter.GetBytes(context.Port)[1]; // 포트
                buffer[51] = BitConverter.GetBytes(context.Port)[0];
                buffer[52] = IPAddress.Parse(context.IpAddress).GetAddressBytes()[0]; // IP
                buffer[53] = IPAddress.Parse(context.IpAddress).GetAddressBytes()[1];
                buffer[54] = IPAddress.Parse(context.IpAddress).GetAddressBytes()[2];
                buffer[55] = IPAddress.Parse(context.IpAddress).GetAddressBytes()[3];
                buffer[56] = 0x01;
                buffer[57] = 0x02;
                BitConverter.GetBytes((int)context.MessageId).CopyTo(buffer, 58); // 메시지 ID
                command.CopyTo(buffer, 62);
                buffer[buffer.Length - 4] = 0x10;
                buffer[buffer.Length - 3] = 0x03;
                MelsecValueConverter.FxCalculateCRC(buffer, 2, 4).CopyTo(buffer, buffer.Length - 2); // CRC
                return GetBytesSend(context, buffer);
            }
            else
            {
                return command;
            }
        }
    }
}
