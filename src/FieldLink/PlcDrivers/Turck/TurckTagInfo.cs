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

namespace FieldLink.PlcDrivers.Turck
{
    /// <summary>TurckTagInfo 프로토콜 값입니다.</summary>
    public sealed class TurckTagInfo
    {
        /// <summary>successfullyInitialized 프로토콜 값입니다.</summary>
        public bool successfullyInitialized { get; set; }
        /// <summary>UID 프로토콜 값입니다.</summary>
        public string UID { get; set; }
        /// <summary>NumberOfBlock 프로토콜 값입니다.</summary>
        public byte NumberOfBlock { get; set; }
        /// <summary>BytesOfBlock 프로토콜 값입니다.</summary>
        public byte BytesOfBlock { get; set; }
    }
}
