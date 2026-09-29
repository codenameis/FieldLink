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
using static FieldLink.PlcDrivers.Melsec.MelsecA3CNetResponseParser;

namespace FieldLink.PlcDrivers.Melsec
{
    /// <summary>MelsecA3CNet 요청 프레임을 생성합니다.</summary>
    public static class MelsecA3CNetCommandBuilder
    {
        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "plc">plc에 사용할 입력값입니다.</param>
        /// <param name = "mcCommand">mcCommand에 사용할 입력값입니다.</param>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackCommand(A3CFrameOptions plc, byte[] mcCommand, byte station = 0)
        {
            MemoryStream ms = new MemoryStream();
            if (plc.Format != 3)
                ms.WriteByte(AsciiControl.ENQ); // STX
            else
                ms.WriteByte(AsciiControl.STX);
            if (plc.Format == 2)
            {
                ms.WriteByte(0x30); // 형식 2의 블록 번호
                ms.WriteByte(0x30);
            }

            ms.WriteByte(0x46); // 알파벳 F9
            ms.WriteByte(0x39);
            ms.WriteByte(ProtocolBytes.BuildAsciiBytesFrom(station)[0]); // 국번
            ms.WriteByte(ProtocolBytes.BuildAsciiBytesFrom(station)[1]);
            ms.WriteByte(0x30); // 네트워크 번호
            ms.WriteByte(0x30);
            ms.WriteByte(0x46); // 프로그래밍 가능한 컨트롤러 번호
            ms.WriteByte(0x46);
            ms.WriteByte(0x30); // 이 사이트 번호
            ms.WriteByte(0x30);
            ms.Write(mcCommand, 0, mcCommand.Length);
            if (plc.Format == 3)
            {
                ms.WriteByte(AsciiControl.ETX);
            }

            // 계산 및 검증
            if (plc.SumCheck)
            {
                byte[] cmd = ms.ToArray();
                int sum = 0;
                for (int i = 1; i < cmd.Length; i++)
                {
                    sum += cmd[i];
                }

                ms.WriteByte(ProtocolBytes.BuildAsciiBytesFrom((byte)sum)[0]);
                ms.WriteByte(ProtocolBytes.BuildAsciiBytesFrom((byte)sum)[1]);
            }

            if (plc.Format == 4)
            {
                ms.WriteByte(AsciiControl.CR); // CR
                ms.WriteByte(AsciiControl.LF); // LF
            }

            byte[] buffer = ms.ToArray();
            ms.Dispose();
            return buffer;
        }
    }
}
