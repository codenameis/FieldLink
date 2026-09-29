using FieldLink.PlcDrivers.Modbus;
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

namespace FieldLink.PlcDrivers.Delta
{
    /// <summary>DeltaAS 주소를 프로토콜의 영역 코드와 위치로 변환합니다.</summary>
    public static class DeltaAsAddressParser
    {
        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static int ParseDeltaBitAddress(string address)
        {
            int bitIndex = address.IndexOf('.');
            if (bitIndex > 0)
                return Convert.ToInt32(address.Substring(0, bitIndex)) * 16 + AddressParameters.CalculateBitStartIndex(address.Substring(bitIndex + 1));
            else
                return Convert.ToInt32(address) * 16;
        }

        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "modbusCode">modbusCode에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<string> ParseDeltaASAddress(string address, byte modbusCode)
        {
            try
            {
                string station = string.Empty;
                OperationResult<int> stationPara = AddressParameters.ExtractParameter(ref address, "s");
                if (stationPara.IsSuccess)
                    station = $"s={stationPara.Content};";
                if (modbusCode == ModbusFrameRules.ReadCoil || modbusCode == ModbusFrameRules.WriteCoil || modbusCode == ModbusFrameRules.WriteOneCoil)
                {
                    if (address.StartsWith("SM") || address.StartsWith("sm"))
                        return OperationResult.CreateSuccessResult(station + (Convert.ToInt32(address.Substring(2)) + 0x4000).ToString());
                    else if (address.StartsWith("HC") || address.StartsWith("hc"))
                        return OperationResult.CreateSuccessResult(station + (Convert.ToInt32(address.Substring(2)) + 0xFC00).ToString());
                    else if (address.StartsWith("S") || address.StartsWith("s"))
                        return OperationResult.CreateSuccessResult(station + (Convert.ToInt32(address.Substring(1)) + 0x5000).ToString());
                    else if (address.StartsWith("X") || address.StartsWith("x"))
                        return OperationResult.CreateSuccessResult(station + "x=2;" + (ParseDeltaBitAddress(address.Substring(1)) + 0x6000).ToString());
                    else if (address.StartsWith("Y") || address.StartsWith("y"))
                        return OperationResult.CreateSuccessResult(station + (ParseDeltaBitAddress(address.Substring(1)) + 0xA000).ToString());
                    else if (address.StartsWith("T") || address.StartsWith("t"))
                        return OperationResult.CreateSuccessResult(station + (Convert.ToInt32(address.Substring(1)) + 0xE000).ToString());
                    else if (address.StartsWith("C") || address.StartsWith("c"))
                        return OperationResult.CreateSuccessResult(station + (Convert.ToInt32(address.Substring(1)) + 0xF000).ToString());
                    else if (address.StartsWith("M") || address.StartsWith("m"))
                        return OperationResult.CreateSuccessResult(station + (Convert.ToInt32(address.Substring(1)) + 0x0000).ToString());
                    else if (address.StartsWith("D") && address.Contains("."))
                        return OperationResult.CreateSuccessResult(station + address);
                }
                else
                {
                    if (address.StartsWith("SR") || address.StartsWith("sr"))
                        return OperationResult.CreateSuccessResult(station + (Convert.ToInt32(address.Substring(2)) + 0xC000).ToString());
                    else if (address.StartsWith("HC") || address.StartsWith("hc"))
                        return OperationResult.CreateSuccessResult(station + (Convert.ToInt32(address.Substring(2)) + 0xFC00).ToString());
                    else if (address.StartsWith("D") || address.StartsWith("d"))
                        return OperationResult.CreateSuccessResult(station + (Convert.ToInt32(address.Substring(1)) + 0x0000).ToString());
                    else if (address.StartsWith("X") || address.StartsWith("x"))
                        return OperationResult.CreateSuccessResult(station + "x=4;" + (Convert.ToInt32(address.Substring(1)) + 0x8000).ToString());
                    else if (address.StartsWith("Y") || address.StartsWith("y"))
                        return OperationResult.CreateSuccessResult(station + (Convert.ToInt32(address.Substring(1)) + 0xA000).ToString());
                    else if (address.StartsWith("C") || address.StartsWith("c"))
                        return OperationResult.CreateSuccessResult(station + (Convert.ToInt32(address.Substring(1)) + 0xF000).ToString());
                    else if (address.StartsWith("T") || address.StartsWith("t"))
                        return OperationResult.CreateSuccessResult(station + (Convert.ToInt32(address.Substring(1)) + 0xE000).ToString());
                    else if (address.StartsWith("E") || address.StartsWith("e"))
                        return OperationResult.CreateSuccessResult(station + (Convert.ToInt32(address.Substring(1)) + 0xFE00).ToString());
                }

                return new OperationResult<string>(ProtocolMessages.NotSupportedDataType);
            }
            catch (Exception ex)
            {
                return new OperationResult<string>(ex.Message);
            }
        }
    }
}
