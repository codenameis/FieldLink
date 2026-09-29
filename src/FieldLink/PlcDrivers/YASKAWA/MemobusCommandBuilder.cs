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
using static FieldLink.PlcDrivers.YASKAWA.MemobusResponseParser;
using static FieldLink.PlcDrivers.YASKAWA.MemobusValueConverter;

namespace FieldLink.PlcDrivers.YASKAWA
{
    /// <summary>Memobus 요청 프레임을 생성합니다.</summary>
    public static class MemobusCommandBuilder
    {
        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "command">포장할 명령 바이트입니다.</param>
        /// <param name = "id">id에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackCommandWithHeader(byte[] command, long id)
        {
            byte[] buffer = new byte[12 + command.Length]; // 12바이트의 218개의 헤드라인을 추가합니다.
            buffer[0] = 0x11; // 0x11:Memobus 명령 0x12: 일반 정보 0x19:Memobus 응답
            buffer[1] = (byte)id;
            buffer[2] = 0x00; // 송신 목표의 채널 번호를 설정 (공유 메모리의 채널 번호) MP 시리즈 이외의 장치에서 액세스 할 때 00H
            buffer[3] = 0x00; // 송신 소스의 채널 번호 ((공유 메모리의 채널 번호) ᅳ MP 시리즈 이외의 장치에서 액세스할 때 00H ᅳ
            buffer[6] = BitConverter.GetBytes(buffer.Length)[0];
            buffer[7] = BitConverter.GetBytes(buffer.Length)[1];
            command.CopyTo(buffer, 12);
            return buffer;
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "mfc">mfc에 사용할 입력값입니다.</param>
        /// <param name = "sfc">sfc에 사용할 입력값입니다.</param>
        /// <param name = "cpuTo">cpuTo에 사용할 입력값입니다.</param>
        /// <param name = "cpuFrom">cpuFrom에 사용할 입력값입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildReadCommand(byte mfc, byte sfc, byte cpuTo, byte cpuFrom, ushort address, ushort length)
        {
            if (sfc == 0x01 || sfc == 0x02 || sfc == 0x03 || sfc == 0x04)
            {
                byte[] buffer = new byte[9];
                SetByteHead(buffer, mfc, sfc, cpuTo, cpuFrom);
                buffer[5] = BitConverter.GetBytes(address)[1];
                buffer[6] = BitConverter.GetBytes(address)[0];
                buffer[7] = BitConverter.GetBytes(length)[1];
                buffer[8] = BitConverter.GetBytes(length)[0];
                return OperationResult.CreateSuccessResult(buffer);
            }
            else if (sfc == 0x09 || sfc == 0x0A)
            {
                byte[] buffer = new byte[10];
                SetByteHead(buffer, mfc, sfc, cpuTo, cpuFrom);
                buffer[6] = BitConverter.GetBytes(address)[0];
                buffer[7] = BitConverter.GetBytes(address)[1];
                buffer[8] = BitConverter.GetBytes(length)[0];
                buffer[9] = BitConverter.GetBytes(length)[1];
                return OperationResult.CreateSuccessResult(buffer);
            }
            else
                return new OperationResult<byte[]>($"SFC:{sfc} {ProtocolMessages.NotSupportedFunction}");
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "mfc">mfc에 사용할 입력값입니다.</param>
        /// <param name = "sfc">sfc에 사용할 입력값입니다.</param>
        /// <param name = "cpuTo">cpuTo에 사용할 입력값입니다.</param>
        /// <param name = "cpuFrom">cpuFrom에 사용할 입력값입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildReadRandomCommand(byte mfc, byte sfc, byte cpuTo, byte cpuFrom, ushort[] address)
        {
            byte[] buffer = new byte[8 + address.Length * 2];
            SetByteHead(buffer, mfc, sfc, cpuTo, cpuFrom);
            buffer[6] = BitConverter.GetBytes(address.Length)[0];
            buffer[7] = BitConverter.GetBytes(address.Length)[1];
            for (int i = 0; i < address.Length; i++)
            {
                buffer[8 + i * 2 + 0] = BitConverter.GetBytes(address[i])[0];
                buffer[8 + i * 2 + 1] = BitConverter.GetBytes(address[i])[1];
            }

            return OperationResult.CreateSuccessResult(buffer);
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "mfc">mfc에 사용할 입력값입니다.</param>
        /// <param name = "sfc">sfc에 사용할 입력값입니다.</param>
        /// <param name = "cpuTo">cpuTo에 사용할 입력값입니다.</param>
        /// <param name = "cpuFrom">cpuFrom에 사용할 입력값입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteCommand(byte mfc, byte sfc, byte cpuTo, byte cpuFrom, ushort address, bool value)
        {
            byte[] buffer = new byte[9];
            SetByteHead(buffer, mfc, sfc, cpuTo, cpuFrom);
            buffer[5] = BitConverter.GetBytes(address)[1];
            buffer[6] = BitConverter.GetBytes(address)[0];
            buffer[7] = (byte)(value ? 0xFF : 0x00);
            buffer[8] = 0x00;
            return OperationResult.CreateSuccessResult(buffer);
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "mfc">mfc에 사용할 입력값입니다.</param>
        /// <param name = "sfc">sfc에 사용할 입력값입니다.</param>
        /// <param name = "cpuTo">cpuTo에 사용할 입력값입니다.</param>
        /// <param name = "cpuFrom">cpuFrom에 사용할 입력값입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteCommand(byte mfc, byte sfc, byte cpuTo, byte cpuFrom, ushort address, bool[] value)
        {
            byte[] data = ProtocolBytes.BoolArrayToByte(value);
            byte[] buffer = new byte[9 + data.Length];
            SetByteHead(buffer, mfc, sfc, cpuTo, cpuFrom);
            buffer[5] = BitConverter.GetBytes(address)[1];
            buffer[6] = BitConverter.GetBytes(address)[0];
            buffer[7] = BitConverter.GetBytes(value.Length)[1];
            buffer[8] = BitConverter.GetBytes(value.Length)[0];
            data.CopyTo(buffer, 9);
            return OperationResult.CreateSuccessResult(buffer);
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "mfc">mfc에 사용할 입력값입니다.</param>
        /// <param name = "sfc">sfc에 사용할 입력값입니다.</param>
        /// <param name = "cpuTo">cpuTo에 사용할 입력값입니다.</param>
        /// <param name = "cpuFrom">cpuFrom에 사용할 입력값입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteCommand(byte mfc, byte sfc, byte cpuTo, byte cpuFrom, ushort address, short value)
        {
            byte[] buffer = new byte[9];
            SetByteHead(buffer, mfc, sfc, cpuTo, cpuFrom);
            buffer[5] = BitConverter.GetBytes(address)[1];
            buffer[6] = BitConverter.GetBytes(address)[0];
            buffer[7] = BitConverter.GetBytes(value)[1];
            buffer[8] = BitConverter.GetBytes(value)[0];
            return OperationResult.CreateSuccessResult(buffer);
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "mfc">mfc에 사용할 입력값입니다.</param>
        /// <param name = "sfc">sfc에 사용할 입력값입니다.</param>
        /// <param name = "cpuTo">cpuTo에 사용할 입력값입니다.</param>
        /// <param name = "cpuFrom">cpuFrom에 사용할 입력값입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteCommand(byte mfc, byte sfc, byte cpuTo, byte cpuFrom, ushort address, ushort value)
        {
            byte[] buffer = new byte[9];
            SetByteHead(buffer, mfc, sfc, cpuTo, cpuFrom);
            buffer[5] = BitConverter.GetBytes(address)[1];
            buffer[6] = BitConverter.GetBytes(address)[0];
            buffer[7] = BitConverter.GetBytes(value)[1];
            buffer[8] = BitConverter.GetBytes(value)[0];
            return OperationResult.CreateSuccessResult(buffer);
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "mfc">mfc에 사용할 입력값입니다.</param>
        /// <param name = "sfc">sfc에 사용할 입력값입니다.</param>
        /// <param name = "cpuTo">cpuTo에 사용할 입력값입니다.</param>
        /// <param name = "cpuFrom">cpuFrom에 사용할 입력값입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteCommand(byte mfc, byte sfc, byte cpuTo, byte cpuFrom, ushort address, byte[] value)
        {
            if (sfc == 0x0B)
            {
                byte[] buffer = new byte[10 + value.Length];
                SetByteHead(buffer, mfc, sfc, cpuTo, cpuFrom);
                buffer[6] = BitConverter.GetBytes(address)[0];
                buffer[7] = BitConverter.GetBytes(address)[1];
                buffer[8] = BitConverter.GetBytes(value.Length / 2)[0];
                buffer[9] = BitConverter.GetBytes(value.Length / 2)[1];
                ProtocolBytes.BytesReverseByWord(value).CopyTo(buffer, 10);
                return OperationResult.CreateSuccessResult(buffer);
            }
            else if (sfc == 0x10)
            {
                byte[] buffer = new byte[9 + value.Length];
                SetByteHead(buffer, mfc, sfc, cpuTo, cpuFrom);
                buffer[5] = BitConverter.GetBytes(address)[1];
                buffer[6] = BitConverter.GetBytes(address)[0];
                buffer[7] = BitConverter.GetBytes(value.Length / 2)[1];
                buffer[8] = BitConverter.GetBytes(value.Length / 2)[0];
                value.CopyTo(buffer, 9);
                return OperationResult.CreateSuccessResult(buffer);
            }
            else
                return new OperationResult<byte[]>($"SFC:{sfc} {ProtocolMessages.NotSupportedFunction}");
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "mfc">mfc에 사용할 입력값입니다.</param>
        /// <param name = "sfc">sfc에 사용할 입력값입니다.</param>
        /// <param name = "cpuTo">cpuTo에 사용할 입력값입니다.</param>
        /// <param name = "cpuFrom">cpuFrom에 사용할 입력값입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteRandomCommand(byte mfc, byte sfc, byte cpuTo, byte cpuFrom, ushort[] address, byte[] value)
        {
            if (value.Length != address.Length * 2)
                return new OperationResult<byte[]>("value.Length must be twice as much as address.Length");
            byte[] buffer = new byte[8 + address.Length * 4];
            SetByteHead(buffer, mfc, sfc, cpuTo, cpuFrom);
            buffer[6] = BitConverter.GetBytes(address.Length)[0];
            buffer[7] = BitConverter.GetBytes(address.Length)[1];
            for (int i = 0; i < address.Length; i++)
            {
                buffer[8 + i * 4 + 0] = BitConverter.GetBytes(address[i])[0];
                buffer[8 + i * 4 + 1] = BitConverter.GetBytes(address[i])[1];
                buffer[8 + i * 4 + 2] = value[i * 2 + 1]; // 이 숫자는 바로 역으로 표시됩니다.
                buffer[8 + i * 4 + 3] = value[i * 2 + 0];
            }

            return OperationResult.CreateSuccessResult(buffer);
        }
    }
}
