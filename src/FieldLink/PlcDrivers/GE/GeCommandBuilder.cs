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
using static FieldLink.PlcDrivers.GE.GeResponseParser;

namespace FieldLink.PlcDrivers.GE
{
    /// <summary>Ge 요청 프레임을 생성합니다.</summary>
    public static class GeCommandBuilder
    {
        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "id">id에 사용할 입력값입니다.</param>
        /// <param name = "code">code에 사용할 입력값입니다.</param>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildReadCoreCommand(long id, byte code, byte[] data)
        {
            byte[] buffer = new byte[56];
            buffer[0] = 0x02;
            buffer[1] = 0x00;
            buffer[2] = BitConverter.GetBytes(id)[0];
            buffer[3] = BitConverter.GetBytes(id)[1];
            buffer[4] = 0x00; // 길이
            buffer[5] = 0x00;
            buffer[9] = 0x01;
            buffer[17] = 0x01;
            buffer[18] = 0x00;
            buffer[30] = 0x06;
            buffer[31] = 0xC0;
            buffer[36] = 0x10;
            buffer[37] = 0x0E;
            buffer[40] = 0x01;
            buffer[41] = 0x01;
            buffer[42] = code; // 시스템 메모리 읽기
            data.CopyTo(buffer, 43);
            return OperationResult.CreateSuccessResult(buffer);
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "id">id에 사용할 입력값입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildReadCommand(long id, GeSrtpAddress address)
        {
            if (address.DataCode == 0x0A || address.DataCode == 0x0C || address.DataCode == 0x08)
            {
                address.Length /= 2;
            }

            byte[] buffer = new byte[5];
            buffer[0] = address.DataCode;
            buffer[1] = BitConverter.GetBytes(address.AddressStart)[0];
            buffer[2] = BitConverter.GetBytes(address.AddressStart)[1];
            buffer[3] = BitConverter.GetBytes(address.Length)[0];
            buffer[4] = BitConverter.GetBytes(address.Length)[1];
            return BuildReadCoreCommand(id, 0x04, buffer);
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "id">id에 사용할 입력값입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <param name = "isBit">비트 단위 요청이면 true입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildReadCommand(long id, string address, ushort length, bool isBit)
        {
            OperationResult<GeSrtpAddress> analysis = GeSrtpAddress.ParseFrom(address, length, isBit);
            if (!analysis.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(analysis);
            return BuildReadCommand(id, analysis.Content);
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "id">id에 사용할 입력값입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteCommand(long id, GeSrtpAddress address, byte[] value)
        {
            int length = address.Length;
            if (address.DataCode == 0x0A || address.DataCode == 0x0C || address.DataCode == 0x08)
            {
                length /= 2;
            }

            byte[] buffer = new byte[56 + value.Length];
            buffer[0] = 0x02;
            buffer[1] = 0x00;
            buffer[2] = BitConverter.GetBytes(id)[0];
            buffer[3] = BitConverter.GetBytes(id)[1];
            buffer[4] = BitConverter.GetBytes(value.Length)[0]; // 길이
            buffer[5] = BitConverter.GetBytes(value.Length)[1];
            buffer[9] = 0x02;
            buffer[17] = 0x02;
            buffer[18] = 0x00;
            buffer[30] = 0x09;
            buffer[31] = 0x80;
            buffer[36] = 0x10;
            buffer[37] = 0x0E;
            buffer[40] = 0x01;
            buffer[41] = 0x01;
            buffer[42] = 0x02;
            buffer[48] = 0x01;
            buffer[49] = 0x01;
            buffer[50] = 0x07; // 데이터 입력
            buffer[51] = address.DataCode;
            buffer[52] = BitConverter.GetBytes(address.AddressStart)[0];
            buffer[53] = BitConverter.GetBytes(address.AddressStart)[1];
            buffer[54] = BitConverter.GetBytes(length)[0];
            buffer[55] = BitConverter.GetBytes(length)[1];
            value.CopyTo(buffer, 56);
            return OperationResult.CreateSuccessResult(buffer);
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "id">id에 사용할 입력값입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteCommand(long id, string address, byte[] value)
        {
            OperationResult<GeSrtpAddress> analysis = GeSrtpAddress.ParseFrom(address, (ushort)value.Length, false);
            if (!analysis.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(analysis);
            return BuildWriteCommand(id, analysis.Content, value);
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "id">id에 사용할 입력값입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteCommand(long id, string address, bool[] value)
        {
            OperationResult<GeSrtpAddress> analysis = GeSrtpAddress.ParseFrom(address, (ushort)value.Length, true);
            if (!analysis.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(analysis);
            bool[] boolArray = new bool[analysis.Content.AddressStart % 8 + value.Length];
            value.CopyTo(boolArray, analysis.Content.AddressStart % 8);
            return BuildWriteCommand(id, analysis.Content, ProtocolBytes.BoolArrayToByte(boolArray));
        }
    }
}
