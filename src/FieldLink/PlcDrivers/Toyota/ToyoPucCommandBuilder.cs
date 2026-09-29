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
using static FieldLink.PlcDrivers.Toyota.ToyoPucResponseParser;
using static FieldLink.PlcDrivers.Toyota.ToyoPucAddressParser;

namespace FieldLink.PlcDrivers.Toyota
{
    /// <summary>ToyoPuc 요청 프레임을 생성합니다.</summary>
    public static class ToyoPucCommandBuilder
    {
        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildReadBoolCommand(string address)
        {
            OperationResult<ToyoPucAddress> analysisAddress = ToyoPucAddress.ParseFrom(address, 1, true);
            if (!analysisAddress.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(analysisAddress);
            ToyoPucAddress pucAddress = analysisAddress.Content;
            if (pucAddress.PRG >= 0)
            {
                return new OperationResult<byte[]>();
            }
            else
            {
                byte[] buffer = new byte[3];
                buffer[0] = 0x20;
                buffer[1] = BitConverter.GetBytes(pucAddress.AddressStart)[0];
                buffer[2] = BitConverter.GetBytes(pucAddress.AddressStart)[1];
                return OperationResult.CreateSuccessResult(buffer);
            }
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildReadWordCommand(string address, ushort length)
        {
            OperationResult<ToyoPucAddress> analysisAddress = ToyoPucAddress.ParseFrom(address, length, false);
            if (!analysisAddress.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(analysisAddress);
            ToyoPucAddress pucAddress = analysisAddress.Content;
            if (pucAddress.PRG >= 0)
            {
                byte[] buffer = new byte[6];
                buffer[0] = 0x94;
                buffer[1] = (byte)pucAddress.PRG;
                buffer[2] = BitConverter.GetBytes(pucAddress.AddressStart)[0];
                buffer[3] = BitConverter.GetBytes(pucAddress.AddressStart)[1];
                buffer[4] = BitConverter.GetBytes(length)[0];
                buffer[5] = BitConverter.GetBytes(length)[1];
                return OperationResult.CreateSuccessResult(buffer);
            }
            else
            {
                byte[] buffer = new byte[5];
                buffer[0] = 0x1C;
                buffer[1] = BitConverter.GetBytes(pucAddress.AddressStart)[0];
                buffer[2] = BitConverter.GetBytes(pucAddress.AddressStart)[1];
                buffer[3] = BitConverter.GetBytes(length)[0];
                buffer[4] = BitConverter.GetBytes(length)[1];
                return OperationResult.CreateSuccessResult(buffer);
            }
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteWordCommand(string address, byte[] value)
        {
            OperationResult<ToyoPucAddress> analysisAddress = ToyoPucAddress.ParseFrom(address, 1, false);
            if (!analysisAddress.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(analysisAddress);
            ToyoPucAddress pucAddress = analysisAddress.Content;
            if (pucAddress.PRG >= 0)
            {
                byte[] buffer = new byte[4 + value.Length];
                buffer[0] = 0x95;
                buffer[1] = (byte)pucAddress.PRG;
                buffer[2] = BitConverter.GetBytes(pucAddress.AddressStart)[0];
                buffer[3] = BitConverter.GetBytes(pucAddress.AddressStart)[1];
                value.CopyTo(buffer, 4);
                return OperationResult.CreateSuccessResult(buffer);
            }
            else
            {
                byte[] buffer = new byte[3 + value.Length];
                buffer[0] = 0x1D;
                buffer[1] = BitConverter.GetBytes(pucAddress.AddressStart)[0];
                buffer[2] = BitConverter.GetBytes(pucAddress.AddressStart)[1];
                value.CopyTo(buffer, 3);
                return OperationResult.CreateSuccessResult(buffer);
            }
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteBoolCommand(string address, bool value)
        {
            OperationResult<ToyoPucAddress> analysisAddress = ToyoPucAddress.ParseFrom(address, 1, true);
            if (!analysisAddress.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(analysisAddress);
            ToyoPucAddress pucAddress = analysisAddress.Content;
            if (pucAddress.PRG >= 0)
            {
                return new OperationResult<byte[]>("Not supported prg write bool");
            }
            else
            {
                byte[] buffer = new byte[4];
                buffer[0] = 0x21;
                buffer[1] = BitConverter.GetBytes(pucAddress.AddressStart)[0];
                buffer[2] = BitConverter.GetBytes(pucAddress.AddressStart)[1];
                if (value)
                    buffer[3] = 0x01;
                return OperationResult.CreateSuccessResult(buffer);
            }
        }

        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "command">포장할 명령 바이트입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackCommandWithHeader(byte[] command)
        {
            byte[] buffer = new byte[command.Length + 4];
            buffer[2] = BitConverter.GetBytes(command.Length)[0];
            buffer[3] = BitConverter.GetBytes(command.Length)[1];
            command.CopyTo(buffer, 4);
            return buffer;
        }
    }
}
