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
using static FieldLink.PlcDrivers.Melsec.McAsciiResponseParser;

namespace FieldLink.PlcDrivers.Melsec
{
    /// <summary>McAscii 요청 프레임을 생성합니다.</summary>
    public static class McAsciiCommandBuilder
    {
        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "mc">mc에 사용할 입력값입니다.</param>
        /// <param name = "mcCore">포장할 명령 바이트입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackMcCommand(McFrameOptions mc, byte[] mcCore)
        {
            if (mcCore == null)
                throw new ArgumentNullException(nameof(mcCore));
            if (mcCore.Length > ushort.MaxValue - 4)
                throw new ArgumentOutOfRangeException(nameof(mcCore), "MC 요청 길이가 4자리 16진수 길이 필드를 초과합니다.");
            byte[] ioStation = ProtocolBytes.BuildAsciiBytesFrom(mc.TargetIOStation);
            byte[] plcCommand = new byte[22 + mcCore.Length];
            plcCommand[0] = 0x35; // 서브헤더
            plcCommand[1] = 0x30;
            plcCommand[2] = 0x30;
            plcCommand[3] = 0x30;
            plcCommand[4] = ProtocolBytes.BuildAsciiBytesFrom(mc.NetworkNumber)[0]; // 네트워크 번호
            plcCommand[5] = ProtocolBytes.BuildAsciiBytesFrom(mc.NetworkNumber)[1];
            plcCommand[6] = ProtocolBytes.BuildAsciiBytesFrom(mc.PLCNumber)[0]; // PLC 번호
            plcCommand[7] = ProtocolBytes.BuildAsciiBytesFrom(mc.PLCNumber)[1];
            plcCommand[8] = ioStation[0]; // 목표 모듈 IO 번호
            plcCommand[9] = ioStation[1];
            plcCommand[10] = ioStation[2];
            plcCommand[11] = ioStation[3];
            plcCommand[12] = ProtocolBytes.BuildAsciiBytesFrom(mc.NetworkStationNumber)[0]; // 목표 모듈 국번
            plcCommand[13] = ProtocolBytes.BuildAsciiBytesFrom(mc.NetworkStationNumber)[1];
            plcCommand[14] = ProtocolBytes.BuildAsciiBytesFrom((ushort)(plcCommand.Length - 18))[0]; // 요청 데이터 길이
            plcCommand[15] = ProtocolBytes.BuildAsciiBytesFrom((ushort)(plcCommand.Length - 18))[1];
            plcCommand[16] = ProtocolBytes.BuildAsciiBytesFrom((ushort)(plcCommand.Length - 18))[2];
            plcCommand[17] = ProtocolBytes.BuildAsciiBytesFrom((ushort)(plcCommand.Length - 18))[3];
            plcCommand[18] = 0x30; // CPU 감시 타이머
            plcCommand[19] = 0x30;
            plcCommand[20] = 0x31;
            plcCommand[21] = 0x30;
            mcCore.CopyTo(plcCommand, 22);
            return plcCommand;
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "addressData">addressData에 사용할 입력값입니다.</param>
        /// <param name = "isBit">비트 단위 요청이면 true입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildAsciiReadMcCoreCommand(McDeviceAddress addressData, bool isBit)
        {
            ValidateBatchCount(addressData.Length, isBit);
            byte[] command = new byte[20];
            command[0] = 0x30; // 데이터 일괄 읽기 명령
            command[1] = 0x34;
            command[2] = 0x30;
            command[3] = 0x31;
            command[4] = 0x30; // 점 단위 또는 워드 단위로 대량으로 읽습니다.
            command[5] = 0x30;
            command[6] = 0x30;
            command[7] = isBit ? (byte)0x31 : (byte)0x30;
            command[8] = Encoding.ASCII.GetBytes(addressData.McDataType.AsciiCode)[0]; // 디바이스 유형
            command[9] = Encoding.ASCII.GetBytes(addressData.McDataType.AsciiCode)[1];
            command[10] = MelsecCommandBuilder.BuildBytesFromAddress(addressData.AddressStart, addressData.McDataType)[0]; // 시작 주소의 하위 바이트
            command[11] = MelsecCommandBuilder.BuildBytesFromAddress(addressData.AddressStart, addressData.McDataType)[1];
            command[12] = MelsecCommandBuilder.BuildBytesFromAddress(addressData.AddressStart, addressData.McDataType)[2];
            command[13] = MelsecCommandBuilder.BuildBytesFromAddress(addressData.AddressStart, addressData.McDataType)[3];
            command[14] = MelsecCommandBuilder.BuildBytesFromAddress(addressData.AddressStart, addressData.McDataType)[4];
            command[15] = MelsecCommandBuilder.BuildBytesFromAddress(addressData.AddressStart, addressData.McDataType)[5];
            command[16] = ProtocolBytes.BuildAsciiBytesFrom(addressData.Length)[0]; // 디바이스 점수
            command[17] = ProtocolBytes.BuildAsciiBytesFrom(addressData.Length)[1];
            command[18] = ProtocolBytes.BuildAsciiBytesFrom(addressData.Length)[2];
            command[19] = ProtocolBytes.BuildAsciiBytesFrom(addressData.Length)[3];
            return command;
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "addressData">addressData에 사용할 입력값입니다.</param>
        /// <param name = "extend">extend에 사용할 입력값입니다.</param>
        /// <param name = "isBit">비트 단위 요청이면 true입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildAsciiReadMcCoreExtendCommand(McDeviceAddress addressData, ushort extend, bool isBit)
        {
            byte[] command = new byte[32];
            command[0] = 0x30; // 데이터 일괄 읽기 명령
            command[1] = 0x34;
            command[2] = 0x30;
            command[3] = 0x31;
            command[4] = 0x30; // 점 단위 또는 워드 단위로 대량으로 읽습니다.
            command[5] = 0x30;
            command[6] = 0x38;
            command[7] = isBit ? (byte)0x31 : (byte)0x30;
            command[8] = 0x30;
            command[9] = 0x30;
            command[10] = 0x4A; // 확장 지정
            command[11] = ProtocolBytes.BuildAsciiBytesFrom(extend)[1];
            command[12] = ProtocolBytes.BuildAsciiBytesFrom(extend)[2];
            command[13] = ProtocolBytes.BuildAsciiBytesFrom(extend)[3];
            command[14] = 0x30;
            command[15] = 0x30;
            command[16] = 0x30;
            command[17] = Encoding.ASCII.GetBytes(addressData.McDataType.AsciiCode)[0]; // 디바이스 유형
            command[18] = Encoding.ASCII.GetBytes(addressData.McDataType.AsciiCode)[1];
            command[19] = MelsecCommandBuilder.BuildBytesFromAddress(addressData.AddressStart, addressData.McDataType)[0]; // 시작 주소의 하위 바이트
            command[20] = MelsecCommandBuilder.BuildBytesFromAddress(addressData.AddressStart, addressData.McDataType)[1];
            command[21] = MelsecCommandBuilder.BuildBytesFromAddress(addressData.AddressStart, addressData.McDataType)[2];
            command[22] = MelsecCommandBuilder.BuildBytesFromAddress(addressData.AddressStart, addressData.McDataType)[3];
            command[23] = MelsecCommandBuilder.BuildBytesFromAddress(addressData.AddressStart, addressData.McDataType)[4];
            command[24] = MelsecCommandBuilder.BuildBytesFromAddress(addressData.AddressStart, addressData.McDataType)[5];
            command[25] = 0x30;
            command[26] = 0x30;
            command[27] = 0x30;
            command[28] = ProtocolBytes.BuildAsciiBytesFrom(addressData.Length)[0]; // 디바이스 점수
            command[29] = ProtocolBytes.BuildAsciiBytesFrom(addressData.Length)[1];
            command[30] = ProtocolBytes.BuildAsciiBytesFrom(addressData.Length)[2];
            command[31] = ProtocolBytes.BuildAsciiBytesFrom(addressData.Length)[3];
            return command;
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "addressData">addressData에 사용할 입력값입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildAsciiWriteWordCoreCommand(McDeviceAddress addressData, byte[] value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            if (value.Length % 2 != 0)
                throw new ArgumentException("MC 워드 쓰기 데이터는 2바이트 단위여야 합니다.", nameof(value));
            ValidateBatchCount(value.Length / 2, false);
            value = MelsecValueConverter.TransByteArrayToAsciiByteArray(value);
            byte[] command = new byte[20 + value.Length];
            command[0] = 0x31; // 대량 입력 명령
            command[1] = 0x34;
            command[2] = 0x30;
            command[3] = 0x31;
            command[4] = 0x30; // 하위 명령
            command[5] = 0x30;
            command[6] = 0x30;
            command[7] = 0x30;
            command[8] = Encoding.ASCII.GetBytes(addressData.McDataType.AsciiCode)[0]; // 디바이스 유형
            command[9] = Encoding.ASCII.GetBytes(addressData.McDataType.AsciiCode)[1];
            command[10] = MelsecCommandBuilder.BuildBytesFromAddress(addressData.AddressStart, addressData.McDataType)[0]; // 시작 주소의 하위 바이트
            command[11] = MelsecCommandBuilder.BuildBytesFromAddress(addressData.AddressStart, addressData.McDataType)[1];
            command[12] = MelsecCommandBuilder.BuildBytesFromAddress(addressData.AddressStart, addressData.McDataType)[2];
            command[13] = MelsecCommandBuilder.BuildBytesFromAddress(addressData.AddressStart, addressData.McDataType)[3];
            command[14] = MelsecCommandBuilder.BuildBytesFromAddress(addressData.AddressStart, addressData.McDataType)[4];
            command[15] = MelsecCommandBuilder.BuildBytesFromAddress(addressData.AddressStart, addressData.McDataType)[5];
            command[16] = ProtocolBytes.BuildAsciiBytesFrom((ushort)(value.Length / 4))[0]; // 디바이스 점수
            command[17] = ProtocolBytes.BuildAsciiBytesFrom((ushort)(value.Length / 4))[1];
            command[18] = ProtocolBytes.BuildAsciiBytesFrom((ushort)(value.Length / 4))[2];
            command[19] = ProtocolBytes.BuildAsciiBytesFrom((ushort)(value.Length / 4))[3];
            value.CopyTo(command, 20);
            return command;
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "addressData">addressData에 사용할 입력값입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildAsciiWriteBitCoreCommand(McDeviceAddress addressData, bool[] value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            ValidateBatchCount(value.Length, true);
            byte[] buffer = value.Select(m => m ? (byte)0x31 : (byte)0x30).ToArray();
            byte[] command = new byte[20 + buffer.Length];
            command[0] = 0x31; // 대량 입력 명령
            command[1] = 0x34;
            command[2] = 0x30;
            command[3] = 0x31;
            command[4] = 0x30; // 하위 명령
            command[5] = 0x30;
            command[6] = 0x30;
            command[7] = 0x31;
            command[8] = Encoding.ASCII.GetBytes(addressData.McDataType.AsciiCode)[0]; // 디바이스 유형
            command[9] = Encoding.ASCII.GetBytes(addressData.McDataType.AsciiCode)[1];
            command[10] = MelsecCommandBuilder.BuildBytesFromAddress(addressData.AddressStart, addressData.McDataType)[0]; // 시작 주소의 하위 바이트
            command[11] = MelsecCommandBuilder.BuildBytesFromAddress(addressData.AddressStart, addressData.McDataType)[1];
            command[12] = MelsecCommandBuilder.BuildBytesFromAddress(addressData.AddressStart, addressData.McDataType)[2];
            command[13] = MelsecCommandBuilder.BuildBytesFromAddress(addressData.AddressStart, addressData.McDataType)[3];
            command[14] = MelsecCommandBuilder.BuildBytesFromAddress(addressData.AddressStart, addressData.McDataType)[4];
            command[15] = MelsecCommandBuilder.BuildBytesFromAddress(addressData.AddressStart, addressData.McDataType)[5];
            command[16] = ProtocolBytes.BuildAsciiBytesFrom((ushort)(value.Length))[0]; // 디바이스 점수
            command[17] = ProtocolBytes.BuildAsciiBytesFrom((ushort)(value.Length))[1];
            command[18] = ProtocolBytes.BuildAsciiBytesFrom((ushort)(value.Length))[2];
            command[19] = ProtocolBytes.BuildAsciiBytesFrom((ushort)(value.Length))[3];
            buffer.CopyTo(command, 20);
            return command;
        }

        private static void ValidateBatchCount(int count, bool isBit)
        {
            // SH-080956ENG-N §5.2: ASCII bit-unit requests have a lower limit than binary.
            if (count < 1 || count > (isBit ? 3584 : 960))
                throw new ArgumentOutOfRangeException(nameof(count), "MC ASCII 일괄 요청의 디바이스 점수가 허용 범위를 벗어났습니다.");
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildAsciiReadRandomWordCommand(McDeviceAddress[] address)
        {
            byte[] command = new byte[12 + address.Length * 8];
            command[0] = 0x30; // 데이터 일괄 읽기 명령
            command[1] = 0x34;
            command[2] = 0x30;
            command[3] = 0x33;
            command[4] = 0x30; // 점 단위 또는 워드 단위로 대량으로 읽습니다.
            command[5] = 0x30;
            command[6] = 0x30;
            command[7] = 0x30;
            command[8] = ProtocolBytes.BuildAsciiBytesFrom((byte)address.Length)[0];
            command[9] = ProtocolBytes.BuildAsciiBytesFrom((byte)address.Length)[1];
            command[10] = 0x30;
            command[11] = 0x30;
            for (int i = 0; i < address.Length; i++)
            {
                command[i * 8 + 12] = Encoding.ASCII.GetBytes(address[i].McDataType.AsciiCode)[0]; // 디바이스 유형
                command[i * 8 + 13] = Encoding.ASCII.GetBytes(address[i].McDataType.AsciiCode)[1];
                command[i * 8 + 14] = MelsecCommandBuilder.BuildBytesFromAddress(address[i].AddressStart, address[i].McDataType)[0]; // 시작 주소의 하위 바이트
                command[i * 8 + 15] = MelsecCommandBuilder.BuildBytesFromAddress(address[i].AddressStart, address[i].McDataType)[1];
                command[i * 8 + 16] = MelsecCommandBuilder.BuildBytesFromAddress(address[i].AddressStart, address[i].McDataType)[2];
                command[i * 8 + 17] = MelsecCommandBuilder.BuildBytesFromAddress(address[i].AddressStart, address[i].McDataType)[3];
                command[i * 8 + 18] = MelsecCommandBuilder.BuildBytesFromAddress(address[i].AddressStart, address[i].McDataType)[4];
                command[i * 8 + 19] = MelsecCommandBuilder.BuildBytesFromAddress(address[i].AddressStart, address[i].McDataType)[5];
            }

            return command;
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildAsciiReadRandomCommand(McDeviceAddress[] address)
        {
            byte[] command = new byte[12 + address.Length * 12];
            command[0] = 0x30; // 데이터 일괄 읽기 명령
            command[1] = 0x34;
            command[2] = 0x30;
            command[3] = 0x36;
            command[4] = 0x30; // 점 단위 또는 워드 단위로 대량으로 읽습니다.
            command[5] = 0x30;
            command[6] = 0x30;
            command[7] = 0x30;
            command[8] = ProtocolBytes.BuildAsciiBytesFrom((byte)address.Length)[0];
            command[9] = ProtocolBytes.BuildAsciiBytesFrom((byte)address.Length)[1];
            command[10] = 0x30;
            command[11] = 0x30;
            for (int i = 0; i < address.Length; i++)
            {
                command[i * 12 + 12] = Encoding.ASCII.GetBytes(address[i].McDataType.AsciiCode)[0]; // 디바이스 유형
                command[i * 12 + 13] = Encoding.ASCII.GetBytes(address[i].McDataType.AsciiCode)[1];
                command[i * 12 + 14] = MelsecCommandBuilder.BuildBytesFromAddress(address[i].AddressStart, address[i].McDataType)[0]; // 시작 주소의 하위 바이트
                command[i * 12 + 15] = MelsecCommandBuilder.BuildBytesFromAddress(address[i].AddressStart, address[i].McDataType)[1];
                command[i * 12 + 16] = MelsecCommandBuilder.BuildBytesFromAddress(address[i].AddressStart, address[i].McDataType)[2];
                command[i * 12 + 17] = MelsecCommandBuilder.BuildBytesFromAddress(address[i].AddressStart, address[i].McDataType)[3];
                command[i * 12 + 18] = MelsecCommandBuilder.BuildBytesFromAddress(address[i].AddressStart, address[i].McDataType)[4];
                command[i * 12 + 19] = MelsecCommandBuilder.BuildBytesFromAddress(address[i].AddressStart, address[i].McDataType)[5];
                command[i * 12 + 20] = ProtocolBytes.BuildAsciiBytesFrom(address[i].Length)[0];
                command[i * 12 + 21] = ProtocolBytes.BuildAsciiBytesFrom(address[i].Length)[1];
                command[i * 12 + 22] = ProtocolBytes.BuildAsciiBytesFrom(address[i].Length)[2];
                command[i * 12 + 23] = ProtocolBytes.BuildAsciiBytesFrom(address[i].Length)[3];
            }

            return command;
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildAsciiReadMemoryCommand(string address, ushort length)
        {
            try
            {
                uint add = uint.Parse(address);
                byte[] command = new byte[20];
                command[0] = 0x30; // 부스 데이터 명령어 읽기
                command[1] = 0x36;
                command[2] = 0x31;
                command[3] = 0x33;
                command[4] = 0x30;
                command[5] = 0x30;
                command[6] = 0x30;
                command[7] = 0x30;
                ProtocolBytes.BuildAsciiBytesFrom(add).CopyTo(command, 8); // 시작 주소 정보
                ProtocolBytes.BuildAsciiBytesFrom(length).CopyTo(command, 16); // 소프트웨어의 길이는
                return OperationResult.CreateSuccessResult(command);
            }
            catch (Exception ex)
            {
                return new OperationResult<byte[]>(ex.Message);
            }
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "module">module에 사용할 입력값입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildAsciiReadSmartModule(ushort module, string address, ushort length)
        {
            try
            {
                uint add = uint.Parse(address);
                byte[] command = new byte[24];
                command[0] = 0x30; // 지능형 버퍼 데이터 명령어 읽기
                command[1] = 0x36;
                command[2] = 0x30;
                command[3] = 0x31;
                command[4] = 0x30;
                command[5] = 0x30;
                command[6] = 0x30;
                command[7] = 0x30;
                ProtocolBytes.BuildAsciiBytesFrom(add).CopyTo(command, 8); // 시작 주소의 하위 바이트
                ProtocolBytes.BuildAsciiBytesFrom(length).CopyTo(command, 16); // 주소의 길이는
                ProtocolBytes.BuildAsciiBytesFrom(module).CopyTo(command, 20); // 모듈 번호
                return OperationResult.CreateSuccessResult(command);
            }
            catch (Exception ex)
            {
                return new OperationResult<byte[]>(ex.Message);
            }
        }
    }
}
