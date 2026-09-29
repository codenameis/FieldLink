using FieldLink.PlcDrivers.AllenBradley;
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
using static FieldLink.PlcDrivers.Omron.OmronConnectedCipNetCommandBuilder;
using static FieldLink.PlcDrivers.Omron.OmronConnectedCipNetResponseParser;

namespace FieldLink.PlcDrivers.Omron
{
    /// <summary>OmronConnectedCipNet 프로토콜의 값과 바이트 표현을 변환합니다.</summary>
    public static class OmronConnectedCipNetValueConverter
    {
        /// <summary>GetMaxTransferBytes 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static int GetMaxTransferBytes() => 1988;
        /// <summary>GetLengthFromRemain 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "dataType">dataType에 사용할 입력값입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static int GetLengthFromRemain(ushort dataType, int length)
        {
            if (dataType == AllenBradleyDefinitions.CIP_Type_Bool || dataType == AllenBradleyDefinitions.CIP_Type_Byte || dataType == AllenBradleyDefinitions.CIP_Type_USInt || dataType == AllenBradleyDefinitions.CIP_Type_BitArray)
            {
                return Math.Min(length, GetMaxTransferBytes());
            }
            else if (dataType == AllenBradleyDefinitions.CIP_Type_UInt || dataType == AllenBradleyDefinitions.CIP_Type_Word)
            {
                return Math.Min(length, GetMaxTransferBytes() / 2);
            }
            else if (dataType == AllenBradleyDefinitions.CIP_Type_DWord || dataType == AllenBradleyDefinitions.CIP_Type_UDint || dataType == AllenBradleyDefinitions.CIP_Type_Real)
            {
                return Math.Min(length, GetMaxTransferBytes() / 4);
            }
            else
            {
                return Math.Min(length, GetMaxTransferBytes() / 8);
            }
        }

        //private long oTConnectionId = 0x80000001;
        /// <summary>GetLargeForwardOpen 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "connectionID">connectionID에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] GetLargeForwardOpen(OmronConnectedCipOptions context, ushort connectionID)
        {
            uint tOConnectionId = 0x80fe0001 + connectionID;
            byte[] buffer = @"
00 00 00 00 00 00 02 00 00 00 00 00 b2 00 34 00
5b 02 20 06 24 01 0e 9c 02 00 00 80 01 00 fe 80
02 00 1b 05 30 a7 2b 03 02 00 00 00 80 84 1e 00
cc 07 00 42 80 84 1e 00 cc 07 00 42 a3 03 20 02
24 01 2c 01".ToHexBytes();
            BitConverter.GetBytes((uint)(0x80000002 + connectionID)).CopyTo(buffer, 24); // O->T 네트워크 연결 ID
            BitConverter.GetBytes(tOConnectionId).CopyTo(buffer, 28); // T->O 네트워크 연결 ID
            BitConverter.GetBytes((ushort)(2 + connectionID)).CopyTo(buffer, 32); // 연결 시리얼 번호
            BitConverter.GetBytes(AllenBradleyDefinitions.OriginatorVendorID).CopyTo(buffer, 34); // 발원자 공급자 ID
            context.OriginatorSerialNumber.CopyTo(buffer, 36); // 발원자 시리얼 번호
            buffer[40] = context.ConnectionTimeoutMultiplier;
            return buffer;
        }
    }
}
