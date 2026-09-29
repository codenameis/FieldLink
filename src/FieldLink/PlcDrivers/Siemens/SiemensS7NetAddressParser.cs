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
using static FieldLink.PlcDrivers.Siemens.SiemensS7NetCommandBuilder;
using static FieldLink.PlcDrivers.Siemens.SiemensS7NetResponseParser;
using static FieldLink.PlcDrivers.Siemens.SiemensS7NetDefinitions;

namespace FieldLink.PlcDrivers.Siemens
{
    /// <summary>SiemensS7Net 주소를 프로토콜의 영역 코드와 위치로 변환합니다.</summary>
    public static class SiemensS7NetAddressParser
    {
        /// <summary>WriteS7AddressToStream 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "ms">ms에 사용할 입력값입니다.</param>
        /// <param name = "add">add에 사용할 입력값입니다.</param>
        /// <param name = "writeType">writeType에 사용할 입력값입니다.</param>
        /// <param name = "dataLen">dataLen에 사용할 입력값입니다.</param>
        internal static void WriteS7AddressToStream(MemoryStream ms, S7DeviceAddress add, byte writeType, int dataLen)
        {
            ms.WriteByte(0x12);
            ms.WriteByte(0x0A);
            ms.WriteByte(0x10);
            if (add.DataCode == 0x1C || add.DataCode == 0x1D)
            {
                // C/T 주소는 항목 인덱스이며 요청의 수량은 2바이트 값의 개수다.
                ms.WriteByte(add.DataCode);
                ms.WriteByte((byte)((dataLen / 2) >> 8));
                ms.WriteByte((byte)(dataLen / 2));
            }
            else if (add.DataCode == 0x06 || add.DataCode == 0x07)
            {
                ms.WriteByte(0x04); // 쓰기 방식, 1은 비트별로, 2는 워드별로 -> Write mode, 1 is bitwise, 2 is by byte, 4 is by word
                ms.WriteByte(BitConverter.GetBytes(dataLen / 2)[1]);
                ms.WriteByte(BitConverter.GetBytes(dataLen / 2)[0]);
            }
            else
            {
                ms.WriteByte(writeType); // 쓰기 방식, 1은 비트별로, 2는 워드별로 -> Write mode, 1 is bitwise, 2 is by byte, 4 is by word
                ms.WriteByte(BitConverter.GetBytes(dataLen)[1]);
                ms.WriteByte(BitConverter.GetBytes(dataLen)[0]);
            }

            ms.WriteByte(BitConverter.GetBytes(add.DbBlock)[1]);
            ms.WriteByte(BitConverter.GetBytes(add.DbBlock)[0]);
            ms.WriteByte(add.DataCode);
            ms.WriteByte(BitConverter.GetBytes(add.AddressStart)[2]);
            ms.WriteByte(BitConverter.GetBytes(add.AddressStart)[1]);
            ms.WriteByte(BitConverter.GetBytes(add.AddressStart)[0]);
        }
    }
}
