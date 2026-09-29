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
    /// <summary>PLC 문자열의 길이 헤더, 바이트 순서 및 패딩을 처리합니다.</summary>
    public static class OmronUnconnectedStringCodec
    {
        /// <summary>비연결형 CIP 문자열에 짝수 길이 패딩을 적용하고, 패딩을 포함한 길이를 헤더에 기록합니다.</summary>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "encoding">문자열 변환에 사용할 인코딩입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] Build(string value, Encoding encoding)
        {
            byte[] data = ProtocolBytes.SpliceArray(new byte[2], ProtocolBytes.ArrayExpandToLengthEven(encoding.GetBytes(value ?? string.Empty)));
            data[0] = BitConverter.GetBytes(data.Length - 2)[0];
            data[1] = BitConverter.GetBytes(data.Length - 2)[1];
            return data;
        }

        /// <summary>2바이트 길이 헤더에 지정된 만큼 CIP 문자열을 읽습니다.</summary>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "encoding">문자열 변환에 사용할 인코딩입니다.</param>
        /// <param name = "transform">프로토콜 값에 적용할 바이트 순서 변환입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<string> Parse(byte[] data, Encoding encoding, IProtocolValueConverter transform)
        {
            try
            {
                return OperationResult.CreateSuccessResult(encoding.GetString(data, 2, transform.ReadUInt16(data, 0)));
            }
            catch (Exception ex)
            {
                return new OperationResult<string>("파싱 실패: " + ex.Message + Environment.NewLine + "원본: " + data.ToHexString(' '));
            }
        }
    }
}
