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
using static FieldLink.PlcDrivers.Melsec.MelsecFxSerialResponseParser;
using static FieldLink.PlcDrivers.Melsec.MelsecFxSerialAddressParser;

namespace FieldLink.PlcDrivers.Melsec
{
    /// <summary>MelsecFxSerial 요청 프레임을 생성합니다.</summary>
    public static class MelsecFxSerialCommandBuilder
    {
        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteBoolPacket(string address, bool value)
        {
            var analysis = FxAnalysisAddress(address);
            if (!analysis.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(analysis);
            // 이 글은 특별한 문자로 쓰여져 있습니다.
            // 두 번째 연산 초기 주소 오차량, 유형에 따라 다르며, 주소 계산 방식이 다르다
            ushort startAddress = analysis.Content2;
            if (analysis.Content1 == MelsecMcDataType.M)
            {
                if (startAddress >= 8000)
                    startAddress = (ushort)(startAddress - 8000 + 0x0F00);
                else
                    startAddress = (ushort)(startAddress + 0x0800);
            }
            else if (analysis.Content1 == MelsecMcDataType.S)
                startAddress = (ushort)(startAddress + 0x0000);
            else if (analysis.Content1 == MelsecMcDataType.X)
                startAddress = (ushort)(startAddress + 0x0400);
            else if (analysis.Content1 == MelsecMcDataType.Y)
                startAddress = (ushort)(startAddress + 0x0500);
            else if (analysis.Content1 == MelsecMcDataType.CS)
                startAddress = (ushort)(startAddress + 0x01C0);
            else if (analysis.Content1 == MelsecMcDataType.CC)
                startAddress = (ushort)(startAddress + 0x03C0);
            else if (analysis.Content1 == MelsecMcDataType.CN)
                startAddress = (ushort)(startAddress + 0x0E00);
            else if (analysis.Content1 == MelsecMcDataType.TS)
                startAddress = (ushort)(startAddress + 0x00C0);
            else if (analysis.Content1 == MelsecMcDataType.TC)
                startAddress = (ushort)(startAddress + 0x02C0);
            else if (analysis.Content1 == MelsecMcDataType.TN)
                startAddress = (ushort)(startAddress + 0x0600);
            else
                return new OperationResult<byte[]>(ProtocolMessages.MelsecCurrentTypeNotSupportedBitOperate);
            byte[] _PLCCommand = new byte[9];
            _PLCCommand[0] = AsciiControl.STX; // STX
            _PLCCommand[1] = value ? (byte)0x37 : (byte)0x38; // 강제 ON 또는 OFF
            _PLCCommand[2] = ProtocolBytes.BuildAsciiBytesFrom(startAddress)[2]; // 오프셋 주소
            _PLCCommand[3] = ProtocolBytes.BuildAsciiBytesFrom(startAddress)[3];
            _PLCCommand[4] = ProtocolBytes.BuildAsciiBytesFrom(startAddress)[0];
            _PLCCommand[5] = ProtocolBytes.BuildAsciiBytesFrom(startAddress)[1];
            _PLCCommand[6] = 0x03; // ETX
            MelsecValueConverter.FxCalculateCRC(_PLCCommand).CopyTo(_PLCCommand, 7); // CRC
            return OperationResult.CreateSuccessResult(_PLCCommand);
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <param name = "isNewVersion">isNewVersion에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<List<byte[]>> BuildReadWordCommand(string address, ushort length, bool isNewVersion)
        {
            var addressResult = FxCalculateWordStartAddress(address, isNewVersion);
            if (!addressResult.IsSuccess)
                return OperationResult.CreateFailedResult<List<byte[]>>(addressResult);
            length = (ushort)(length * 2);
            ushort startAddress = addressResult.Content;
            int[] splits = ProtocolBytes.SplitIntegerToArray(length, 254);
            List<byte[]> array = new List<byte[]>();
            for (int i = 0; i < splits.Length; i++)
            {
                if (isNewVersion)
                {
                    byte[] _PLCCommand = new byte[13];
                    _PLCCommand[0] = AsciiControl.STX; //헤더
                    _PLCCommand[1] = 0x45; //명령 형식
                    _PLCCommand[2] = 0x30; //읽기
                    _PLCCommand[3] = 0x30; //읽기
                    _PLCCommand[4] = ProtocolBytes.BuildAsciiBytesFrom(startAddress)[0]; //시작 주소
                    _PLCCommand[5] = ProtocolBytes.BuildAsciiBytesFrom(startAddress)[1]; //시작 주소
                    _PLCCommand[6] = ProtocolBytes.BuildAsciiBytesFrom(startAddress)[2]; //시작 주소
                    _PLCCommand[7] = ProtocolBytes.BuildAsciiBytesFrom(startAddress)[3]; //시작 주소
                    _PLCCommand[8] = ProtocolBytes.BuildAsciiBytesFrom((byte)splits[i])[0]; // 읽을 길이
                    _PLCCommand[9] = ProtocolBytes.BuildAsciiBytesFrom((byte)splits[i])[1];
                    _PLCCommand[10] = AsciiControl.ETX;
                    MelsecValueConverter.FxCalculateCRC(_PLCCommand).CopyTo(_PLCCommand, 11); // CRC
                    array.Add(_PLCCommand);
                    startAddress = (ushort)(startAddress + splits[i]);
                }
                else
                {
                    byte[] _PLCCommand = new byte[11];
                    _PLCCommand[0] = AsciiControl.STX; // STX
                    _PLCCommand[1] = 0x30; // 읽기
                    _PLCCommand[2] = ProtocolBytes.BuildAsciiBytesFrom(startAddress)[0]; // 오프셋 주소
                    _PLCCommand[3] = ProtocolBytes.BuildAsciiBytesFrom(startAddress)[1];
                    _PLCCommand[4] = ProtocolBytes.BuildAsciiBytesFrom(startAddress)[2];
                    _PLCCommand[5] = ProtocolBytes.BuildAsciiBytesFrom(startAddress)[3];
                    _PLCCommand[6] = ProtocolBytes.BuildAsciiBytesFrom((byte)splits[i])[0]; // 읽을 길이
                    _PLCCommand[7] = ProtocolBytes.BuildAsciiBytesFrom((byte)splits[i])[1];
                    _PLCCommand[8] = AsciiControl.ETX; // ETX
                    MelsecValueConverter.FxCalculateCRC(_PLCCommand).CopyTo(_PLCCommand, 9); // CRC
                    array.Add(_PLCCommand);
                    startAddress = (ushort)(startAddress + splits[i]);
                }
            }

            return OperationResult.CreateSuccessResult(array);
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <param name = "isNewVersion">isNewVersion에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<List<byte[]>, int> BuildReadBoolCommand(string address, ushort length, bool isNewVersion)
        {
            var addressResult = FxCalculateBoolStartAddress(address, isNewVersion);
            if (!addressResult.IsSuccess)
                return OperationResult.CreateFailedResult<List<byte[]>, int>(addressResult);
            // 실제로 읽어야 할 데이터의 길이를 계산합니다.
            ushort length2 = (ushort)AddressParameters.CalculateOccupyLength(addressResult.Content2, length);
            ushort startAddress = addressResult.Content1;
            int[] splits = ProtocolBytes.SplitIntegerToArray(length2, 254);
            List<byte[]> array = new List<byte[]>();
            for (int i = 0; i < splits.Length; i++)
            {
                if (isNewVersion)
                {
                    byte[] _PLCCommand = new byte[13];
                    _PLCCommand[0] = AsciiControl.STX; //헤더
                    _PLCCommand[1] = 0x45; //명령 형식
                    _PLCCommand[2] = 0x30; //읽기
                    _PLCCommand[3] = 0x30; //읽기
                    _PLCCommand[4] = ProtocolBytes.BuildAsciiBytesFrom(startAddress)[0]; //시작 주소
                    _PLCCommand[5] = ProtocolBytes.BuildAsciiBytesFrom(startAddress)[1]; //시작 주소
                    _PLCCommand[6] = ProtocolBytes.BuildAsciiBytesFrom(startAddress)[2]; //시작 주소
                    _PLCCommand[7] = ProtocolBytes.BuildAsciiBytesFrom(startAddress)[3]; //시작 주소
                    _PLCCommand[8] = ProtocolBytes.BuildAsciiBytesFrom((byte)splits[i])[0]; // 읽을 길이
                    _PLCCommand[9] = ProtocolBytes.BuildAsciiBytesFrom((byte)splits[i])[1];
                    _PLCCommand[10] = AsciiControl.ETX;
                    MelsecValueConverter.FxCalculateCRC(_PLCCommand).CopyTo(_PLCCommand, 11); // CRC
                    array.Add(_PLCCommand);
                }
                else
                {
                    byte[] _PLCCommand = new byte[11];
                    _PLCCommand[0] = AsciiControl.STX; // STX
                    _PLCCommand[1] = 0x30; // 읽기
                    _PLCCommand[2] = ProtocolBytes.BuildAsciiBytesFrom(startAddress)[0]; // 오프셋 주소
                    _PLCCommand[3] = ProtocolBytes.BuildAsciiBytesFrom(startAddress)[1];
                    _PLCCommand[4] = ProtocolBytes.BuildAsciiBytesFrom(startAddress)[2];
                    _PLCCommand[5] = ProtocolBytes.BuildAsciiBytesFrom(startAddress)[3];
                    _PLCCommand[6] = ProtocolBytes.BuildAsciiBytesFrom((byte)splits[i])[0]; // 읽을 길이
                    _PLCCommand[7] = ProtocolBytes.BuildAsciiBytesFrom((byte)splits[i])[1];
                    _PLCCommand[8] = AsciiControl.ETX; // ETX
                    MelsecValueConverter.FxCalculateCRC(_PLCCommand).CopyTo(_PLCCommand, 9); // CRC
                    array.Add(_PLCCommand);
                }

                startAddress = (ushort)(startAddress + splits[i]);
            }

            return OperationResult.CreateSuccessResult(array, (int)addressResult.Content3); // 반환
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "isNewVersion">isNewVersion에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteWordCommand(string address, byte[] value, bool isNewVersion)
        {
            var addressResult = FxCalculateWordStartAddress(address, isNewVersion);
            if (!addressResult.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(addressResult);
            // 바이트 데이터를 ASCII 형식으로 변환
            if (value != null)
                value = ProtocolBytes.BuildAsciiBytesFrom(value);
            ushort startAddress = addressResult.Content;
            if (isNewVersion)
            {
                byte[] _PLCCommand = new byte[13 + value.Length];
                _PLCCommand[0] = AsciiControl.STX; //헤더
                _PLCCommand[1] = 0x45; //명령 형식
                _PLCCommand[2] = 0x31; //쓰기
                _PLCCommand[3] = 0x30; //쓰기
                _PLCCommand[4] = ProtocolBytes.BuildAsciiBytesFrom(startAddress)[0]; // 목적 주소
                _PLCCommand[5] = ProtocolBytes.BuildAsciiBytesFrom(startAddress)[1];
                _PLCCommand[6] = ProtocolBytes.BuildAsciiBytesFrom(startAddress)[2];
                _PLCCommand[7] = ProtocolBytes.BuildAsciiBytesFrom(startAddress)[3];
                _PLCCommand[8] = ProtocolBytes.BuildAsciiBytesFrom((byte)(value.Length / 2))[0]; // 읽기 길이
                _PLCCommand[9] = ProtocolBytes.BuildAsciiBytesFrom((byte)(value.Length / 2))[1];
                Array.Copy(value, 0, _PLCCommand, 10, value.Length);
                _PLCCommand[_PLCCommand.Length - 3] = AsciiControl.ETX; // ETX
                MelsecValueConverter.FxCalculateCRC(_PLCCommand).CopyTo(_PLCCommand, _PLCCommand.Length - 2); // CRC
                return OperationResult.CreateSuccessResult(_PLCCommand);
            }
            else
            {
                byte[] _PLCCommand = new byte[11 + value.Length];
                _PLCCommand[0] = AsciiControl.STX; //헤더
                _PLCCommand[1] = 0x31; //쓰기
                _PLCCommand[2] = ProtocolBytes.BuildAsciiBytesFrom(startAddress)[0]; // 목적 주소
                _PLCCommand[3] = ProtocolBytes.BuildAsciiBytesFrom(startAddress)[1];
                _PLCCommand[4] = ProtocolBytes.BuildAsciiBytesFrom(startAddress)[2];
                _PLCCommand[5] = ProtocolBytes.BuildAsciiBytesFrom(startAddress)[3];
                _PLCCommand[6] = ProtocolBytes.BuildAsciiBytesFrom((byte)(value.Length / 2))[0]; // 읽기 길이
                _PLCCommand[7] = ProtocolBytes.BuildAsciiBytesFrom((byte)(value.Length / 2))[1];
                Array.Copy(value, 0, _PLCCommand, 8, value.Length);
                _PLCCommand[_PLCCommand.Length - 3] = AsciiControl.ETX; // ETX
                MelsecValueConverter.FxCalculateCRC(_PLCCommand).CopyTo(_PLCCommand, _PLCCommand.Length - 2); // CRC
                return OperationResult.CreateSuccessResult(_PLCCommand);
            }
        }
    }
}
