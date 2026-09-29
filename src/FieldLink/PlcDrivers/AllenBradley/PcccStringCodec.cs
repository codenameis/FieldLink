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
    /// <summary>PLC 문자열의 길이 헤더, 바이트 순서 및 패딩을 처리합니다.</summary>
    public static class PcccStringCodec
    {
        /// <summary>ST 문자열 헤더를 읽은 뒤 필요한 바이트 수를 계산합니다.</summary>
        /// <param name = "characters">characters에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static ushort GetReadLength(ushort characters) => (ushort)(characters % 2 != 0 ? characters + 3 : characters + 2);
        /// <summary>ST 문자열의 little-endian 길이와 워드 내 문자 순서를 해석합니다.</summary>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "encoding">문자열 변환에 사용할 인코딩입니다.</param>
        /// <param name = "limitToReceivedLength">문자열 길이를 실제 수신한 바이트 수로 제한하면 true입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string Parse(byte[] data, Encoding encoding, bool limitToReceivedLength)
        {
            int length = BitConverter.ToUInt16(data, 0);
            if (limitToReceivedLength && length + 2 > data.Length)
                length = data.Length - 2;
            return encoding.GetString(ProtocolBytes.BytesReverseByWord(data), 2, length);
        }

        /// <summary>ST 문자열에 2바이트 길이와 짝수 바이트 패딩을 적용합니다.</summary>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "encoding">문자열 변환에 사용할 인코딩입니다.</param>
        /// <param name = "transform">프로토콜 값에 적용할 바이트 순서 변환입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] Build(string value, Encoding encoding, IProtocolValueConverter transform)
        {
            byte[] data = transform.GetBytes(value, encoding);
            int length = data.Length;
            data = ProtocolBytes.ArrayExpandToLengthEven(data);
            return ProtocolBytes.SpliceArray(new byte[] { BitConverter.GetBytes(length)[0], BitConverter.GetBytes(length)[1] }, ProtocolBytes.BytesReverseByWord(data));
        }
    }
}
