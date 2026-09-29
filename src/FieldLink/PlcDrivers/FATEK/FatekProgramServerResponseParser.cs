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
using static FieldLink.PlcDrivers.FATEK.FatekProgramServerCommandBuilder;
using static FieldLink.PlcDrivers.FATEK.FatekProgramServerDefinitions;

namespace FieldLink.PlcDrivers.FATEK
{
    /// <summary>FatekProgramServer 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class FatekProgramServerResponseParser
    {
        /// <summary>입력 데이터가 프로토콜의 검증 조건을 만족하는지 확인합니다.</summary>
        /// <param name = "buffer">프레임 데이터가 담긴 버퍼입니다.</param>
        /// <param name = "receivedLength">receivedLength에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static bool CheckSerialReceiveDataComplete(byte[] buffer, int receivedLength)
        {
            if (receivedLength < 5)
                return false;
            return buffer[receivedLength - 1] == AsciiControl.ETX;
        }
    }
}
