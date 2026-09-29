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

namespace FieldLink.PlcDrivers.Melsec
{
    /// <summary>Mc 프로토콜의 값과 바이트 표현을 변환합니다.</summary>
    public static class McValueConverter
    {
        /// <summary>GetReadWordLength 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "type">type에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static int GetReadWordLength(McType type)
        {
            if (type == McType.McBinary || type == McType.McRBinary)
                return 950;
            return 460;
        }

        /// <summary>GetReadBoolLength 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "type">type에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static int GetReadBoolLength(McType type)
        {
            if (type == McType.McBinary || type == McType.McRBinary)
                return 7168;
            return 3584;
        }
    }
}
