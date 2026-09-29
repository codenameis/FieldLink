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
using static FieldLink.PlcDrivers.Omron.OmronFinsServerDefinitions;

namespace FieldLink.PlcDrivers.Omron
{
    /// <summary>OmronFinsServer 요청 프레임을 생성합니다.</summary>
    public static class OmronFinsServerCommandBuilder
    {
        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "status">status에 사용할 입력값입니다.</param>
        /// <param name = "finsCore">finsCore에 사용할 입력값입니다.</param>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackCommand(int status, byte[] finsCore, byte[] data)
        {
            if (data == null)
                data = new byte[0];
            byte[] back = new byte[30 + data.Length];
            ProtocolBytes.HexStringToBytes("46 49 4E 53 00 00 00 00" + "00 00 00 02 00 00 00 00" + "C0 00 02 00 EF 00 00 33 00 00 00 00 00 00").CopyTo(back, 0);
            if (data.Length > 0)
                data.CopyTo(back, 30);
            back[25] = back[26] = finsCore[0];
            back[27] = finsCore[1];
            BitConverter.GetBytes(back.Length - 8).ReverseNew().CopyTo(back, 4);
            BitConverter.GetBytes(status).ReverseNew().CopyTo(back, 12);
            return back;
        }
    }
}
