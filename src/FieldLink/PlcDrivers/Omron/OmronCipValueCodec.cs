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
    /// <summary>OmronCipValue 데이터의 프로토콜 표현을 생성하고 해석합니다.</summary>
    public static class OmronCipValueCodec
    {
        /// <summary>연결형 CIP 문자열에 패딩 없는 2바이트 길이 헤더를 붙입니다.</summary>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "encoding">문자열 변환에 사용할 인코딩입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildConnectedString(string value, Encoding encoding)
        {
            byte[] buffer = string.IsNullOrEmpty(value) ? new byte[0] : encoding.GetBytes(value);
            return ProtocolBytes.SpliceArray(BitConverter.GetBytes((ushort)buffer.Length), buffer);
        }

        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildBoolean(bool value) => value ? new byte[]
        {
            0xff,
            0xff
        }

        : new byte[]
        {
            0,
            0
        };
        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <param name = "values">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildBooleanArray(bool[] values) => values.Select(value => value ? (byte)1 : (byte)0).ToArray();
        /// <summary>GetBooleanElementCount 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "values">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static int GetBooleanElementCount(string address, bool[] values) => AddressParameters.IsAddressEndWithIndex(address) ? values.Length : 1;
    }
}
