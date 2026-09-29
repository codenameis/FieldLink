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
using static FieldLink.PlcDrivers.Toyota.ToyoPucServerValueConverter;

namespace FieldLink.PlcDrivers.Toyota
{
    /// <summary>ToyoPucServer 프로토콜의 명령 및 데이터 타입 상수입니다.</summary>
    public static class ToyoPucServerDefinitions
    {
        /// <summary>DataPoolLength 프로토콜 값입니다.</summary>
        public const int DataPoolLength = 65536; // 데이터 길이
    }
}
