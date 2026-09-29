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

namespace FieldLink.PlcDrivers.AllenBradley
{
    /// <summary>AllenBradleyMicroCip 요청 프레임을 생성합니다.</summary>
    public static class AllenBradleyMicroCipCommandBuilder
    {
        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "portSlot">portSlot에 사용할 입력값입니다.</param>
        /// <param name = "cips">cips에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackCommandService(byte[] portSlot, params byte[][] cips) => AllenBradleyCommandBuilder.PackCleanCommandService(portSlot, cips);
    }
}
