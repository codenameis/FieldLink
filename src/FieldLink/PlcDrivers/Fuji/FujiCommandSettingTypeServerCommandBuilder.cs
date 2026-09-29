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
using static FieldLink.PlcDrivers.Fuji.FujiCommandSettingTypeServerDefinitions;

namespace FieldLink.PlcDrivers.Fuji
{
    /// <summary>FujiCommandSettingTypeServer 요청 프레임을 생성합니다.</summary>
    public static class FujiCommandSettingTypeServerCommandBuilder
    {
        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "command">포장할 명령 바이트입니다.</param>
        /// <param name = "err">err에 사용할 입력값입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackResponseResult(byte[] command, byte err, byte[] value)
        {
            if (err > 0 || command[0] == 0x01)
            {
                byte[] back = new byte[9];
                Array.Copy(command, 0, back, 0, 9);
                back[1] = err;
                back[4] = 0x04;
                return back;
            }
            else
            {
                if (value == null)
                    value = new byte[0];
                byte[] back = new byte[10 + value.Length];
                Array.Copy(command, 0, back, 0, 9);
                back[4] = (byte)(0x05 + value.Length);
                value.CopyTo(back, 10);
                return back;
            }
        }
    }
}
