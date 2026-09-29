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

namespace FieldLink.PlcDrivers.Omron
{
    /// <summary>Omron의 Fins 프로토콜의 주소형 객체</summary>
    public class OmronFinsAddress : DeviceAddress
    {
        /// <summary>비트 조작을 위한 명령어</summary>
        public byte BitCode { get; set; }
        /// <summary>워드 조작을 위한 명령어</summary>
        public byte WordCode { get; set; }

        /// <summary>정해진 주소 정보에서 실제 장치 주소 정보로 분해</summary>
        /// <param name = "address">주소 정보</param>
        /// <param name = "length">데이터 길이</param>
        public override void Parse(string address, ushort length)
        {
            OperationResult<OmronFinsAddress> addressData = ParseFrom(address, length, OmronPlcType.CSCJ);
            if (!addressData.IsSuccess)
                throw new FormatException(addressData.Message);
            AddressStart = addressData.Content.AddressStart;
            Length = addressData.Content.Length;
            BitCode = addressData.Content.BitCode;
            WordCode = addressData.Content.WordCode;
        }

        /// <summary>실제 옴롱의 주소에서 주소 객체를 분석합니다.</summary>
        /// <param name = "address">옴롱의 주소 데이터 정보</param>
        /// <returns>성공 여부를 나타내는 결과 객체</returns>
        public static OperationResult<OmronFinsAddress> ParseFrom(string address)
        {
            return ParseFrom(address, 0, OmronPlcType.CSCJ);
        }

        /// <summary>CalculateBitIndex 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        private static int CalculateBitIndex(string address)
        {
            string[] splits = address.SplitDot();
            int addr = ushort.Parse(splits[0]) * 16;
            // D100.F와 같은 비트 포함 된 경우
            if (splits.Length > 1)
                addr += AddressParameters.CalculateBitStartIndex(splits[1]);
            return addr;
        }

