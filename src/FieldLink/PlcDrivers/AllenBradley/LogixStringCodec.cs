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
    public static class LogixStringCodec
    {
        /// <summary>연결형 CIP STRING 구조체에서 타입 헤더와 32비트 길이를 해석합니다.</summary>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "encoding">문자열 변환에 사용할 인코딩입니다.</param>
        /// <param name = "transform">프로토콜 값에 적용할 바이트 순서 변환입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<string> Parse(byte[] data, Encoding encoding, IProtocolValueConverter transform)
        {
            try
            {
                return OperationResult.CreateSuccessResult(data.Length >= 6 ? encoding.GetString(data, 6, transform.ReadInt32(data, 2)) : encoding.GetString(data));
            }
            catch (Exception ex)
            {
                return new OperationResult<string>(ex.Message + " 원본: " + data.ToHexString(' '));
            }
        }

        /// <summary>.LEN 필드와 .DATA[0] 필드에 순서대로 쓸 데이터를 만듭니다.</summary>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "encoding">문자열 변환에 사용할 인코딩입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <remarks>첫 번째 결과는 .LEN의 4바이트 값, 두 번째는 짝수 길이로 확장한 문자 바이트입니다. 실제 문자 길이는 첫 번째 결과에 있습니다.</remarks>
        public static OperationResult<byte[], byte[]> Build(string value, Encoding encoding)
        {
            byte[] data = encoding.GetBytes(value ?? string.Empty);
            return OperationResult.CreateSuccessResult(BitConverter.GetBytes(data.Length), ProtocolBytes.ArrayExpandToLengthEven(data));
        }
    }
}
