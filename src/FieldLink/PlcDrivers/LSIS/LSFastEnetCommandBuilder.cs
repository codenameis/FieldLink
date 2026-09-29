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
using static FieldLink.PlcDrivers.LSIS.LSFastEnetAddressParser;
using static FieldLink.PlcDrivers.LSIS.LSFastEnetValueConverter;
using static FieldLink.PlcDrivers.LSIS.LSFastEnetResponseParser;
using static FieldLink.PlcDrivers.LSIS.LSFastEnetDefinitions;

namespace FieldLink.PlcDrivers.LSIS
{
    /// <summary>LSFastEnet 요청 프레임을 생성합니다.</summary>
    public static class LSFastEnetCommandBuilder
    {
        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "dataType">dataType에 사용할 입력값입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildReadIndividualCommand(byte dataType, string address)
        {
            return BuildReadIndividualCommand(dataType, new string[] { address });
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "dataType">dataType에 사용할 입력값입니다.</param>
        /// <param name = "addresses">addresses에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildReadIndividualCommand(byte dataType, string[] addresses)
        {
            MemoryStream ms = new MemoryStream();
            ms.WriteByte(0x54); // 읽기
            ms.WriteByte(0x00);
            ms.WriteByte(dataType);
            ms.WriteByte(0x00);
            ms.WriteByte(0x00); // 예약
            ms.WriteByte(0x00);
            ms.WriteByte((byte)addresses.Length); // 요청 블록 수: 1
            ms.WriteByte(0x00);
            foreach (string address in addresses)
            {
                var analysisResult = AnalysisAddress(address);
                if (!analysisResult.IsSuccess)
                    return OperationResult.CreateFailedResult<byte[]>(analysisResult);
                ms.WriteByte((byte)analysisResult.Content.Length); //  변수 길이
                ms.WriteByte(0x00);
                byte[] buffer = Encoding.ASCII.GetBytes(analysisResult.Content);
                ms.Write(buffer, 0, buffer.Length);
            }

            return OperationResult.CreateSuccessResult(ms.ToArray());
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildReadByteCommand(string address, ushort length)
        {
            var analysisResult = AnalysisAddress(address);
            if (!analysisResult.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(analysisResult);
            var DataTypeResult = GetDataTypeToAddress(address);
            if (!DataTypeResult.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(DataTypeResult);
            byte[] command = new byte[12 + analysisResult.Content.Length];
            command[0] = 0x54; // 읽기
            command[1] = 0x00;
            switch (DataTypeResult.Content)
            {
                case "Bit":
                    command[2] = 0x00;
                    break;
                case "Word":
                case "DWord":
                case "LWord":
                case "Continuous":
                    command[2] = 0x14;
                    break; // 계속 읽기
                default:
                    break;
            }

            command[3] = 0x00;
            command[4] = 0x00; // 예약
            command[5] = 0x00;
            command[6] = 0x01; // 요청 블록 수: 1
            command[7] = 0x00;
            command[8] = (byte)analysisResult.Content.Length; //  변수 길이
            command[9] = 0x00;
            Encoding.ASCII.GetBytes(analysisResult.Content).CopyTo(command, 10);
            BitConverter.GetBytes(length).CopyTo(command, command.Length - 2);
            return OperationResult.CreateSuccessResult(command);
        }

        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "command">포장할 명령 바이트입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackCommandWithHeader(FastEnetFrameOptions context, byte[] command)
        {
            byte[] buffer = new byte[command.Length + 20];
            Encoding.ASCII.GetBytes(context.CompanyID).CopyTo(buffer, 0);
            switch (context.cpuInfo)
            {
                case LSCpuInfo.XGK:
                    buffer[12] = 0xA0;
                    break;
                case LSCpuInfo.XGI:
                    buffer[12] = 0xA4;
                    break;
                case LSCpuInfo.XGR:
                    buffer[12] = 0xA8;
                    break;
                case LSCpuInfo.XGB_MK:
                    buffer[12] = 0xB0;
                    break;
                case LSCpuInfo.XGB_IEC:
                    buffer[12] = 0xB4;
                    break;
                default:
                    break;
            }

            buffer[13] = 0x33;
            BitConverter.GetBytes((short)command.Length).CopyTo(buffer, 16);
            buffer[18] = (byte)(context.baseNo * 16 + context.slotNo);
            int count = 0;
            for (int i = 0; i < 19; i++)
            {
                count += buffer[i];
            }

            buffer[19] = (byte)count;
            command.CopyTo(buffer, 20);
            return buffer;
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteByteCommand(FastEnetFrameOptions context, string address, byte[] data)
        {
            OperationResult<string> analysisResult = AnalysisAddress(address);
            if (!analysisResult.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(analysisResult);
            var DataTypeResult = GetDataTypeToAddress(address);
            if (!DataTypeResult.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(DataTypeResult);
            byte[] command = new byte[12 + analysisResult.Content.Length + data.Length];
            switch (DataTypeResult.Content)
            {
                case "Bit":
                    command[2] = 0x00;
                    break;
                case "Byte":
                    command[2] = 0x01;
                    break;
                case "Word":
                    command[2] = 0x02;
                    break;
                case "DWord":
                    command[2] = 0x03;
                    break;
                case "LWord":
                    command[2] = 0x04;
                    break;
                case "Continuous":
                    command[2] = 0x14;
                    break;
                default:
                    break;
            }

            command[0] = 0x58; // 쓰기
            command[1] = 0x00;
            command[3] = 0x00;
            command[4] = 0x00; // 예약
            command[5] = 0x00;
            command[6] = 0x01; // 요청 블록 수: 1
            command[7] = 0x00;
            command[8] = (byte)analysisResult.Content.Length; //  변수 길이
            command[9] = 0x00;
            Encoding.ASCII.GetBytes(analysisResult.Content).CopyTo(command, 10);
            // 데이터 길이는 2바이트 필드다. Int32 전체를 복사하면 1바이트 비트 쓰기에서 버퍼를 넘는다.
            int dataLengthOffset = command.Length - 2 - data.Length;
            command[dataLengthOffset] = (byte)data.Length;
            command[dataLengthOffset + 1] = (byte)(data.Length >> 8);
            data.CopyTo(command, command.Length - data.Length);
            return OperationResult.CreateSuccessResult(command);
        }
    }
}
