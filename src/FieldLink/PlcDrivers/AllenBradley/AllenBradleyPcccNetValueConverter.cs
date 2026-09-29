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

namespace FieldLink.PlcDrivers.AllenBradley
{
    /// <summary>AllenBradleyPcccNet 프로토콜의 값과 바이트 표현을 변환합니다.</summary>
    public static class AllenBradleyPcccNetValueConverter
    {
        /// <summary>GetLargeForwardOpen 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "connectionID">connectionID에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] GetLargeForwardOpen(PcccConnectionOptions context, ushort connectionID)
        {
            byte[] buffer = @"
00 00 00 00 0a 00 02 00 00 00 00 00 b2 00 30 00
54 02 20 06 24 01 0a 05 00 00 00 00 e8 a3 14 00
27 04 09 10 0b 46 a5 c1 07 00 00 00 01 40 20 00
f4 43 01 40 20 00 f4 43 a3 03 01 00 20 02 24 01".ToHexBytes();
            BitConverter.GetBytes(AllenBradleyDefinitions.OriginatorVendorID).CopyTo(buffer, 34);
            BitConverter.GetBytes(AllenBradleyDefinitions.OriginatorSerialNumber).CopyTo(buffer, 36);
            BitConverter.GetBytes(context.TOConnectionId).CopyTo(buffer, 28);
            return buffer;
        }

        /// <summary>GetLargeForwardClose 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] GetLargeForwardClose(PcccConnectionOptions context)
        {
            return @"
00 00 00 00 0a 00 02 00 00 00 00 00 b2 00 18 00
4e 02 20 06 24 01 0a 05 27 04 09 10 0b 46 a5 c1
03 00 01 00 20 02 24 01".ToHexBytes();
        }
    }
}
