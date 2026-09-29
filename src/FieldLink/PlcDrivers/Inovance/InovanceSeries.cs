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

namespace FieldLink.PlcDrivers.Inovance
{
    /// <summary>Inovance 시리즈 열거형 정보</summary>
    public enum InovanceSeries
    {
        /// <summary>AM400, AM400_800, AC800 등에 적용</summary>
        AM,
        /// <summary>H3U, XP 등의 시리즈에 적용</summary>
        H3U,
        /// <summary>H5U 시리즈에 적용</summary>
        H5U,
    }
}
