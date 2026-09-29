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
using static FieldLink.PlcDrivers.Melsec.McBinaryResponseParser;

namespace FieldLink.PlcDrivers.Melsec
{
    /// <summary>McBinary 요청 프레임을 생성합니다.</summary>
    public static class McBinaryCommandBuilder
    {
        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "mc">mc에 사용할 입력값입니다.</param>
        /// <param name = "mcCore">포장할 명령 바이트입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackMcCommand(McFrameOptions mc, byte[] mcCore)
        {
            if (mcCore == null)
                throw new ArgumentNullException(nameof(mcCore));
            if (mcCore.Length > ushort.MaxValue - 2)
                throw new ArgumentOutOfRangeException(nameof(mcCore), "MC 요청 길이가 16비트 길이 필드를 초과합니다.");
            byte[] _PLCCommand = new byte[11 + mcCore.Length];
            _PLCCommand[0] = 0x50; // 서브헤더
            _PLCCommand[1] = 0x00;
            _PLCCommand[2] = mc.NetworkNumber; // 네트워크 번호
            _PLCCommand[3] = mc.PLCNumber; // PLC 번호
            _PLCCommand[4] = BitConverter.GetBytes(mc.TargetIOStation)[0]; // 목표 모듈 IO 번호
            _PLCCommand[5] = BitConverter.GetBytes(mc.TargetIOStation)[1];
            _PLCCommand[6] = mc.NetworkStationNumber; // 목표 모듈 국번
            _PLCCommand[7] = (byte)((_PLCCommand.Length - 9) % 256); // 요청 데이터 길이
            _PLCCommand[8] = (byte)((_PLCCommand.Length - 9) / 256);
            _PLCCommand[9] = 0x0A; // CPU 감시 타이머
            _PLCCommand[10] = 0x00;
            mcCore.CopyTo(_PLCCommand, 11);
            return _PLCCommand;
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "addressData">addressData에 사용할 입력값입니다.</param>
        /// <param name = "isBit">비트 단위 요청이면 true입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildReadMcCoreCommand(McDeviceAddress addressData, bool isBit)
        {
            ValidateBatchCount(addressData.Length, isBit);
            byte[] command = new byte[10];
            command[0] = 0x01; // 데이터 일괄 읽기 명령
            command[1] = 0x04;
            command[2] = isBit ? (byte)0x01 : (byte)0x00; // 점 단위 또는 워드 단위로 대량으로 읽습니다.
            command[3] = 0x00;
            command[4] = BitConverter.GetBytes(addressData.AddressStart)[0]; // 시작 주소의 하위 바이트
            command[5] = BitConverter.GetBytes(addressData.AddressStart)[1];
            command[6] = BitConverter.GetBytes(addressData.AddressStart)[2];
            command[7] = (byte)addressData.McDataType.DataCode; // 데이터를 표시하세요
            command[8] = (byte)(addressData.Length % 256); // 소프트웨어의 길이는
            command[9] = (byte)(addressData.Length / 256);
            return command;
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "addressData">addressData에 사용할 입력값입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildWriteWordCoreCommand(McDeviceAddress addressData, byte[] value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            if (value.Length % 2 != 0)
                throw new ArgumentException("MC 워드 쓰기 데이터는 2바이트 단위여야 합니다.", nameof(value));
            ValidateBatchCount(value.Length / 2, false);
            byte[] command = new byte[10 + value.Length];
            command[0] = 0x01; // 데이터 명령어에 대량 입력
            command[1] = 0x14;
            command[2] = 0x00; // 워드 단위로 뭉쳐서 읽습니다.
            command[3] = 0x00;
            command[4] = BitConverter.GetBytes(addressData.AddressStart)[0]; // 시작 주소의 하위 바이트
            command[5] = BitConverter.GetBytes(addressData.AddressStart)[1];
            command[6] = BitConverter.GetBytes(addressData.AddressStart)[2];
            command[7] = (byte)addressData.McDataType.DataCode; // 입력된 데이터를 지정합니다.
            command[8] = (byte)(value.Length / 2 % 256); // 디바이스 길이의 위치
            command[9] = (byte)(value.Length / 2 / 256);
            value.CopyTo(command, 10);
            return command;
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "addressData">addressData에 사용할 입력값입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildWriteBitCoreCommand(McDeviceAddress addressData, bool[] value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            ValidateBatchCount(value.Length, true);
            byte[] buffer = MelsecValueConverter.TransBoolArrayToByteData(value);
            byte[] command = new byte[10 + buffer.Length];
            command[0] = 0x01; // 데이터 명령어에 대량 입력
            command[1] = 0x14;
            command[2] = 0x01; // 단위로 묶어서 적어
            command[3] = 0x00;
            command[4] = BitConverter.GetBytes(addressData.AddressStart)[0]; // 시작 주소의 하위 바이트
            command[5] = BitConverter.GetBytes(addressData.AddressStart)[1];
            command[6] = BitConverter.GetBytes(addressData.AddressStart)[2];
            command[7] = (byte)addressData.McDataType.DataCode; // 입력된 데이터를 지정합니다.
            command[8] = (byte)(value.Length % 256); // 디바이스 길이의 위치
            command[9] = (byte)(value.Length / 256);
            buffer.CopyTo(command, 10);
            return command;
        }

        private static void ValidateBatchCount(int count, bool isBit)
        {
            // SH-080956ENG-N §5.2, subcommands 0000/0001; device-specific limits can be lower.
            if (count < 1 || count > (isBit ? 7168 : 960))
                throw new ArgumentOutOfRangeException(nameof(count), "MC 일괄 요청의 디바이스 점수가 허용 범위를 벗어났습니다.");
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "addressData">addressData에 사용할 입력값입니다.</param>
        /// <param name = "extend">extend에 사용할 입력값입니다.</param>
        /// <param name = "isBit">비트 단위 요청이면 true입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildReadMcCoreExtendCommand(McDeviceAddress addressData, ushort extend, bool isBit)
        {
            byte[] command = new byte[17];
            command[0] = 0x01; // 데이터 일괄 읽기 명령
            command[1] = 0x04;
            command[2] = isBit ? (byte)0x81 : (byte)0x80; // 점 단위 또는 워드 단위로 대량으로 읽습니다.
            command[3] = 0x00;
            command[4] = 0x00;
            command[5] = 0x00;
            command[6] = BitConverter.GetBytes(addressData.AddressStart)[0]; // 시작 주소의 하위 바이트
            command[7] = BitConverter.GetBytes(addressData.AddressStart)[1];
            command[8] = BitConverter.GetBytes(addressData.AddressStart)[2];
            command[9] = (byte)addressData.McDataType.DataCode; // 데이터를 표시하세요
            command[10] = 0x00;
            command[11] = 0x00;
            command[12] = BitConverter.GetBytes(extend)[0];
            command[13] = BitConverter.GetBytes(extend)[1];
            command[14] = 0xF9;
            command[15] = (byte)(addressData.Length % 256); // 소프트웨어의 길이는
            command[16] = (byte)(addressData.Length / 256);
            return command;
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildReadRandomWordCommand(McDeviceAddress[] address)
        {
            byte[] command = new byte[6 + address.Length * 4];
            command[0] = 0x03; // 데이터 일괄 읽기 명령
            command[1] = 0x04;
            command[2] = 0x00;
            command[3] = 0x00;
            command[4] = (byte)address.Length; // 방문자 수
            command[5] = 0x00; // 이중어 접속점 수
            for (int i = 0; i < address.Length; i++)
            {
                command[i * 4 + 6] = BitConverter.GetBytes(address[i].AddressStart)[0]; // 소프트웨어 시작 주소
                command[i * 4 + 7] = BitConverter.GetBytes(address[i].AddressStart)[1];
                command[i * 4 + 8] = BitConverter.GetBytes(address[i].AddressStart)[2];
                command[i * 4 + 9] = (byte)address[i].McDataType.DataCode; // 디바이스 코드
            }

            return command;
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildReadRandomCommand(McDeviceAddress[] address)
        {
            byte[] command = new byte[6 + address.Length * 6];
            command[0] = 0x06; // 데이터 일괄 읽기 명령
            command[1] = 0x04;
            command[2] = 0x00; // 하위 명령
            command[3] = 0x00;
            command[4] = (byte)address.Length; // 글자소프트웨어의 블록 수
            command[5] = 0x00; // 비트소프트웨어의 블록 수
            for (int i = 0; i < address.Length; i++)
            {
                command[i * 6 + 6] = BitConverter.GetBytes(address[i].AddressStart)[0]; // 글자 디바이스의 번호
                command[i * 6 + 7] = BitConverter.GetBytes(address[i].AddressStart)[1];
                command[i * 6 + 8] = BitConverter.GetBytes(address[i].AddressStart)[2];
                command[i * 6 + 9] = (byte)address[i].McDataType.DataCode; // 글자소프트웨어의 코드
                command[i * 6 + 10] = (byte)(address[i].Length % 256); // 소프트웨어의 길이는
                command[i * 6 + 11] = (byte)(address[i].Length / 256);
            }

            return command;
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "tags">tags에 사용할 입력값입니다.</param>
        /// <param name = "lengths">lengths에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildReadTag(string[] tags, ushort[] lengths)
        {
            if (tags.Length != lengths.Length)
                throw new Exception(ProtocolMessages.TwoParametersLengthIsNotSame);
            MemoryStream command = new MemoryStream();
            command.WriteByte(0x1A); // 라벨을 대량으로 읽도록 지시합니다.
            command.WriteByte(0x04);
            command.WriteByte(0x00); // 하위 명령
            command.WriteByte(0x00);
            command.WriteByte(BitConverter.GetBytes(tags.Length)[0]); // 점수
            command.WriteByte(BitConverter.GetBytes(tags.Length)[1]);
            command.WriteByte(0x00); // 지정하지 마세요
            command.WriteByte(0x00);
            for (int i = 0; i < tags.Length; i++)
            {
                byte[] tagBuffer = Encoding.Unicode.GetBytes(tags[i]);
                command.WriteByte(BitConverter.GetBytes(tagBuffer.Length / 2)[0]); // 태그 길이는
                command.WriteByte(BitConverter.GetBytes(tagBuffer.Length / 2)[1]);
                command.Write(tagBuffer, 0, tagBuffer.Length); // 표지 이름
                command.WriteByte(0x01); // 단위 지정
                command.WriteByte(0x00); // 고정값
                command.WriteByte(BitConverter.GetBytes(lengths[i] * 2)[0]); // 데이터 길이
                command.WriteByte(BitConverter.GetBytes(lengths[i] * 2)[1]);
            }

            byte[] buffer = command.ToArray();
            command.Dispose();
            return buffer;
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildReadMemoryCommand(string address, ushort length)
        {
            try
            {
                uint add = uint.Parse(address);
                byte[] command = new byte[10];
                command[0] = 0x13; // 부스 데이터 명령어 읽기
                command[1] = 0x06;
                command[2] = 0x00;
                command[3] = 0x00;
                command[4] = BitConverter.GetBytes(add)[0]; // 시작 주소의 하위 바이트
                command[5] = BitConverter.GetBytes(add)[1];
                command[6] = BitConverter.GetBytes(add)[2];
                command[7] = BitConverter.GetBytes(add)[3];
                command[8] = (byte)(length % 256); // 소프트웨어의 길이는
                command[9] = (byte)(length / 256);
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
        public static OperationResult<byte[]> BuildReadSmartModule(ushort module, string address, ushort length)
        {
            try
            {
                uint add = uint.Parse(address);
                byte[] command = new byte[12];
                command[0] = 0x01; // 지능형 버퍼 데이터 명령어 읽기
                command[1] = 0x06;
                command[2] = 0x00;
                command[3] = 0x00;
                command[4] = BitConverter.GetBytes(add)[0]; // 시작 주소의 하위 바이트
                command[5] = BitConverter.GetBytes(add)[1];
                command[6] = BitConverter.GetBytes(add)[2];
                command[7] = BitConverter.GetBytes(add)[3];
                command[8] = (byte)(length % 256); // 주소의 길이는
                command[9] = (byte)(length / 256);
                command[10] = BitConverter.GetBytes(module)[0]; // 모듈 번호
                command[11] = BitConverter.GetBytes(module)[1];
                return OperationResult.CreateSuccessResult(command);
            }
            catch (Exception ex)
            {
                return new OperationResult<byte[]>(ex.Message);
            }
        }
    }
}
