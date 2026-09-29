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
    /// <summary>FxSerialSession 요청 프레임을 생성합니다.</summary>
    public static class FxSerialSessionCommandBuilder
    {
        /// <summary>PLC 활성화 또는 속도 변경 전에 보낼 ENQ 프레임입니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildEnquiry() => new byte[]
        {
            0x05
        };
        /// <summary>PLC의 전송 속도 변경 명령입니다. 원본과 같이 목록 외 속도는 115200 명령으로 처리합니다.</summary>
        /// <param name = "baudRate">baudRate에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildBaudRateChange(int baudRate) => baudRate == 19200 ? new byte[]
        {
            0x02,
            0x41,
            0x31,
            0x03,
            0x37,
            0x35
        }

        : baudRate == 38400 ? new byte[]
        {
            0x02,
            0x41,
            0x32,
            0x03,
            0x37,
            0x36
        }

        : baudRate == 57600 ? new byte[]
        {
            0x02,
            0x41,
            0x33,
            0x03,
            0x37,
            0x37
        }

        : baudRate == 115200 ? new byte[]
        {
            0x02,
            0x41,
            0x35,
            0x03,
            0x37,
            0x39
        }

        : new byte[]
        {
            0x02,
            0x41,
            0x35,
            0x03,
            0x37,
            0x39
        };
    }
}
