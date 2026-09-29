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
using static FieldLink.PlcDrivers.AllenBradley.AllenBradleyPcccServerDefinitions;

namespace FieldLink.PlcDrivers.AllenBradley
{
    /// <summary>AllenBradleyPcccServer 프로토콜의 값과 바이트 표현을 변환합니다.</summary>
    public static class AllenBradleyPcccServerValueConverter
    {
        /// <summary>GetDynamicLengthData 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "fccc">fccc에 사용할 입력값입니다.</param>
        /// <param name = "offset">offset에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static int GetDynamicLengthData(byte[] fccc, ref int offset)
        {
            int data = fccc[offset++];
            if (data == 0xFF)
            {
                data = BitConverter.ToUInt16(fccc, offset);
                offset += 2;
            }

            return data;
        }

        /// <summary>GetResponse 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "status">status에 사용할 입력값입니다.</param>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] GetResponse(PcccResponseOptions context, int status, byte[] data)
        {
            byte[] back = AllenBradleyCommandBuilder.PackRequestHeader(0x70, context.sessionID, AllenBradleyCommandBuilder.PackCommandSpecificData(AllenBradleyCommandBuilder.PackCommandSingleService("e8 a3 14 00".ToHexBytes(), 0xA1), AllenBradleyCommandBuilder.PackCommandSingleService(ProtocolBytes.SpliceArray("09 00 cb 00 00 00 07 09 10 0b 46 a5 c1 4f 00 08 00".ToHexBytes(), data), 0xB1)));
            context.ValueConverter.GetBytes(status).CopyTo(back, 8);
            return back;
        }
    }
}
