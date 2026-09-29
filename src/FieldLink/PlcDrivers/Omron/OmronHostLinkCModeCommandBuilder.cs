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
using static FieldLink.PlcDrivers.Omron.OmronHostLinkCModeAddressParser;
using static FieldLink.PlcDrivers.Omron.OmronHostLinkCModeResponseParser;
using static FieldLink.PlcDrivers.Omron.OmronHostLinkCModeControlCommandBuilder;
using static FieldLink.PlcDrivers.Omron.OmronHostLinkCModeControlResponseParser;

namespace FieldLink.PlcDrivers.Omron
{
    /// <summary>OmronHostLinkCMode 요청 프레임을 생성합니다.</summary>
    public static class OmronHostLinkCModeCommandBuilder
    {
        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <param name = "isBit">비트 단위 요청이면 true입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<List<byte[]>> BuildReadCommand(string address, ushort length, bool isBit)
        {
            var analysis = AnalysisAddress(address, isBit, true);
            if (!analysis.IsSuccess)
                return OperationResult.CreateFailedResult<List<byte[]>>(analysis);
            int[] lens = ProtocolBytes.SplitIntegerToArray(length, 30);
            List<byte[]> array = new List<byte[]>();
            for (int i = 0; i < lens.Length; i++)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(analysis.Content1);
                sb.Append(analysis.Content2.ToString("D4"));
                sb.Append(lens[i].ToString("D4"));
                array.Add(Encoding.ASCII.GetBytes(sb.ToString()));
                analysis.Content2 += lens[i];
            }

            return OperationResult.CreateSuccessResult(array);
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<List<byte[]>> BuildWriteWordCommand(string address, byte[] value)
        {
            var analysis = AnalysisAddress(address, false, false);
            if (!analysis.IsSuccess)
                return OperationResult.CreateFailedResult<List<byte[]>>(analysis);
            List<byte[]> array = ProtocolBytes.ArraySplitByLength(value, 60);
            List<byte[]> result = new List<byte[]>();
            for (int i = 0; i < array.Count; i++)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(analysis.Content1);
                sb.Append(analysis.Content2.ToString("D4"));
                if (array[i].Length > 0)
                    sb.Append(array[i].ToHexString());
                result.Add(Encoding.ASCII.GetBytes(sb.ToString()));
                analysis.Content2 += array[i].Length / 2;
            }

            return OperationResult.CreateSuccessResult(result);
        }

        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "cmd">포장할 명령 바이트입니다.</param>
        /// <param name = "unitNumber">unitNumber에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackCommand(byte[] cmd, byte unitNumber)
        {
            byte[] buffer = new byte[7 + cmd.Length];
            buffer[0] = (byte)'@';
            buffer[1] = ProtocolBytes.BuildAsciiBytesFrom(unitNumber)[0];
            buffer[2] = ProtocolBytes.BuildAsciiBytesFrom(unitNumber)[1];
            buffer[buffer.Length - 2] = (byte)'*';
            buffer[buffer.Length - 1] = 0x0D;
            cmd.CopyTo(buffer, 3);
            // FCS 계산
            int tmp = buffer[0];
            for (int i = 1; i < buffer.Length - 4; i++)
            {
                tmp = (tmp ^ buffer[i]);
            }

            buffer[buffer.Length - 4] = ProtocolBytes.BuildAsciiBytesFrom((byte)tmp)[0];
            buffer[buffer.Length - 3] = ProtocolBytes.BuildAsciiBytesFrom((byte)tmp)[1];
            return buffer;
        }
    }
}
