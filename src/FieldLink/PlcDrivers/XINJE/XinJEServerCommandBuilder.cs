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
using static FieldLink.PlcDrivers.XINJE.XinJEServerDefinitions;

namespace FieldLink.PlcDrivers.XINJE
{
    /// <summary>XinJEServer 요청 프레임을 생성합니다.</summary>
    public static class XinJEServerCommandBuilder
    {
        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "command">포장할 명령 바이트입니다.</param>
        /// <param name = "status">status에 사용할 입력값입니다.</param>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackCommand(byte[] command, ushort status, byte[] data)
        {
            if (data == null)
            {
                byte[] buffer = command.SelectBegin(14);
                buffer[4] = 0x00;
                buffer[5] = 0x08;
                if (status == 0)
                    return buffer;
                buffer[7] = (byte)(buffer[7] + 0x80 + status);
                return buffer;
            }
            else
            {
                byte[] buffer = new byte[9 + data.Length];
                Array.Copy(command, 0, buffer, 0, 8);
                buffer[4] = 0x00;
                buffer[5] = (byte)(buffer.Length - 6);
                buffer[8] = (byte)data.Length;
                data.CopyTo(buffer, 9);
                return buffer;
            }
        }
    }
}
