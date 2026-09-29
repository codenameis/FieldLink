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
    /// <summary>PpiControl 데이터의 프로토콜 표현을 생성하고 해석합니다.</summary>
    public static class PpiControlCodec
    {
        /// <summary>운전 시작 명령 프레임을 생성합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildStart(byte station) => new byte[]
        {
            0x68,
            0x21,
            0x21,
            0x68,
            station,
            0x00,
            0x6C,
            0x32,
            0x01,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x14,
            0x00,
            0x00,
            0x28,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0xFD,
            0x00,
            0x00,
            0x09,
            0x50,
            0x5F,
            0x50,
            0x52,
            0x4F,
            0x47,
            0x52,
            0x41,
            0x4D,
            0xAA,
            0x16
        };
        /// <summary>운전 정지 명령 프레임을 생성합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildStop(byte station) => new byte[]
        {
            0x68,
            0x1D,
            0x1D,
            0x68,
            station,
            0x00,
            0x6C,
            0x32,
            0x01,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x10,
            0x00,
            0x00,
            0x29,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x09,
            0x50,
            0x5F,
            0x50,
            0x52,
            0x4F,
            0x47,
            0x52,
            0x41,
            0x4D,
            0xAA,
            0x16
        };
        /// <summary>입력 데이터가 프로토콜의 검증 조건을 만족하는지 확인합니다.</summary>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult CheckAcknowledgement(byte[] response) => response != null && response.Length > 0 && response[0] == 0xe5 ? OperationResult.CreateSuccessResult() : new OperationResult("PLC 확인 응답이 E5가 아닙니다.");
    }
}
