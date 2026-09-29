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
using static FieldLink.PlcDrivers.LSIS.LSCnetResponseParser;
using static FieldLink.PlcDrivers.LSIS.LSCnetAddressParser;
using static FieldLink.PlcDrivers.LSIS.LSCnetValueConverter;
using static FieldLink.PlcDrivers.LSIS.LSCnetCommandBuilder;

namespace FieldLink.PlcDrivers.LSIS
{
    /// <summary>LSCnet 프로토콜의 명령 및 데이터 타입 상수입니다.</summary>
    public static class LSCnetDefinitions
    {
        /// <summary>CnetTypes 프로토콜 값입니다.</summary>
        public const string CnetTypes = "PMLKFTCDSQINUZR";
    }
}
