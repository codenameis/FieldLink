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
using static FieldLink.PlcDrivers.FATEK.FatekProgramServerResponseParser;
using static FieldLink.PlcDrivers.FATEK.FatekProgramServerDefinitions;

namespace FieldLink.PlcDrivers.FATEK
{
    /// <summary>FatekProgramServer 요청 프레임을 생성합니다.</summary>
    public static class FatekProgramServerCommandBuilder
    {
        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "receive">receive에 사용할 입력값입니다.</param>
        /// <param name = "err">err에 사용할 입력값입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackResponseBack(byte[] receive, byte err, byte[] value)
        {
            if (value == null)
                value = new byte[0];
            byte[] buffer = new byte[9 + value.Length];
            buffer[0] = AsciiControl.STX;
            buffer[1] = receive[1];
            buffer[2] = receive[2];
            buffer[3] = receive[3];
            buffer[4] = receive[4];
            buffer[5] = err;
            value.CopyTo(buffer, 6);
            AdditiveChecksum.WriteAscii(buffer, 0, buffer.Length - 3);
            buffer[buffer.Length - 1] = AsciiControl.ETX;
            return buffer;
        }
    }
}
