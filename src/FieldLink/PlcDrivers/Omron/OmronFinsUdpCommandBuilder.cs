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

namespace FieldLink.PlcDrivers.Omron
{
    /// <summary>OmronFinsUdp 요청 프레임을 생성합니다.</summary>
    public static class OmronFinsUdpCommandBuilder
    {
        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "cmd">포장할 명령 바이트입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackCommand(FinsUdpFrameOptions context, byte[] cmd)
        {
            byte[] buffer = new byte[10 + cmd.Length];
            buffer[0] = context.ICF;
            buffer[1] = context.RSV;
            buffer[2] = context.GCT;
            buffer[3] = context.DNA;
            buffer[4] = context.DA1;
            buffer[5] = context.DA2;
            buffer[6] = context.SNA;
            buffer[7] = context.SA1;
            buffer[8] = context.SA2;
            buffer[9] = (byte)context.SID;
            cmd.CopyTo(buffer, 10);
            return buffer;
        }
    }
}
