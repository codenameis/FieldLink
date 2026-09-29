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
using static FieldLink.PlcDrivers.FATEK.FatekProgramValueConverter;
using static FieldLink.PlcDrivers.FATEK.FatekProgramResponseParser;
using static FieldLink.PlcDrivers.FATEK.FatekControlCommandBuilder;
using static FieldLink.PlcDrivers.FATEK.FatekProgramControlCommandBuilder;
using static FieldLink.PlcDrivers.FATEK.FatekProgramControlResponseParser;

namespace FieldLink.PlcDrivers.FATEK
{
    /// <summary>FatekProgram 요청 프레임을 생성합니다.</summary>
    public static class FatekProgramCommandBuilder
    {
        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "cmd">포장할 명령 바이트입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackFatekCommand(byte station, string cmd)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append((char)AsciiControl.STX);
            stringBuilder.Append(station.ToString("X2"));
            byte[] buffer = new byte[6 + cmd.Length];
            buffer[0] = AsciiControl.STX;
            buffer[1] = ProtocolBytes.BuildAsciiBytesFrom(station)[0];
            buffer[2] = ProtocolBytes.BuildAsciiBytesFrom(station)[1];
            Encoding.ASCII.GetBytes(cmd).CopyTo(buffer, 3);
            AdditiveChecksum.WriteAscii(buffer, 0, buffer.Length - 3);
            buffer[buffer.Length - 1] = AsciiControl.ETX;
            return buffer;
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<List<byte[]>> BuildReadWordCommand(byte station, string address, ushort length)
        {
            station = (byte)AddressParameters.ExtractParameter(ref address, "s", station);
            OperationResult<FatekProgramAddress> addressAnalysis = FatekProgramAddress.ParseFrom(address, length);
            if (!addressAnalysis.IsSuccess)
                return addressAnalysis.ConvertFailed<List<byte[]>>();
            List<byte[]> contentArray = new List<byte[]>();
            int[] splits = ProtocolBytes.SplitIntegerToArray(length, 64);
            for (int i = 0; i < splits.Length; i++)
            {
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.Append("46");
                stringBuilder.Append(splits[i].ToString("X2"));
                if (addressAnalysis.Content.DataCode.StartsWith("X") || addressAnalysis.Content.DataCode.StartsWith("Y") || addressAnalysis.Content.DataCode.StartsWith("M") || addressAnalysis.Content.DataCode.StartsWith("S") || addressAnalysis.Content.DataCode.StartsWith("T") || addressAnalysis.Content.DataCode.StartsWith("C"))
                {
                    stringBuilder.Append("W");
                }

                stringBuilder.Append(addressAnalysis.Content.ToString());
                contentArray.Add(PackFatekCommand(station, stringBuilder.ToString()));
                if (addressAnalysis.Content.DataCode.StartsWith("X") || addressAnalysis.Content.DataCode.StartsWith("Y") || addressAnalysis.Content.DataCode.StartsWith("M") || addressAnalysis.Content.DataCode.StartsWith("S") || addressAnalysis.Content.DataCode.StartsWith("T") || addressAnalysis.Content.DataCode.StartsWith("C"))
                {
                    addressAnalysis.Content.AddressStart += splits[i] * 16;
                }
                else
                {
                    addressAnalysis.Content.AddressStart += splits[i];
                }
            }

            return OperationResult.CreateSuccessResult(contentArray);
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<List<byte[]>> BuildReadBoolCommand(byte station, string address, ushort length)
        {
            station = (byte)AddressParameters.ExtractParameter(ref address, "s", station);
            OperationResult<FatekProgramAddress> addressAnalysis = FatekProgramAddress.ParseFrom(address, length);
            if (!addressAnalysis.IsSuccess)
                return addressAnalysis.ConvertFailed<List<byte[]>>();
            List<byte[]> contentArray = new List<byte[]>();
            int[] splits = ProtocolBytes.SplitIntegerToArray(length, 255);
            for (int i = 0; i < splits.Length; i++)
            {
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.Append("44");
                stringBuilder.Append(splits[i].ToString("X2"));
                stringBuilder.Append(addressAnalysis.Content.ToString());
                contentArray.Add(PackFatekCommand(station, stringBuilder.ToString()));
                addressAnalysis.Content.AddressStart += splits[i];
            }

            return OperationResult.CreateSuccessResult(contentArray);
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteBoolCommand(byte station, string address, bool[] value)
        {
            station = (byte)AddressParameters.ExtractParameter(ref address, "s", station);
            OperationResult<FatekProgramAddress> addressAnalysis = FatekProgramAddress.ParseFrom(address, 0);
            if (!addressAnalysis.IsSuccess)
                return addressAnalysis.ConvertFailed<byte[]>();
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("45");
            stringBuilder.Append(value.Length.ToString("X2"));
            stringBuilder.Append(addressAnalysis.Content.ToString());
            for (int i = 0; i < value.Length; i++)
            {
                stringBuilder.Append(value[i] ? "1" : "0");
            }

            return OperationResult.CreateSuccessResult(PackFatekCommand(station, stringBuilder.ToString()));
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteByteCommand(byte station, string address, byte[] value)
        {
            station = (byte)AddressParameters.ExtractParameter(ref address, "s", station);
            OperationResult<FatekProgramAddress> addressAnalysis = FatekProgramAddress.ParseFrom(address, 0);
            if (!addressAnalysis.IsSuccess)
                return addressAnalysis.ConvertFailed<byte[]>();
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("47");
            stringBuilder.Append((value.Length / 2).ToString("X2"));
            if (addressAnalysis.Content.DataCode.StartsWith("X") || addressAnalysis.Content.DataCode.StartsWith("Y") || addressAnalysis.Content.DataCode.StartsWith("M") || addressAnalysis.Content.DataCode.StartsWith("S") || addressAnalysis.Content.DataCode.StartsWith("T") || addressAnalysis.Content.DataCode.StartsWith("C"))
            {
                stringBuilder.Append("W");
            }

            stringBuilder.Append(addressAnalysis.Content.ToString());
            byte[] buffer = new byte[value.Length * 2];
            for (int i = 0; i < value.Length / 2; i++)
            {
                ProtocolBytes.BuildAsciiBytesFrom(BitConverter.ToUInt16(value, i * 2)).CopyTo(buffer, 4 * i);
            }

            stringBuilder.Append(Encoding.ASCII.GetString(buffer));
            return OperationResult.CreateSuccessResult(PackFatekCommand(station, stringBuilder.ToString()));
        }
    }
}
