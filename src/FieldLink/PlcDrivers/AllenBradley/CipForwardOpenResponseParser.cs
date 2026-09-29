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
    /// <summary>CipForwardOpen 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class CipForwardOpenResponseParser
    {
        /// <summary>Forward Open 응답에서 확장 상태를 확인하고 O→T 연결 식별자를 읽습니다.</summary>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <param name = "transform">프로토콜 값에 적용할 바이트 순서 변환입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <remarks>재시도 횟수와 중복 연결 처리 정책은 호출자가 결정합니다.</remarks>
        public static OperationResult<uint> Parse(byte[] response, IProtocolValueConverter transform)
        {
            try
            {
                if (response.Length >= 46 && response[42] != 0)
                {
                    ushort error = transform.ReadUInt16(response, 44);
                    if (error == 0x100)
                        return new OperationResult<uint>("연결이 사용 중이거나 Forward Open 요청이 중복되었습니다.");
                    if (error == 0x113)
                        return new OperationResult<uint>("확장 상태: 사용 가능한 연결이 없습니다(0x0113).");
                    return new OperationResult<uint>("Forward Open 실패, 코드: " + error);
                }

                return OperationResult.CreateSuccessResult(transform.ReadUInt32(response, 44));
            }
            catch (Exception ex)
            {
                return new OperationResult<uint>(ex.Message + Environment.NewLine + "원본: " + response.ToHexString(' '));
            }
        }
    }
}
