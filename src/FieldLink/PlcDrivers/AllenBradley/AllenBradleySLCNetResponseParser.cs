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
using static FieldLink.PlcDrivers.AllenBradley.AllenBradleySLCNetCommandBuilder;

namespace FieldLink.PlcDrivers.AllenBradley
{
    /// <summary>AllenBradleySLCNet 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class AllenBradleySLCNetResponseParser
    {
        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "bitIndex">bitIndex에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string AnalysisBitIndex(string address, out int bitIndex)
        {
            bitIndex = 0;
            int index = address.IndexOf('/');
            if (index < 0)
                index = address.IndexOf('.');
            if (index > 0)
            {
                bitIndex = int.Parse(address.Substring(index + 1));
                address = address.Substring(0, index);
            }

            return address;
        }

        /// <summary>프로토콜 응답에서 데이터와 상태 정보를 추출합니다.</summary>
        /// <param name = "content">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> ExtraActualContent(byte[] content)
        {
            if (content.Length < 36)
            {
                return new OperationResult<byte[]>(ProtocolMessages.ReceiveDataLengthTooShort + content.ToHexString(' '));
            }
            else
            {
                return OperationResult.CreateSuccessResult(content.RemoveBegin(36));
            }
        }
    }
}
