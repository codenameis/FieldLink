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

namespace FieldLink.PlcDrivers.Knx
{
    /// <summary>KnxDecodedMessage 프로토콜 값입니다.</summary>
    public sealed class KnxDecodedMessage
    {
        /// <summary>Channel 프로토콜 값입니다.</summary>
        public byte Channel { get; set; }
        /// <summary>IsConnected 프로토콜 값입니다.</summary>
        public bool IsConnected { get; set; }
        /// <summary>Address 프로토콜 값입니다.</summary>
        public short Address { get; set; }
        /// <summary>DataLength 프로토콜 값입니다.</summary>
        public byte DataLength { get; set; }
        /// <summary>Data 프로토콜 값입니다.</summary>
        public byte[] Data { get; set; }
        /// <summary>Reply 프로토콜 값입니다.</summary>
        public byte[] Reply { get; set; }
    }
}
