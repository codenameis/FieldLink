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

namespace FieldLink.PlcDrivers.Inovance
{
    /// <summary>Inovance 주소를 프로토콜의 영역 코드와 위치로 변환합니다.</summary>
    public static class InovanceAddressParser
    {
        /// <summary>CalculateStartAddress 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static int CalculateStartAddress(string address)
        {
            if (address.IndexOf('.') < 0)
                return int.Parse(address);
            else
            {
                string[] splits = address.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
                return int.Parse(splits[0]) * 8 + int.Parse(splits[1]);
            }
        }

        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "series">series에 사용할 입력값입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "modbusCode">modbusCode에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<string> PraseInovanceAddress(InovanceSeries series, string address, byte modbusCode)
        {
            if (series == InovanceSeries.AM)
                return PraseInovanceAMAddress(address, modbusCode);
            else if (series == InovanceSeries.H3U)
                return PraseInovanceH3UAddress(address, modbusCode);
            else if (series == InovanceSeries.H5U)
                return PraseInovanceH5UAddress(address, modbusCode);
            else
                return new OperationResult<string>($"[{series}] Not supported series of plc");
        }

        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "modbusCode">modbusCode에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<string> PraseInovanceAMAddress(string address, byte modbusCode)
        {
            try
            {
                string station = string.Empty;
                OperationResult<int> stationPara = AddressParameters.ExtractParameter(ref address, "s");
                if (stationPara.IsSuccess)
                    station = $"s={stationPara.Content};";
                if (address.StartsWith("QX") || address.StartsWith("qx"))
                    return OperationResult.CreateSuccessResult(station + CalculateStartAddress(address.Substring(2)).ToString());
                else if (address.StartsWith("Q") || address.StartsWith("q"))
                    return OperationResult.CreateSuccessResult(station + CalculateStartAddress(address.Substring(1)).ToString());
                else if (address.StartsWith("IX") || address.StartsWith("ix"))
                    return OperationResult.CreateSuccessResult(station + "x=2;" + CalculateStartAddress(address.Substring(2)).ToString());
                else if (address.StartsWith("I") || address.StartsWith("i"))
                    return OperationResult.CreateSuccessResult(station + "x=2;" + CalculateStartAddress(address.Substring(1)).ToString());
                else if (address.StartsWith("MW") || address.StartsWith("mw"))
                    return OperationResult.CreateSuccessResult(station + address.Substring(2));
                else if (address.StartsWith("MD") || address.StartsWith("md"))
                {
                    int add = Convert.ToInt32(address.Substring(2)) * 2;
                    return OperationResult.CreateSuccessResult(station + add.ToString());
                }
                else if (address.StartsWith("MX") || address.StartsWith("mx"))
                {
                    if (modbusCode == ModbusFrameRules.ReadCoil || modbusCode == ModbusFrameRules.WriteCoil || modbusCode == ModbusFrameRules.WriteOneCoil)
                    {
                        if (address.IndexOf('.') > 0)
                        {
                            string[] splits = address.Substring(2).SplitDot();
                            int add = Convert.ToInt32(splits[0]);
                            int bit = Convert.ToInt32(splits[1]);
                            return OperationResult.CreateSuccessResult(station + (add / 2).ToString() + "." + (add % 2 * 8 + bit).ToString());
                        }
                        else
                        {
                            int add = Convert.ToInt32(address.Substring(2));
                            return OperationResult.CreateSuccessResult(station + (add / 2).ToString() + ".0");
                        }
                    }
                    else
                    {
                        int add = Convert.ToInt32(address.Substring(2));
                        return OperationResult.CreateSuccessResult(station + (add / 2).ToString());
                    }
                }
                else if (address.StartsWith("M") || address.StartsWith("m"))
                    return OperationResult.CreateSuccessResult(station + address.Substring(1));
                else
                {
                    if (modbusCode == ModbusFrameRules.ReadCoil || modbusCode == ModbusFrameRules.WriteCoil || modbusCode == ModbusFrameRules.WriteOneCoil)
                    {
                        if (address.StartsWith("SMX") || address.StartsWith("smx"))
                            return OperationResult.CreateSuccessResult(station + $"x={modbusCode + 0x30};" + CalculateStartAddress(address.Substring(3)).ToString());
                        else if (address.StartsWith("SM") || address.StartsWith("sm"))
                            return OperationResult.CreateSuccessResult(station + $"x={modbusCode + 0x30};" + CalculateStartAddress(address.Substring(2)).ToString());
                    }
                    else
                    {
                        if (address.StartsWith("SDW") || address.StartsWith("sdw"))
                            return OperationResult.CreateSuccessResult(station + $"x={modbusCode + 0x30};" + address.Substring(3));
                        else if (address.StartsWith("SD") || address.StartsWith("sd"))
                            return OperationResult.CreateSuccessResult(station + $"x={modbusCode + 0x30};" + address.Substring(2));
                    }
                }

                return new OperationResult<string>(ProtocolMessages.NotSupportedDataType);
            }
            catch (Exception ex)
            {
                return new OperationResult<string>(ex.Message);
            }
        }

