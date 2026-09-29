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
using static FieldLink.PlcDrivers.Omron.OmronFinsNetResponseParser;
using static FieldLink.PlcDrivers.Omron.FinsControlCommandBuilder;

namespace FieldLink.PlcDrivers.Omron
{
    /// <summary>OmronFinsNet 요청 프레임을 생성합니다.</summary>
    public static class OmronFinsNetCommandBuilder
    {
        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "plcType">plcType에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<List<byte[]>> BuildReadCommand(string[] address, OmronPlcType plcType)
        {
            List<byte[]> cmds = new List<byte[]>();
            List<string[]> splits = ProtocolBytes.ArraySplitByLength(address, 89);
            for (int i = 0; i < splits.Count; i++)
            {
                string[] adds = splits[i];
                byte[] _PLCCommand = new byte[2 + 4 * adds.Length];
                _PLCCommand[0] = 0x01; // 대량 저장 영역 데이터를 읽는다
                _PLCCommand[1] = 0x04;
                for (int j = 0; j < adds.Length; j++)
                {
                    var analysis = OmronFinsAddress.ParseFrom(adds[j], 1, plcType);
                    if (!analysis.IsSuccess)
                        return OperationResult.CreateFailedResult<List<byte[]>>(analysis);
                    _PLCCommand[2 + 4 * j] = analysis.Content.WordCode;
                    _PLCCommand[3 + 4 * j] = (byte)(analysis.Content.AddressStart / 16 / 256);
                    _PLCCommand[4 + 4 * j] = (byte)(analysis.Content.AddressStart / 16 % 256);
                    _PLCCommand[5 + 4 * j] = (byte)(analysis.Content.AddressStart % 16);
                }

                cmds.Add(_PLCCommand);
            }

            return OperationResult.CreateSuccessResult(cmds);
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "plcType">plcType에 사용할 입력값입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <param name = "isBit">비트 단위 요청이면 true입니다.</param>
        /// <param name = "splitLength">splitLength에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<List<byte[]>> BuildReadCommand(OmronPlcType plcType, string address, ushort length, bool isBit, int splitLength = 500)
        {
            OperationResult<OmronFinsAddress> analysis = OmronFinsAddress.ParseFrom(address, length, plcType);
            if (!analysis.IsSuccess)
                return OperationResult.CreateFailedResult<List<byte[]>>(analysis);
            List<byte[]> cmds = new List<byte[]>();
            int[] lengths = ProtocolBytes.SplitIntegerToArray(length, isBit ? 1998 : splitLength);
            for (int i = 0; i < lengths.Length; i++)
            {
                cmds.Add(BuildReadCommand(analysis.Content, (ushort)lengths[i], isBit));
                // 원래 주소 지연
                analysis.Content.AddressStart += isBit ? lengths[i] : lengths[i] * 16;
            }

            return OperationResult.CreateSuccessResult(cmds);
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <param name = "isBit">비트 단위 요청이면 true입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildReadCommand(OmronFinsAddress address, ushort length, bool isBit)
        {
            byte[] _PLCCommand = new byte[8];
            _PLCCommand[0] = 0x01; // 저장 구역 데이터를 읽어
            _PLCCommand[1] = 0x01;
            if (isBit)
                _PLCCommand[2] = address.BitCode;
            else
                _PLCCommand[2] = address.WordCode;
            _PLCCommand[3] = (byte)(address.AddressStart / 16 / 256);
            _PLCCommand[4] = (byte)(address.AddressStart / 16 % 256);
            _PLCCommand[5] = (byte)(address.AddressStart % 16);
            _PLCCommand[6] = (byte)(length / 256); // 길이
            _PLCCommand[7] = (byte)(length % 256);
            return _PLCCommand;
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "plcType">plcType에 사용할 입력값입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "isBit">비트 단위 요청이면 true입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteWordCommand(OmronPlcType plcType, string address, byte[] value, bool isBit)
        {
            var analysis = OmronFinsAddress.ParseFrom(address, 0, plcType);
            if (!analysis.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(analysis);
            byte[] _PLCCommand = new byte[8 + value.Length];
            _PLCCommand[0] = 0x01;
            _PLCCommand[1] = 0x02;
            if (isBit)
                _PLCCommand[2] = analysis.Content.BitCode;
            else
                _PLCCommand[2] = analysis.Content.WordCode;
            _PLCCommand[3] = (byte)(analysis.Content.AddressStart / 16 / 256);
            _PLCCommand[4] = (byte)(analysis.Content.AddressStart / 16 % 256);
            _PLCCommand[5] = (byte)(analysis.Content.AddressStart % 16);
            if (isBit)
            {
                _PLCCommand[6] = (byte)(value.Length / 256);
                _PLCCommand[7] = (byte)(value.Length % 256);
            }
            else
            {
                _PLCCommand[6] = (byte)(value.Length / 2 / 256);
                _PLCCommand[7] = (byte)(value.Length / 2 % 256);
            }

            value.CopyTo(_PLCCommand, 8);
            return OperationResult.CreateSuccessResult(_PLCCommand);
        }

        /// <summary>일반 명령어를 전체 명령어로 포장합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "cmd">FINS의 핵심 지침</param>
        /// <returns>PLC를 전송하는 명령어 전체</returns>
        public static byte[] PackCommand(FinsTcpFrameOptions context, byte[] cmd)
        {
            byte[] buffer = new byte[26 + cmd.Length];
            Array.Copy(context.handSingle, 0, buffer, 0, 4);
            byte[] tmp = BitConverter.GetBytes(buffer.Length - 8);
            Array.Reverse(tmp);
            tmp.CopyTo(buffer, 4);
            buffer[11] = 0x02;
            buffer[16] = context.ICF;
            buffer[17] = context.RSV;
            buffer[18] = context.GCT;
            buffer[19] = context.DNA;
            buffer[20] = context.DA1;
            buffer[21] = context.DA2;
            buffer[22] = context.SNA;
            buffer[23] = context.SA1;
            buffer[24] = context.SA2;
            buffer[25] = (byte)context.SID;
            ;
            cmd.CopyTo(buffer, 26);
            return buffer;
        }
    }
}
