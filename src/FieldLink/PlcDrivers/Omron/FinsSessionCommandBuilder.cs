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

namespace FieldLink.PlcDrivers.Omron
{
    /// <summary>FinsSession 요청 프레임을 생성합니다.</summary>
    public static class FinsSessionCommandBuilder
    {
        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildNodeAddressRequest() => new byte[]
        {
            0x46,
            0x49,
            0x4E,
            0x53, // FINS
            0x00,
            0x00,
            0x00,
            0x0C, // 다음 명령의 길이는
            0x00,
            0x00,
            0x00,
            0x00, // 명령 코드
            0x00,
            0x00,
            0x00,
            0x00, // 오류 코드
            0x00,
            0x00,
            0x00,
            0x00 // 노드 번호가 0이면 자동으로 가져옵니다.
        };
    }
}
