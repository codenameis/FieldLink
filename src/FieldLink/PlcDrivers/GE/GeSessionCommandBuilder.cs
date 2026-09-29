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

namespace FieldLink.PlcDrivers.GE
{
    /// <summary>GeSession 요청 프레임을 생성합니다.</summary>
    public static class GeSessionCommandBuilder
    {
        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildConnect() => new byte[56];
        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "id">id에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildReadTime(long id) => GeCommandBuilder.BuildReadCoreCommand(id, 0x25, new byte[] { 0, 0, 0, 2, 0 });
        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "id">id에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildReadProgramName(long id) => GeCommandBuilder.BuildReadCoreCommand(id, 1, new byte[] { 0, 0, 0, 2, 0 });
    }
}
