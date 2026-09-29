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
using static FieldLink.PlcDrivers.Melsec.MelsecMcRNetAddressParser;

namespace FieldLink.PlcDrivers.Melsec
{
    /// <summary>MelsecMcRNet 요청 프레임을 생성합니다.</summary>
    public static class MelsecMcRNetCommandBuilder
    {
        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "isBit">비트 단위 요청이면 true입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildReadMcCoreCommand(McDeviceAddress address, bool isBit)
        {
            byte[] command = new byte[12];
            command[0] = 0x01; // 데이터 일괄 읽기 명령
            command[1] = 0x04;
            command[2] = isBit ? (byte)0x01 : (byte)0x00; // 점 단위 또는 워드 단위로 대량으로 읽습니다.
            command[3] = 0x00;
            command[4] = BitConverter.GetBytes(address.AddressStart)[0]; // 시작 주소의 하위 바이트
            command[5] = BitConverter.GetBytes(address.AddressStart)[1];
            command[6] = BitConverter.GetBytes(address.AddressStart)[2];
            command[7] = BitConverter.GetBytes(address.AddressStart)[3];
            command[8] = BitConverter.GetBytes(address.McDataType.DataCode)[0]; // 데이터를 표시하세요
            command[9] = BitConverter.GetBytes(address.McDataType.DataCode)[1];
            command[10] = (byte)(address.Length % 256); // 소프트웨어의 길이는
            command[11] = (byte)(address.Length / 256);
            return command;
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildWriteWordCoreCommand(McDeviceAddress address, byte[] value)
        {
            if (value == null)
                value = new byte[0];
            byte[] command = new byte[12 + value.Length];
            command[0] = 0x01; // 데이터 명령어에 대량 입력
            command[1] = 0x14;
            command[2] = 0x00; // 워드 단위로 뭉쳐서 읽습니다.
            command[3] = 0x00;
            command[4] = BitConverter.GetBytes(address.AddressStart)[0]; // 시작 주소의 하위 바이트
            command[5] = BitConverter.GetBytes(address.AddressStart)[1];
            command[6] = BitConverter.GetBytes(address.AddressStart)[2];
            command[7] = BitConverter.GetBytes(address.AddressStart)[3];
            command[8] = BitConverter.GetBytes(address.McDataType.DataCode)[0]; // 데이터를 표시하세요
            command[9] = BitConverter.GetBytes(address.McDataType.DataCode)[1];
            command[10] = (byte)(value.Length / 2 % 256); // 디바이스 길이의 위치
            command[11] = (byte)(value.Length / 2 / 256);
            value.CopyTo(command, 12);
            return command;
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildWriteBitCoreCommand(McDeviceAddress address, bool[] value)
        {
            if (value == null)
                value = new bool[0];
            byte[] buffer = MelsecValueConverter.TransBoolArrayToByteData(value);
            byte[] command = new byte[12 + buffer.Length];
            command[0] = 0x01; // 데이터 명령어에 대량 입력
            command[1] = 0x14;
            command[2] = 0x01; // 단위로 묶어서 적어
            command[3] = 0x00;
            command[4] = BitConverter.GetBytes(address.AddressStart)[0]; // 시작 주소의 하위 바이트
            command[5] = BitConverter.GetBytes(address.AddressStart)[1];
            command[6] = BitConverter.GetBytes(address.AddressStart)[2];
            command[7] = BitConverter.GetBytes(address.AddressStart)[3];
            command[8] = BitConverter.GetBytes(address.McDataType.DataCode)[0]; // 데이터를 표시하세요
            command[9] = BitConverter.GetBytes(address.McDataType.DataCode)[1];
            command[10] = (byte)(value.Length % 256); // 디바이스 길이의 위치
            command[11] = (byte)(value.Length / 256);
            buffer.CopyTo(command, 12);
            return command;
        }
    }
}
