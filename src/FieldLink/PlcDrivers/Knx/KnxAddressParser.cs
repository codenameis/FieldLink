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

namespace FieldLink.PlcDrivers.Knx
{
    /// <summary>Knx 주소를 프로토콜의 영역 코드와 위치로 변환합니다.</summary>
    public static class KnxAddressParser
    {
        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "addr">addr에 사용할 입력값입니다.</param>
        /// <param name = "is_ok">is_ok에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static short ParseGroupAddress(string addr, out bool is_ok)
        {
            short out_addr = 0;
            var x = addr.Split('\\');
            if (x.Length == 3)
            {
                int H = int.Parse(x[0]);
                int M = int.Parse(x[1]);
                int L = int.Parse(x[2]);
                if ((H > 31 || M > 7 || L > 255) || (H < 0 || M < 0 || L < 0))
                {
                    is_ok = false;
                    return out_addr;
                }
                else
                {
                    H = H << 11;
                    M = M << 8;
                    var y = H | M | L;
                    out_addr = (short)y;
                    //
                    is_ok = true;
                    return out_addr;
                }
            }
            else
            {
                is_ok = false;
                return out_addr;
            }
        }
    }
}
