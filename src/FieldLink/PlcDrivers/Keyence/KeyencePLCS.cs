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

namespace FieldLink.PlcDrivers.Keyence
{
    /// <summary>키너스 PLC의 다양한 제품군</summary>
    public enum KeyencePLCS
    {
        /// <summary>KV5500 시리즈</summary>
        KV5500,
        /// <summary>KV5000 시리즈</summary>
        KV5000,
        /// <summary>KV3000 시리즈</summary>
        KV3000,
        /// <summary>KV1000 시리즈</summary>
        KV1000,
        /// <summary>KV700 시리즈</summary>
        KV700
    }
}
