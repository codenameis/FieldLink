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
using static FieldLink.PlcDrivers.Siemens.SiemensS7NetAddressParser;
using static FieldLink.PlcDrivers.Siemens.SiemensS7NetResponseParser;
using static FieldLink.PlcDrivers.Siemens.SiemensS7NetDefinitions;

namespace FieldLink.PlcDrivers.Siemens
{
    /// <summary>SiemensS7Net 요청 프레임을 생성합니다.</summary>
    public static class SiemensS7NetCommandBuilder
    {
        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "s7Addresses">s7Addresses에 사용할 입력값입니다.</param>
        /// <param name = "msgId">msgId에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildReadCommand(S7DeviceAddress[] s7Addresses, int msgId)
        {
            if (s7Addresses == null || s7Addresses.Length == 0)
                return new OperationResult<byte[]>("S7 읽기 주소가 없습니다.");
            if (s7Addresses.Length > 19)
                throw new Exception(ProtocolMessages.SiemensReadLengthCannotLargerThan19);
            foreach (S7DeviceAddress address in s7Addresses)
            {
                OperationResult validation = S7DeviceAddress.ValidateWireAddress(address);
                if (!validation.IsSuccess)
                    return validation.ConvertFailed<byte[]>();
            }
            int readCount = s7Addresses.Length;
            byte[] _PLCCommand = new byte[19 + readCount * 12];
            // ======================================================================================
            _PLCCommand[0] = 0x03; // 제목 -> Head
            _PLCCommand[1] = 0x00;
            _PLCCommand[2] = (byte)(_PLCCommand.Length / 256); // 길이 -> Length
            _PLCCommand[3] = (byte)(_PLCCommand.Length % 256);
            _PLCCommand[4] = 0x02; // 고정 -> 고정
            _PLCCommand[5] = 0xF0;
            _PLCCommand[6] = 0x80;
            _PLCCommand[7] = 0x32; // 프로토콜 식별 -> Protocol identification
            _PLCCommand[8] = 0x01; // 명령: 보내 -> Command: Send
            _PLCCommand[9] = 0x00; // redundancy identification (reserved): 0x0000;
            _PLCCommand[10] = 0x00;
            _PLCCommand[11] = BitConverter.GetBytes(msgId)[1]; // protocol data unit reference; it’s increased by request event;
            _PLCCommand[12] = BitConverter.GetBytes(msgId)[0];
            _PLCCommand[13] = (byte)((_PLCCommand.Length - 17) / 256); // 매개 변수 명령 데이터 총 길이 -> Parameter command Data total length
            _PLCCommand[14] = (byte)((_PLCCommand.Length - 17) % 256);
            _PLCCommand[15] = 0x00; // 내부 데이터를 읽을 때는 00, CPU 모델을 읽을 때는 데이터 데이터 길이 -> Read internal data is 00, read CPU model is data length
            _PLCCommand[16] = 0x00;
            // =====================================================================================
            _PLCCommand[17] = 0x04; // 읽기-쓰기 명령, 04 읽기, 05 쓰기 -> Read-write instruction, 04 read, 05 Write
            _PLCCommand[18] = (byte)readCount; // 데이터 블록의 수 -> Number of data blocks read
            for (int ii = 0; ii < readCount; ii++)
            {
                //===========================================================================================
                // 유효값 유형을 지정 -> Specify a valid value type
                _PLCCommand[19 + ii * 12] = 0x12;
                // 다음번의 주소 접근 길이 -> The next time the address access length
                _PLCCommand[20 + ii * 12] = 0x0A;
                // 문법 표기, ANY -> Syntax tag, any
                _PLCCommand[21 + ii * 12] = 0x10;
                // 단위별 -> by word
                if (s7Addresses[ii].DataCode >= 0x1C && s7Addresses[ii].DataCode <= 0x1F)
                {
                    _PLCCommand[22 + ii * 12] = s7Addresses[ii].DataCode;
                    // 액세스 데이터의 수 -> Number of Access data
                    // C/T Length는 응답 파서와 같이 항목 개수다. 각 항목은 2바이트 값이다.
                    // 근거: https://snap7.sourceforge.net/sharp7.html (ReadArea, Amount / Word size)
                    _PLCCommand[23 + ii * 12] = (byte)(s7Addresses[ii].Length / 256);
                    _PLCCommand[24 + ii * 12] = (byte)(s7Addresses[ii].Length % 256);
                }
                else
                {
                    if (s7Addresses[ii].DataCode == 0x06 | s7Addresses[ii].DataCode == 0x07)
                    {
                        // 액세스 데이터의 수 -> Number of Access data
                        _PLCCommand[22 + ii * 12] = 0x04;
                        _PLCCommand[23 + ii * 12] = (byte)(s7Addresses[ii].Length / 2 / 256);
                        _PLCCommand[24 + ii * 12] = (byte)(s7Addresses[ii].Length / 2 % 256);
                    }
                    else
                    {
                        _PLCCommand[22 + ii * 12] = 0x02;
                        // 액세스 데이터의 수 -> Number of Access data
                        _PLCCommand[23 + ii * 12] = (byte)(s7Addresses[ii].Length / 256);
                        _PLCCommand[24 + ii * 12] = (byte)(s7Addresses[ii].Length % 256);
                    }
                }

                // DB 블록 번호, 만약 DB 블록을 접근하고 있다면 -> DB block number, if you are accessing a DB block
                _PLCCommand[25 + ii * 12] = (byte)(s7Addresses[ii].DbBlock / 256);
                _PLCCommand[26 + ii * 12] = (byte)(s7Addresses[ii].DbBlock % 256);
                // 데이터 타입에 접근 -> Accessing data types
                _PLCCommand[27 + ii * 12] = s7Addresses[ii].DataCode;
                // 오프셋 위치 -> 오프셋 위치
                _PLCCommand[28 + ii * 12] = (byte)(s7Addresses[ii].AddressStart / 256 / 256 % 256);
                _PLCCommand[29 + ii * 12] = (byte)(s7Addresses[ii].AddressStart / 256 % 256);
                _PLCCommand[30 + ii * 12] = (byte)(s7Addresses[ii].AddressStart % 256);
            }

            return OperationResult.CreateSuccessResult(_PLCCommand);
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "msgId">msgId에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildBitReadCommand(string address, int msgId)
        {
            OperationResult<S7DeviceAddress> analysis = S7DeviceAddress.ParseFrom(address);
            if (!analysis.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(analysis);
            if (analysis.Content.DataCode >= 0x1C && analysis.Content.DataCode <= 0x1F)
                return new OperationResult<byte[]>("S7 카운터·타이머 영역은 비트 읽기를 지원하지 않습니다.");
            byte[] _PLCCommand = new byte[31];
            _PLCCommand[0] = 0x03;
            _PLCCommand[1] = 0x00;
            // 길이 -> Length
            _PLCCommand[2] = (byte)(_PLCCommand.Length / 256);
            _PLCCommand[3] = (byte)(_PLCCommand.Length % 256);
            // 고정 -> 고정
            _PLCCommand[4] = 0x02;
            _PLCCommand[5] = 0xF0;
            _PLCCommand[6] = 0x80;
            _PLCCommand[7] = 0x32;
            // 명령: 보내 -> command to send
            _PLCCommand[8] = 0x01;
            // ID 시리얼 번호
            _PLCCommand[9] = 0x00;
            _PLCCommand[10] = 0x00;
            _PLCCommand[11] = BitConverter.GetBytes(msgId)[1];
            _PLCCommand[12] = BitConverter.GetBytes(msgId)[0];
            // 명령 데이터의 총 길이 -> Identification serial Number
            _PLCCommand[13] = (byte)((_PLCCommand.Length - 17) / 256);
            _PLCCommand[14] = (byte)((_PLCCommand.Length - 17) % 256);
            _PLCCommand[15] = 0x00;
            _PLCCommand[16] = 0x00;
            // 명령 시작자 -> Command start character
            _PLCCommand[17] = 0x04;
            // 데이터 블록의 수 -> Number of data blocks read
            _PLCCommand[18] = 0x01;
            //===========================================================================================
            // 주소의 사전 -> Read the prefix of the address
            _PLCCommand[19] = 0x12;
            _PLCCommand[20] = 0x0A;
            _PLCCommand[21] = 0x10;
            // 데이터 읽기-시간 비트 -> Data read-time bit
            _PLCCommand[22] = 0x01;
            // 액세스 데이터의 수 -> Number of Access data
            _PLCCommand[23] = 0x00;
            _PLCCommand[24] = 0x01;
            // DB 블록 번호, 만약 DB 블록을 접근하고 있다면 -> DB block number, if you are accessing a DB block
            _PLCCommand[25] = (byte)(analysis.Content.DbBlock / 256);
            _PLCCommand[26] = (byte)(analysis.Content.DbBlock % 256);
            // 데이터 타입 -> Types of reading data
            _PLCCommand[27] = analysis.Content.DataCode;
            // 오프셋 위치 -> 오프셋 위치
            _PLCCommand[28] = (byte)(analysis.Content.AddressStart / 256 / 256 % 256);
            _PLCCommand[29] = (byte)(analysis.Content.AddressStart / 256 % 256);
            _PLCCommand[30] = (byte)(analysis.Content.AddressStart % 256);
            return OperationResult.CreateSuccessResult(_PLCCommand);
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "s7Address">s7Address에 사용할 입력값입니다.</param>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "msgId">msgId에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteByteCommand(S7DeviceAddress s7Address, byte[] data, int msgId)
        {
            return BuildWriteByteCommand(new S7DeviceAddress[] { s7Address }, new List<byte[]>() { data }, msgId);
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "s7Address">s7Address에 사용할 입력값입니다.</param>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "msgId">msgId에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteByteCommand(S7DeviceAddress[] s7Address, List<byte[]> data, int msgId)
        {
            if (s7Address == null || data == null || s7Address.Length == 0 || s7Address.Length != data.Count)
                return new OperationResult<byte[]>("쓰기 주소와 데이터 항목 수가 일치해야 합니다.");
            if (s7Address.Length > byte.MaxValue)
                return new OperationResult<byte[]>("S7 쓰기 항목 수가 1바이트 전송 필드 범위를 초과합니다.");
            long packetLength = 19 + 12 * s7Address.Length;
            for (int i = 0; i < s7Address.Length; i++)
            {
                OperationResult validation = S7DeviceAddress.ValidateWireAddress(s7Address[i]);
                if (!validation.IsSuccess)
                    return validation.ConvertFailed<byte[]>();
                if (data[i] == null)
                    return new OperationResult<byte[]>("쓰기 데이터가 없습니다.");
                if (s7Address[i].DataCode == 0x1E || s7Address[i].DataCode == 0x1F)
                    return new OperationResult<byte[]>("S7-200 IEC 카운터·타이머 영역의 쓰기 형식은 지원하지 않습니다.");
                bool wordArea = (s7Address[i].DataCode >= 0x1C && s7Address[i].DataCode <= 0x1F) ||
                    s7Address[i].DataCode == 0x06 || s7Address[i].DataCode == 0x07;
                if (wordArea && data[i].Length % 2 != 0)
                    return new OperationResult<byte[]>("워드 영역의 쓰기 데이터는 2바이트 단위여야 합니다.");
                bool timerOrCounter = s7Address[i].DataCode == 0x1C || s7Address[i].DataCode == 0x1D;
                int maximumDataLength = timerOrCounter ? ushort.MaxValue : ushort.MaxValue / 8;
                if (data[i].Length > maximumDataLength)
                    return new OperationResult<byte[]>("S7 쓰기 데이터 길이가 16비트 전송 필드 범위를 초과합니다.");
                packetLength += 4L + data[i].Length;
                if (i < data.Count - 1 && data[i].Length % 2 != 0)
                    packetLength++;
            }
            if (packetLength > ushort.MaxValue)
                return new OperationResult<byte[]>("S7 쓰기 패킷이 TPKT의 16비트 전체 길이 범위를 초과합니다.");
            MemoryStream ms = new MemoryStream();
            ms.Write(new byte[] { 0x03, 0x00, 0x00, 0x00, 0x02, 0xf0, 0x80 });
            ms.WriteByte(0x32); // 요청
            ms.WriteByte(0x01); // 명령 보내 -> command to send
            ms.WriteByte(0x00);
            ms.WriteByte(0x00);
            ms.WriteByte(BitConverter.GetBytes(msgId)[1]); // 식별 시리얼 번호 -> Identification serial Number
            ms.WriteByte(BitConverter.GetBytes(msgId)[0]);
            ms.WriteByte(BitConverter.GetBytes(s7Address.Length * 12 + 2)[1]); // 파라미터 길이
            ms.WriteByte(BitConverter.GetBytes(s7Address.Length * 12 + 2)[0]);
            ms.WriteByte(0x00); // 그리고 그 다음에 계산해야 합니다.
            ms.WriteByte(0x00);
            ms.WriteByte(0x05); // 명령어 쓰기 -> Read and write instructions
            ms.WriteByte((byte)s7Address.Length); // 데이터 블록의 수 -> Number of data blocks written
            for (int i = 0; i < s7Address.Length; i++)
            {
                WriteS7AddressToStream(ms, s7Address[i], writeType: 0x02, dataLen: data[i] == null ? 0 : data[i].Length); // 글자 그대로 적어
            }

            int dataLen = (int)ms.Length;
            for (int i = 0; i < data.Count; i++)
            {
                ms.WriteByte(0x00); // 반환 코드
                bool timerOrCounter = s7Address[i].DataCode == 0x1C || s7Address[i].DataCode == 0x1D;
                // Sharp7 WriteArea: C/T는 OCTET(09), 데이터 길이는 바이트 단위다.
                ms.WriteByte(timerOrCounter ? (byte)0x09 : (byte)0x04);
                if (data[i] != null)
                {
                    int wireLength = data[i].Length * (timerOrCounter ? 1 : 8);
                    ms.WriteByte((byte)(wireLength >> 8));
                    ms.WriteByte((byte)wireLength);
                    ms.Write(data[i]);
                    if (i < data.Count - 1 && (data[i].Length % 2 == 1))
                        ms.WriteByte(0x00); // 마지막 숫자가 아니라면 홀수 길이는 0을 더합니다.
                }
                else
                {
                    ms.WriteByte(0x00);
                    ms.WriteByte(0x00);
                }
            }

            byte[] _PLCCommand = ms.ToArray();
            _PLCCommand[2] = (byte)(_PLCCommand.Length / 256);
            _PLCCommand[3] = (byte)(_PLCCommand.Length % 256);
            _PLCCommand[15] = BitConverter.GetBytes(_PLCCommand.Length - dataLen)[1];
            _PLCCommand[16] = BitConverter.GetBytes(_PLCCommand.Length - dataLen)[0];
            return OperationResult.CreateSuccessResult(_PLCCommand);
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "msgId">msgId에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteBitCommand(string address, bool data, int msgId)
        {
            OperationResult<S7DeviceAddress> analysis = S7DeviceAddress.ParseFrom(address);
            if (!analysis.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(analysis);
            if (analysis.Content.DataCode >= 0x1C && analysis.Content.DataCode <= 0x1F)
                return new OperationResult<byte[]>("S7 카운터·타이머 영역은 비트 쓰기를 지원하지 않습니다.");
            byte[] buffer = new byte[1];
            buffer[0] = data ? (byte)0x01 : (byte)0x00;
            byte[] _PLCCommand = new byte[35 + buffer.Length];
            _PLCCommand[0] = 0x03;
            _PLCCommand[1] = 0x00;
            // 길이 -> length
            _PLCCommand[2] = (byte)((35 + buffer.Length) / 256);
            _PLCCommand[3] = (byte)((35 + buffer.Length) % 256);
            // 고정 -> 고정
            _PLCCommand[4] = 0x02;
            _PLCCommand[5] = 0xF0;
            _PLCCommand[6] = 0x80;
            _PLCCommand[7] = 0x32;
            // 명령 보내 -> command to send
            _PLCCommand[8] = 0x01;
            // 식별 시리얼 번호 -> Identification serial Number
            _PLCCommand[9] = 0x00;
            _PLCCommand[10] = 0x00;
            _PLCCommand[11] = BitConverter.GetBytes(msgId)[1];
            _PLCCommand[12] = BitConverter.GetBytes(msgId)[0];
            // 고정 -> 고정
            _PLCCommand[13] = 0x00;
            _PLCCommand[14] = 0x0E;
            // 길이+4 -> Write Length +4
            _PLCCommand[15] = (byte)((4 + buffer.Length) / 256);
            _PLCCommand[16] = (byte)((4 + buffer.Length) % 256);
            // 명령 시작자 -> Command start character
            _PLCCommand[17] = 0x05;
            // 데이터 블록의 수 -> Number of data blocks written
            _PLCCommand[18] = 0x01;
            _PLCCommand[19] = 0x12;
            _PLCCommand[20] = 0x0A;
            _PLCCommand[21] = 0x10;
            // 쓰기 방식, 1은 비트별로, 2는 워드별로 -> Write mode, 1 is bitwise, 2 is by word
            _PLCCommand[22] = 0x01;
            // 데이터의 집합 -> Number of Write Data
            _PLCCommand[23] = (byte)(buffer.Length / 256);
            _PLCCommand[24] = (byte)(buffer.Length % 256);
            // DB 블록 번호, 만약 DB 블록을 접근하고 있다면 -> DB block number, if you are accessing a DB block
            _PLCCommand[25] = (byte)(analysis.Content.DbBlock / 256);
            _PLCCommand[26] = (byte)(analysis.Content.DbBlock % 256);
            // 데이터의 쓰기 유형 -> Types of writing data
            _PLCCommand[27] = analysis.Content.DataCode;
            // 오프셋 위치 -> 오프셋 위치
            _PLCCommand[28] = (byte)(analysis.Content.AddressStart / 256 / 256);
            _PLCCommand[29] = (byte)(analysis.Content.AddressStart / 256);
            _PLCCommand[30] = (byte)(analysis.Content.AddressStart % 256);
            // 비트 데이터의 전송 크기 코드는 BIT(03)이다. C/T는 위에서 거부한다.
            _PLCCommand[31] = 0x00;
            _PLCCommand[32] = 0x03;

            // 비트 위 계산의 길이 -> The length of the bitwise calculation
            _PLCCommand[33] = (byte)(buffer.Length / 256);
            _PLCCommand[34] = (byte)(buffer.Length % 256);
            buffer.CopyTo(_PLCCommand, 35);
            return OperationResult.CreateSuccessResult(_PLCCommand);
        }
    }
}
