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
using static FieldLink.PlcDrivers.LSIS.LSisServerValueConverter;
using static FieldLink.PlcDrivers.LSIS.LSisServerAddressParser;
using static FieldLink.PlcDrivers.LSIS.LSisServerDefinitions;

namespace FieldLink.PlcDrivers.LSIS
{
    /// <summary>LSisServer 요청 프레임을 생성합니다.</summary>
    public static class LSisServerCommandBuilder
    {
        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "receive">receive에 사용할 입력값입니다.</param>
        /// <param name = "err">err에 사용할 입력값입니다.</param>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackReadSerialResponse(CnetResponseOptions context, byte[] receive, short err, List<byte[]> data)
        {
            var result = new List<byte>(24);
            if (err == 0)
                result.Add(AsciiControl.ACK);
            else
                result.Add(AsciiControl.NAK);
            result.AddRange(ProtocolBytes.BuildAsciiBytesFrom((byte)context.station));
            result.Add(receive[3]); // command r / w
            result.Add(receive[4]); // 명령 타입: SB / SS
            result.Add(receive[5]);
            if (err == 0)
            {
                if (data != null)
                {
                    if (Encoding.ASCII.GetString(receive, 4, 2) == "SS")
                        result.AddRange(ProtocolBytes.BuildAsciiBytesFrom((byte)data.Count)); // 블록 수
                    else if (Encoding.ASCII.GetString(receive, 4, 2) == "SB")
                        result.AddRange(Encoding.ASCII.GetBytes("01"));
                    for (int i = 0; i < data.Count; i++)
                    {
                        result.AddRange(ProtocolBytes.BuildAsciiBytesFrom((byte)data[i].Length)); // 데이터 수
                        result.AddRange(ProtocolBytes.BytesToAsciiBytes(data[i])); // 데이터
                    }
                }
            }
            else
            {
                result.AddRange(ProtocolBytes.BuildAsciiBytesFrom(err));
            }

            result.Add(AsciiControl.ETX); // ETX
            int sum1 = 0;
            for (int i = 0; i < result.Count; i++)
            {
                sum1 += result[i];
            }

            result.AddRange(ProtocolBytes.BuildAsciiBytesFrom((byte)sum1));
            return result.ToArray();
        }
    }
}
