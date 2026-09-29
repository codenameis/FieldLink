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
    /// <summary>CPU 상태</summary>
    public enum LSCpuStatus
    {
        /// <summary>실행 중</summary>
        RUN = 1,
        /// <summary>운영 중단</summary>
        STOP,
        /// <summary>잘못된 상태</summary>
        ERROR,
        /// <summary>시작 모드</summary>
        DEBUG
    }
}
