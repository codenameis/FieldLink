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

namespace FieldLink.PlcDrivers.LSIS
{
    /// <summary>예약 영역을 통과하는 XGK/I/R 시리즈로 결정됩니다.</summary>
    public enum LSCpuInfo
    {
        /// <summary>XGK 프로토콜 값입니다.</summary>
        XGK = 1,
        /// <summary>XGI 프로토콜 값입니다.</summary>
        XGI,
        /// <summary>XGR 프로토콜 값입니다.</summary>
        XGR,
        /// <summary>XGB_MK 프로토콜 값입니다.</summary>
        XGB_MK,
        /// <summary>XGB_IEC 프로토콜 값입니다.</summary>
        XGB_IEC,
        /// <summary>XGB 프로토콜 값입니다.</summary>
        XGB
    }
}
