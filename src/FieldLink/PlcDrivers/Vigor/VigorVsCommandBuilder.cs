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
using static FieldLink.PlcDrivers.Vigor.VigorVsResponseParser;
using static FieldLink.PlcDrivers.Vigor.VigorVsAddressParser;

namespace FieldLink.PlcDrivers.Vigor
{
    /// <summary>VigorVs 요청 프레임을 생성합니다.</summary>
    public static class VigorVsCommandBuilder
    {
        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "command">포장할 명령 바이트입니다.</param>
        /// <param name = "code">code에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackCommand(byte[] command, byte code = 0x02)
        {
            if (command == null)
                command = new byte[0];
            MemoryStream ms = new MemoryStream();
            ms.WriteByte(0x10);
            ms.WriteByte(code);
            int sum = 0;
            for (int i = 0; i < command.Length; i++)
            {
                sum += command[i];
                ms.WriteByte(command[i]);
                if (command[i] == 0x10)
                    ms.WriteByte(command[i]);
            }

            ms.WriteByte(0x10);
            ms.WriteByte(0x03);
            byte[] crc = Encoding.ASCII.GetBytes((sum % 256).ToString("X2"));
            ms.WriteByte(crc[0]);
            ms.WriteByte(crc[1]);
            return ms.ToArray();
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <param name = "isBool">isBool에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<List<byte[]>> BuildReadCommand(byte station, string address, ushort length, bool isBool)
        {
            OperationResult<VigorAddress> analysis = VigorAddress.ParseFrom(address, length, isBool);
            if (!analysis.IsSuccess)
                return OperationResult.CreateFailedResult<List<byte[]>>(analysis);
            int[] splits = ProtocolBytes.SplitIntegerToArray(length, isBool ? 1024 : analysis.Content.DataCode == 0xAD ? 32 : 64);
            List<byte[]> result = new List<byte[]>();
            for (int i = 0; i < splits.Length; i++)
            {
                byte[] add = GetBytesAddress(analysis.Content.AddressStart);
                byte[] buffer = new byte[10];
                buffer[0] = station;
                buffer[1] = 0x07;
                buffer[2] = 0x00;
                buffer[3] = isBool ? (byte)0x21 : (byte)0x20;
                buffer[4] = analysis.Content.DataCode;
                buffer[5] = add[2];
                buffer[6] = add[1];
                buffer[7] = add[0];
                buffer[8] = BitConverter.GetBytes(splits[i])[0];
                buffer[9] = BitConverter.GetBytes(splits[i])[1];
                result.Add(PackCommand(buffer));
                analysis.Content.AddressStart += splits[i];
            }

            return OperationResult.CreateSuccessResult(result);
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteWordCommand(byte station, string address, byte[] value)
        {
            OperationResult<VigorAddress> analysis = VigorAddress.ParseFrom(address, 1, false);
            if (!analysis.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(analysis);
            byte[] add = GetBytesAddress(analysis.Content.AddressStart);
            byte[] buffer = new byte[10 + value.Length];
            buffer[0] = station;
            buffer[1] = BitConverter.GetBytes(7 + value.Length)[0];
            buffer[2] = BitConverter.GetBytes(7 + value.Length)[1];
            buffer[3] = 0x28;
            buffer[4] = analysis.Content.DataCode;
            buffer[5] = add[2];
            buffer[6] = add[1];
            buffer[7] = add[0];
            if (analysis.Content.DataCode == 0xAD)
            {
                buffer[8] = BitConverter.GetBytes(value.Length / 4)[0];
                buffer[9] = BitConverter.GetBytes(value.Length / 4)[1];
            }
            else
            {
                buffer[8] = BitConverter.GetBytes(value.Length / 2)[0];
                buffer[9] = BitConverter.GetBytes(value.Length / 2)[1];
            }

            value.CopyTo(buffer, 10);
            return OperationResult.CreateSuccessResult(PackCommand(buffer));
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteBoolCommand(byte station, string address, bool[] value)
        {
            OperationResult<VigorAddress> analysis = VigorAddress.ParseFrom(address, 1, true);
            if (!analysis.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(analysis);
            byte[] add = GetBytesAddress(analysis.Content.AddressStart);
            byte[] value_byte = value.ToByteArray();
            byte[] buffer = new byte[10 + value_byte.Length];
            buffer[0] = station;
            buffer[1] = BitConverter.GetBytes(7 + value_byte.Length)[0];
            buffer[2] = BitConverter.GetBytes(7 + value_byte.Length)[1];
            buffer[3] = 0x29;
            buffer[4] = analysis.Content.DataCode;
            buffer[5] = add[2];
            buffer[6] = add[1];
            buffer[7] = add[0];
            buffer[8] = BitConverter.GetBytes(value.Length)[0];
            buffer[9] = BitConverter.GetBytes(value.Length)[1];
            value_byte.CopyTo(buffer, 10);
            return OperationResult.CreateSuccessResult(PackCommand(buffer));
        }
    }
}
