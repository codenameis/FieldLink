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
using static FieldLink.PlcDrivers.Beckhoff.AdsResponseParser;
using static FieldLink.PlcDrivers.Beckhoff.AdsAddressParser;
using static FieldLink.PlcDrivers.Beckhoff.AdsValueConverter;

namespace FieldLink.PlcDrivers.Beckhoff
{
    /// <summary>Ads 요청 프레임을 생성합니다.</summary>
    public static class AdsCommandBuilder
    {
        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <param name = "commandId">commandId에 사용할 입력값입니다.</param>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildAmsHeaderCommand(ushort commandId, byte[] data)
        {
            // 3개의 변수가 있습니다. 통신할 때, 동적 부여, source netid, target netid, 그리고 invoke id
            if (data == null)
                data = new byte[0];
            byte[] buffer = new byte[32 + data.Length];
            buffer[16] = BitConverter.GetBytes(commandId)[0]; // 명령 ID
            buffer[17] = BitConverter.GetBytes(commandId)[1];
            buffer[18] = 0x04; // flag: Tcp, Request
            buffer[19] = 0x00;
            buffer[20] = BitConverter.GetBytes(data.Length)[0]; // 데이터 범위의 크기. 단위는 바이트입니다
            buffer[21] = BitConverter.GetBytes(data.Length)[1];
            buffer[22] = BitConverter.GetBytes(data.Length)[2];
            buffer[23] = BitConverter.GetBytes(data.Length)[3];
            buffer[24] = 0x00; // AMS 오류 번호
            buffer[25] = 0x00;
            buffer[26] = 0x00;
            buffer[27] = 0x00;
            data.CopyTo(buffer, 32);
            return PackAmsTcpHelper(AmsTcpHeaderFlags.Command, buffer);
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildReadDeviceInfoCommand()
        {
            return OperationResult.CreateSuccessResult(BuildAmsHeaderCommand(BeckhoffCommandId.ReadDeviceInfo, null));
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildReadStateCommand()
        {
            return OperationResult.CreateSuccessResult(BuildAmsHeaderCommand(BeckhoffCommandId.ReadState, null));
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "state">state에 사용할 입력값입니다.</param>
        /// <param name = "deviceState">deviceState에 사용할 입력값입니다.</param>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteControlCommand(short state, short deviceState, byte[] data)
        {
            if (data == null)
                data = new byte[0];
            byte[] buffer = new byte[8 + data.Length];
            return OperationResult.CreateSuccessResult(BuildAmsHeaderCommand(BeckhoffCommandId.WriteControl, ProtocolBytes.SpliceArray(BitConverter.GetBytes(state), BitConverter.GetBytes(deviceState), BitConverter.GetBytes(data.Length), data)));
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <param name = "isBit">비트 단위 요청이면 true입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildReadCommand(string address, int length, bool isBit)
        {
            OperationResult<uint, uint> analysis = AnalysisAddress(address, isBit);
            if (!analysis.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(analysis);
            byte[] data = new byte[12];
            BitConverter.GetBytes(analysis.Content1).CopyTo(data, 0);
            BitConverter.GetBytes(analysis.Content2).CopyTo(data, 4);
            BitConverter.GetBytes(length).CopyTo(data, 8);
            return OperationResult.CreateSuccessResult(BuildAmsHeaderCommand(BeckhoffCommandId.Read, data));
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildReadCommand(string[] address, ushort[] length)
        {
            byte[] data = new byte[12 * address.Length];
            int lenCount = 0;
            for (int i = 0; i < address.Length; i++)
            {
                OperationResult<uint, uint> analysis = AnalysisAddress(address[i], false);
                if (!analysis.IsSuccess)
                    return OperationResult.CreateFailedResult<byte[]>(analysis);
                BitConverter.GetBytes(analysis.Content1).CopyTo(data, 12 * i + 0);
                BitConverter.GetBytes(analysis.Content2).CopyTo(data, 12 * i + 4);
                BitConverter.GetBytes((int)length[i]).CopyTo(data, 12 * i + 8);
                lenCount += length[i];
            }

            return BuildReadWriteCommand("ig=0xF080;0", lenCount, false, data);
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <param name = "isBit">비트 단위 요청이면 true입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildReadWriteCommand(string address, int length, bool isBit, byte[] value)
        {
            OperationResult<uint, uint> analysis = AnalysisAddress(address, isBit);
            if (!analysis.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(analysis);
            byte[] data = new byte[16 + value.Length];
            BitConverter.GetBytes(analysis.Content1).CopyTo(data, 0);
            BitConverter.GetBytes(analysis.Content2).CopyTo(data, 4);
            BitConverter.GetBytes(length).CopyTo(data, 8);
            BitConverter.GetBytes(value.Length).CopyTo(data, 12);
            value.CopyTo(data, 16);
            return OperationResult.CreateSuccessResult(BuildAmsHeaderCommand(BeckhoffCommandId.ReadWrite, data));
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteCommand(string[] address, List<byte[]> value)
        {
            MemoryStream ms = new MemoryStream();
            int lenCount = 0;
            for (int i = 0; i < address.Length; i++)
            {
                OperationResult<uint, uint> analysis = AnalysisAddress(address[i], false);
                if (!analysis.IsSuccess)
                    return OperationResult.CreateFailedResult<byte[]>(analysis);
                ms.Write(BitConverter.GetBytes(analysis.Content1));
                ms.Write(BitConverter.GetBytes(analysis.Content2));
                ms.Write(BitConverter.GetBytes(value[i].Length));
                ms.Write(value[i]);
                lenCount += value[i].Length;
            }

            //for (int i = 0; i < value.Count; i++)
            //{
            //	ms.Write( value[i] );
            //}
            return BuildReadWriteCommand("ig=0xF081;0", lenCount, false, ms.ToArray());
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "isBit">비트 단위 요청이면 true입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteCommand(string address, byte[] value, bool isBit)
        {
            OperationResult<uint, uint> analysis = AnalysisAddress(address, isBit);
            if (!analysis.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(analysis);
            byte[] data = new byte[12 + value.Length];
            BitConverter.GetBytes(analysis.Content1).CopyTo(data, 0);
            BitConverter.GetBytes(analysis.Content2).CopyTo(data, 4);
            BitConverter.GetBytes(value.Length).CopyTo(data, 8);
            value.CopyTo(data, 12);
            return OperationResult.CreateSuccessResult(BuildAmsHeaderCommand(BeckhoffCommandId.Write, data));
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "isBit">비트 단위 요청이면 true입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteCommand(string address, bool[] value, bool isBit)
        {
            OperationResult<uint, uint> analysis = AnalysisAddress(address, isBit);
            if (!analysis.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(analysis);
            byte[] buffer = value.Select(m => m ? (byte)1 : (byte)0).ToArray();
            byte[] data = new byte[12 + buffer.Length];
            BitConverter.GetBytes(analysis.Content1).CopyTo(data, 0);
            BitConverter.GetBytes(analysis.Content2).CopyTo(data, 4);
            BitConverter.GetBytes(buffer.Length).CopyTo(data, 8);
            buffer.CopyTo(data, 12);
            return OperationResult.CreateSuccessResult(BuildAmsHeaderCommand(BeckhoffCommandId.Write, data));
        }

        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <param name = "handle">handle에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildReleaseSystemHandle(uint handle)
        {
            byte[] data = new byte[16];
            BitConverter.GetBytes(0xF006).CopyTo(data, 0);
            BitConverter.GetBytes(0x0004).CopyTo(data, 8);
            BitConverter.GetBytes(handle).CopyTo(data, 12);
            return OperationResult.CreateSuccessResult(BuildAmsHeaderCommand(BeckhoffCommandId.Write, data));
        }

        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "headerFlags">headerFlags에 사용할 입력값입니다.</param>
        /// <param name = "command">포장할 명령 바이트입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackAmsTcpHelper(AmsTcpHeaderFlags headerFlags, byte[] command)
        {
            byte[] buffer = new byte[6 + command.Length];
            BitConverter.GetBytes((ushort)headerFlags).CopyTo(buffer, 0);
            BitConverter.GetBytes(command.Length).CopyTo(buffer, 2);
            command.CopyTo(buffer, 6);
            return buffer;
        }
    }
}
