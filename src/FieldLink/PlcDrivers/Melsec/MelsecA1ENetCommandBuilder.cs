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
using static FieldLink.PlcDrivers.Melsec.MelsecA1ENetResponseParser;

namespace FieldLink.PlcDrivers.Melsec
{
    /// <summary>MelsecA1ENet 요청 프레임을 생성합니다.</summary>
    public static class MelsecA1ENetCommandBuilder
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
                byte[] _PLCCommand = new byte[12];
                _PLCCommand[0] = subtitle; // 서브헤더
                _PLCCommand[1] = plcNumber; // PLC 번호
                _PLCCommand[2] = 0x0A; // CPU 감시 타이머 ((L) 이 여기 0x00,0x0A로 설정되어 있으며, CPU의 반환을 기다리는 시간은 10*250ms=2.5초입니다.
                _PLCCommand[3] = 0x00; // CPU 감시 타이머 (H)
                _PLCCommand[4] = BitConverter.GetBytes(analysis.Content2)[0]; // 시작 소프트웨어 (읽기 시작하는 주소)
                _PLCCommand[5] = BitConverter.GetBytes(analysis.Content2)[1];
                _PLCCommand[6] = BitConverter.GetBytes(analysis.Content2)[2];
                _PLCCommand[7] = BitConverter.GetBytes(analysis.Content2)[3];
                _PLCCommand[8] = BitConverter.GetBytes(analysis.Content1.DataCode)[0]; // 소프트웨어 코드
                _PLCCommand[9] = BitConverter.GetBytes(analysis.Content1.DataCode)[1]; // 소프트웨어 코드 (H)
                int len = splits[i];
                if (len == 256)
                    len = 0x00;
                _PLCCommand[10] = BitConverter.GetBytes(len)[0]; // 디바이스 점수
                _PLCCommand[11] = BitConverter.GetBytes(len)[1];
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
            byte[] _PLCCommand = new byte[12 + value.Length];
            _PLCCommand[0] = 0x03; // 부제목, 단위 단위로 작성
            _PLCCommand[1] = plcNumber; // PLC 번호
            _PLCCommand[2] = 0x0A; // CPU 감시 타이머 ((L) 이 여기 0x00,0x0A로 설정되어 있으며, CPU의 반환을 기다리는 시간은 10*250ms=2.5초입니다.
            _PLCCommand[3] = 0x00; // CPU 감시 타이머 (H)
            _PLCCommand[4] = BitConverter.GetBytes(analysis.Content2)[0]; // 시작 소프트웨어 (읽기 시작하는 주소)
            _PLCCommand[5] = BitConverter.GetBytes(analysis.Content2)[1];
            _PLCCommand[6] = BitConverter.GetBytes(analysis.Content2)[2];
            _PLCCommand[7] = BitConverter.GetBytes(analysis.Content2)[3];
            _PLCCommand[8] = BitConverter.GetBytes(analysis.Content1.DataCode)[0]; // 소프트웨어 코드
            _PLCCommand[9] = BitConverter.GetBytes(analysis.Content1.DataCode)[1]; // 소프트웨어 코드 (H)
            _PLCCommand[10] = BitConverter.GetBytes(value.Length / 2)[0]; // 디바이스 점수
            _PLCCommand[11] = BitConverter.GetBytes(value.Length / 2)[1];
            Array.Copy(value, 0, _PLCCommand, 12, value.Length); // 특정 입력 데이터를 입력 명령 뒤에 부착합니다.
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
            byte[] buffer = MelsecValueConverter.TransBoolArrayToByteData(value);
            byte[] _PLCCommand = new byte[12 + buffer.Length];
            _PLCCommand[0] = 02; // 부제목, 비트 단위로 작성
            _PLCCommand[1] = plcNumber; // PLC 번호
            _PLCCommand[2] = 0x0A; // CPU 감시 타이머 ((L) 이 여기 0x00,0x0A로 설정되어 있으며, CPU의 반환을 기다리는 시간은 10*250ms=2.5초입니다.
            _PLCCommand[3] = 0x00; // CPU 감시 타이머 (H)
            _PLCCommand[4] = BitConverter.GetBytes(analysis.Content2)[0]; // 시작 소프트웨어 (읽기 시작하는 주소)
            _PLCCommand[5] = BitConverter.GetBytes(analysis.Content2)[1];
            _PLCCommand[6] = BitConverter.GetBytes(analysis.Content2)[2];
            _PLCCommand[7] = BitConverter.GetBytes(analysis.Content2)[3];
            _PLCCommand[8] = BitConverter.GetBytes(analysis.Content1.DataCode)[0]; // 소프트웨어 코드
            _PLCCommand[9] = BitConverter.GetBytes(analysis.Content1.DataCode)[1]; // 소프트웨어 코드 (H)
            _PLCCommand[10] = BitConverter.GetBytes(value.Length)[0]; // 디바이스 점수
            _PLCCommand[11] = BitConverter.GetBytes(value.Length)[1];
            Array.Copy(buffer, 0, _PLCCommand, 12, buffer.Length); // 특정 입력 데이터를 입력 명령 뒤에 부착합니다.
            return OperationResult.CreateSuccessResult(_PLCCommand);
        }
    }
}
