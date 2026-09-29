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

namespace FieldLink.PlcDrivers.AllenBradley
{
    /// <summary>검증 방법</summary>
    public enum CheckType
    {
        /// <summary>합계 검사</summary>
        BCC,
        /// <summary>CRC 검증 방법</summary>
        CRC16,
    }
}
