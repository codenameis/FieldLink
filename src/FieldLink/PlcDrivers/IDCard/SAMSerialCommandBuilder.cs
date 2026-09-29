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
using static FieldLink.PlcDrivers.IDCard.SAMSerialResponseParser;

namespace FieldLink.PlcDrivers.IDCard
{
    /// <summary>SAMSerial 요청 프레임을 생성합니다.</summary>
    public static class SAMSerialCommandBuilder
    {
        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "command">포장할 명령 바이트입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackToSAMCommand(byte[] command)
        {
            byte[] buffer = new byte[command.Length + 8];
            buffer[0] = 0xAA;
            buffer[1] = 0xAA;
            buffer[2] = 0xAA;
            buffer[3] = 0x96;
            buffer[4] = 0x69;
            buffer[5] = BitConverter.GetBytes(buffer.Length - 7)[1];
            buffer[6] = BitConverter.GetBytes(buffer.Length - 7)[0];
            command.CopyTo(buffer, 7);
            int count = 0;
            for (int i = 5; i < buffer.Length - 1; i++)
            {
                count ^= buffer[i];
            }

            buffer[buffer.Length - 1] = (byte)count;
            return buffer;
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "cmd">포장할 명령 바이트입니다.</param>
        /// <param name = "para">para에 사용할 입력값입니다.</param>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildReadCommand(byte cmd, byte para, byte[] data)
        {
            if (data == null)
                data = new byte[0];
            byte[] buffer = new byte[2 + data.Length];
            buffer[0] = cmd;
            buffer[1] = para;
            data.CopyTo(buffer, 2);
            return buffer;
        }
    }
}
