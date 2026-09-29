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
using static FieldLink.PlcDrivers.YASKAWA.MemobusTcpServerValueConverter;
using static FieldLink.PlcDrivers.YASKAWA.MemobusTcpServerDefinitions;

namespace FieldLink.PlcDrivers.YASKAWA
{
    /// <summary>MemobusTcpServer 요청 프레임을 생성합니다.</summary>
    public static class MemobusTcpServerCommandBuilder
    {
        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "cmds">cmds에 사용할 입력값입니다.</param>
        /// <param name = "err">err에 사용할 입력값입니다.</param>
        /// <param name = "result">result에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackCommandBack(byte[] cmds, byte err, byte[] result)
        {
            if (result == null)
                result = new byte[0];
            if (err > 0)
            {
                byte[] back = new byte[6 + result.Length];
                back[0] = 0x04; // Length L
                back[1] = 0x00; // Length H
                back[2] = cmds[14]; // MFC
                back[3] = (byte)(cmds[15] + 0x80); // SFC
                back[4] = TransByteHighLow(cmds[16]); // CPU 번호
                back[5] = err; // 오류
                return MemobusCommandBuilder.PackCommandWithHeader(back, cmds[1]);
            }
            else
            {
                if (cmds[15] == 0x01 || cmds[15] == 0x02 || cmds[15] == 0x03 || cmds[15] == 0x04)
                {
                    byte[] back = new byte[5 + result.Length];
                    back[0] = BitConverter.GetBytes(3 + result.Length)[0]; // Length L
                    back[1] = BitConverter.GetBytes(3 + result.Length)[1]; // Length H
                    back[2] = cmds[14]; // MFC
                    back[3] = cmds[15]; // SFC
                    back[4] = TransByteHighLow(cmds[16]); // CPU 번호
                    result.CopyTo(back, 5);
                    return MemobusCommandBuilder.PackCommandWithHeader(back, cmds[1]);
                }
                else if (cmds[15] == 0x05 || cmds[15] == 0x06 || cmds[15] == 0x08)
                {
                    byte[] back = cmds.RemoveBegin(12);
                    back[0] = BitConverter.GetBytes(back.Length - 2)[0]; // Length L
                    back[1] = BitConverter.GetBytes(back.Length - 2)[1]; // Length H
                    back[4] = TransByteHighLow(cmds[16]); // CPU 번호
                    return MemobusCommandBuilder.PackCommandWithHeader(back, cmds[1]);
                }
                else if (cmds[15] == 0x09 || cmds[15] == 0x0A || cmds[15] == 0x0D)
                {
                    byte[] back = new byte[8 + result.Length];
                    back[0] = BitConverter.GetBytes(back.Length - 2)[0]; // Length L
                    back[1] = BitConverter.GetBytes(back.Length - 2)[1]; // Length H
                    back[2] = cmds[14]; // MFC
                    back[3] = cmds[15]; // SFC
                    back[4] = TransByteHighLow(cmds[16]); // CPU 번호
                    back[6] = BitConverter.GetBytes(result.Length / 2)[0]; // 레지스터 길이의 하위 바이트
                    back[7] = BitConverter.GetBytes(result.Length / 2)[1]; // 레지스터 길이의 상위 바이트
                    result.CopyTo(back, 8);
                    return MemobusCommandBuilder.PackCommandWithHeader(back, cmds[1]);
                }
                else if (cmds[15] == 0x0B)
                {
                    byte[] back = cmds.SelectMiddle(12, 10);
                    back[0] = BitConverter.GetBytes(back.Length - 2)[0]; // Length L
                    back[1] = BitConverter.GetBytes(back.Length - 2)[1]; // Length H
                    back[4] = TransByteHighLow(cmds[16]); // CPU 번호
                    return MemobusCommandBuilder.PackCommandWithHeader(back, cmds[1]);
                }
                else if (cmds[15] == 0x0E)
                {
                    byte[] back = cmds.SelectMiddle(12, 8);
                    back[0] = BitConverter.GetBytes(back.Length - 2)[0]; // Length L
                    back[1] = BitConverter.GetBytes(back.Length - 2)[1]; // Length H
                    back[4] = TransByteHighLow(cmds[16]); // CPU 번호
                    return MemobusCommandBuilder.PackCommandWithHeader(back, cmds[1]);
                }
                else if (cmds[15] == 0x0F || cmds[15] == 0x10)
                {
                    byte[] back = cmds.SelectMiddle(12, 9);
                    back[0] = BitConverter.GetBytes(back.Length - 2)[0]; // Length L
                    back[1] = BitConverter.GetBytes(back.Length - 2)[1]; // Length H
                    back[4] = TransByteHighLow(cmds[16]); // CPU 번호
                    return MemobusCommandBuilder.PackCommandWithHeader(back, cmds[1]);
                }
            }

            return PackCommandBack(cmds, 0x03, null);
        }
    }
}
