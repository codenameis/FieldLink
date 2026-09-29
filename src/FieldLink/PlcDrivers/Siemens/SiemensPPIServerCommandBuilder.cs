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

namespace FieldLink.PlcDrivers.Siemens
{
    /// <summary>SiemensPPIServer 요청 프레임을 생성합니다.</summary>
    public static class SiemensPPIServerCommandBuilder
    {
        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "command">포장할 명령 바이트입니다.</param>
        /// <param name = "content">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackReadBack(byte[] command, List<byte> content)
        {
            byte[] back = new byte[21 + content.Count + 2];
            ProtocolBytes.HexStringToBytes("68 1D 1D 68 00 02 08 32 03 00 00 00 00 00 02 00 0C 00 00 04 01").CopyTo(back, 0);
            back[1] = (byte)(back.Length - 6);
            back[2] = (byte)(back.Length - 6);
            back[15] = (byte)(content.Count / 256);
            back[16] = (byte)(content.Count % 256);
            back[20] = command[18];
            content.CopyTo(back, 21);
            back[back.Length - 2] = (byte)AdditiveChecksum.Compute(new ArraySegment<byte>(back, 4, back.Length - 4 - 2));
            back[back.Length - 1] = AsciiControl.SYN;
            return back;
        }

        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "packCommand">packCommand에 사용할 입력값입니다.</param>
        /// <param name = "status">status에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackWriteBack(byte[] packCommand, byte[] status)
        {
            byte[] buffer = new byte[23 + status.Length];
            ProtocolBytes.HexStringToBytes("68 12 12 68 00 02 08 32 03 00 00 00 01 00 02 00 01 00 00 05 01 04 00 16").CopyTo(buffer, 0);
            buffer[20] = (byte)status.Length;
            status.CopyTo(buffer, 21);
            buffer[buffer.Length - 2] = (byte)AdditiveChecksum.Compute(new ArraySegment<byte>(buffer, 4, buffer.Length - 4 - 2));
            buffer[buffer.Length - 1] = AsciiControl.SYN;
            return buffer;
        }
    }
}
