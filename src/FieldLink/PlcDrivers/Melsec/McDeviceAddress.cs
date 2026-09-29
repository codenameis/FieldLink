using FieldLink.PlcDrivers.Common;
using FieldLink.PlcDrivers.Panasonic;
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

namespace FieldLink.PlcDrivers.Melsec
{
    /// <summary>Mitsubishi 데이터 주소 표현 형식</summary>
    public class McDeviceAddress : DeviceAddress
    {
        /// <summary>기본 객체를 생성합니다.</summary>
        public McDeviceAddress()
        {
            McDataType = MelsecMcDataType.D;
        }

        /// <summary>Mitsubishi 데이터 타입 및 주소 정보</summary>
        public MelsecMcDataType McDataType { get; set; }

        /// <summary>지정된 주소 정보에서 실제 장치 주소 정보로 분석, 기본적으로 Mitsubishi의 주소</summary>
        /// <param name = "address">주소 정보</param>
        /// <param name = "length">데이터 길이</param>
        public override void Parse(string address, ushort length)
        {
            OperationResult<McDeviceAddress> addressData = ParseMelsecFrom(address, length, false);
            if (!addressData.IsSuccess)
                throw new FormatException(addressData.Message);
            AddressStart = addressData.Content.AddressStart;
            Length = addressData.Content.Length;
            McDataType = addressData.Content.McDataType;
        }

