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
using static FieldLink.PlcDrivers.Toyota.ToyoPucResponseParser;
using static FieldLink.PlcDrivers.Toyota.ToyoPucCommandBuilder;

namespace FieldLink.PlcDrivers.Toyota
{
    /// <summary>ToyoPuc 주소를 프로토콜의 영역 코드와 위치로 변환합니다.</summary>
    public static class ToyoPucAddressParser
    {
        /// <summary>프로토콜 응답에서 데이터와 상태 정보를 추출합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static OperationResult<WordAddress> ExtraWordAddress(string address, ushort length)
        {
            try
            {
                int bitIndex = 0;
                int index = address.IndexOf('.');
                if (index > 0)
                {
                    bitIndex = Convert.ToInt32(address.Substring(index + 1), 16);
                    address = address.Substring(0, index);
                }
                else
                {
                    // 주소만 입력할 수 있습니다.
                    bitIndex = Convert.ToInt32(address.Substring(address.Length - 1), 16);
                    address = address.Substring(0, address.Length - 1);
                    if (address.StartsWith("EK", StringComparison.OrdinalIgnoreCase) || address.StartsWith("EV", StringComparison.OrdinalIgnoreCase) || address.StartsWith("ET", StringComparison.OrdinalIgnoreCase) || address.StartsWith("EC", StringComparison.OrdinalIgnoreCase) || address.StartsWith("EL", StringComparison.OrdinalIgnoreCase) || address.StartsWith("EX", StringComparison.OrdinalIgnoreCase) || address.StartsWith("EY", StringComparison.OrdinalIgnoreCase) || address.StartsWith("EM", StringComparison.OrdinalIgnoreCase))
                    {
                        if (address.Length == 2)
                            address = address + "0";
                    }
                    else
                    {
                        if (address.Length == 1)
                            address = address + "0";
                    }
                }

                int readLength = (bitIndex + length + 15) / 16;
                return OperationResult.CreateSuccessResult(new WordAddress() { Address = address, BitIndex = bitIndex, WordLength = (ushort)readLength });
            }
            catch (Exception ex)
            {
                return new OperationResult<WordAddress>($"ExtraWordAddress failed: {ex.Message}");
            }
        }
    }
}
