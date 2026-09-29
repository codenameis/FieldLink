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
using static FieldLink.PlcDrivers.Siemens.SiemensFetchWriteServerDefinitions;

namespace FieldLink.PlcDrivers.Siemens
{
    /// <summary>SiemensFetchWriteServer 요청 프레임을 생성합니다.</summary>
    public static class SiemensFetchWriteServerCommandBuilder
    {
        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "opCode">opCode에 사용할 입력값입니다.</param>
        /// <param name = "err">err에 사용할 입력값입니다.</param>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackCommandResponse(byte opCode, byte err, byte[] data)
        {
            if (data == null)
                data = new byte[0];
            byte[] buffer = new byte[16 + data.Length];
            buffer[0] = 0x53;
            buffer[1] = 0x35;
            buffer[2] = 0x10;
            buffer[3] = 0x01;
            buffer[4] = 0x03;
            buffer[5] = opCode;
            buffer[6] = 0x0f;
            buffer[7] = 0x03;
            buffer[8] = err;
            buffer[9] = 0xff;
            buffer[10] = 0x07;
            if (data.Length > 0)
                data.CopyTo(buffer, 16);
            return buffer;
        }
    }
}
