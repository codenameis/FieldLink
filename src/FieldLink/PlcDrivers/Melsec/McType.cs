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

namespace FieldLink.PlcDrivers.Melsec
{
    /// <summary>MC 프로토콜의 종류</summary>
    public enum McType
    {
        /// <summary>바이너리 기반 MC 프로토콜</summary>
        McBinary,
        /// <summary>ASCII 형식을 기반으로 한 MC 프로토콜</summary>
        MCAscii,
        /// <summary>R 시리즈 기반의 이진 MC 프로토콜</summary>
        McRBinary,
        /// <summary>R 시리즈 기반의 ASCII 형식의 MC 프로토콜</summary>
        McRAscii,
    }
}
