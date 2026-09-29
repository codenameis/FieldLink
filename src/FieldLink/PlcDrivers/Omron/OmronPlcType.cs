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

namespace FieldLink.PlcDrivers.Omron
{
    /// <summary>옴롱 PLC의 종류</summary>
    public enum OmronPlcType
    {
        /// <summary>CS/CJ 시리즈</summary>
        CSCJ = 1,
        /// <summary>CV 시리즈</summary>
        CV,
    }
}
