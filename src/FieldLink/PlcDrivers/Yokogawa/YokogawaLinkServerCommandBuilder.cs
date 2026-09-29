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
using static FieldLink.PlcDrivers.Yokogawa.YokogawaLinkServerDefinitions;

namespace FieldLink.PlcDrivers.Yokogawa
{
    /// <summary>YokogawaLinkServer 요청 프레임을 생성합니다.</summary>
    public static class YokogawaLinkServerCommandBuilder
    {
        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "cmd">포장할 명령 바이트입니다.</param>
        /// <param name = "err">err에 사용할 입력값입니다.</param>
        /// <param name = "result">result에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackCommandBack(byte cmd, byte err, byte[] result)
        {
            if (result == null)
                result = new byte[0];
            byte[] back = new byte[4 + result.Length];
            back[0] = (byte)(cmd + 0x80);
            back[1] = err;
            back[2] = BitConverter.GetBytes(result.Length)[1];
            back[3] = BitConverter.GetBytes(result.Length)[0];
            result.CopyTo(back, 4);
            return back;
        }
    }
}
