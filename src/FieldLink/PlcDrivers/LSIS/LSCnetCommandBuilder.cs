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
using static FieldLink.PlcDrivers.LSIS.LSCnetResponseParser;
using static FieldLink.PlcDrivers.LSIS.LSCnetAddressParser;
using static FieldLink.PlcDrivers.LSIS.LSCnetValueConverter;
using static FieldLink.PlcDrivers.LSIS.LSCnetDefinitions;

namespace FieldLink.PlcDrivers.LSIS
{
    /// <summary>LSCnet 요청 프레임을 생성합니다.</summary>
    public static class LSCnetCommandBuilder
    {
        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildReadByteCommand(byte station, string address, ushort length)
        {
            var analysisResult = AnalysisAddress(address);
            if (!analysisResult.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(analysisResult);
            List<byte> command = new List<byte>();
            command.Add(AsciiControl.ENQ); // ENQ
            command.AddRange(ProtocolBytes.BuildAsciiBytesFrom(station));
            command.Add(0x72); // command r
            command.Add(0x53); // 명령 타입: SB
            command.Add(0x42);
            command.AddRange(ProtocolBytes.BuildAsciiBytesFrom((byte)analysisResult.Content.Length));
            command.AddRange(Encoding.ASCII.GetBytes(analysisResult.Content));
            command.AddRange(ProtocolBytes.BuildAsciiBytesFrom((byte)length));
            command.Add(AsciiControl.EOT); // EOT
            AddBccTail(command); // BCC
            return OperationResult.CreateSuccessResult(command.ToArray());
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildReadIndividualCommand(byte station, string address) => BuildReadIndividualCommand(station, new string[] { address });
        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "addresses">addresses에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildReadIndividualCommand(byte station, string[] addresses)
        {
            List<byte> command = new List<byte>();
            command.Add(AsciiControl.ENQ); // ENQ
            command.AddRange(ProtocolBytes.BuildAsciiBytesFrom(station));
            command.Add(0x72); // command r
            command.Add(0x53); // 명령 타입: SS
            command.Add(0x53);
            command.AddRange(ProtocolBytes.BuildAsciiBytesFrom((byte)addresses.Length)); // 블록 수
            if (addresses.Length > 1)
            {
                foreach (var address in addresses)
                {
                    string add = address.StartsWith("%") ? address : "%" + address;
                    command.AddRange(ProtocolBytes.BuildAsciiBytesFrom((byte)add.Length));
                    command.AddRange(Encoding.ASCII.GetBytes(add));
                }
            }
            else
            {
                foreach (var address in addresses)
                {
                    var analysisResult = AnalysisAddress(address);
                    if (!analysisResult.IsSuccess)
                        return OperationResult.CreateFailedResult<byte[]>(analysisResult);
                    command.AddRange(ProtocolBytes.BuildAsciiBytesFrom((byte)analysisResult.Content.Length));
                    command.AddRange(Encoding.ASCII.GetBytes(analysisResult.Content));
                }
            }

            command.Add(AsciiControl.EOT); // EOT
            AddBccTail(command); // BCC
            return OperationResult.CreateSuccessResult(command.ToArray());
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildReadCommand(byte station, string address, ushort length)
        {
            var DataTypeResult = LSFastEnetAddressParser.GetDataTypeToAddress(address);
            if (!DataTypeResult.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(DataTypeResult);
            switch (DataTypeResult.Content)
            {
                case "Bit":
                    return BuildReadIndividualCommand(station, address);
                case "Word":
                case "DWord":
                case "LWord":
                case "Continuous":
                    return BuildReadByteCommand(station, address, length);
                default:
                    return new OperationResult<byte[]>(ProtocolMessages.NotSupportedDataType);
            }
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
            command.Add(AsciiControl.ENQ); // ENQ
            command.AddRange(ProtocolBytes.BuildAsciiBytesFrom(station));
            command.Add(0x77); // command w
            command.Add(0x53); // 명령 타입: S
            command.Add(0x42); // 명령 타입: B
            command.AddRange(ProtocolBytes.BuildAsciiBytesFrom((byte)analysisResult.Content.Length));
            command.AddRange(Encoding.ASCII.GetBytes(analysisResult.Content));
            command.AddRange(ProtocolBytes.BuildAsciiBytesFrom((byte)value.Length));
            command.AddRange(ProtocolBytes.BytesToAsciiBytes(value));
            command.Add(AsciiControl.EOT); // EOT
            AddBccTail(command); // BCC
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
            command.Add(AsciiControl.ENQ); // ENQ
            command.AddRange(ProtocolBytes.BuildAsciiBytesFrom(station));
            command.Add(0x77); // command w
            command.Add(0x53); // 명령 타입: S
            command.Add(0x53); // 명령 타입: S
            command.Add(0x30); // 블록 수
            command.Add(0x31);
            command.AddRange(ProtocolBytes.BuildAsciiBytesFrom((byte)analysisResult.Content.Length));
            command.AddRange(Encoding.ASCII.GetBytes(analysisResult.Content));
            command.AddRange(ProtocolBytes.BytesToAsciiBytes(value));
            command.Add(AsciiControl.EOT); // EOT
            AddBccTail(command); // BCC
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
