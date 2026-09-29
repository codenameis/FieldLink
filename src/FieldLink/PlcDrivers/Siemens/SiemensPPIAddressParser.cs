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
using static FieldLink.PlcDrivers.Siemens.SiemensPPICommandBuilder;
using static FieldLink.PlcDrivers.Siemens.SiemensPPIResponseParser;

namespace FieldLink.PlcDrivers.Siemens
{
    /// <summary>SiemensPPI 주소를 프로토콜의 영역 코드와 위치로 변환합니다.</summary>
    public static class SiemensPPIAddressParser
    {
        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<S7DeviceAddress> AnalysisAddress(string address)
        {
            S7DeviceAddress s7AddressData = new S7DeviceAddress();
            try
            {
                s7AddressData.DbBlock = 0;
                if (address.StartsWith("SYS"))
                {
                    s7AddressData.DataCode = 0x03;
                    s7AddressData.AddressStart = S7DeviceAddress.CalculateAddressStarted(address.Substring(3));
                }
                else if (address.StartsWith("AI"))
                {
                    s7AddressData.DataCode = 0x06;
                    s7AddressData.AddressStart = S7DeviceAddress.CalculateAddressStarted(address.Substring(2));
                }
                else if (address.StartsWith("AQ"))
                {
                    s7AddressData.DataCode = 0x07;
                    s7AddressData.AddressStart = S7DeviceAddress.CalculateAddressStarted(address.Substring(2));
                }
                else if (address[0] == 'T')
                {
                    s7AddressData.DataCode = 0x1F;
                    s7AddressData.AddressStart = S7DeviceAddress.CalculateAddressStarted(address.Substring(1));
                }
                else if (address[0] == 'C')
                {
                    s7AddressData.DataCode = 0x1E;
                    s7AddressData.AddressStart = S7DeviceAddress.CalculateAddressStarted(address.Substring(1));
                }
                else if (address.StartsWith("SM"))
                {
                    s7AddressData.DataCode = 0x05;
                    s7AddressData.AddressStart = S7DeviceAddress.CalculateAddressStarted(address.Substring(2));
                }
                else if (address[0] == 'S')
                {
                    s7AddressData.DataCode = 0x04;
                    s7AddressData.AddressStart = S7DeviceAddress.CalculateAddressStarted(address.Substring(1));
                }
                else if (address[0] == 'I')
                {
                    s7AddressData.DataCode = 0x81;
                    s7AddressData.AddressStart = S7DeviceAddress.CalculateAddressStarted(address.Substring(1));
                }
                else if (address[0] == 'Q')
                {
                    s7AddressData.DataCode = 0x82;
                    s7AddressData.AddressStart = S7DeviceAddress.CalculateAddressStarted(address.Substring(1));
                }
                else if (address[0] == 'M')
                {
                    s7AddressData.DataCode = 0x83;
                    s7AddressData.AddressStart = S7DeviceAddress.CalculateAddressStarted(address.Substring(1));
                }
                else if (address[0] == 'D' || address.StartsWith("DB"))
                {
                    s7AddressData.DataCode = 0x84;
                    string[] adds = address.Split('.');
                    if (address[1] == 'B')
                    {
                        s7AddressData.DbBlock = Convert.ToUInt16(adds[0].Substring(2));
                    }
                    else
                    {
                        s7AddressData.DbBlock = Convert.ToUInt16(adds[0].Substring(1));
                    }

                    s7AddressData.AddressStart = S7DeviceAddress.CalculateAddressStarted(address.Substring(address.IndexOf('.') + 1));
                }
                else if (address[0] == 'V')
                {
                    s7AddressData.DataCode = 0x84;
                    s7AddressData.DbBlock = 1;
                    s7AddressData.AddressStart = S7DeviceAddress.CalculateAddressStarted(address.Substring(1));
                }
                else
                {
                    return new OperationResult<S7DeviceAddress>(ProtocolMessages.NotSupportedDataType);
                }
            }
            catch (Exception ex)
            {
                return new OperationResult<S7DeviceAddress>(ex.Message);
            }

            return OperationResult.CreateSuccessResult(s7AddressData);
        }
    }
}
