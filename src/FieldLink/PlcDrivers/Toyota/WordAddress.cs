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

namespace FieldLink.PlcDrivers.Toyota
{
    /// <summary>WordAddress 프로토콜 값입니다.</summary>
    public class WordAddress
    {
        /// <summary>Address 프로토콜 값입니다.</summary>
        public string Address { get; set; }
        /// <summary>BitIndex 프로토콜 값입니다.</summary>
        public int BitIndex { get; set; }
        /// <summary>WordLength 프로토콜 값입니다.</summary>
        public ushort WordLength { get; set; }
    }
}
