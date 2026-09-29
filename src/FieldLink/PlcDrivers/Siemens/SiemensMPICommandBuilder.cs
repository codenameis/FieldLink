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
using static FieldLink.PlcDrivers.Siemens.SiemensMPIResponseParser;

namespace FieldLink.PlcDrivers.Siemens
{
    /// <summary>SiemensMPI 요청 프레임을 생성합니다.</summary>
    public static class SiemensMPICommandBuilder
    {
        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <param name = "isBit">비트 단위 요청이면 true입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildReadCommand(byte station, string address, ushort length, bool isBit)
        {
            OperationResult<S7DeviceAddress> analysis = S7DeviceAddress.ParseFrom(address, length);
            if (!analysis.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(analysis);
            byte[] _PLCCommand = new byte[38];
            _PLCCommand[0] = 0x68;
            _PLCCommand[1] = BitConverter.GetBytes(_PLCCommand.Length - 6)[0];
            _PLCCommand[2] = BitConverter.GetBytes(_PLCCommand.Length - 6)[0];
            _PLCCommand[3] = 0x68;
            _PLCCommand[4] = (byte)(station + 0x80);
            _PLCCommand[5] = (byte)(0x00 + 0x80);
            _PLCCommand[6] = 0x7C;
            _PLCCommand[7] = 0x16;
            _PLCCommand[8] = 0x01;
            _PLCCommand[9] = 0xF1;
            _PLCCommand[10] = 0x00;
            _PLCCommand[11] = 0x32;
            _PLCCommand[12] = 0x01;
            _PLCCommand[13] = 0x00;
            _PLCCommand[14] = 0x00;
            _PLCCommand[15] = 0x33;
            _PLCCommand[16] = 0x02;
            _PLCCommand[17] = 0x00;
            _PLCCommand[18] = 0x0E;
            _PLCCommand[19] = 0x00;
            _PLCCommand[20] = 0x00;
            _PLCCommand[21] = 0x04;
            _PLCCommand[22] = 0x01;
            _PLCCommand[23] = 0x12;
            _PLCCommand[24] = 0x0A;
            _PLCCommand[25] = 0x10;
            _PLCCommand[26] = isBit ? (byte)0x01 : (byte)0x02;
            _PLCCommand[27] = BitConverter.GetBytes(length)[1];
            _PLCCommand[28] = BitConverter.GetBytes(length)[0];
            _PLCCommand[29] = BitConverter.GetBytes(analysis.Content.DbBlock)[1];
            _PLCCommand[30] = BitConverter.GetBytes(analysis.Content.DbBlock)[0];
            _PLCCommand[31] = GetLegacyMpiArea(analysis.Content.DataCode);
            _PLCCommand[32] = BitConverter.GetBytes(analysis.Content.AddressStart)[2];
            _PLCCommand[33] = BitConverter.GetBytes(analysis.Content.AddressStart)[1];
            _PLCCommand[34] = BitConverter.GetBytes(analysis.Content.AddressStart)[0];
            int count = 0;
            for (int i = 4; i < 35; i++)
            {
                count += _PLCCommand[i];
            }

            _PLCCommand[35] = BitConverter.GetBytes(count)[0];
            _PLCCommand[36] = 0x16;
            _PLCCommand[37] = 0xE5;
            return OperationResult.CreateSuccessResult(_PLCCommand);
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "values">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteCommand(byte station, string address, byte[] values)
        {
            OperationResult<S7DeviceAddress> analysis = S7DeviceAddress.ParseFrom(address);
            if (!analysis.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(analysis);
            int length = values.Length;
            // 68 21 21 68 02 00 6C 32 01 00 00 00 00 00 0E 00 00 04 01 12 0A 10
            byte[] _PLCCommand = new byte[42 + values.Length];
            _PLCCommand[0] = 0x68;
            _PLCCommand[1] = BitConverter.GetBytes(_PLCCommand.Length - 6)[0];
            _PLCCommand[2] = BitConverter.GetBytes(_PLCCommand.Length - 6)[0];
            _PLCCommand[3] = 0x68;
            _PLCCommand[4] = (byte)(station + 0x80);
            _PLCCommand[5] = (byte)(0x00 + 0x80);
            _PLCCommand[6] = 0x5C;
            _PLCCommand[7] = 0x16;
            _PLCCommand[8] = 0x02;
            _PLCCommand[9] = 0xF1;
            _PLCCommand[10] = 0x00;
            _PLCCommand[11] = 0x32;
            _PLCCommand[12] = 0x01;
            _PLCCommand[13] = 0x00;
            _PLCCommand[14] = 0x00;
            _PLCCommand[15] = 0x43;
            _PLCCommand[16] = 0x02;
            _PLCCommand[17] = 0x00;
            _PLCCommand[18] = 0x0E;
            _PLCCommand[19] = 0x00;
            _PLCCommand[20] = (byte)(values.Length + 4);
            _PLCCommand[21] = 0x05;
            _PLCCommand[22] = 0x01;
            _PLCCommand[23] = 0x12;
            _PLCCommand[24] = 0x0A;
            _PLCCommand[25] = 0x10;
            _PLCCommand[26] = 0x02;
            _PLCCommand[27] = BitConverter.GetBytes(length)[0];
            _PLCCommand[28] = BitConverter.GetBytes(length)[1];
            _PLCCommand[29] = BitConverter.GetBytes(analysis.Content.DbBlock)[0];
            _PLCCommand[30] = BitConverter.GetBytes(analysis.Content.DbBlock)[1];
            _PLCCommand[31] = GetLegacyMpiArea(analysis.Content.DataCode);
            _PLCCommand[32] = BitConverter.GetBytes(analysis.Content.AddressStart)[2];
            _PLCCommand[33] = BitConverter.GetBytes(analysis.Content.AddressStart)[1];
            _PLCCommand[34] = BitConverter.GetBytes(analysis.Content.AddressStart)[0];
            _PLCCommand[35] = 0x00;
            _PLCCommand[36] = 0x04;
            _PLCCommand[37] = BitConverter.GetBytes(length * 8)[1];
            _PLCCommand[38] = BitConverter.GetBytes(length * 8)[0];
            values.CopyTo(_PLCCommand, 39);
            int count = 0;
            for (int i = 4; i < _PLCCommand.Length - 3; i++)
            {
                count += _PLCCommand[i];
            }

            _PLCCommand[_PLCCommand.Length - 3] = BitConverter.GetBytes(count)[0];
            _PLCCommand[_PLCCommand.Length - 2] = 0x16;
            _PLCCommand[_PLCCommand.Length - 1] = 0xE5;
            return OperationResult.CreateSuccessResult(_PLCCommand);
        }

        private static byte GetLegacyMpiArea(byte dataCode)
        {
            // 공유 주소 파서의 ISO/TCP C/T 수정이 미검증 MPI 프레임을 바꾸지 않도록 기존 매핑을 보존한다.
            // MPI 기종별 영역·단위 정상화는 별도 문서와 장비 검증이 필요하다.
            return dataCode == 0x1C || dataCode == 0x1D ? (byte)(dataCode + 2) : dataCode;
        }
    }
}
