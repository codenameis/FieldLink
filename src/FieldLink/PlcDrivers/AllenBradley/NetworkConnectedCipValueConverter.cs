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
using static FieldLink.PlcDrivers.AllenBradley.ConnectedCipResponseParser;
using static FieldLink.PlcDrivers.AllenBradley.NetworkConnectedCipCommandBuilder;

namespace FieldLink.PlcDrivers.AllenBradley
{
    /// <summary>NetworkConnectedCip 프로토콜의 값과 바이트 표현을 변환합니다.</summary>
    public static class NetworkConnectedCipValueConverter
    {
        /// <summary>데이터 통신을 얻기 위한 전면 켜기 명령은, 다른 PLC의 정보는 다르죠.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "connectionID">연결 ID</param>
        /// <returns>원본 명령 데이터</returns>
        public static byte[] GetLargeForwardOpen(ConnectedCipOptions context, ushort connectionID)
        {
            return @"
00 00 00 00 00 00 02 00 00 00 00 00 b2 00 34 00
5b 02 20 06 24 01 0e 9c 02 00 00 80 01 00 fe 80
02 00 1b 05 30 a7 2b 03 02 00 00 00 80 84 1e 00
cc 07 00 42 80 84 1e 00 cc 07 00 42 a3 03 20 02
24 01 2c 01".ToHexBytes();
        }

        /// <summary>GetOTConnectionIdService 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] GetOTConnectionIdService(ConnectedCipOptions context)
        {
            byte[] buffer = new byte[8];
            buffer[0] = 0xA1; // 연결된 주소 항목
            buffer[1] = 0x00;
            buffer[2] = 0x04; // 길이
            buffer[3] = 0x00;
            context.ValueConverter.GetBytes(context.OTConnectionId).CopyTo(buffer, 4);
            return buffer;
        }
    }
}