        /// <summary>CalculateH3UStartAddress 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static int CalculateH3UStartAddress(string address)
        {
            if (address.IndexOf('.') < 0)
                return Convert.ToInt32(address, 8);
            else
            {
                string[] splits = address.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
                return Convert.ToInt32(splits[0], 8) * 8 + int.Parse(splits[1]);
            }
        }

        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "modbusCode">modbusCode에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<string> PraseInovanceH3UAddress(string address, byte modbusCode)
        {
            try
            {
                string station = string.Empty;
                OperationResult<int> stationPara = AddressParameters.ExtractParameter(ref address, "s");
                if (stationPara.IsSuccess)
                    station = $"s={stationPara.Content};";
                if (modbusCode == ModbusFrameRules.ReadCoil || modbusCode == ModbusFrameRules.WriteCoil || modbusCode == ModbusFrameRules.WriteOneCoil)
                {
                    if (ModbusAddressMapping.TransAddressToModbus(station, address, new string[] { "X", "Y" }, new int[] { 0xF800, 0xFC00 }, CalculateH3UStartAddress, out string xAddress))
                        return OperationResult.CreateSuccessResult(xAddress);
                    if (ModbusAddressMapping.TransAddressToModbus(station, address, new string[] { "SM", "S", "T", "C" }, new int[] { 0x2400, 0xE000, 0xF000, 0xF400 }, out string mAddress))
                        return OperationResult.CreateSuccessResult(mAddress);
                    if (address.StartsWith("M") || address.StartsWith("m"))
                    {
                        int add = Convert.ToInt32(address.Substring(1));
                        if (add >= 8000)
                            return OperationResult.CreateSuccessResult(station + (add - 8000 + 0x1F40).ToString());
                        else
                            return OperationResult.CreateSuccessResult(station + add.ToString());
                    }

                    // D,R 레지스터에 대한 소수점의 경우
                    if (ModbusAddressMapping.TransPointAddressToModbus(station, address, new string[] { "D", "SD", "R" }, new int[] { 0x0000, 0x2400, 0x3000 }, out string newAddress))
                        return OperationResult.CreateSuccessResult(newAddress);
                }
                else
                {
                    if (ModbusAddressMapping.TransAddressToModbus(station, address, new string[] { "D", "SD", "R", "T" }, new int[] { 0x0000, 0x2400, 0x3000, 0xF000 }, out string newAddress))
                        return OperationResult.CreateSuccessResult(newAddress);
                    if (address.StartsWith("C", StringComparison.InvariantCultureIgnoreCase))
                    {
                        int add = Convert.ToInt32(address.Substring(1));
                        if (add >= 200)
                            return OperationResult.CreateSuccessResult(station + ((add - 200) * 2 + 0xF700).ToString());
                        else
                            return OperationResult.CreateSuccessResult(station + (add + 0xF400).ToString());
                    }
                }

                return new OperationResult<string>(ProtocolMessages.NotSupportedDataType);
            }
            catch (Exception ex)
            {
                return new OperationResult<string>(ex.Message);
            }
        }

        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "modbusCode">modbusCode에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<string> PraseInovanceH5UAddress(string address, byte modbusCode)
        {
            try
            {
                string station = string.Empty;
                OperationResult<int> stationPara = AddressParameters.ExtractParameter(ref address, "s");
                if (stationPara.IsSuccess)
                    station = $"s={stationPara.Content};";
                if (modbusCode == ModbusFrameRules.ReadCoil || modbusCode == ModbusFrameRules.WriteCoil || modbusCode == ModbusFrameRules.WriteOneCoil)
                {
                    if (ModbusAddressMapping.TransAddressToModbus(station, address, new string[] { "X", "Y" }, new int[] { 0xF800, 0xFC00 }, CalculateH3UStartAddress, out string xAddress))
                        return OperationResult.CreateSuccessResult(xAddress);
                    if (ModbusAddressMapping.TransAddressToModbus(station, address, new string[] { "S", "B", "M" }, new int[] { 0xE000, 0x3000, 0x0000 }, out string mAddress))
                        return OperationResult.CreateSuccessResult(mAddress);
                    // D,R 레지스터에 대한 소수점의 경우
                    if (ModbusAddressMapping.TransPointAddressToModbus(station, address, new string[] { "D", "R" }, new int[] { 0x0000, 0x3000 }, out string newAddress))
                        return OperationResult.CreateSuccessResult(newAddress);
                }
                else
                {
                    if (ModbusAddressMapping.TransAddressToModbus(station, address, new string[] { "D", "R" }, new int[] { 0x0000, 0x3000 }, out string newAddress))
                        return OperationResult.CreateSuccessResult(newAddress);
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
