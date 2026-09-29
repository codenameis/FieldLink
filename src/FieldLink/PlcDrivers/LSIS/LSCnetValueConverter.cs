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
using static FieldLink.PlcDrivers.LSIS.LSCnetResponseParser;
using static FieldLink.PlcDrivers.LSIS.LSCnetAddressParser;
using static FieldLink.PlcDrivers.LSIS.LSCnetCommandBuilder;
using static FieldLink.PlcDrivers.LSIS.LSCnetDefinitions;

namespace FieldLink.PlcDrivers.LSIS
{
    /// <summary>LSCnet 프로토콜의 값과 바이트 표현을 변환합니다.</summary>
    public static class LSCnetValueConverter
    {
        /// <summary>AddBccTail 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "command">포장할 명령 바이트입니다.</param>
        internal static void AddBccTail(List<byte> command)
        {
            int sum = 0;
            for (int i = 0; i < command.Count; i++)
            {
                sum += command[i];
            }

            command.AddRange(ProtocolBytes.BuildAsciiBytesFrom((byte)sum));
        }
    }
}
