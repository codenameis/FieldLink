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
using static FieldLink.PlcDrivers.Vigor.VigorServerDefinitions;

namespace FieldLink.PlcDrivers.Vigor
{
    /// <summary>VigorServer 프로토콜의 값과 바이트 표현을 변환합니다.</summary>
    public static class VigorServerValueConverter
    {
        /// <summary>CreateResponseBack 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "request">request에 사용할 입력값입니다.</param>
        /// <param name = "err">err에 사용할 입력값입니다.</param>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] CreateResponseBack(byte[] request, byte err, byte[] data)
        {
            if (data == null)
                data = new byte[0];
            byte[] command = new byte[4 + data.Length];
            command[0] = request[2];
            command[1] = BitConverter.GetBytes(1 + data.Length)[0];
            command[2] = BitConverter.GetBytes(1 + data.Length)[1];
            command[3] = err;
            if (data.Length > 0)
                data.CopyTo(command, 4);
            return VigorVsCommandBuilder.PackCommand(command, 0x06);
        }
    }
}
