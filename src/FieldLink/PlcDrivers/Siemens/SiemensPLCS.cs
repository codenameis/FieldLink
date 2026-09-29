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

namespace FieldLink.PlcDrivers.Siemens
{
    /// <summary>Siemens의 PLC 유형, 현재 지원되는 접근 유형</summary>
    public enum SiemensPLCS
    {
        /// <summary>1200 시리즈</summary>
        S1200 = 1,
        /// <summary>300 시리즈</summary>
        S300 = 2,
        /// <summary>400 시리즈</summary>
        S400 = 3,
        /// <summary>1500 시리즈 PLC</summary>
        S1500 = 4,
        /// <summary>200의 smart 시리즈</summary>
        S200Smart = 5,
        /// <summary>200 시스템, 추가로 이더넷 모듈을 구성해야 합니다</summary>
        S200 = 6
    }
}
