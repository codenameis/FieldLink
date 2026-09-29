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

namespace FieldLink.PlcDrivers.MegMeet
{
    /// <summary>MegMeet 주소를 프로토콜의 영역 코드와 위치로 변환합니다.</summary>
    public static class MegMeetAddressParser
    {
        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "modbusCode">modbusCode에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static OperationResult<string> PraseMegMeetAddress(string address, byte modbusCode)
        {
            try
            {
                string station = string.Empty;
                OperationResult<int> stationPara = AddressParameters.ExtractParameter(ref address, "s");
                if (stationPara.IsSuccess)
                    station = $"s={stationPara.Content};";
                if (modbusCode == ModbusFrameRules.ReadCoil || modbusCode == ModbusFrameRules.WriteCoil || modbusCode == ModbusFrameRules.WriteOneCoil)
                {
                    if (address.StartsWithAndNumber("X"))
                        return OperationResult.CreateSuccessResult(station + "x=2;" + Convert.ToInt32(address.Substring(1), 8).ToString());
                    else if (address.StartsWithAndNumber("Y"))
                        return OperationResult.CreateSuccessResult(station + Convert.ToInt32(address.Substring(1), 8).ToString());
                    else if (address.StartsWithAndNumber("M"))
                    {
                        int add = Convert.ToInt32(address.Substring(1));
                        if (add < 2048)
                            return OperationResult.CreateSuccessResult(station + (add + 2000).ToString());
                        else
                            return OperationResult.CreateSuccessResult(station + (add - 2048 + 12000).ToString());
                    }
                    else if (address.StartsWithAndNumber("SM"))
                    {
                        int add = Convert.ToInt32(address.Substring(2));
                        if (add < 256)
                            return OperationResult.CreateSuccessResult(station + (add + 4400).ToString());
                        else
                            return OperationResult.CreateSuccessResult(station + (add - 256 + 30000).ToString());
                    }
                    else if (address.StartsWithAndNumber("S"))
                    {
                        int add = Convert.ToInt32(address.Substring(1));
                        if (add < 1024)
                            return OperationResult.CreateSuccessResult(station + (add + 6000).ToString());
                        else
                            return OperationResult.CreateSuccessResult(station + (add - 1024 + 31000).ToString());
                    }
                    else if (address.StartsWithAndNumber("T"))
                    {
                        int add = Convert.ToInt32(address.Substring(1));
                        if (add < 256)
                            return OperationResult.CreateSuccessResult(station + (add + 8000).ToString());
                        else
                            return OperationResult.CreateSuccessResult(station + (add - 256 + 11000).ToString());
                    }
                    else if (address.StartsWithAndNumber("C"))
                    {
                        int add = Convert.ToInt32(address.Substring(1));
                        if (add < 256)
                            return OperationResult.CreateSuccessResult(station + (add + 9200).ToString());
                        else
                            return OperationResult.CreateSuccessResult(station + (add - 256 + 10000).ToString());
                    }

                    if (ModbusAddressMapping.TransPointAddressToModbus(station, address, new string[] { "D", "R" }, new int[] { 0, 13000 }, out string dAddress))
                        return OperationResult.CreateSuccessResult(dAddress);
                }
                else
                {
                    if (address.StartsWithAndNumber("T"))
                    {
                        int add = Convert.ToInt32(address.Substring(1));
                        if (add < 256)
                            return OperationResult.CreateSuccessResult(station + (add + 9000).ToString());
                        else
                            return OperationResult.CreateSuccessResult(station + (add - 256 + 11000).ToString());
                    }
                    else if (address.StartsWithAndNumber("C"))
                    {
                        int add = Convert.ToInt32(address.Substring(1));
                        if (add < 200)
                            return OperationResult.CreateSuccessResult(station + (add + 9500).ToString());
                        else if (add < 256)
                            return OperationResult.CreateSuccessResult(station + (add * 2 - 200 + 9700).ToString());
                        else
                            return OperationResult.CreateSuccessResult(station + (add * 2 - 256 + 10000).ToString());
                    }
                    else if (address.StartsWithAndNumber("D"))
                    {
                        int add = Convert.ToInt32(address.Substring(1));
                        return OperationResult.CreateSuccessResult(station + add.ToString());
                    }
                    else if (address.StartsWithAndNumber("SD"))
                    {
                        int add = Convert.ToInt32(address.Substring(2));
                        if (add < 256)
                            return OperationResult.CreateSuccessResult(station + (add + 8000).ToString());
                        else
                            return OperationResult.CreateSuccessResult(station + (add - 256 + 12000).ToString());
                    }
                    else if (address.StartsWithAndNumber("Z"))
                    {
                        int add = Convert.ToInt32(address.Substring(1));
                        return OperationResult.CreateSuccessResult(station + (add + 8500).ToString());
                    }
                    else if (address.StartsWithAndNumber("R"))
                    {
                        int add = Convert.ToInt32(address.Substring(1));
                        return OperationResult.CreateSuccessResult(station + (add + 13000).ToString());
                    }
                }

                return new OperationResult<string>(ProtocolMessages.NotSupportedDataType);
            }
            catch (Exception ex)
            {
                return new OperationResult<string>(ex.Message);
            }
        }

        /// <summary>GetBoolCuttingAddress 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static List<CuttingAddress> GetBoolCuttingAddress()
        {
            List<CuttingAddress> list = new List<CuttingAddress>();
            list.Add(new CuttingAddress("M", 2048));
            list.Add(new CuttingAddress("SM", 256));
            list.Add(new CuttingAddress("S", 1024));
            list.Add(new CuttingAddress("T", 256));
            list.Add(new CuttingAddress("C", 256));
            return list;
        }

        /// <summary>GetWordCuttingAddress 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static List<CuttingAddress> GetWordCuttingAddress()
        {
            List<CuttingAddress> list = new List<CuttingAddress>();
            list.Add(new CuttingAddress("SD", 256));
            list.Add(new CuttingAddress("T", 256));
            list.Add(new CuttingAddress("C", 256));
            return list;
        }
    }
}
