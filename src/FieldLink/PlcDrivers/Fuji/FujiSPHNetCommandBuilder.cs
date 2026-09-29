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
using static FieldLink.PlcDrivers.Fuji.FujiSPHNetResponseParser;

namespace FieldLink.PlcDrivers.Fuji
{
    /// <summary>FujiSPHNet 요청 프레임을 생성합니다.</summary>
    public static class FujiSPHNetCommandBuilder
    {
        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "connectionId">connectionId에 사용할 입력값입니다.</param>
        /// <param name = "command">포장할 명령 바이트입니다.</param>
        /// <param name = "mode">mode에 사용할 입력값입니다.</param>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackCommand(byte connectionId, byte command, byte mode, byte[] data)
        {
            if (data == null)
                data = new byte[0];
            byte[] buffer = new byte[20 + data.Length];
            buffer[0] = 0xFB;
            buffer[1] = 0x80;
            buffer[2] = 0x80;
            buffer[3] = 0x00;
            buffer[4] = 0xFF;
            buffer[5] = 0x7B;
            buffer[6] = connectionId; // 연결 아이디
            buffer[7] = 0x00;
            buffer[8] = 0x11;
            buffer[9] = 0x00;
            buffer[10] = 0x00;
            buffer[11] = 0x00;
            buffer[12] = 0x00;
            buffer[13] = 0x00;
            buffer[14] = command; // 명령
            buffer[15] = mode; // 모드
            buffer[16] = 0x00;
            buffer[17] = 0x01;
            buffer[18] = BitConverter.GetBytes(data.Length)[0]; // 길이
            buffer[19] = BitConverter.GetBytes(data.Length)[1];
            if (data.Length > 0)
                data.CopyTo(buffer, 20);
            return buffer;
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "connectionId">connectionId에 사용할 입력값입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<List<byte[]>> BuildReadCommand(byte connectionId, string address, ushort length)
        {
            OperationResult<FujiSphAddress> analysis = FujiSphAddress.ParseFrom(address);
            if (!analysis.IsSuccess)
                return analysis.ConvertFailed<List<byte[]>>();
            return BuildReadCommand(connectionId, analysis.Content, length);
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "connectionId">connectionId에 사용할 입력값입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<List<byte[]>> BuildReadCommand(byte connectionId, FujiSphAddress address, ushort length)
        {
            // 한 번에 최대 486 바이트 (234 워드) 의 데이터를 읽고 쓸 수 있습니다.
            List<byte[]> array = new List<byte[]>();
            int[] splits = ProtocolBytes.SplitIntegerToArray(length, 230);
            for (int i = 0; i < splits.Length; i++)
            {
                byte[] buffer = new byte[6];
                buffer[0] = address.TypeCode;
                buffer[1] = BitConverter.GetBytes(address.AddressStart)[0];
                buffer[2] = BitConverter.GetBytes(address.AddressStart)[1];
                buffer[3] = BitConverter.GetBytes(address.AddressStart)[2];
                buffer[4] = BitConverter.GetBytes(splits[i])[0];
                buffer[5] = BitConverter.GetBytes(splits[i])[1];
                array.Add(PackCommand(connectionId, 0x00, 0x00, buffer));
                address.AddressStart += splits[i];
            }

            return OperationResult.CreateSuccessResult(array);
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "connectionId">connectionId에 사용할 입력값입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteCommand(byte connectionId, string address, byte[] data)
        {
            OperationResult<FujiSphAddress> analysis = FujiSphAddress.ParseFrom(address);
            if (!analysis.IsSuccess)
                return analysis.ConvertFailed<byte[]>();
            int length = data.Length / 2;
            byte[] buffer = new byte[6 + data.Length];
            buffer[0] = analysis.Content.TypeCode;
            buffer[1] = BitConverter.GetBytes(analysis.Content.AddressStart)[0];
            buffer[2] = BitConverter.GetBytes(analysis.Content.AddressStart)[1];
            buffer[3] = BitConverter.GetBytes(analysis.Content.AddressStart)[2];
            buffer[4] = BitConverter.GetBytes(length)[0];
            buffer[5] = BitConverter.GetBytes(length)[1];
            data.CopyTo(buffer, 6);
            return OperationResult.CreateSuccessResult(PackCommand(connectionId, 0x01, 0x00, buffer));
        }
    }
}
