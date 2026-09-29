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

namespace FieldLink.PlcDrivers.Siemens
{
    /// <summary>MpiConfirmation 요청 프레임을 생성합니다.</summary>
    public static class MpiConfirmationCommandBuilder
    {
        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildReadTemplate() => new byte[]
        {
            0x68,
            0x08,
            0x08,
            0x68,
            0x82,
            0x80,
            0x5C,
            0x16,
            0x02,
            0xB0,
            0x07,
            0x00,
            0x2D,
            0x16,
            0xE5
        };
        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildWriteTemplate() => new byte[]
        {
            0x68,
            0x08,
            0x08,
            0x68,
            0x82,
            0x80,
            0x7C,
            0x16,
            0x02,
            0xB0,
            0x07,
            0x00,
            0x4D,
            0x16,
            0xE5
        };
    }
}