        /// <summary>현재 프로토콜 값의 문자열 표현을 반환합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public override string ToString() => McDataType.AsciiCode.Replace("*", "") + Convert.ToString(AddressStart, McDataType.FromBase);
        /// <summary>실제 Mitsubishi의 주소에서 우리가 필요로 하는 주소 유형을 파싱합니다.</summary>
        /// <param name = "address">Mitsubishi 주소 데이터 정보</param>
        /// <param name = "length">읽을 데이터 길이</param>
        /// <param name = "isBit">bool를 읽거나 쓸 수 있는지</param>
        /// <returns>성공 여부를 나타내는 결과 객체</returns>
        public static OperationResult<McDeviceAddress> ParseMelsecFrom(string address, ushort length, bool isBit)
        {
            McDeviceAddress addressData = new McDeviceAddress();
            addressData.Length = length;
            try
            {
                switch (address[0])
                {
                    case 'M':
                    case 'm':
                    {
                        addressData.McDataType = MelsecMcDataType.M;
                        addressData.AddressStart = Convert.ToInt32(address.Substring(1), MelsecMcDataType.M.FromBase);
                        break;
                    }

                    case 'X':
                    case 'x':
                    {
                        addressData.McDataType = MelsecMcDataType.X;
                        address = address.Substring(1);
                        if (address.StartsWith("0"))
                            addressData.AddressStart = Convert.ToInt32(address, 8);
                        else
                            addressData.AddressStart = Convert.ToInt32(address, MelsecMcDataType.X.FromBase);
                        break;
                    }

                    case 'Y':
                    case 'y':
                    {
                        addressData.McDataType = MelsecMcDataType.Y;
                        address = address.Substring(1);
                        if (address.StartsWith("0"))
                            addressData.AddressStart = Convert.ToInt32(address, 8);
                        else
                            addressData.AddressStart = Convert.ToInt32(address, MelsecMcDataType.Y.FromBase);
                        break;
                    }

                    case 'D':
                    case 'd':
                    {
                        if (address[1] == 'X' || address[1] == 'x')
                        {
                            addressData.McDataType = MelsecMcDataType.DX;
                            address = address.Substring(2);
                            if (address.StartsWith("0"))
                                addressData.AddressStart = Convert.ToInt32(address, 8);
                            else
                                addressData.AddressStart = Convert.ToInt32(address, MelsecMcDataType.DX.FromBase);
                            break;
                        }
                        else if (address[1] == 'Y' || address[1] == 's')
                        {
                            addressData.McDataType = MelsecMcDataType.DY;
                            address = address.Substring(2);
                            if (address.StartsWith("0"))
                                addressData.AddressStart = Convert.ToInt32(address, 8);
                            else
                                addressData.AddressStart = Convert.ToInt32(address, MelsecMcDataType.DY.FromBase);
                            break;
                        }
                        else
                        {
                            addressData.McDataType = MelsecMcDataType.D;
                            addressData.AddressStart = Convert.ToInt32(address.Substring(1), MelsecMcDataType.D.FromBase);
                            break;
                        }
                    }

                    case 'W':
                    case 'w':
                    {
                        addressData.McDataType = MelsecMcDataType.W;
                        addressData.AddressStart = Convert.ToInt32(address.Substring(1), MelsecMcDataType.W.FromBase);
                        break;
                    }

                    case 'L':
                    case 'l':
                    {
                        addressData.McDataType = MelsecMcDataType.L;
                        addressData.AddressStart = Convert.ToInt32(address.Substring(1), MelsecMcDataType.L.FromBase);
                        break;
                    }

                    case 'F':
                    case 'f':
                    {
                        addressData.McDataType = MelsecMcDataType.F;
                        addressData.AddressStart = Convert.ToInt32(address.Substring(1), MelsecMcDataType.F.FromBase);
                        break;
                    }

                    case 'V':
                    case 'v':
                    {
                        addressData.McDataType = MelsecMcDataType.V;
                        addressData.AddressStart = Convert.ToInt32(address.Substring(1), MelsecMcDataType.V.FromBase);
                        break;
                    }

                    case 'B':
                    case 'b':
                    {
                        addressData.McDataType = MelsecMcDataType.B;
                        addressData.AddressStart = Convert.ToInt32(address.Substring(1), MelsecMcDataType.B.FromBase);
                        break;
                    }

                    case 'R':
                    case 'r':
                    {
                        addressData.McDataType = MelsecMcDataType.R;
                        addressData.AddressStart = Convert.ToInt32(address.Substring(1), MelsecMcDataType.R.FromBase);
                        break;
                    }

                    case 'S':
                    case 's':
                    {
                        if (address[1] == 'N' || address[1] == 'n')
                        {
                            addressData.McDataType = MelsecMcDataType.SN;
                            addressData.AddressStart = Convert.ToInt32(address.Substring(2), MelsecMcDataType.SN.FromBase);
                            break;
                        }
                        else if (address[1] == 'S' || address[1] == 's')
                        {
                            addressData.McDataType = MelsecMcDataType.SS;
                            addressData.AddressStart = Convert.ToInt32(address.Substring(2), MelsecMcDataType.SS.FromBase);
                            break;
                        }
                        else if (address[1] == 'C' || address[1] == 'c')
                        {
                            addressData.McDataType = MelsecMcDataType.SC;
                            addressData.AddressStart = Convert.ToInt32(address.Substring(2), MelsecMcDataType.SC.FromBase);
                            break;
                        }
                        else if (address[1] == 'M' || address[1] == 'm')
                        {
                            addressData.McDataType = MelsecMcDataType.SM;
                            addressData.AddressStart = Convert.ToInt32(address.Substring(2), MelsecMcDataType.SM.FromBase);
                            break;
                        }
                        else if (address[1] == 'D' || address[1] == 'd')
                        {
                            addressData.McDataType = MelsecMcDataType.SD;
                            addressData.AddressStart = Convert.ToInt32(address.Substring(2), MelsecMcDataType.SD.FromBase);
                            break;
                        }
                        else if (address[1] == 'B' || address[1] == 'b')
                        {
                            addressData.McDataType = MelsecMcDataType.SB;
                            addressData.AddressStart = Convert.ToInt32(address.Substring(2), MelsecMcDataType.SB.FromBase);
                            break;
                        }
                        else if (address[1] == 'W' || address[1] == 'w')
                        {
                            addressData.McDataType = MelsecMcDataType.SW;
                            addressData.AddressStart = Convert.ToInt32(address.Substring(2), MelsecMcDataType.SW.FromBase);
                            break;
                        }
                        else
                        {
                            addressData.McDataType = MelsecMcDataType.S;
                            addressData.AddressStart = Convert.ToInt32(address.Substring(1), MelsecMcDataType.S.FromBase);
                            break;
                        }
                    }

                    case 'Z':
                    case 'z':
                    {
                        if (address.StartsWith("ZR") || address.StartsWith("zr"))
                        {
                            addressData.McDataType = MelsecMcDataType.ZR;
                            addressData.AddressStart = Convert.ToInt32(address.Substring(2), MelsecMcDataType.ZR.FromBase);
                            break;
                        }
                        else
                        {
                            addressData.McDataType = MelsecMcDataType.Z;
                            addressData.AddressStart = Convert.ToInt32(address.Substring(1), MelsecMcDataType.Z.FromBase);
                            break;
                        }
                    }

                    case 'T':
                    case 't':
                    {
                        if (address[1] == 'N' || address[1] == 'n')
                        {
                            addressData.McDataType = MelsecMcDataType.TN;
                            addressData.AddressStart = Convert.ToInt32(address.Substring(2), MelsecMcDataType.TN.FromBase);
                            break;
                        }
                        else if (address[1] == 'S' || address[1] == 's')
                        {
                            addressData.McDataType = MelsecMcDataType.TS;
                            addressData.AddressStart = Convert.ToInt32(address.Substring(2), MelsecMcDataType.TS.FromBase);
                            break;
                        }
                        else if (address[1] == 'C' || address[1] == 'c')
                        {
                            addressData.McDataType = MelsecMcDataType.TC;
                            addressData.AddressStart = Convert.ToInt32(address.Substring(2), MelsecMcDataType.TC.FromBase);
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
                            addressData.McDataType = MelsecMcDataType.CN;
                            addressData.AddressStart = Convert.ToInt32(address.Substring(2), MelsecMcDataType.CN.FromBase);
                            break;
                        }
                        else if (address[1] == 'S' || address[1] == 's')
                        {
                            addressData.McDataType = MelsecMcDataType.CS;
                            addressData.AddressStart = Convert.ToInt32(address.Substring(2), MelsecMcDataType.CS.FromBase);
                            break;
                        }
                        else if (address[1] == 'C' || address[1] == 'c')
                        {
                            addressData.McDataType = MelsecMcDataType.CC;
                            addressData.AddressStart = Convert.ToInt32(address.Substring(2), MelsecMcDataType.CC.FromBase);
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
                return new OperationResult<McDeviceAddress>(ex.Message);
            }

            return OperationResult.CreateSuccessResult(addressData);
        }

        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <param name = "isBit">비트 단위 요청이면 true입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<McDeviceAddress> ParseMelsecRFrom(string address, ushort length, bool isBit)
        {
            OperationResult<MelsecMcDataType, int> analysis = MelsecMcRNetAddressParser.AnalysisAddress(address);
            if (!analysis.IsSuccess)
                return OperationResult.CreateFailedResult<McDeviceAddress>(analysis);
            return OperationResult.CreateSuccessResult(new McDeviceAddress() { McDataType = analysis.Content1, AddressStart = analysis.Content2, Length = length });
        }

        /// <summary>CalculateComplexAddress 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        private static int CalculateComplexAddress(string address)
        {
            int add = 0;
            if (address.IndexOf(".") < 0)
            {
                if (address.Length <= 2)
                    add = Convert.ToInt32(address);
                else
                    add = Convert.ToInt32(address.Substring(0, address.Length - 2)) * 16 + Convert.ToInt32(address.Substring(address.Length - 2), 10);
            }
            else
            {
                add = Convert.ToInt32(address.Substring(0, address.IndexOf("."))) * 16;
                string bit = address.Substring(address.IndexOf(".") + 1);
                add += AddressParameters.CalculateBitStartIndex(bit);
            }

            return add;
        }

        /// <summary>그리고 우리는 실제 키너스의 주소에서 필요한 주소를 찾아냅니다.</summary>
        /// <param name = "address">킨스의 주소 데이터</param>
        /// <param name = "length">읽을 데이터 길이</param>
        /// <param name = "isBit">bool를 읽거나 쓰거나</param>
        /// <returns>성공 여부를 나타내는 결과 객체</returns>
        public static OperationResult<McDeviceAddress> ParseKeyenceFrom(string address, ushort length, bool isBit)
        {
            McDeviceAddress addressData = new McDeviceAddress();
            addressData.Length = length;
            try
            {
                switch (address[0])
                {
                    case 'M':
                    case 'm':
                    {
                        if (address[1] == 'R' || address[1] == 'r')
                        {
                            addressData.McDataType = MelsecMcDataType.Keyence_M;
                            addressData.AddressStart = CalculateComplexAddress(address.Substring(2));
                            break;
                        }
                        else
                        {
                            addressData.McDataType = MelsecMcDataType.Keyence_M;
                            addressData.AddressStart = Convert.ToInt32(address.Substring(1), MelsecMcDataType.Keyence_M.FromBase);
                            break;
                        }
                    }

                    case 'X':
                    case 'x':
                    {
                        addressData.McDataType = MelsecMcDataType.Keyence_X;
                        addressData.AddressStart = Convert.ToInt32(address.Substring(1), MelsecMcDataType.Keyence_X.FromBase);
                        break;
                    }

                    case 'Y':
                    case 'y':
                    {
                        addressData.McDataType = MelsecMcDataType.Keyence_Y;
                        addressData.AddressStart = Convert.ToInt32(address.Substring(1), MelsecMcDataType.Keyence_Y.FromBase);
                        break;
                    }

                    case 'B':
                    case 'b':
                    {
                        addressData.McDataType = MelsecMcDataType.Keyence_B;
                        addressData.AddressStart = Convert.ToInt32(address.Substring(1), MelsecMcDataType.Keyence_B.FromBase);
                        break;
                    }

                    case 'L':
                    case 'l':
                    {
                        if (address[1] == 'R' || address[1] == 'r')
                        {
                            addressData.McDataType = MelsecMcDataType.Keyence_L;
                            addressData.AddressStart = CalculateComplexAddress(address.Substring(2));
                            break;
                        }
                        else
                        {
                            addressData.McDataType = MelsecMcDataType.Keyence_L;
                            addressData.AddressStart = Convert.ToInt32(address.Substring(1), MelsecMcDataType.Keyence_L.FromBase);
                            break;
                        }
                    }

                    case 'S':
                    case 's':
                    {
                        if (address[1] == 'M' || address[1] == 'm')
                        {
                            addressData.McDataType = MelsecMcDataType.Keyence_SM;
                            addressData.AddressStart = Convert.ToInt32(address.Substring(2), MelsecMcDataType.Keyence_SM.FromBase);
                            break;
                        }
                        else if (address[1] == 'D' || address[1] == 'd')
                        {
                            addressData.McDataType = MelsecMcDataType.Keyence_SD;
                            addressData.AddressStart = Convert.ToInt32(address.Substring(2), MelsecMcDataType.Keyence_SD.FromBase);
                            break;
                        }
                        else
                        {
                            throw new Exception(ProtocolMessages.NotSupportedDataType);
                        }
                    }

                    case 'D':
                    case 'd':
                    {
                        if (address[1] == 'M' || address[1] == 'm')
                        {
                            addressData.McDataType = MelsecMcDataType.Keyence_D;
                            addressData.AddressStart = Convert.ToInt32(address.Substring(2), MelsecMcDataType.Keyence_D.FromBase);
                        }
                        else
                        {
                            addressData.McDataType = MelsecMcDataType.Keyence_D;
                            addressData.AddressStart = Convert.ToInt32(address.Substring(1), MelsecMcDataType.Keyence_D.FromBase);
                        }

                        break;
                    }

                    case 'E':
                    case 'e':
                    {
                        if (address[1] == 'M' || address[1] == 'm')
                        {
                            addressData.McDataType = MelsecMcDataType.Keyence_D;
                            addressData.AddressStart = Convert.ToInt32(address.Substring(2), MelsecMcDataType.Keyence_D.FromBase) + 100_000;
                            break;
                        }
                        else
                        {
                            throw new Exception(ProtocolMessages.NotSupportedDataType);
                        }
                    }

                    case 'F':
                    case 'f':
                    {
                        if (address[1] == 'M' || address[1] == 'm')
                        {
                            addressData.McDataType = MelsecMcDataType.Keyence_R;
                            addressData.AddressStart = Convert.ToInt32(address.Substring(2), MelsecMcDataType.Keyence_R.FromBase);
                            break;
                        }
                        else
                        {
                            throw new Exception(ProtocolMessages.NotSupportedDataType);
                        }
                    }

                    case 'R':
                    case 'r':
                    {
                        if (isBit)
                        {
                            // y의 자리입니다.
                            addressData.McDataType = MelsecMcDataType.Keyence_Y;
                            addressData.AddressStart = CalculateComplexAddress(address.Substring(1));
                            break;
                        }
                        else
                        {
                            // 글쓰기는 Mitsubishi R 레지스터입니다.
                            addressData.McDataType = MelsecMcDataType.Keyence_R;
                            addressData.AddressStart = Convert.ToInt32(address.Substring(1), MelsecMcDataType.Keyence_R.FromBase);
                            break;
                        }
                    }

                    case 'Z':
                    case 'z':
                    {
                        if (address[1] == 'R' || address[1] == 'r')
                        {
                            addressData.McDataType = MelsecMcDataType.Keyence_ZR;
                            addressData.AddressStart = Convert.ToInt32(address.Substring(2), MelsecMcDataType.Keyence_ZR.FromBase);
                            break;
                        }

                        if (address[1] == 'F' || address[1] == 'f')
                        {
                            addressData.McDataType = MelsecMcDataType.Keyence_ZR;
                            addressData.AddressStart = Convert.ToInt32(address.Substring(2), 10);
                            break;
                        }
                        else
                        {
                            throw new Exception(ProtocolMessages.NotSupportedDataType);
                        }
                    }

                    case 'W':
                    case 'w':
                    {
                        addressData.McDataType = MelsecMcDataType.Keyence_W;
                        addressData.AddressStart = Convert.ToInt32(address.Substring(1), MelsecMcDataType.Keyence_W.FromBase);
                        break;
                    }

                    case 'T':
                    case 't':
                    {
                        if (address[1] == 'N' || address[1] == 'n')
                        {
                            addressData.McDataType = MelsecMcDataType.Keyence_TN;
                            addressData.AddressStart = Convert.ToInt32(address.Substring(2), MelsecMcDataType.Keyence_TN.FromBase);
                            break;
                        }
                        else if (address[1] == 'S' || address[1] == 's')
                        {
                            addressData.McDataType = MelsecMcDataType.Keyence_TS;
                            addressData.AddressStart = Convert.ToInt32(address.Substring(2), MelsecMcDataType.Keyence_TS.FromBase);
                            break;
                        }
                        else if (address[1] == 'C' || address[1] == 'c')
                        {
                            addressData.McDataType = MelsecMcDataType.Keyence_TC;
                            addressData.AddressStart = Convert.ToInt32(address.Substring(2), MelsecMcDataType.Keyence_TC.FromBase);
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
                            addressData.McDataType = MelsecMcDataType.Keyence_CN;
                            addressData.AddressStart = Convert.ToInt32(address.Substring(2), MelsecMcDataType.Keyence_CN.FromBase);
                            break;
                        }
                        else if (address[1] == 'S' || address[1] == 's')
                        {
                            addressData.McDataType = MelsecMcDataType.Keyence_CS;
                            addressData.AddressStart = Convert.ToInt32(address.Substring(2), MelsecMcDataType.Keyence_CS.FromBase);
                            break;
                        }
                        else if (address[1] == 'C' || address[1] == 'c')
                        {
                            addressData.McDataType = MelsecMcDataType.Keyence_CC;
                            addressData.AddressStart = Convert.ToInt32(address.Substring(2), MelsecMcDataType.Keyence_CC.FromBase);
                            break;
                        }

                        if (address[1] == 'R' || address[1] == 'r')
                        {
                            addressData.McDataType = MelsecMcDataType.Keyence_SM;
                            addressData.AddressStart = CalculateComplexAddress(address.Substring(2));
                            break;
                        }
                        else if (address[1] == 'M' || address[1] == 'm')
                        {
                            addressData.McDataType = MelsecMcDataType.Keyence_SD;
                            addressData.AddressStart = Convert.ToInt32(address.Substring(2), MelsecMcDataType.Keyence_SD.FromBase);
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
                return new OperationResult<McDeviceAddress>(ex.Message);
            }

            return OperationResult.CreateSuccessResult(addressData);
        }

        /// <summary>실제 파송된 주소에서 MC 프로토콜 표준의 주소 객체를 파싱</summary>
        /// <param name = "address">Panasonic 주소 데이터 정보</param>
        /// <param name = "length">읽을 데이터 길이</param>
        /// <param name = "isBit">bool 타입의 읽기/쓰기 작업을 수행할 것인지</param>
        /// <returns>성공 여부를 나타내는 결과 객체</returns>
        public static OperationResult<McDeviceAddress> ParsePanasonicFrom(string address, ushort length, bool isBit)
        {
            McDeviceAddress addressData = new McDeviceAddress();
            addressData.Length = length;
            try
            {
                switch (address[0])
                {
                    case 'R':
                    case 'r':
                    {
                        int add = PanasonicAddressParser.CalculateComplexAddress(address.Substring(1));
                        if (add < 14400)
                        {
                            addressData.McDataType = MelsecMcDataType.Panasonic_R;
                            addressData.AddressStart = add;
                        }
                        else
                        {
                            addressData.McDataType = MelsecMcDataType.Panasonic_SM;
                            addressData.AddressStart = add - 14400;
                        }

                        break;
                    }

                    case 'X':
                    case 'x':
                    {
                        addressData.McDataType = MelsecMcDataType.Panasonic_X;
                        addressData.AddressStart = PanasonicAddressParser.CalculateComplexAddress(address.Substring(1));
                        break;
                    }

                    case 'Y':
                    case 'y':
                    {
                        addressData.McDataType = MelsecMcDataType.Panasonic_Y;
                        addressData.AddressStart = PanasonicAddressParser.CalculateComplexAddress(address.Substring(1));
                        break;
                    }

                    case 'L':
                    case 'l':
                    {
                        if (address[1] == 'D' || address[1] == 'd')
                        {
                            addressData.McDataType = MelsecMcDataType.Panasonic_LD;
                            addressData.AddressStart = Convert.ToInt32(address.Substring(2));
                            break;
                        }
                        else
                        {
                            addressData.McDataType = MelsecMcDataType.Panasonic_L;
                            addressData.AddressStart = PanasonicAddressParser.CalculateComplexAddress(address.Substring(1));
                        }

                        break;
                    }

                    case 'D':
                    case 'd':
                    {
                        int add = Convert.ToInt32(address.Substring(1));
                        if (add < 90000)
                        {
                            addressData.McDataType = MelsecMcDataType.Panasonic_DT;
                            addressData.AddressStart = Convert.ToInt32(address.Substring(1));
                        }
                        else
                        {
                            addressData.McDataType = MelsecMcDataType.Panasonic_SD;
                            addressData.AddressStart = Convert.ToInt32(address.Substring(1)) - 90000;
                        }

                        break;
                    }

                    case 'T':
                    case 't':
                    {
                        if (address[1] == 'N' || address[1] == 'n')
                        {
                            addressData.McDataType = MelsecMcDataType.Panasonic_TN;
                            addressData.AddressStart = Convert.ToInt32(address.Substring(2));
                            break;
                        }
                        else if (address[1] == 'S' || address[1] == 's')
                        {
                            addressData.McDataType = MelsecMcDataType.Panasonic_TS;
                            addressData.AddressStart = Convert.ToInt32(address.Substring(2));
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
                            addressData.McDataType = MelsecMcDataType.Panasonic_CN;
                            addressData.AddressStart = Convert.ToInt32(address.Substring(2));
                            break;
                        }
                        else if (address[1] == 'S' || address[1] == 's')
                        {
                            addressData.McDataType = MelsecMcDataType.Panasonic_CS;
                            addressData.AddressStart = Convert.ToInt32(address.Substring(2));
                            break;
                        }
                        else
                        {
                            throw new Exception(ProtocolMessages.NotSupportedDataType);
                        }
                    }

                    case 'S':
                    case 's':
                    {
                        if (address[1] == 'D' || address[1] == 'd')
                        {
                            addressData.McDataType = MelsecMcDataType.Panasonic_SD;
                            addressData.AddressStart = Convert.ToInt32(address.Substring(2));
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
                return new OperationResult<McDeviceAddress>(ex.Message);
            }

            return OperationResult.CreateSuccessResult(addressData);
        }
    }
}
