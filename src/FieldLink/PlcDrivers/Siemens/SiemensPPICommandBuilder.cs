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
using static FieldLink.PlcDrivers.Siemens.SiemensPPIAddressParser;
using static FieldLink.PlcDrivers.Siemens.SiemensPPIResponseParser;

namespace FieldLink.PlcDrivers.Siemens
{
    /// <summary>SiemensPPI 요청 프레임을 생성합니다.</summary>
    public static class SiemensPPICommandBuilder
    {
        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <param name = "isBit">비트 단위 요청이면 true입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildReadCommand(byte station, string address, ushort length, bool isBit)
        {
            OperationResult<S7DeviceAddress> analysis = AnalysisAddress(address);
            if (!analysis.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(analysis);
            return BuildReadCommand(station, analysis.Content, length, isBit);
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <param name = "isBit">비트 단위 요청이면 true입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildReadCommand(byte station, S7DeviceAddress address, ushort length, bool isBit)
        {
            OperationResult validation = S7DeviceAddress.ValidateWireAddress(address);
            if (!validation.IsSuccess)
                return validation.ConvertFailed<byte[]>();
            if (address.DbBlock > byte.MaxValue)
                return new OperationResult<byte[]>("PPI DB 번호가 1바이트 전송 필드 범위를 초과합니다.");
            byte[] _PLCCommand = new byte[33];
            _PLCCommand[0] = 0x68;
            _PLCCommand[1] = BitConverter.GetBytes(_PLCCommand.Length - 6)[0];
            _PLCCommand[2] = BitConverter.GetBytes(_PLCCommand.Length - 6)[0];
            _PLCCommand[3] = 0x68;
            _PLCCommand[4] = station;
            _PLCCommand[5] = 0x00;
            _PLCCommand[6] = 0x6C;
            _PLCCommand[7] = 0x32;
            _PLCCommand[8] = 0x01;
            _PLCCommand[9] = 0x00;
            _PLCCommand[10] = 0x00;
            _PLCCommand[11] = 0x00;
            _PLCCommand[12] = 0x00;
            _PLCCommand[13] = 0x00;
            _PLCCommand[14] = 0x0E;
            _PLCCommand[15] = 0x00;
            _PLCCommand[16] = 0x00;
            _PLCCommand[17] = 0x04;
            _PLCCommand[18] = 0x01;
            _PLCCommand[19] = 0x12;
            _PLCCommand[20] = 0x0A;
            _PLCCommand[21] = 0x10;
            _PLCCommand[22] = isBit ? (byte)0x01 : (byte)0x02;
            _PLCCommand[23] = 0x00;
            _PLCCommand[24] = BitConverter.GetBytes(length)[0];
            _PLCCommand[25] = BitConverter.GetBytes(length)[1];
            _PLCCommand[26] = (byte)address.DbBlock;
            _PLCCommand[27] = address.DataCode;
            _PLCCommand[28] = BitConverter.GetBytes(address.AddressStart)[2];
            _PLCCommand[29] = BitConverter.GetBytes(address.AddressStart)[1];
            _PLCCommand[30] = BitConverter.GetBytes(address.AddressStart)[0];
            int count = 0;
            for (int i = 4; i < 31; i++)
            {
                count += _PLCCommand[i];
            }

            _PLCCommand[31] = BitConverter.GetBytes(count)[0];
            _PLCCommand[32] = 0x16;
            return OperationResult.CreateSuccessResult(_PLCCommand);
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "values">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteCommand(byte station, string address, byte[] values)
        {
            OperationResult<S7DeviceAddress> analysis = AnalysisAddress(address);
            if (!analysis.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(analysis);
            int length = values.Length;
            // 68 21 21 68 02 00 6C 32 01 00 00 00 00 00 0E 00 00 04 01 12 0A 10
            byte[] _PLCCommand = new byte[37 + values.Length];
            _PLCCommand[0] = 0x68;
            _PLCCommand[1] = BitConverter.GetBytes(_PLCCommand.Length - 6)[0];
            _PLCCommand[2] = BitConverter.GetBytes(_PLCCommand.Length - 6)[0];
            _PLCCommand[3] = 0x68;
            _PLCCommand[4] = station;
            _PLCCommand[5] = 0x00;
            _PLCCommand[6] = 0x7C;
            _PLCCommand[7] = 0x32;
            _PLCCommand[8] = 0x01;
            _PLCCommand[9] = 0x00;
            _PLCCommand[10] = 0x00;
            _PLCCommand[11] = 0x00;
            _PLCCommand[12] = 0x00;
            _PLCCommand[13] = 0x00;
            _PLCCommand[14] = 0x0E;
            _PLCCommand[15] = 0x00;
            _PLCCommand[16] = (byte)(values.Length + 4);
            _PLCCommand[17] = 0x05;
            _PLCCommand[18] = 0x01;
            _PLCCommand[19] = 0x12;
            _PLCCommand[20] = 0x0A;
            _PLCCommand[21] = 0x10;
            _PLCCommand[22] = 0x02;
            _PLCCommand[23] = 0x00;
            _PLCCommand[24] = BitConverter.GetBytes(length)[0];
            _PLCCommand[25] = BitConverter.GetBytes(length)[1];
            _PLCCommand[26] = (byte)analysis.Content.DbBlock;
            _PLCCommand[27] = analysis.Content.DataCode;
            _PLCCommand[28] = BitConverter.GetBytes(analysis.Content.AddressStart)[2];
            _PLCCommand[29] = BitConverter.GetBytes(analysis.Content.AddressStart)[1];
            _PLCCommand[30] = BitConverter.GetBytes(analysis.Content.AddressStart)[0];
            _PLCCommand[31] = 0x00;
            _PLCCommand[32] = 0x04;
            _PLCCommand[33] = BitConverter.GetBytes(length * 8)[1];
            _PLCCommand[34] = BitConverter.GetBytes(length * 8)[0];
            values.CopyTo(_PLCCommand, 35);
            int count = 0;
            for (int i = 4; i < _PLCCommand.Length - 2; i++)
            {
                count += _PLCCommand[i];
            }

            _PLCCommand[_PLCCommand.Length - 2] = BitConverter.GetBytes(count)[0];
            _PLCCommand[_PLCCommand.Length - 1] = 0x16;
            return OperationResult.CreateSuccessResult(_PLCCommand);
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "values">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteCommand(byte station, string address, bool[] values)
        {
            OperationResult<S7DeviceAddress> analysis = AnalysisAddress(address);
            if (!analysis.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(analysis);
            byte[] bytesValue = ProtocolBytes.BoolArrayToByte(values);
            // 68 21 21 68 02 00 6C 32 01 00 00 00 00 00 0E 00 00 04 01 12 0A 10
            byte[] _PLCCommand = new byte[37 + bytesValue.Length];
            _PLCCommand[0] = 0x68;
            _PLCCommand[1] = BitConverter.GetBytes(_PLCCommand.Length - 6)[0];
            _PLCCommand[2] = BitConverter.GetBytes(_PLCCommand.Length - 6)[0];
            _PLCCommand[3] = 0x68;
            _PLCCommand[4] = station;
            _PLCCommand[5] = 0x00;
            _PLCCommand[6] = 0x7C;
            _PLCCommand[7] = 0x32;
            _PLCCommand[8] = 0x01;
            _PLCCommand[9] = 0x00;
            _PLCCommand[10] = 0x00;
            _PLCCommand[11] = 0x00;
            _PLCCommand[12] = 0x00;
            _PLCCommand[13] = 0x00;
            _PLCCommand[14] = 0x0E;
            _PLCCommand[15] = 0x00;
            _PLCCommand[16] = 0x05;
            _PLCCommand[17] = 0x05;
            _PLCCommand[18] = 0x01;
            _PLCCommand[19] = 0x12;
            _PLCCommand[20] = 0x0A;
            _PLCCommand[21] = 0x10;
            _PLCCommand[22] = 0x01;
            _PLCCommand[23] = 0x00;
            _PLCCommand[24] = BitConverter.GetBytes(values.Length)[0];
            _PLCCommand[25] = BitConverter.GetBytes(values.Length)[1];
            _PLCCommand[26] = (byte)analysis.Content.DbBlock;
            _PLCCommand[27] = analysis.Content.DataCode;
            _PLCCommand[28] = BitConverter.GetBytes(analysis.Content.AddressStart)[2];
            _PLCCommand[29] = BitConverter.GetBytes(analysis.Content.AddressStart)[1];
            _PLCCommand[30] = BitConverter.GetBytes(analysis.Content.AddressStart)[0];
            _PLCCommand[31] = 0x00;
            _PLCCommand[32] = 0x03;
            _PLCCommand[33] = BitConverter.GetBytes(values.Length)[1];
            _PLCCommand[34] = BitConverter.GetBytes(values.Length)[0];
            bytesValue.CopyTo(_PLCCommand, 35);
            int count = 0;
            for (int i = 4; i < _PLCCommand.Length - 2; i++)
            {
                count += _PLCCommand[i];
            }

            _PLCCommand[_PLCCommand.Length - 2] = BitConverter.GetBytes(count)[0];
            _PLCCommand[_PLCCommand.Length - 1] = 0x16;
            return OperationResult.CreateSuccessResult(_PLCCommand);
        }

        /// <summary>GetExecuteConfirm 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] GetExecuteConfirm(byte station)
        {
            byte[] buffer = new byte[]
            {
                0x10,
                0x02,
                0x00,
                0x5C,
                0x5E,
                0x16
            };
            buffer[1] = station;
            int count = 0;
            for (int i = 1; i < 4; i++)
            {
                count += buffer[i];
            }

            buffer[4] = (byte)count;
            return buffer;
        }
    }
}
