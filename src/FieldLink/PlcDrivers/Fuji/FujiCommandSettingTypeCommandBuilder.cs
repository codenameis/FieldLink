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
using static FieldLink.PlcDrivers.Fuji.FujiCommandSettingTypeResponseParser;

namespace FieldLink.PlcDrivers.Fuji
{
    /// <summary>FujiCommandSettingType 요청 프레임을 생성합니다.</summary>
    public static class FujiCommandSettingTypeCommandBuilder
    {
        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildReadCommand(string address, ushort length)
        {
            OperationResult<FujiCommandSettingTypeAddress> analysisAddress = FujiCommandSettingTypeAddress.ParseFrom(address, length);
            if (!analysisAddress.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(analysisAddress);
            byte[] buffer = new byte[9];
            buffer[0] = 0x00;
            buffer[1] = 0x00;
            buffer[2] = 0x00;
            buffer[3] = analysisAddress.Content.DataCode;
            buffer[4] = 0x04;
            buffer[5] = BitConverter.GetBytes(analysisAddress.Content.AddressStart)[0];
            buffer[6] = BitConverter.GetBytes(analysisAddress.Content.AddressStart)[1];
            buffer[7] = BitConverter.GetBytes(analysisAddress.Content.Length)[0];
            buffer[8] = BitConverter.GetBytes(analysisAddress.Content.Length)[1];
            return OperationResult.CreateSuccessResult(buffer);
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteCommand(string address, byte[] value)
        {
            OperationResult<FujiCommandSettingTypeAddress> analysisAddress = FujiCommandSettingTypeAddress.ParseFrom(address, 0);
            if (!analysisAddress.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(analysisAddress);
            byte[] buffer = new byte[9 + value.Length];
            buffer[0] = 0x01;
            buffer[1] = 0x00;
            buffer[2] = 0x00;
            buffer[3] = analysisAddress.Content.DataCode;
            buffer[4] = (byte)(0x04 + value.Length);
            buffer[5] = BitConverter.GetBytes(analysisAddress.Content.AddressStart)[0];
            buffer[6] = BitConverter.GetBytes(analysisAddress.Content.AddressStart)[1];
            buffer[7] = BitConverter.GetBytes(analysisAddress.Content.Length)[0];
            buffer[8] = BitConverter.GetBytes(analysisAddress.Content.Length)[0];
            value.CopyTo(buffer, 9);
            return OperationResult.CreateSuccessResult(buffer);
        }
    }
}
