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
    /// <summary>OmronFinsUdpServer 요청 프레임을 생성합니다.</summary>
    public static class OmronFinsUdpServerCommandBuilder
    {
        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "status">status에 사용할 입력값입니다.</param>
        /// <param name = "finsCore">finsCore에 사용할 입력값입니다.</param>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackCommand(int status, byte[] finsCore, byte[] data)
        {
            // 그리고 이것은 finsudp와 finstcp가 다르기 때문에 fins의 전체 메시지를 포장하는 메서드를 다시 작성했습니다.
            if (data == null)
                data = new byte[0];
            byte[] back = new byte[14 + data.Length];
            ProtocolBytes.HexStringToBytes("C0 00 02 00 00 00 00 00 00 00 00 00 00 00").CopyTo(back, 0);
            if (data.Length > 0)
                data.CopyTo(back, 14);
            back[10] = finsCore[0];
            back[11] = finsCore[1];
            back[12] = BitConverter.GetBytes(status)[1];
            back[13] = BitConverter.GetBytes(status)[0];
            return back;
        }
    }
}
