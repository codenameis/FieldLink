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
using static FieldLink.PlcDrivers.Melsec.MelsecResponseParser;
using static FieldLink.PlcDrivers.Melsec.MelsecCommandBuilder;
using static FieldLink.PlcDrivers.Melsec.MelsecValueConverter;

namespace FieldLink.PlcDrivers.Melsec
{
    /// <summary>Melsec 주소를 프로토콜의 영역 코드와 위치로 변환합니다.</summary>
    public static class MelsecAddressParser
    {
        /// <summary>McA1EAnalysisAddress 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<MelsecA1EDataType, int> McA1EAnalysisAddress(string address)
        {
            var result = new OperationResult<MelsecA1EDataType, int>();
            try
            {
                switch (address[0])
                {
                    case 'T':
                    case 't':
                    {
                        if (address[1] == 'S' || address[1] == 's')
                        {
                            result.Content1 = MelsecA1EDataType.TS;
                            result.Content2 = Convert.ToInt32(address.Substring(2), MelsecA1EDataType.TS.FromBase);
                        }
                        else if (address[1] == 'C' || address[1] == 'c')
                        {
                            result.Content1 = MelsecA1EDataType.TC;
                            result.Content2 = Convert.ToInt32(address.Substring(2), MelsecA1EDataType.TC.FromBase);
                        }
                        else if (address[1] == 'N' || address[1] == 'n')
                        {
                            result.Content1 = MelsecA1EDataType.TN;
                            result.Content2 = Convert.ToInt32(address.Substring(2), MelsecA1EDataType.TN.FromBase);
                        }
                        else
                        {
                            throw new Exception(ProtocolMessages.NotSupportedDataType);
                        }

                        break;
                    }

                    case 'C':
                    case 'c':
                    {
                        if (address[1] == 'S' || address[1] == 's')
                        {
                            result.Content1 = MelsecA1EDataType.CS;
                            result.Content2 = Convert.ToInt32(address.Substring(2), MelsecA1EDataType.CS.FromBase);
                        }
                        else if (address[1] == 'C' || address[1] == 'c')
                        {
                            result.Content1 = MelsecA1EDataType.CC;
                            result.Content2 = Convert.ToInt32(address.Substring(2), MelsecA1EDataType.CC.FromBase);
                        }
                        else if (address[1] == 'N' || address[1] == 'n')
                        {
                            result.Content1 = MelsecA1EDataType.CN;
                            result.Content2 = Convert.ToInt32(address.Substring(2), MelsecA1EDataType.CN.FromBase);
                        }
                        else
                        {
                            throw new Exception(ProtocolMessages.NotSupportedDataType);
                        }

                        break;
                    }

                    case 'X':
                    case 'x':
                    {
                        result.Content1 = MelsecA1EDataType.X;
                        address = address.Substring(1);
                        if (address.StartsWith("0"))
                            result.Content2 = Convert.ToInt32(address, 8);
                        else
                            result.Content2 = Convert.ToInt32(address, MelsecA1EDataType.X.FromBase);
                        break;
                    }

                    case 'Y':
                    case 'y':
                    {
                        result.Content1 = MelsecA1EDataType.Y;
                        address = address.Substring(1);
                        if (address.StartsWith("0"))
                            result.Content2 = Convert.ToInt32(address, 8);
                        else
                            result.Content2 = Convert.ToInt32(address, MelsecA1EDataType.Y.FromBase);
                        break;
                    }

                    case 'M':
                    case 'm':
                    {
                        result.Content1 = MelsecA1EDataType.M;
                        result.Content2 = Convert.ToInt32(address.Substring(1), MelsecA1EDataType.M.FromBase);
                        break;
                    }

                    case 'S':
                    case 's':
                    {
                        result.Content1 = MelsecA1EDataType.S;
                        result.Content2 = Convert.ToInt32(address.Substring(1), MelsecA1EDataType.S.FromBase);
                        break;
                    }

                    case 'F':
                    case 'f':
                    {
                        result.Content1 = MelsecA1EDataType.F;
                        result.Content2 = Convert.ToInt32(address.Substring(1), MelsecA1EDataType.F.FromBase);
                        break;
                    }

                    case 'B':
                    case 'b':
                    {
                        result.Content1 = MelsecA1EDataType.B;
                        result.Content2 = Convert.ToInt32(address.Substring(1), MelsecA1EDataType.B.FromBase);
                        break;
                    }

                    case 'D':
                    case 'd':
                    {
                        result.Content1 = MelsecA1EDataType.D;
                        result.Content2 = Convert.ToInt32(address.Substring(1), MelsecA1EDataType.D.FromBase);
                        break;
                    }

                    case 'R':
                    case 'r':
                    {
                        result.Content1 = MelsecA1EDataType.R;
                        result.Content2 = Convert.ToInt32(address.Substring(1), MelsecA1EDataType.R.FromBase);
                        break;
                    }

                    case 'W':
                    case 'w':
                    {
                        result.Content1 = MelsecA1EDataType.W;
                        result.Content2 = Convert.ToInt32(address.Substring(1), MelsecA1EDataType.W.FromBase);
                        break;
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
    }
}
