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

namespace FieldLink.PlcDrivers.Toledo
{
    /// <summary>ToledoFrameParser 프로토콜 값입니다.</summary>
    public static class ToledoFrameParser
    {
        /// <summary>CR 종결 위치와 체크 바이트 유무로 연속 출력 프레임의 완성을 확인합니다.</summary>
        /// <param name = "buffer">프레임 데이터가 담긴 버퍼입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <param name = "hasCheckByte">hasCheckByte에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static bool IsComplete(byte[] buffer, int length, bool hasCheckByte)
        {
            if (length <= 15)
                return false;
            if (hasCheckByte)
                return buffer[length - 2] == AsciiControl.CR;
            return buffer[length - 1] == AsciiControl.CR || buffer[length - 2] == AsciiControl.CR;
        }
    }
}
