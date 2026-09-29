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
using static FieldLink.PlcDrivers.Fuji.FujiSPHServerDefinitions;

namespace FieldLink.PlcDrivers.Fuji
{
    /// <summary>FujiSPHServer 요청 프레임을 생성합니다.</summary>
    public static class FujiSPHServerCommandBuilder
    {
        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "cmd">포장할 명령 바이트입니다.</param>
        /// <param name = "err">err에 사용할 입력값입니다.</param>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackCommand(byte[] cmd, byte err, byte[] data)
        {
            if (data == null)
                data = new byte[0];
            byte[] buffer = new byte[26 + data.Length];
            buffer[0] = 0xFB;
            buffer[1] = 0x80;
            buffer[2] = 0x80;
            buffer[3] = 0x00;
            buffer[4] = err;
            buffer[5] = 0x7B;
            buffer[6] = cmd[6]; // 연결 아이디
            buffer[7] = 0x00;
            buffer[8] = 0x11;
            buffer[9] = 0x00;
            buffer[10] = 0x00;
            buffer[11] = 0x00;
            buffer[12] = 0x00;
            buffer[13] = 0x00;
            buffer[14] = cmd[14]; // 명령
            buffer[15] = cmd[15]; // 모드
            buffer[16] = 0x00;
            buffer[17] = 0x01;
            buffer[18] = BitConverter.GetBytes(data.Length + 6)[0]; // 길이
            buffer[19] = BitConverter.GetBytes(data.Length + 6)[1];
            Array.Copy(cmd, 20, buffer, 20, 6);
            if (data.Length > 0)
                data.CopyTo(buffer, 26);
            return buffer;
        }
    }
}
