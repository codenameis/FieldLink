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
using static FieldLink.PlcDrivers.LSIS.LSCpuResponseParser;
using static FieldLink.PlcDrivers.LSIS.LSCpuAddressParser;
using static FieldLink.PlcDrivers.LSIS.LSCpuCommandBuilder;
using static FieldLink.PlcDrivers.LSIS.LSCpuDefinitions;

namespace FieldLink.PlcDrivers.LSIS
{
    /// <summary>LSCpu 프로토콜의 값과 바이트 표현을 변환합니다.</summary>
    public static class LSCpuValueConverter
    {
        /// <summary>GetBytesFromHex 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "IP">IP에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] GetBytesFromHex(string IP)
        {
            byte[] array = new byte[IP.Length / 2];
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = Convert.ToByte(IP.Substring(i * 2, 2), 16);
            }

            return array;
        }

        /// <summary>AddBccTail 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "command">포장할 명령 바이트입니다.</param>
        internal static void AddBccTail(List<byte> command)
        {
            int sum = 0;
            for (int i = 0; i < command.Count; i++)
            {
                sum += command[i];
            }

            command.AddRange(ProtocolBytes.BuildAsciiBytesFrom((byte)sum));
        }

        /// <summary>GetMemoryType 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static int GetMemoryType(string address)
        {
            switch (address[0])
            {
                case 'P':
                    return 0;
                case 'M':
                    return 1;
                case 'K':
                    return 2;
                case 'F':
                    return 3;
                case 'T':
                    return 4;
                case 'C':
                    return 5;
                case 'U':
                    return 6;
                case 'Z':
                    return 7;
                case 'S':
                    return 8;
                case 'L':
                    return 9;
                case 'N':
                    return 10;
                case 'D':
                    return 11;
                case 'R':
                    return 12;
                //case 'ZR':
                //    return 13;
                default:
                    return 128;
            }
        }

        /// <summary>GetDataType 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static int GetDataType(string address)
        {
            switch (address[1])
            {
                case 'X':
                    return 1;
                case 'W':
                    return 2;
                case 'D':
                    return 4;
                case 'F':
                    return 8;
                default:
                    break;
            }

            return 1;
        }

        /// <summary>sprintf 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "input">input에 사용할 입력값입니다.</param>
        /// <param name = "inpVars">inpVars에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static string sprintf(string input, params object[] inpVars)
        {
            int i = -1;
            input = Regex.Replace(input, "%.", m => "{" + ++i + "}");
            return string.Format(input, inpVars);
        }

        /// <summary>select_data_code 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "MemoryType">MemoryType에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static char select_data_code(int MemoryType)
        {
            char result; // al
            switch (MemoryType)
            {
                case 0:
                    result = (char)104;
                    break;
                case 2:
                    result = (char)107;
                    break;
                case 3:
                    result = (char)110;
                    break;
                case 4:
                    result = (char)100;
                    break;
                case 5:
                    result = (char)109;
                    break;
                case 6:
                    result = (char)113;
                    break;
                case 7:
                    result = (char)122;
                    break;
                case 8:
                    result = (char)111;
                    break;
                case 9:
                    result = (char)106;
                    break;
                case 10:
                    result = (char)112;
                    break;
                case 11:
                    result = (char)97;
                    break;
                case 12:
                    result = (char)114;
                    break;
                case 13:
                    result = (char)123;
                    break;
                default:
                    result = (char)105;
                    break;
            }

            return result;
        }

        /// <summary>GetDataSize 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "datatype">datatype에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static int GetDataSize(int address, int datatype)
        {
            if (datatype == 2)
            {
                return 2 * address;
            }

            if (datatype > 2 && datatype <= 5)
            {
                return 4 * address;
            }

            return 10 * address / 8;
        }

        /// <summary>GetDataSize2 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "Staraddress">Staraddress에 사용할 입력값입니다.</param>
        /// <param name = "DataSize">DataSize에 사용할 입력값입니다.</param>
        /// <param name = "datatype">datatype에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static int GetDataSize2(int Staraddress, int DataSize, int datatype)
        {
            int v4;
            int v5;
            if (datatype == 2)
            {
                return 2 * DataSize;
            }

            if (datatype == 3 || datatype == 4 || datatype == 5)
            {
                return 4 * DataSize;
            }

            v5 = 10 * DataSize / 8;
            v4 = (8 - 10 * Staraddress % 8) % 8;
            if (v4 + 8 * v5 < 10 * DataSize)
            {
                ++v5;
            }

            if (v4 != 0)
            {
                ++v5;
            }

            return v5;
        }

        /// <summary>HIBYTE 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "n">n에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static byte? HIBYTE(short n)
        {
            byte[] bytes = new byte[4];
            bytes[0] = (byte)(n >> 24 & 0xFF);
            bytes[1] = (byte)(n >> 16 & 0xFF);
            bytes[2] = (byte)(n >> 8 & 0xFF);
            bytes[3] = (byte)(n & 0xFF);
            if ((byte)((n >> 8) & 0xFF) != 255 && (byte)((n >> 8) & 0xFF) != 0)
                return (byte)((n >> 8) & 0xFF);
            if ((byte)(n & 0xFF) != 255 && (byte)(n & 0xFF) != 0)
                return (byte)(n & 0xFF);
            return bytes[3];
        }

        /// <summary>HexToOct 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "hexNum">hexNum에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static int HexToOct(string hexNum)
        {
            // 헥사데시멀에서 소수점으로 변환하는 지도
            Dictionary<char, int> hexToDecMap = new Dictionary<char, int>()
            {
                {
                    '0',
                    0
                },
                {
                    '1',
                    1
                },
                {
                    '2',
                    2
                },
                {
                    '3',
                    3
                },
                {
                    '4',
                    4
                },
                {
                    '5',
                    5
                },
                {
                    '6',
                    6
                },
                {
                    '7',
                    7
                },
                {
                    '8',
                    8
                },
                {
                    '9',
                    9
                },
                {
                    'A',
                    10
                },
                {
                    'B',
                    11
                },
                {
                    'C',
                    12
                },
                {
                    'D',
                    13
                },
                {
                    'E',
                    14
                },
                {
                    'F',
                    15
                }
            };
            // 헥사데시멀 숫자를 십진수로 변환
            int decimalNum = 0;
            foreach (char digit in hexNum)
            {
                decimalNum = decimalNum * 16 + hexToDecMap[digit];
            }

            //// 십진수를 8진수로 변환
            //string octalNum = "";
            //while (decimalNum > 0)
            //{
            //    int octalDigit = decimalNum % 8;
            //    octalNum = octalDigit.ToString() + octalNum;
            //    decimalNum /= 8;
            //}
            return decimalNum;
        }
    }
}
