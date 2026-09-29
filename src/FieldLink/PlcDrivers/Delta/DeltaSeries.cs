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

namespace FieldLink.PlcDrivers.Delta
{
    /// <summary>Delta 시리즈</summary>
    public enum DeltaSeries
    {
        /// <summary>Delta의 DVP 시리즈, DVP-ES/EX/EC/SS 모델, DVP-SA/SC/SX/EH 모델에 적용</summary>
        Dvp,
        /// <summary>AS300 시리즈에 적용</summary>
        AS,
    }
}
