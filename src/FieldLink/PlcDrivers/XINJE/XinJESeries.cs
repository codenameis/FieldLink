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

namespace FieldLink.PlcDrivers.XINJE
{
    /// <summary>Xinje PLC의 다양한 시리즈의 개수</summary>
    public enum XinJESeries
    {
        /// <summary>XC 시리즈</summary>
        XC,
        /// <summary>XD 시리즈</summary>
        XD,
        /// <summary>XL 시리즈</summary>
        XL,
    }
}
