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
    /// <summary>DeltaDvp 주소를 프로토콜의 영역 코드와 위치로 변환합니다.</summary>
    public static class DeltaDvpAddressParser
    {
        /// <summary>TransDAdressToModbusAddress 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static int TransDAdressToModbusAddress(string address)
        {
            int add = Convert.ToInt32(address);
            if (add >= 4096)
                return add - 4096 + 0x9000;
            else
                return add + 0x1000;
        }

        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "modbusCode">modbusCode에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<string> ParseDeltaDvpAddress(string address, byte modbusCode)
        {
            try
            {
                string station = string.Empty;
                OperationResult<int> stationPara = AddressParameters.ExtractParameter(ref address, "s");
                if (stationPara.IsSuccess)
                    station = $"s={stationPara.Content};";
                if (modbusCode == ModbusFrameRules.ReadCoil || modbusCode == ModbusFrameRules.WriteCoil || modbusCode == ModbusFrameRules.WriteOneCoil)
                {
                    if (address.StartsWithAndNumber("S"))
                        return OperationResult.CreateSuccessResult(station + Convert.ToInt32(address.Substring(1)).ToString());
                    else if (address.StartsWithAndNumber("X"))
                        return OperationResult.CreateSuccessResult(station + "x=2;" + (Convert.ToInt32(address.Substring(1), 8) + 0x400).ToString());
                    else if (address.StartsWithAndNumber("Y"))
                        return OperationResult.CreateSuccessResult(station + (Convert.ToInt32(address.Substring(1), 8) + 0x500).ToString());
                    else if (address.StartsWithAndNumber("T"))
                        return OperationResult.CreateSuccessResult(station + (Convert.ToInt32(address.Substring(1)) + 0x600).ToString());
                    else if (address.StartsWithAndNumber("C"))
                        return OperationResult.CreateSuccessResult(station + (Convert.ToInt32(address.Substring(1)) + 0xE00).ToString());
                    else if (address.StartsWithAndNumber("M"))
                    {
                        int add = Convert.ToInt32(address.Substring(1));
                        if (add >= 1536)
                            return OperationResult.CreateSuccessResult(station + (add - 1536 + 0xB000).ToString());
                        else
                            return OperationResult.CreateSuccessResult(station + (add + 0x800).ToString());
                    }

                    if (ModbusAddressMapping.TransPointAddressToModbus(station, address, new string[] { "D" }, new int[] { 0 }, TransDAdressToModbusAddress, out string dAddress))
                        return OperationResult.CreateSuccessResult(dAddress);
                }
                else
                {
                    if (address.StartsWithAndNumber("D"))
                    {
                        return OperationResult.CreateSuccessResult(station + TransDAdressToModbusAddress(address.Substring(1)));
                    }
                    else if (address.StartsWithAndNumber("C"))
                    {
                        int add = Convert.ToInt32(address.Substring(1));
                        if (add >= 200)
                            return OperationResult.CreateSuccessResult(station + (add - 200 + 0x0EC8).ToString());
                        else
                            return OperationResult.CreateSuccessResult(station + (add + 0x0E00).ToString());
                    }
                    else if (address.StartsWithAndNumber("T"))
                        return OperationResult.CreateSuccessResult(station + (Convert.ToInt32(address.Substring(1)) + 0x600).ToString());
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
