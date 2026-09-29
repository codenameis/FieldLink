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

namespace FieldLink.PlcDrivers.Melsec
{
    /// <summary>MelsecA1EServer 요청 프레임을 생성합니다.</summary>
    public static class MelsecA1EServerCommandBuilder
    {
        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "mcCore">포장할 명령 바이트입니다.</param>
        /// <param name = "err">err에 사용할 입력값입니다.</param>
        /// <param name = "code">code에 사용할 입력값입니다.</param>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackResponseCommand(byte[] mcCore, byte err, byte code, byte[] data)
        {
            byte[] head = new byte[]
            {
                (byte)(mcCore[0] + 0x80),
                err
            };
            if (err != 0)
            {
                if (err == 0x5B)
                    return ProtocolBytes.SpliceArray(head, new byte[] { code });
                return head;
            }

            if (data == null)
                return head;
            return ProtocolBytes.SpliceArray(head, data);
        }

        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "mcCore">포장할 명령 바이트입니다.</param>
        /// <param name = "err">err에 사용할 입력값입니다.</param>
        /// <param name = "code">code에 사용할 입력값입니다.</param>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackResponseCommand(byte[] mcCore, byte err, byte code, bool[] data)
        {
            byte[] head = new byte[]
            {
                (byte)(mcCore[0] + 0x80),
                err
            };
            if (err != 0)
            {
                if (err == 0x5B)
                    return ProtocolBytes.SpliceArray(head, new byte[] { code });
                return head;
            }

            if (data == null)
                return head;
            return ProtocolBytes.SpliceArray(head, MelsecValueConverter.TransBoolArrayToByteData(data));
        }

        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "mcCore">포장할 명령 바이트입니다.</param>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackAsciiResponseCommand(byte[] mcCore, byte[] data)
        {
            byte[] head = new byte[]
            {
                (byte)(mcCore[0] + 0x08),
                mcCore[1],
                0x30,
                0x30
            };
            if (data == null)
                return head;
            return ProtocolBytes.SpliceArray(head, MelsecValueConverter.TransByteArrayToAsciiByteArray(data));
        }

        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "mcCore">포장할 명령 바이트입니다.</param>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackAsciiResponseCommand(byte[] mcCore, bool[] data)
        {
            byte[] head = new byte[]
            {
                (byte)(mcCore[0] + 0x08),
                mcCore[1],
                0x30,
                0x30
            };
            if (data == null)
                return head;
            if (data.Length % 2 == 1)
                data = ProtocolBytes.ArrayExpandToLength(data, data.Length + 1);
            return ProtocolBytes.SpliceArray(head, data.Select(m => m ? (byte)0x31 : (byte)0x30).ToArray());
        }
    }
}
