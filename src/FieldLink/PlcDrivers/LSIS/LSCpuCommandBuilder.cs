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
using static FieldLink.PlcDrivers.LSIS.LSCpuValueConverter;
using static FieldLink.PlcDrivers.LSIS.LSCpuAddressParser;
using static FieldLink.PlcDrivers.LSIS.LSCpuDefinitions;

namespace FieldLink.PlcDrivers.LSIS
{
    /// <summary>LSCpu 요청 프레임을 생성합니다.</summary>
    public static class LSCpuCommandBuilder
    {
        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildReadByteCommand(byte station, string address, ushort length)
        {
            List<byte> command = new List<byte>();
            byte crc = 0;
            int i;
            command.Clear();
            var MemoryType = GetMemoryType(address);
            var datatype = GetDataType(address);
            int address2 = HexToOct(address.Substring(1, address.Length - 1));
            command.Add(AsciiControl.STX); // STX
            command.AddRange(Encoding.ASCII.GetBytes(sprintf("r%C", select_data_code(MemoryType))));
            command.AddRange(Encoding.ASCII.GetBytes(string.Format("{0:X4}", (byte)0)));
            command.AddRange(Encoding.ASCII.GetBytes(string.Format("{0:X2}", (byte)0)));
            command.AddRange(Encoding.ASCII.GetBytes(string.Format("{0:X2}", (byte)GetDataSize2(address2, length, datatype))));
            for (i = 1; i <= 10; i++)
            {
                crc += command[i];
            }

            command.AddRange(Encoding.ASCII.GetBytes(string.Format("{0:X2}", crc)));
            command.Add(AsciiControl.ETX);
            return OperationResult.CreateSuccessResult(command.ToArray());
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildReadCommand(byte station, string address, ushort length)
        {
            return BuildReadByteCommand(station, address, length);
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteByteCommand(byte station, string address, byte[] value)
        {
            var analysisResult = AnalysisAddress(address);
            if (!analysisResult.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(analysisResult);
            List<byte> command = new List<byte>();
            string Buffer = new string (new char[60]);
            ushort crc = 0;
            int i;
            int commCountSend;
            float[] Src = new float[3];
            char[] v11 = new char[4];
            short v13;
            short ValueHex = BitConverter.ToInt16(value, 0);
            var datatype = GetDataType(analysisResult.Content);
            var MemoryType = GetMemoryType(analysisResult.Content);
            int address2 = int.Parse(analysisResult.Content.Substring(2, analysisResult.Content.Length - 2));
            if (MemoryType == 128)
            {
                Console.WriteLine("Memory Type Input Error. Memory Type = P, M, K, T, C, U, Z, S, L, N, D, R, ZR", address2);
            }
            else
            {
                command.Clear();
                command.Add(AsciiControl.STX); // STX
                command.AddRange(Encoding.ASCII.GetBytes(sprintf("w%C", select_data_code(MemoryType))));
                if (Buffer != null)
                {
                    // Buffer = string.Format("{0:X4}", address);
                    command.AddRange(Encoding.ASCII.GetBytes(string.Format("{0:X2}{1:X2}", GetDataSize((ushort)Convert.ToInt32(address2), 2), (byte)0)));
                }

                command.AddRange(Encoding.ASCII.GetBytes(string.Format("{0:X2}", (byte)0)));
                command.AddRange(Encoding.ASCII.GetBytes(string.Format("{0:X2}", (byte)GetDataSize2(address2, 1, datatype))));
                switch (datatype)
                {
                    case 1:
                        command.AddRange(Encoding.ASCII.GetBytes(string.Format("{0:X2}", (byte)ValueHex)));
                        commCountSend = 13;
                        break;
                    case 3:
                        v13 = (short)((ulong)ValueHex >> 16);
                        command.AddRange(Encoding.ASCII.GetBytes(string.Format("{0:X2}{1:X2}", (uint)ValueHex, (byte)(ValueHex))));
                        command.AddRange(Encoding.ASCII.GetBytes(string.Format("{0:X2}{1:X2}", (uint)v13, (ulong)ValueHex >> 24)));
                        commCountSend = 18;
                        break;
                    case 4:
                        Src[0] = (float)ValueHex;
                        Array.Copy(v11, Src, sizeof(char));
                        command.AddRange(Encoding.ASCII.GetBytes(string.Format("{0:X2}{1:X2}", (uint)v11[0], (uint)v11[1])));
                        command.AddRange(Encoding.ASCII.GetBytes(string.Format("{0:X2}{1:X2}", (uint)v11[2], (uint)v11[3])));
                        commCountSend = 18;
                        break;
                    case 5:
                        Src[0] = (float)ValueHex;
                        Array.Copy(v11, Src, sizeof(char));
                        command.AddRange(Encoding.ASCII.GetBytes(string.Format("{0:X2}{1:X2}", (uint)v11[3], (uint)v11[2])));
                        command.AddRange(Encoding.ASCII.GetBytes(string.Format("{0:X2}{1:X2}", (uint)v11[1], (uint)v11[0])));
                        commCountSend = 18;
                        break;
                    default:
                        command.AddRange(Encoding.ASCII.GetBytes(string.Format("{0:X2}{1:X2}", (sbyte)ValueHex, ValueHex >> 8)));
                        commCountSend = 14;
                        break;
                }

                for (i = 1; i <= commCountSend; i++)
                {
                    crc += command[i];
                }

                command.AddRange(Encoding.ASCII.GetBytes(string.Format("{0:X2}", (byte)crc)));
                command.Add(AsciiControl.ETX);
            }

            return OperationResult.CreateSuccessResult(command.ToArray());
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteOneCommand(byte station, string address, byte[] value)
        {
            var analysisResult = AnalysisAddress(address);
            if (!analysisResult.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(analysisResult);
            List<byte> command = new List<byte>();
            string Buffer = new string (new char[31]);
            ushort crc = 0;
            //int count;
            short v10;
            int i;
            var MemoryType = GetMemoryType(analysisResult.Content);
            int address2 = HexToOct(analysisResult.Content.Substring(2, analysisResult.Content.Length - 2));
            string ValueHex = value[0].ToString("X2");
            if (MemoryType == 128)
            {
                Console.WriteLine("Memory Type Input Error. Memory Type = P, M, K, T, C, U, Z, S, L, N, D, R, ZR", address);
            }
            else
            {
                command.Clear();
                command.Add(AsciiControl.STX); // STX
                if (ValueHex == "01")
                {
                    command.AddRange(Encoding.ASCII.GetBytes(sprintf("o%C", select_data_code(MemoryType))));
                }
                else
                {
                    command.AddRange(Encoding.ASCII.GetBytes(sprintf("n%C", select_data_code(MemoryType))));
                }

                if (MemoryType != 1)
                {
                    Buffer = string.Format("{0:X4}", address2 >> 4);
                    command.AddRange(Encoding.ASCII.GetBytes(string.Format("{0:X2}{1:X2}", GetDataSize((ushort)Convert.ToInt32(Buffer), 2), (byte)0)));
                }
                else
                {
                    command.AddRange(Encoding.ASCII.GetBytes(string.Format("{0:X2}{1:X2}", (uint)(sbyte)((ushort)address2 / 16), (ushort)((ushort)address2 / 16) >> 16)));
                }

                command.AddRange(Encoding.ASCII.GetBytes(string.Format("{0:X2}", (byte)0)));
                if (ValueHex != "00")
                {
                    v10 = (short)Math.Pow(2, address2 % 16);
                }
                else
                {
                    v10 = (short)(-1 - Math.Pow(2, address2 % 16));
                }

                command.AddRange(Encoding.ASCII.GetBytes(string.Format("{0:X2}{1:X2}", (sbyte)v10, HIBYTE(v10))));
                for (i = 1; i <= 12; i++)
                {
                    crc += command[i];
                }

                command.AddRange(Encoding.ASCII.GetBytes(string.Format("{0:X2}", (byte)crc)));
                command.Add(AsciiControl.ETX);
            }

            return OperationResult.CreateSuccessResult(command.ToArray());
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteCommand(byte station, string address, byte[] value)
        {
            var DataTypeResult = LSFastEnetAddressParser.GetDataTypeToAddress(address);
            if (!DataTypeResult.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(DataTypeResult);
            switch (DataTypeResult.Content)
            {
                case "Bit":
                    return BuildWriteOneCommand(station, address, value);
                case "Word":
                case "DWord":
                case "LWord":
                case "Continuous":
                    return BuildWriteByteCommand(station, address, value);
                default:
                    return new OperationResult<byte[]>(ProtocolMessages.NotSupportedDataType);
            }
        }
    }
}
