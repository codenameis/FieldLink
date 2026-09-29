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
using static FieldLink.PlcDrivers.Turck.ReaderNetResponseParser;
using static FieldLink.PlcDrivers.Turck.ReaderNetAddressParser;
using static FieldLink.PlcDrivers.Turck.ReaderNetValueConverter;

namespace FieldLink.PlcDrivers.Turck
{
    /// <summary>ReaderNet 요청 프레임을 생성합니다.</summary>
    public static class ReaderNetCommandBuilder
    {
        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "command">포장할 명령 바이트입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackReaderCommand(byte[] command)
        {
            byte[] buffer = new byte[5 + command.Length];
            buffer[0] = 0xAA;
            buffer[1] = (byte)buffer.Length;
            buffer[2] = (byte)buffer.Length;
            command.CopyTo(buffer, 3);
            CalculateAndFillCRC(buffer, 3 + command.Length);
            return buffer;
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "startBlock">startBlock에 사용할 입력값입니다.</param>
        /// <param name = "numberBlock">numberBlock에 사용할 입력값입니다.</param>
        /// <param name = "bytesOfBlock">bytesOfBlock에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static List<byte[]> BuildReadCommand(byte startBlock, byte numberBlock, byte bytesOfBlock)
        {
            int splitLength = 64 / bytesOfBlock;
            int[] split = ProtocolBytes.SplitIntegerToArray(numberBlock, splitLength);
            List<byte[]> list = new List<byte[]>();
            for (int i = 0; i < split.Length; i++)
            {
                list.Add(PackReaderCommand(new byte[] { 0x68, 0x00, startBlock, (byte)(split[i] - 1) }));
                startBlock += (byte)split[i];
            }

            return list;
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "startBlock">startBlock에 사용할 입력값입니다.</param>
        /// <param name = "numberBlock">numberBlock에 사용할 입력값입니다.</param>
        /// <param name = "bytesOfBlock">bytesOfBlock에 사용할 입력값입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static List<byte[]> BuildWriteCommand(byte startBlock, byte numberBlock, byte bytesOfBlock, byte[] value)
        {
            if (value == null)
                value = new byte[0];
            int splitLength = 64 / bytesOfBlock;
            int[] split = ProtocolBytes.SplitIntegerToArray(numberBlock, splitLength);
            List<byte[]> list = new List<byte[]>();
            int index = 0;
            for (int i = 0; i < split.Length; i++)
            {
                byte[] buffer = new byte[4 + split[i] * bytesOfBlock];
                buffer[0] = 0x69;
                buffer[1] = 0x00;
                buffer[2] = startBlock;
                buffer[3] = (byte)(split[i] - 1);
                value.SelectMiddle(index, split[i] * bytesOfBlock).CopyTo(buffer, 4);
                startBlock += (byte)split[i];
                index += split[i] * bytesOfBlock;
                list.Add(PackReaderCommand(buffer));
            }

            return list;
        }
    }
}
