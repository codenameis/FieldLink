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
using static FieldLink.PlcDrivers.Melsec.MelsecA1EAsciiNetResponseParser;

namespace FieldLink.PlcDrivers.Melsec
{
    /// <summary>MelsecA1EAsciiNet 요청 프레임을 생성합니다.</summary>
    public static class MelsecA1EAsciiNetCommandBuilder
    {
        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <param name = "isBit">비트 단위 요청이면 true입니다.</param>
        /// <param name = "plcNumber">plcNumber에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<List<byte[]>> BuildReadCommand(string address, ushort length, bool isBit, byte plcNumber)
        {
            var analysis = MelsecAddressParser.McA1EAnalysisAddress(address);
            if (!analysis.IsSuccess)
                return OperationResult.CreateFailedResult<List<byte[]>>(analysis);
            // 기본 정보 -- 주의: 높은 낮은 바이트 오차
            // byte subtitle = analysis.Content1.DataType == 0x01 ? (byte)0x00 : (byte)0x01;
            byte subtitle = isBit ? (byte)0x00 : (byte)0x01;
            int[] splits = ProtocolBytes.SplitIntegerToArray(length, isBit ? 256 : 64);
            List<byte[]> array = new List<byte[]>();
            for (int i = 0; i < splits.Length; i++)
            {
                byte[] _PLCCommand = new byte[24];
                _PLCCommand[0] = ProtocolBytes.BuildAsciiBytesFrom(subtitle)[0]; // 서브헤더
                _PLCCommand[1] = ProtocolBytes.BuildAsciiBytesFrom(subtitle)[1];
                _PLCCommand[2] = ProtocolBytes.BuildAsciiBytesFrom(plcNumber)[0]; // PLC 번호
                _PLCCommand[3] = ProtocolBytes.BuildAsciiBytesFrom(plcNumber)[1];
                _PLCCommand[4] = 0x30; // 감시 타이머, 10*250ms=2.5초
                _PLCCommand[5] = 0x30;
                _PLCCommand[6] = 0x30;
                _PLCCommand[7] = 0x41;
                _PLCCommand[8] = ProtocolBytes.BuildAsciiBytesFrom(BitConverter.GetBytes(analysis.Content1.DataCode)[1])[0];
                _PLCCommand[9] = ProtocolBytes.BuildAsciiBytesFrom(BitConverter.GetBytes(analysis.Content1.DataCode)[1])[1];
                _PLCCommand[10] = ProtocolBytes.BuildAsciiBytesFrom(BitConverter.GetBytes(analysis.Content1.DataCode)[0])[0];
                _PLCCommand[11] = ProtocolBytes.BuildAsciiBytesFrom(BitConverter.GetBytes(analysis.Content1.DataCode)[0])[1];
                _PLCCommand[12] = ProtocolBytes.BuildAsciiBytesFrom(BitConverter.GetBytes(analysis.Content2)[3])[0];
                _PLCCommand[13] = ProtocolBytes.BuildAsciiBytesFrom(BitConverter.GetBytes(analysis.Content2)[3])[1];
                _PLCCommand[14] = ProtocolBytes.BuildAsciiBytesFrom(BitConverter.GetBytes(analysis.Content2)[2])[0];
                _PLCCommand[15] = ProtocolBytes.BuildAsciiBytesFrom(BitConverter.GetBytes(analysis.Content2)[2])[1];
                _PLCCommand[16] = ProtocolBytes.BuildAsciiBytesFrom(BitConverter.GetBytes(analysis.Content2)[1])[0];
                _PLCCommand[17] = ProtocolBytes.BuildAsciiBytesFrom(BitConverter.GetBytes(analysis.Content2)[1])[1];
                _PLCCommand[18] = ProtocolBytes.BuildAsciiBytesFrom(BitConverter.GetBytes(analysis.Content2)[0])[0];
                _PLCCommand[19] = ProtocolBytes.BuildAsciiBytesFrom(BitConverter.GetBytes(analysis.Content2)[0])[1];
                int len = splits[i];
                if (len == 256)
                    len = 0x00;
                _PLCCommand[20] = ProtocolBytes.BuildAsciiBytesFrom(BitConverter.GetBytes(len % 256)[0])[0];
                _PLCCommand[21] = ProtocolBytes.BuildAsciiBytesFrom(BitConverter.GetBytes(len % 256)[0])[1];
                _PLCCommand[22] = 0x30;
                _PLCCommand[23] = 0x30;
                array.Add(_PLCCommand);
                analysis.Content2 += splits[i];
            }

            return OperationResult.CreateSuccessResult(array);
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "plcNumber">plcNumber에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteWordCommand(string address, byte[] value, byte plcNumber)
        {
            var analysis = MelsecAddressParser.McA1EAnalysisAddress(address);
            if (!analysis.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(analysis);
            value = MelsecValueConverter.TransByteArrayToAsciiByteArray(value);
            byte[] _PLCCommand = new byte[24 + value.Length];
            _PLCCommand[0] = 0x30; // 서브헤더
            _PLCCommand[1] = 0x33;
            _PLCCommand[2] = ProtocolBytes.BuildAsciiBytesFrom(plcNumber)[0]; // PLC 번호
            _PLCCommand[3] = ProtocolBytes.BuildAsciiBytesFrom(plcNumber)[1];
            _PLCCommand[4] = 0x30; // 감시 타이머, 10*250ms=2.5초
            _PLCCommand[5] = 0x30;
            _PLCCommand[6] = 0x30;
            _PLCCommand[7] = 0x41;
            _PLCCommand[8] = ProtocolBytes.BuildAsciiBytesFrom(BitConverter.GetBytes(analysis.Content1.DataCode)[1])[0];
            _PLCCommand[9] = ProtocolBytes.BuildAsciiBytesFrom(BitConverter.GetBytes(analysis.Content1.DataCode)[1])[1];
            _PLCCommand[10] = ProtocolBytes.BuildAsciiBytesFrom(BitConverter.GetBytes(analysis.Content1.DataCode)[0])[0];
            _PLCCommand[11] = ProtocolBytes.BuildAsciiBytesFrom(BitConverter.GetBytes(analysis.Content1.DataCode)[0])[1];
            _PLCCommand[12] = ProtocolBytes.BuildAsciiBytesFrom(BitConverter.GetBytes(analysis.Content2)[3])[0];
            _PLCCommand[13] = ProtocolBytes.BuildAsciiBytesFrom(BitConverter.GetBytes(analysis.Content2)[3])[1];
            _PLCCommand[14] = ProtocolBytes.BuildAsciiBytesFrom(BitConverter.GetBytes(analysis.Content2)[2])[0];
            _PLCCommand[15] = ProtocolBytes.BuildAsciiBytesFrom(BitConverter.GetBytes(analysis.Content2)[2])[1];
            _PLCCommand[16] = ProtocolBytes.BuildAsciiBytesFrom(BitConverter.GetBytes(analysis.Content2)[1])[0];
            _PLCCommand[17] = ProtocolBytes.BuildAsciiBytesFrom(BitConverter.GetBytes(analysis.Content2)[1])[1];
            _PLCCommand[18] = ProtocolBytes.BuildAsciiBytesFrom(BitConverter.GetBytes(analysis.Content2)[0])[0];
            _PLCCommand[19] = ProtocolBytes.BuildAsciiBytesFrom(BitConverter.GetBytes(analysis.Content2)[0])[1];
            _PLCCommand[20] = ProtocolBytes.BuildAsciiBytesFrom(BitConverter.GetBytes(value.Length / 4)[0])[0];
            _PLCCommand[21] = ProtocolBytes.BuildAsciiBytesFrom(BitConverter.GetBytes(value.Length / 4)[0])[1];
            _PLCCommand[22] = 0x30;
            _PLCCommand[23] = 0x30;
            value.CopyTo(_PLCCommand, 24);
            return OperationResult.CreateSuccessResult(_PLCCommand);
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "plcNumber">plcNumber에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteBoolCommand(string address, bool[] value, byte plcNumber)
        {
            var analysis = MelsecAddressParser.McA1EAnalysisAddress(address);
            if (!analysis.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(analysis);
            byte[] buffer = value.Select(m => m ? (byte)0x31 : (byte)0x30).ToArray();
            if (buffer.Length % 2 == 1)
                buffer = ProtocolBytes.SpliceArray<byte>(buffer, new byte[] { 0x30 });
            byte[] _PLCCommand = new byte[24 + buffer.Length];
            _PLCCommand[0] = 0x30; // 서브헤더
            _PLCCommand[1] = 0x32;
            _PLCCommand[2] = ProtocolBytes.BuildAsciiBytesFrom(plcNumber)[0]; // PLC 번호
            _PLCCommand[3] = ProtocolBytes.BuildAsciiBytesFrom(plcNumber)[1];
            _PLCCommand[4] = 0x30; // 감시 타이머, 10*250ms=2.5초
            _PLCCommand[5] = 0x30;
            _PLCCommand[6] = 0x30;
            _PLCCommand[7] = 0x41;
            _PLCCommand[8] = ProtocolBytes.BuildAsciiBytesFrom(BitConverter.GetBytes(analysis.Content1.DataCode)[1])[0];
            _PLCCommand[9] = ProtocolBytes.BuildAsciiBytesFrom(BitConverter.GetBytes(analysis.Content1.DataCode)[1])[1];
            _PLCCommand[10] = ProtocolBytes.BuildAsciiBytesFrom(BitConverter.GetBytes(analysis.Content1.DataCode)[0])[0];
            _PLCCommand[11] = ProtocolBytes.BuildAsciiBytesFrom(BitConverter.GetBytes(analysis.Content1.DataCode)[0])[1];
            _PLCCommand[12] = ProtocolBytes.BuildAsciiBytesFrom(BitConverter.GetBytes(analysis.Content2)[3])[0];
            _PLCCommand[13] = ProtocolBytes.BuildAsciiBytesFrom(BitConverter.GetBytes(analysis.Content2)[3])[1];
            _PLCCommand[14] = ProtocolBytes.BuildAsciiBytesFrom(BitConverter.GetBytes(analysis.Content2)[2])[0];
            _PLCCommand[15] = ProtocolBytes.BuildAsciiBytesFrom(BitConverter.GetBytes(analysis.Content2)[2])[1];
            _PLCCommand[16] = ProtocolBytes.BuildAsciiBytesFrom(BitConverter.GetBytes(analysis.Content2)[1])[0];
            _PLCCommand[17] = ProtocolBytes.BuildAsciiBytesFrom(BitConverter.GetBytes(analysis.Content2)[1])[1];
            _PLCCommand[18] = ProtocolBytes.BuildAsciiBytesFrom(BitConverter.GetBytes(analysis.Content2)[0])[0];
            _PLCCommand[19] = ProtocolBytes.BuildAsciiBytesFrom(BitConverter.GetBytes(analysis.Content2)[0])[1];
            _PLCCommand[20] = ProtocolBytes.BuildAsciiBytesFrom(BitConverter.GetBytes(value.Length)[0])[0];
            _PLCCommand[21] = ProtocolBytes.BuildAsciiBytesFrom(BitConverter.GetBytes(value.Length)[0])[1];
            _PLCCommand[22] = 0x30;
            _PLCCommand[23] = 0x30;
            buffer.CopyTo(_PLCCommand, 24);
            return OperationResult.CreateSuccessResult(_PLCCommand);
        }
    }
}
