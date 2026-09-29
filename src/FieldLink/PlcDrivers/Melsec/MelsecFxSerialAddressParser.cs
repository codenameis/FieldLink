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
using static FieldLink.PlcDrivers.Melsec.MelsecFxSerialResponseParser;
using static FieldLink.PlcDrivers.Melsec.MelsecFxSerialCommandBuilder;

namespace FieldLink.PlcDrivers.Melsec
{
    /// <summary>MelsecFxSerial 주소를 프로토콜의 영역 코드와 위치로 변환합니다.</summary>
    public static class MelsecFxSerialAddressParser
    {
        /// <summary>FxAnalysisAddress 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<MelsecMcDataType, ushort> FxAnalysisAddress(string address)
        {
            var result = new OperationResult<MelsecMcDataType, ushort>();
            try
            {
                switch (address[0])
                {
                    case 'M':
                    case 'm':
                    {
                        result.Content1 = MelsecMcDataType.M;
                        result.Content2 = Convert.ToUInt16(address.Substring(1), MelsecMcDataType.M.FromBase);
                        break;
                    }

                    case 'X':
                    case 'x':
                    {
                        result.Content1 = MelsecMcDataType.X;
                        result.Content2 = Convert.ToUInt16(address.Substring(1), 8);
                        break;
                    }

                    case 'Y':
                    case 'y':
                    {
                        result.Content1 = MelsecMcDataType.Y;
                        result.Content2 = Convert.ToUInt16(address.Substring(1), 8);
                        break;
                    }

                    case 'D':
                    case 'd':
                    {
                        result.Content1 = MelsecMcDataType.D;
                        result.Content2 = Convert.ToUInt16(address.Substring(1), MelsecMcDataType.D.FromBase);
                        break;
                    }

                    case 'S':
                    case 's':
                    {
                        result.Content1 = MelsecMcDataType.S;
                        result.Content2 = Convert.ToUInt16(address.Substring(1), MelsecMcDataType.S.FromBase);
                        break;
                    }

                    case 'T':
                    case 't':
                    {
                        if (address[1] == 'N' || address[1] == 'n')
                        {
                            result.Content1 = MelsecMcDataType.TN;
                            result.Content2 = Convert.ToUInt16(address.Substring(2), MelsecMcDataType.TN.FromBase);
                            break;
                        }
                        else if (address[1] == 'S' || address[1] == 's')
                        {
                            result.Content1 = MelsecMcDataType.TS;
                            result.Content2 = Convert.ToUInt16(address.Substring(2), MelsecMcDataType.TS.FromBase);
                            break;
                        }
                        else if (address[1] == 'C' || address[1] == 'c')
                        {
                            result.Content1 = MelsecMcDataType.TC;
                            result.Content2 = Convert.ToUInt16(address.Substring(2), MelsecMcDataType.TC.FromBase);
                            break;
                        }
                        else
                        {
                            throw new Exception(ProtocolMessages.NotSupportedDataType);
                        }
                    }

                    case 'C':
                    case 'c':
                    {
                        if (address[1] == 'N' || address[1] == 'n')
                        {
                            result.Content1 = MelsecMcDataType.CN;
                            result.Content2 = Convert.ToUInt16(address.Substring(2), MelsecMcDataType.CN.FromBase);
                            break;
                        }
                        else if (address[1] == 'S' || address[1] == 's')
                        {
                            result.Content1 = MelsecMcDataType.CS;
                            result.Content2 = Convert.ToUInt16(address.Substring(2), MelsecMcDataType.CS.FromBase);
                            break;
                        }
                        else if (address[1] == 'C' || address[1] == 'c')
                        {
                            result.Content1 = MelsecMcDataType.CC;
                            result.Content2 = Convert.ToUInt16(address.Substring(2), MelsecMcDataType.CC.FromBase);
                            break;
                        }
                        else
                        {
                            throw new Exception(ProtocolMessages.NotSupportedDataType);
                        }
                    }

                    default:
                        throw new Exception(ProtocolMessages.NotSupportedDataType);
                }
            }
            catch (Exception ex)
            {
                result.Message = ex.Message;
                return result;
            }

            result.IsSuccess = true;
            return result;
        }

