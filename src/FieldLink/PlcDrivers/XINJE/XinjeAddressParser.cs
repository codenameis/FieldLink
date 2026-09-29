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
using static FieldLink.PlcDrivers.XINJE.XinJEValueConverter;
using static FieldLink.PlcDrivers.XINJE.XinJECommandBuilder;

namespace FieldLink.PlcDrivers.XINJE
{
    /// <summary>XinJE 주소를 프로토콜의 영역 코드와 위치로 변환합니다.</summary>
    public static class XinjeAddressParser
    {
        /// <summary>CalculateXinJEStartAddress 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static int CalculateXinJEStartAddress(string address)
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
        /// <param name = "series">series에 사용할 입력값입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "modbusCode">modbusCode에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<string> PraseXinJEAddress(XinJESeries series, string address, byte modbusCode)
        {
            string station = string.Empty;
            OperationResult<int> stationPara = AddressParameters.ExtractParameter(ref address, "s");
            if (stationPara.IsSuccess)
                station = $"s={stationPara.Content};";
            string function = string.Empty;
            OperationResult<int> functionPara = AddressParameters.ExtractParameter(ref address, "x");
            if (functionPara.IsSuccess)
                function = $"x={functionPara.Content};";
            if (series == XinJESeries.XC)
            {
                try
                {
                    if (Regex.IsMatch(address, "^X[0-9]+", RegexOptions.IgnoreCase) || Regex.IsMatch(address, "^Y[0-9]+", RegexOptions.IgnoreCase) || Regex.IsMatch(address, "^M[0-9]+", RegexOptions.IgnoreCase) || Regex.IsMatch(address, "^S[0-9]+", RegexOptions.IgnoreCase))
                    {
                        if (modbusCode == ModbusFrameRules.ReadRegister)
                        {
                            modbusCode = ModbusFrameRules.ReadCoil;
                            function = "x=1;";
                        }
                    }
                }
                catch
                {
                }

                return PraseXinJEXCAddress(station + function, address, modbusCode);
            }
            else
            {
                try
                {
                    if (Regex.IsMatch(address, "^X[0-9]+", RegexOptions.IgnoreCase) || Regex.IsMatch(address, "^Y[0-9]+", RegexOptions.IgnoreCase) || Regex.IsMatch(address, "^M[0-9]+", RegexOptions.IgnoreCase) || Regex.IsMatch(address, "^S[0-9]+", RegexOptions.IgnoreCase) || Regex.IsMatch(address, "^SEM[0-9]+", RegexOptions.IgnoreCase) || Regex.IsMatch(address, "^HSC[0-9]+", RegexOptions.IgnoreCase) || Regex.IsMatch(address, "^SM[0-9]+", RegexOptions.IgnoreCase) || Regex.IsMatch(address, "^ET[0-9]+", RegexOptions.IgnoreCase) || Regex.IsMatch(address, "^HM[0-9]+", RegexOptions.IgnoreCase) || Regex.IsMatch(address, "^HS[0-9]+", RegexOptions.IgnoreCase) || Regex.IsMatch(address, "^HT[0-9]+", RegexOptions.IgnoreCase) || Regex.IsMatch(address, "^HC[0-9]+", RegexOptions.IgnoreCase))
                    {
                        if (modbusCode == ModbusFrameRules.ReadRegister)
                        {
                            modbusCode = ModbusFrameRules.ReadCoil;
                            function = "x=1;";
                        }
                    }
                }
                catch
                {
                }

                return PraseXinJEXD1XD2XD3XL1XL3Address(station + function, address, modbusCode);
            }
        }

        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "modbusCode">modbusCode에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<string> PraseXinJEXCAddress(string station, string address, byte modbusCode)
        {
            try
            {
                if (modbusCode == ModbusFrameRules.ReadCoil || modbusCode == ModbusFrameRules.ReadDiscrete || modbusCode == ModbusFrameRules.WriteCoil || modbusCode == ModbusFrameRules.WriteOneCoil)
                {
                    if (ModbusAddressMapping.TransAddressToModbus(station, address, new string[] { "X", "Y" }, new int[] { 0x4000, 0x4800 }, CalculateXinJEStartAddress, out string xAddress))
                        return OperationResult.CreateSuccessResult(xAddress);
                    if (ModbusAddressMapping.TransAddressToModbus(station, address, new string[] { "S", "T", "C" }, new int[] { 0x5000, 0x6400, 0x6C00 }, out string sAddress))
                        return OperationResult.CreateSuccessResult(sAddress);
                    if (address.StartsWithAndNumber("M"))
                    {
                        int add = Convert.ToInt32(address.Substring(1));
                        if (add >= 8000)
                            return OperationResult.CreateSuccessResult(station + (add - 8000 + 0x6000).ToString());
                        else
                            return OperationResult.CreateSuccessResult(station + add.ToString());
                    }

                    // D 레지스터에 대한 쓰기 기능
                    if (ModbusAddressMapping.TransPointAddressToModbus(station, address, new string[] { "D" }, new int[] { 0 }, CalculateXC_D, out string dAddress))
                        return OperationResult.CreateSuccessResult(dAddress);
                }
                else
                {
                    if (address.StartsWithAndNumber("D"))
                    {
                        return OperationResult.CreateSuccessResult(station + CalculateXC_D(address.Substring(1)));
                    }
                    else if (address.StartsWithAndNumber("F"))
                    {
                        int add = Convert.ToInt32(address.Substring(1));
                        if (add >= 8000)
                            return OperationResult.CreateSuccessResult(station + (add - 8000 + 0x6800).ToString());
                        else
                            return OperationResult.CreateSuccessResult(station + (add + 0x4800).ToString());
                    }

                    if (ModbusAddressMapping.TransAddressToModbus(station, address, new string[] { "E", "T", "C" }, new int[] { 0x7000, 0x3000, 0x3800 }, out string eAdderss))
                        return OperationResult.CreateSuccessResult(eAdderss);
                }

                return new OperationResult<string>(ProtocolMessages.NotSupportedDataType);
            }
            catch (Exception ex)
            {
                return new OperationResult<string>(ex.Message);
            }
        }

        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "modbusCode">modbusCode에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<string> PraseXinJEXD1XD2XD3XL1XL3Address(string station, string address, byte modbusCode)
        {
            try
            {
                if (modbusCode == ModbusFrameRules.ReadCoil || modbusCode == ModbusFrameRules.ReadDiscrete || modbusCode == ModbusFrameRules.WriteCoil || modbusCode == ModbusFrameRules.WriteOneCoil)
                {
                    if (address.StartsWith("X") || address.StartsWith("x"))
                    {
                        int start = CalculateXinJEStartAddress(address.Substring(1));
                        if (start < 0x1000)
                            return OperationResult.CreateSuccessResult(station + (start - 0x0000 + 0x5000).ToString()); // X0 - X77
                        if (start < 0x2000)
                            return OperationResult.CreateSuccessResult(station + (start - 0x1000 + 0x5100).ToString()); // X10000 - X11177 10개의 모듈
                        if (start < 0x3000)
                            return OperationResult.CreateSuccessResult(station + (start - 0x2000 + 0x58D0).ToString()); // X20000 - X20177 2개의 모듈
                        return OperationResult.CreateSuccessResult(station + (start - 0x3000 + 0x5BF0).ToString()); // #1 ED
                    }
                    else if (address.StartsWith("Y") || address.StartsWith("y"))
                    {
                        int start = CalculateXinJEStartAddress(address.Substring(1));
                        if (start < 0x1000)
                            return OperationResult.CreateSuccessResult(station + (start - 0x0000 + 0x6000).ToString()); // Y0 - Y77
                        if (start < 0x2000)
                            return OperationResult.CreateSuccessResult(station + (start - 0x1000 + 0x6100).ToString()); // Y10000 - Y11177 10개의 모듈
                        if (start < 0x3000)
                            return OperationResult.CreateSuccessResult(station + (start - 0x2000 + 0x68D0).ToString()); // Y20000 - Y20177 2개의 모듈
                        return OperationResult.CreateSuccessResult(station + (start - 0x3000 + 0x6BF0).ToString()); // #1 ED
                    }

                    if (ModbusAddressMapping.TransAddressToModbus(station, address, new string[] { "SEM", "HSC", "SM", "ET", "HM", "HS", "HT", "HC", "S", "T", "C", "M" }, new int[] { 0xC080, 0xE900, 0x9000, 0xC000, 0xC100, 0xD900, 0xE100, 0xE500, 0x7000, 0xA000, 0xB000, 0x0000 }, out string mAddress))
                        return OperationResult.CreateSuccessResult(mAddress);
                    // 글자 레지스터에 위치하는 경우
                    if (ModbusAddressMapping.TransPointAddressToModbus(station, address, new string[] { "D", "SD", "HD" }, new int[] { 0x0000, 0x7000, 0xA080 }, out string dAddress))
                        return OperationResult.CreateSuccessResult(dAddress);
                }
                else
                {
                    if (address.StartsWith("ID") || address.StartsWith("id"))
                    {
                        int start = Convert.ToInt32(address.Substring(2));
                        if (start < 10000)
                            return OperationResult.CreateSuccessResult(station + (start + 0x5000).ToString()); // ID0 - ID99
                        if (start < 20000)
                            return OperationResult.CreateSuccessResult(station + (start - 10000 + 0x5100).ToString()); // ID10000 - ID10999
                        if (start < 30000)
                            return OperationResult.CreateSuccessResult(station + (start - 20000 + 0x58D0).ToString()); // ID20000 - ID20199
                        return OperationResult.CreateSuccessResult(station + (start - 30000 + 0x5BF0).ToString()); // ID30000 - ID30099
                    }
                    else if (address.StartsWith("QD") || address.StartsWith("qd"))
                    {
                        int start = Convert.ToInt32(address.Substring(2));
                        if (start < 10000)
                            return OperationResult.CreateSuccessResult(station + (start + 0x6000).ToString()); // QD0 - QD99
                        if (start < 20000)
                            return OperationResult.CreateSuccessResult(station + (start - 10000 + 0x6100).ToString()); // QD10000 - QD10999
                        if (start < 30000)
                            return OperationResult.CreateSuccessResult(station + (start - 20000 + 0x68D0).ToString()); // QD20000 - QD20199
                        return OperationResult.CreateSuccessResult(station + (start - 30000 + 0x6BF0).ToString()); // QD30000 - QD30099
                    }

                    if (ModbusAddressMapping.TransAddressToModbus(station, address, new string[] { "HSCD", "ETD", "HSD", "HTD", "HCD", "SFD", "SD", "TD", "CD", "HD", "FD", "FS", "D" }, new int[] { 0xC480, 0xA000, 0xB880, 0xBC80, 0xC080, 0xE4C0, 0x7000, 0x8000, 0x9000, 0xA080, 0xC4C0, 0xF4C0, 0x0000 }, out string dAddress))
                        return OperationResult.CreateSuccessResult(dAddress);
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
