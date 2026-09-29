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

namespace FieldLink.PlcDrivers.Melsec
{
    /// <summary>MelsecMcServer 요청 프레임을 생성합니다.</summary>
    public static class MelsecMcServerCommandBuilder
    {
        /// <summary>상태코드, 데이터를 완성된 응답 메시지에 포장합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "status">상태 정보</param>
        /// <param name = "data">데이터</param>
        /// <returns>상태 정보</returns>
        public static byte[] PackCommand(McResponseOptions context, ushort status, byte[] data)
        {
            if (data == null)
                data = new byte[0];
            if (context.isBinary)
            {
                byte[] back = new byte[11 + data.Length];
                ProtocolBytes.HexStringToBytes("D0 00 00 FF FF 03 00 00 00 00 00").CopyTo(back, 0);
                if (data.Length > 0)
                    data.CopyTo(back, 11);
                BitConverter.GetBytes((short)(data.Length + 2)).CopyTo(back, 7);
                BitConverter.GetBytes(status).CopyTo(back, 9);
                return back;
            }
            else
            {
                byte[] back = new byte[22 + data.Length];
                Encoding.ASCII.GetBytes("D00000FF03FF0000000000").CopyTo(back, 0);
                if (data.Length > 0)
                    data.CopyTo(back, 22);
                Encoding.ASCII.GetBytes((data.Length + 4).ToString("X4")).CopyTo(back, 14);
                Encoding.ASCII.GetBytes(status.ToString("X4")).CopyTo(back, 18);
                return back;
            }
        }
    }
}
