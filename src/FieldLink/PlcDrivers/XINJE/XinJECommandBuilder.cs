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
using static FieldLink.PlcDrivers.XINJE.XinjeAddressParser;
using static FieldLink.PlcDrivers.XINJE.XinJEValueConverter;

namespace FieldLink.PlcDrivers.XINJE
{
    /// <summary>XinJE 요청 프레임을 생성합니다.</summary>
    public static class XinJECommandBuilder
    {
        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <param name = "isBit">비트 단위 요청이면 true입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<List<byte[]>> BuildReadCommand(byte station, string address, ushort length, bool isBit)
        {
            OperationResult<XinjeAddress> read = XinjeAddress.ParseFrom(address, station);
            if (!read.IsSuccess)
                return OperationResult.CreateFailedResult<List<byte[]>>(read);
            return BuildReadCommand(read.Content, length, isBit);
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <param name = "isBit">비트 단위 요청이면 true입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<List<byte[]>> BuildReadCommand(XinjeAddress address, ushort length, bool isBit)
        {
            List<byte[]> array = new List<byte[]>();
            int[] splits = ProtocolBytes.SplitIntegerToArray(length, isBit ? 120 * 16 : 120);
            for (int i = 0; i < splits.Length; i++)
            {
                byte[] command = new byte[8];
                command[0] = address.Station;
                command[1] = isBit ? (byte)0x1E : (byte)0x20;
                command[2] = address.DataCode;
                command[3] = BitConverter.GetBytes(address.AddressStart)[2];
                command[4] = BitConverter.GetBytes(address.AddressStart)[1];
                command[5] = BitConverter.GetBytes(address.AddressStart)[0];
                command[6] = BitConverter.GetBytes(splits[i])[1];
                command[7] = BitConverter.GetBytes(splits[i])[0];
                address.AddressStart += splits[i];
                array.Add(command);
            }

            return OperationResult.CreateSuccessResult(array);
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteWordCommand(byte station, string address, byte[] value)
        {
            OperationResult<XinjeAddress> read = XinjeAddress.ParseFrom(address, station);
            if (!read.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(read);
            return BuildWriteWordCommand(read.Content, value);
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteWordCommand(XinjeAddress address, byte[] value)
        {
            byte[] command = new byte[9 + value.Length];
            command[0] = address.Station;
            command[1] = 0x21;
            command[2] = address.DataCode;
            command[3] = BitConverter.GetBytes(address.AddressStart)[2];
            command[4] = BitConverter.GetBytes(address.AddressStart)[1];
            command[5] = BitConverter.GetBytes(address.AddressStart)[0];
            command[6] = BitConverter.GetBytes(value.Length / 2)[1];
            command[7] = BitConverter.GetBytes(value.Length / 2)[0];
            command[8] = (byte)value.Length;
            value.CopyTo(command, 9);
            return OperationResult.CreateSuccessResult(command);
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteBoolCommand(byte station, string address, bool[] value)
        {
            OperationResult<XinjeAddress> read = XinjeAddress.ParseFrom(address, station);
            if (!read.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(read);
            return BuildWriteBoolCommand(read.Content, value);
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteBoolCommand(XinjeAddress address, bool[] value)
        {
            byte[] buffer = value.ToByteArray();
            byte[] command = new byte[9 + buffer.Length];
            command[0] = address.Station;
            command[1] = 0x1F;
            command[2] = address.DataCode;
            command[3] = BitConverter.GetBytes(address.AddressStart)[2];
            command[4] = BitConverter.GetBytes(address.AddressStart)[1];
            command[5] = BitConverter.GetBytes(address.AddressStart)[0];
            command[6] = BitConverter.GetBytes(value.Length)[1];
            command[7] = BitConverter.GetBytes(value.Length)[0];
            command[8] = (byte)buffer.Length;
            buffer.CopyTo(command, 9);
            return OperationResult.CreateSuccessResult(command);
        }
    }
}
