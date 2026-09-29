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
using static FieldLink.PlcDrivers.Omron.OmronFinsNetCommandBuilder;
using static FieldLink.PlcDrivers.Omron.OmronFinsNetResponseParser;

namespace FieldLink.PlcDrivers.Omron
{
    /// <summary>FinsControl 요청 프레임을 생성합니다.</summary>
    public static class FinsControlCommandBuilder
    {
        /// <summary>운전 시작 명령 프레임을 생성합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildRun() => new byte[]
        {
            0x04,
            0x01,
            0xFF,
            0xFF,
            0x04
        };
        /// <summary>운전 정지 명령 프레임을 생성합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildStop() => new byte[]
        {
            0x04,
            0x02,
            0xFF,
            0xFF
        };
        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildReadCpuUnitData() => new byte[]
        {
            0x05,
            0x01,
            0x00
        };
        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildReadCpuUnitStatus() => new byte[]
        {
            0x06,
            0x01
        };
        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildReadCpuTime() => new byte[]
        {
            0x07,
            0x01
        };
    }
}
