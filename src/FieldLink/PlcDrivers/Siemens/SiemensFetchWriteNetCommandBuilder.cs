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
using static FieldLink.PlcDrivers.Siemens.SiemensFetchWriteNetAddressParser;
using static FieldLink.PlcDrivers.Siemens.SiemensFetchWriteNetResponseParser;

namespace FieldLink.PlcDrivers.Siemens
{
    /// <summary>SiemensFetchWriteNet 요청 프레임을 생성합니다.</summary>
    public static class SiemensFetchWriteNetCommandBuilder
    {
        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "count">count에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildReadCommand(string address, ushort count)
        {
            OperationResult<byte, int, ushort> analysis = AnalysisAddress(address);
            if (!analysis.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(analysis);
            byte[] _PLCCommand = new byte[16];
            _PLCCommand[0] = 0x53;
            _PLCCommand[1] = 0x35;
            _PLCCommand[2] = 0x10;
            _PLCCommand[3] = 0x01;
            _PLCCommand[4] = 0x03;
            _PLCCommand[5] = 0x05;
            _PLCCommand[6] = 0x03;
            _PLCCommand[7] = 0x08;
            // 데이터 영역을 지정 -> Specify Data area
            _PLCCommand[8] = analysis.Content1;
            _PLCCommand[9] = (byte)analysis.Content3;
            // 데이터 주소를 지정 -> Specify Data address
            _PLCCommand[10] = (byte)(analysis.Content2 / 256);
            _PLCCommand[11] = (byte)(analysis.Content2 % 256);
            // DB 블록, 타이머, 카운터는 길이를 문자 단위로 읽습니다. 1은 2 바이트를, I, Q, M의 1은 1 바이트를 ->
            // DB 블록, 타이머, 카운터 워드당 읽기 길이, 1 2 바이트, i,q,m 1 1 바이트
            if (analysis.Content1 == 0x01 || analysis.Content1 == 0x06 || analysis.Content1 == 0x07)
            {
                if (count % 2 != 0)
                {
                    return new OperationResult<byte[]>(ProtocolMessages.SiemensReadLengthMustBeEvenNumber);
                }
                else
                {
                    _PLCCommand[12] = BitConverter.GetBytes(count / 2)[1];
                    _PLCCommand[13] = BitConverter.GetBytes(count / 2)[0];
                }
            }
            else
            {
                _PLCCommand[12] = BitConverter.GetBytes(count)[1];
                _PLCCommand[13] = BitConverter.GetBytes(count)[0];
            }

            _PLCCommand[14] = 0xff;
            _PLCCommand[15] = 0x02;
            return OperationResult.CreateSuccessResult(_PLCCommand);
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteCommand(string address, byte[] data)
        {
            if (data == null)
                data = new byte[0];
            OperationResult<byte, int, ushort> analysis = AnalysisAddress(address);
            if (!analysis.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(analysis);
            byte[] _PLCCommand = new byte[16 + data.Length];
            _PLCCommand[0] = 0x53;
            _PLCCommand[1] = 0x35;
            _PLCCommand[2] = 0x10;
            _PLCCommand[3] = 0x01;
            _PLCCommand[4] = 0x03;
            _PLCCommand[5] = 0x03;
            _PLCCommand[6] = 0x03;
            _PLCCommand[7] = 0x08;
            // 데이터 영역을 지정 -> Specify Data area
            _PLCCommand[8] = analysis.Content1;
            _PLCCommand[9] = (byte)analysis.Content3;
            // 데이터 주소를 지정 -> Specify Data address
            _PLCCommand[10] = (byte)(analysis.Content2 / 256);
            _PLCCommand[11] = (byte)(analysis.Content2 % 256);
            if (analysis.Content1 == 0x01 || analysis.Content1 == 0x06 || analysis.Content1 == 0x07)
            {
                if (data.Length % 2 != 0)
                {
                    return new OperationResult<byte[]>(ProtocolMessages.SiemensReadLengthMustBeEvenNumber);
                }
                else
                {
                    // 데이터 길이 -> Specify data length
                    _PLCCommand[12] = BitConverter.GetBytes(data.Length / 2)[1];
                    _PLCCommand[13] = BitConverter.GetBytes(data.Length / 2)[0];
                }
            }
            else
            {
                // 데이터 길이 -> Specify data length
                _PLCCommand[12] = BitConverter.GetBytes(data.Length)[1];
                _PLCCommand[13] = BitConverter.GetBytes(data.Length)[0];
            }

            _PLCCommand[14] = 0xff;
            _PLCCommand[15] = 0x02;
            // 데이터 배치 -> 데이터 배치
            Array.Copy(data, 0, _PLCCommand, 16, data.Length);
            return OperationResult.CreateSuccessResult(_PLCCommand);
        }
    }
}
