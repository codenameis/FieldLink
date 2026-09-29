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
using static FieldLink.PlcDrivers.LSIS.LSCpuResponseParser;
using static FieldLink.PlcDrivers.LSIS.LSCpuValueConverter;
using static FieldLink.PlcDrivers.LSIS.LSCpuAddressParser;
using static FieldLink.PlcDrivers.LSIS.LSCpuCommandBuilder;

namespace FieldLink.PlcDrivers.LSIS
{
    /// <summary>LSCpu 프로토콜의 명령 및 데이터 타입 상수입니다.</summary>
    public static class LSCpuDefinitions
    {
        /// <summary>CpuTypes 프로토콜 값입니다.</summary>
        public const string CpuTypes = "PMLKFTCDSQINUZR";
    }
}
