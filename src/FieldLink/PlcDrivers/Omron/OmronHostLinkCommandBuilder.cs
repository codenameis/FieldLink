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
using static FieldLink.PlcDrivers.Omron.OmronHostLinkResponseParser;

namespace FieldLink.PlcDrivers.Omron
{
    /// <summary>OmronHostLink 요청 프레임을 생성합니다.</summary>
    public static class OmronHostLinkCommandBuilder
    {
        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "hostLink">hostLink에 사용할 입력값입니다.</param>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "cmd">포장할 명령 바이트입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackCommand(HostLinkFrameOptions hostLink, byte station, byte[] cmd)
        {
            cmd = ProtocolBytes.BytesToAsciiBytes(cmd);
            byte[] buffer = new byte[18 + cmd.Length];
            buffer[0] = (byte)'@';
            buffer[1] = ProtocolBytes.BuildAsciiBytesFrom(station)[0];
            buffer[2] = ProtocolBytes.BuildAsciiBytesFrom(station)[1];
            buffer[3] = (byte)'F';
            buffer[4] = (byte)'A';
            buffer[5] = hostLink.ResponseWaitTime;
            buffer[6] = ProtocolBytes.BuildAsciiBytesFrom(hostLink.ICF)[0];
            buffer[7] = ProtocolBytes.BuildAsciiBytesFrom(hostLink.ICF)[1];
            buffer[8] = ProtocolBytes.BuildAsciiBytesFrom(hostLink.DA2)[0];
            buffer[9] = ProtocolBytes.BuildAsciiBytesFrom(hostLink.DA2)[1];
            buffer[10] = ProtocolBytes.BuildAsciiBytesFrom(hostLink.SA2)[0];
            buffer[11] = ProtocolBytes.BuildAsciiBytesFrom(hostLink.SA2)[1];
            buffer[12] = ProtocolBytes.BuildAsciiBytesFrom(hostLink.SID)[0];
            buffer[13] = ProtocolBytes.BuildAsciiBytesFrom(hostLink.SID)[1];
            buffer[buffer.Length - 2] = (byte)'*';
            buffer[buffer.Length - 1] = AsciiControl.CR;
            cmd.CopyTo(buffer, 14);
            // FCS 계산
            int tmp = buffer[0];
            for (int i = 1; i < buffer.Length - 4; i++)
            {
                tmp = (tmp ^ buffer[i]);
            }

            buffer[buffer.Length - 4] = ProtocolBytes.BuildAsciiBytesFrom((byte)tmp)[0];
            buffer[buffer.Length - 3] = ProtocolBytes.BuildAsciiBytesFrom((byte)tmp)[1];
            return buffer;
        }
    }
}