        /// <summary>실제 옴롱의 주소에서 주소 객체를 분석합니다.</summary>
        /// <param name = "address">옴롱의 주소 데이터 정보</param>
        /// <param name = "length">읽을 데이터 길이</param>
        /// <param name = "plcType">PLC의 타입 정보</param>
        /// <returns>성공 여부를 나타내는 결과 객체</returns>
        public static OperationResult<OmronFinsAddress> ParseFrom(string address, ushort length, OmronPlcType plcType)
        {
            OmronFinsAddress addressData = new OmronFinsAddress();
            try
            {
                addressData.Length = length;
                if (address.StartsWith("DR") || address.StartsWith("dr"))
                {
                    if (plcType == OmronPlcType.CV)
                    {
                        addressData.WordCode = 0x9C;
                        addressData.AddressStart = CalculateBitIndex(address.Substring(2)) + 0x03 * 16;
                    }
                    else
                    {
                        addressData.WordCode = 0xBC;
                        addressData.AddressStart = CalculateBitIndex(address.Substring(2)) + 0x200 * 16;
                    }
                }
                else if (address.StartsWith("IR") || address.StartsWith("ir"))
                {
                    addressData.WordCode = 0xDC;
                    addressData.AddressStart = CalculateBitIndex(address.Substring(2)) + 0x100 * 16;
                }
                else if (address.StartsWith("DM") || address.StartsWith("dm"))
                {
                    // DM 영역 데이터
                    addressData.BitCode = OmronFinsDataType.DM.BitCode;
                    addressData.WordCode = OmronFinsDataType.DM.WordCode;
                    addressData.AddressStart = CalculateBitIndex(address.Substring(2));
                }
                else if (address.StartsWith("TIM") || address.StartsWith("tim"))
                {
                    if (plcType == OmronPlcType.CV)
                    {
                        addressData.BitCode = 0x01;
                        addressData.WordCode = 0x81;
                    }
                    else
                    {
                        addressData.BitCode = OmronFinsDataType.TIM.BitCode;
                        addressData.WordCode = OmronFinsDataType.TIM.WordCode;
                    }

                    addressData.AddressStart = CalculateBitIndex(address.Substring(3));
                }
                else if (address.StartsWith("CNT") || address.StartsWith("cnt"))
                {
                    if (plcType == OmronPlcType.CV)
                    {
                        addressData.BitCode = 0x01;
                        addressData.WordCode = 0x81;
                        addressData.AddressStart = CalculateBitIndex(address.Substring(3)) + 0x800 * 16;
                    }
                    else
                    {
                        addressData.BitCode = OmronFinsDataType.TIM.BitCode;
                        addressData.WordCode = OmronFinsDataType.TIM.WordCode;
                        addressData.AddressStart = CalculateBitIndex(address.Substring(3)) + 0x8000 * 16;
                    }
                }
                else if (address.StartsWith("CIO") || address.StartsWith("cio"))
                {
                    if (plcType == OmronPlcType.CV)
                    {
                        addressData.BitCode = 0x00;
                        addressData.WordCode = 0x80;
                    }
                    else
                    {
                        addressData.BitCode = OmronFinsDataType.CIO.BitCode;
                        addressData.WordCode = OmronFinsDataType.CIO.WordCode;
                    }

                    addressData.AddressStart = CalculateBitIndex(address.Substring(3));
                }
                else if (address.StartsWith("WR") || address.StartsWith("wr"))
                {
                    addressData.BitCode = OmronFinsDataType.WR.BitCode;
                    addressData.WordCode = OmronFinsDataType.WR.WordCode;
                    addressData.AddressStart = CalculateBitIndex(address.Substring(2));
                }
                else if (address.StartsWith("HR") || address.StartsWith("hr"))
                {
                    addressData.BitCode = OmronFinsDataType.HR.BitCode;
                    addressData.WordCode = OmronFinsDataType.HR.WordCode;
                    addressData.AddressStart = CalculateBitIndex(address.Substring(2));
                }
                else if (address.StartsWith("AR") || address.StartsWith("ar"))
                {
                    if (plcType == OmronPlcType.CV)
                    {
                        addressData.BitCode = 0x00;
                        addressData.WordCode = 0x80;
                        addressData.AddressStart = CalculateBitIndex(address.Substring(2)) + 0xB00 * 16;
                    }
                    else
                    {
                        addressData.BitCode = OmronFinsDataType.AR.BitCode;
                        addressData.WordCode = OmronFinsDataType.AR.WordCode;
                        addressData.AddressStart = CalculateBitIndex(address.Substring(2));
                    }
                }
                else if (address.StartsWith("CF") || address.StartsWith("cf"))
                {
                    addressData.BitCode = 0x07;
                    addressData.AddressStart = CalculateBitIndex(address.Substring(2));
                }
                else if (address.StartsWith("EM") || address.StartsWith("em") || address.StartsWith("E") || address.StartsWith("e"))
                {
                    // E 구역은 좀 더 복잡해서 특별한 계산이 필요합니다.
                    if (address.IndexOf('.') > 0)
                    {
                        // 소수점을 포함하는 경우 E0.100 EF.100 E10.100
                        string[] splits = address.SplitDot();
                        int block = Convert.ToInt32(splits[0].Substring((address[1] == 'M' || address[1] == 'm') ? 2 : 1), 16);
                        if (block < 16)
                        {
                            addressData.BitCode = (byte)(0x20 + block);
                            if (plcType == OmronPlcType.CV)
                                addressData.WordCode = (byte)(0x90 + block);
                            else
                                addressData.WordCode = (byte)(0xA0 + block);
                        }
                        else
                        {
                            addressData.BitCode = (byte)(0xE0 + block - 16);
                            addressData.WordCode = (byte)(0x60 + block - 16);
                        }

                        addressData.AddressStart = CalculateBitIndex(address.Substring(address.IndexOf('.') + 1));
                    }
                    else
                    {
                        // 소수점 없이, 즉 현재 E 구역, E100
                        addressData.BitCode = 0x0A;
                        addressData.WordCode = 0x98;
                        addressData.AddressStart = CalculateBitIndex(address.Substring((address[1] == 'M' || address[1] == 'm') ? 2 : 1));
                    }
                }
                else if (address.StartsWith("D") || address.StartsWith("d"))
                {
                    // DM 영역 데이터
                    addressData.BitCode = OmronFinsDataType.DM.BitCode;
                    addressData.WordCode = OmronFinsDataType.DM.WordCode;
                    addressData.AddressStart = CalculateBitIndex(address.Substring(1));
                }
                else if (address.StartsWith("C") || address.StartsWith("c"))
                {
                    if (plcType == OmronPlcType.CV)
                    {
                        addressData.BitCode = 0x00;
                        addressData.WordCode = 0x80;
                    }
                    else
                    {
                        addressData.BitCode = OmronFinsDataType.CIO.BitCode;
                        addressData.WordCode = OmronFinsDataType.CIO.WordCode;
                    }

                    addressData.AddressStart = CalculateBitIndex(address.Substring(1));
                }
                else if (address.StartsWith("W") || address.StartsWith("w"))
                {
                    addressData.BitCode = OmronFinsDataType.WR.BitCode;
                    addressData.WordCode = OmronFinsDataType.WR.WordCode;
                    addressData.AddressStart = CalculateBitIndex(address.Substring(1));
                }
                else if (address.StartsWith("H") || address.StartsWith("h"))
                {
                    addressData.BitCode = OmronFinsDataType.HR.BitCode;
                    addressData.WordCode = OmronFinsDataType.HR.WordCode;
                    addressData.AddressStart = CalculateBitIndex(address.Substring(1));
                }
                else if (address.StartsWith("A") || address.StartsWith("a"))
                {
                    if (plcType == OmronPlcType.CV)
                    {
                        addressData.BitCode = 0x00;
                        addressData.WordCode = 0x80;
                        addressData.AddressStart = CalculateBitIndex(address.Substring(1)) + 0xB00 * 16;
                    }
                    else
                    {
                        addressData.BitCode = OmronFinsDataType.AR.BitCode;
                        addressData.WordCode = OmronFinsDataType.AR.WordCode;
                        addressData.AddressStart = CalculateBitIndex(address.Substring(1));
                    }
                }
                else
                {
                    throw new Exception(ProtocolMessages.NotSupportedDataType);
                }
            }
            catch (Exception ex)
            {
                return new OperationResult<OmronFinsAddress>(ex.Message);
            }

            return OperationResult.CreateSuccessResult(addressData);
        }
    }
}
