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
using static FieldLink.PlcDrivers.YASKAWA.MemobusCommandBuilder;
using static FieldLink.PlcDrivers.YASKAWA.MemobusResponseParser;

namespace FieldLink.PlcDrivers.YASKAWA
{
    /// <summary>Memobus 프로토콜의 값과 바이트 표현을 변환합니다.</summary>
    public static class MemobusValueConverter
    {
        /// <summary>SetByteHead 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "buffer">프레임 데이터가 담긴 버퍼입니다.</param>
        /// <param name = "mfc">mfc에 사용할 입력값입니다.</param>
        /// <param name = "sfc">sfc에 사용할 입력값입니다.</param>
        /// <param name = "cpuTo">cpuTo에 사용할 입력값입니다.</param>
        /// <param name = "cpuFrom">cpuFrom에 사용할 입력값입니다.</param>
        internal static void SetByteHead(byte[] buffer, byte mfc, byte sfc, byte cpuTo, byte cpuFrom)
        {
            buffer[0] = BitConverter.GetBytes(buffer.Length - 2)[0];
            buffer[1] = BitConverter.GetBytes(buffer.Length - 2)[1];
            buffer[2] = mfc;
            buffer[3] = sfc;
            buffer[4] = (byte)((cpuTo << 4) + cpuFrom);
        }
    }
}
