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

namespace FieldLink.PlcDrivers.Sick
{
    /// <summary>SickIcrTcpServer 프로토콜의 값과 바이트 표현을 변환합니다.</summary>
    public static class SickIcrTcpServerValueConverter
    {
        /// <summary>TranslateCode 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "code">code에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string TranslateCode(string code)
        {
            StringBuilder temp = new StringBuilder("");
            for (int i = 0; i < code.Length; i++)
            {
                if (char.IsLetterOrDigit(code, i))
                {
                    temp.Append(code[i]);
                }
            }

            return temp.ToString();
        }
    }
}