        /// <summary>FxCalculateWordStartAddress 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "isNewVersion">isNewVersion에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static OperationResult<ushort> FxCalculateWordStartAddress(string address, bool isNewVersion)
        {
            // 초기 파싱, 실패로 돌아갑니다.
            var analysis = FxAnalysisAddress(address);
            if (!analysis.IsSuccess)
                return OperationResult.CreateFailedResult<ushort>(analysis);
            // 2차 분석
            ushort startAddress = analysis.Content2;
            if (analysis.Content1 == MelsecMcDataType.D)
            {
                if (startAddress >= 8000)
                    startAddress = (ushort)((startAddress - 8000) * 2 + (isNewVersion ? 0x8000 : 0x0E00));
                else
                    startAddress = isNewVersion ? (ushort)(startAddress * 2 + 0x4000) : (ushort)(startAddress * 2 + 0x1000);
            }
            else if (analysis.Content1 == MelsecMcDataType.CN)
            {
                if (startAddress >= 200)
                    startAddress = (ushort)((startAddress - 200) * 4 + 0x0C00);
                else
                    startAddress = (ushort)(startAddress * 2 + 0x0A00);
            }
            else if (analysis.Content1 == MelsecMcDataType.TN)
            {
                if (isNewVersion)
                {
                    startAddress = (ushort)(startAddress * 2 + 0x1000);
                }
                else
                {
                    startAddress = (ushort)(startAddress * 2 + 0x0800);
                }
            }
            else
                return new OperationResult<ushort>(ProtocolMessages.MelsecCurrentTypeNotSupportedWordOperate);
            return OperationResult.CreateSuccessResult(startAddress);
        }

        /// <summary>FxCalculateBoolStartAddress 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "isNewVersion">isNewVersion에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static OperationResult<ushort, ushort, ushort> FxCalculateBoolStartAddress(string address, bool isNewVersion)
        {
            // 1차 분석
            var analysis = FxAnalysisAddress(address);
            if (!analysis.IsSuccess)
                return OperationResult.CreateFailedResult<ushort, ushort, ushort>(analysis);
            // 2차 분석
            ushort startAddress = analysis.Content2;
            if (analysis.Content1 == MelsecMcDataType.M)
            {
                if (isNewVersion)
                {
                    if (startAddress >= 8000)
                        startAddress = (ushort)((startAddress - 8000) / 8 + 0x8C00);
                    else
                        startAddress = (ushort)(startAddress / 8 + 0x8800);
                }
                else
                {
                    if (startAddress >= 8000)
                        startAddress = (ushort)((startAddress - 8000) / 8 + 0x01E0);
                    else
                        startAddress = (ushort)(startAddress / 8 + 0x0100);
                }
            }
            else if (analysis.Content1 == MelsecMcDataType.X)
                startAddress = (ushort)(startAddress / 8 + (isNewVersion ? 0x8CA0 : 0x0080));
            else if (analysis.Content1 == MelsecMcDataType.Y)
                startAddress = (ushort)(startAddress / 8 + (isNewVersion ? 0x8BC0 : 0x00A0));
            else if (analysis.Content1 == MelsecMcDataType.S)
                startAddress = (ushort)(startAddress / 8 + (isNewVersion ? 0x8CE0 : 0x0000));
            else if (analysis.Content1 == MelsecMcDataType.CS)
                startAddress = (ushort)(startAddress / 8 + (isNewVersion ? 0x9340 : 0x01C0));
            else if (analysis.Content1 == MelsecMcDataType.CC)
                startAddress = (ushort)(startAddress / 8 + (isNewVersion ? 0x92E0 : 0x03C0));
            else if (analysis.Content1 == MelsecMcDataType.TS)
                startAddress = (ushort)(startAddress / 8 + (isNewVersion ? 0x9360 : 0x00C0));
            else if (analysis.Content1 == MelsecMcDataType.TC)
                startAddress = (ushort)(startAddress / 8 + (isNewVersion ? 0x9300 : 0x02C0));
            else
                return new OperationResult<ushort, ushort, ushort>(ProtocolMessages.MelsecCurrentTypeNotSupportedBitOperate);
            return OperationResult.CreateSuccessResult(startAddress, analysis.Content2, (ushort)(analysis.Content2 % 8));
        }
    }
}
