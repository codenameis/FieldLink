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
using static FieldLink.PlcDrivers.Siemens.SiemensS7ServerDefinitions;

namespace FieldLink.PlcDrivers.Siemens
{
    /// <summary>SiemensS7Server 요청 프레임을 생성합니다.</summary>
    public static class SiemensS7ServerCommandBuilder
    {
        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "command">포장할 명령 바이트입니다.</param>
        /// <param name = "content">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackReadBack(byte[] command, List<byte> content)
        {
            byte[] back = new byte[21 + content.Count];
            ProtocolBytes.HexStringToBytes("03 00 00 1A 02 F0 80 32 03 00 00 00 01 00 02 00 05 00 00 04 01").CopyTo(back, 0);
            back[2] = (byte)(back.Length / 256);
            back[3] = (byte)(back.Length % 256);
            back[15] = (byte)(content.Count / 256);
            back[16] = (byte)(content.Count % 256);
            back[20] = command[18];
            back[11] = command[11];
            back[12] = command[12];
            content.CopyTo(back, 21);
            return back;
        }

        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackReadBitCommandBack(bool value)
        {
            byte[] back = new byte[5];
            back[0] = 0xFF;
            back[1] = 0x03;
            back[2] = 0x00;
            back[3] = 0x01;
            back[4] = (byte)(value ? 0x01 : 0x00);
            return back;
        }

        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "packCommand">packCommand에 사용할 입력값입니다.</param>
        /// <param name = "status">status에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackWriteBack(byte[] packCommand, byte status) => PackWriteBack(packCommand, new byte[] { status });
        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "packCommand">packCommand에 사용할 입력값입니다.</param>
        /// <param name = "status">status에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackWriteBack(byte[] packCommand, byte[] status)
        {
            byte[] buffer = new byte[21 + status.Length];
            ProtocolBytes.HexStringToBytes("03 00 00 16 02 F0 80 32 03 00 00 00 01 00 02 00 01 00 00 05 01").CopyTo(buffer, 0);
            buffer[20] = (byte)status.Length;
            buffer[11] = packCommand[11];
            buffer[12] = packCommand[12];
            buffer[15] = (byte)(status.Length >> 8);
            buffer[16] = (byte)status.Length;
            status.CopyTo(buffer, 21);
            buffer[2] = BitConverter.GetBytes(buffer.Length)[1];
            buffer[3] = BitConverter.GetBytes(buffer.Length)[0];
            return buffer;
        }

        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "err">err에 사용할 입력값입니다.</param>
        /// <param name = "result">result에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackReadWordCommandBack(S7ResponseOptions context, short err, byte[] result)
        {
            if (err > 0)
            {
                byte[] back = new byte[4];
                BitConverter.GetBytes(err).CopyTo(back, 0);
                return back;
            }
            else
            {
                byte[] back = new byte[4 + result.Length];
                back[0] = 0xFF;
                back[1] = 0x04;
                context.ValueConverter.GetBytes((ushort)(result.Length * 8)).CopyTo(back, 2);
                result.CopyTo(back, 4);
                return back;
            }
        }

        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "result">result에 사용할 입력값입니다.</param>
        /// <param name = "dataLength">dataLength에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackReadCTCommandBack(S7ResponseOptions context, byte[] result, int dataLength)
        {
            byte[] back = new byte[4 + result.Length * dataLength / 2];
            back[0] = 0xFF;
            back[1] = 0x09;
            context.ValueConverter.GetBytes((ushort)(back.Length - 4)).CopyTo(back, 2);
            for (int i = 0; i < result.Length / 2; i++)
            {
                result.SelectMiddle(i * 2, 2).CopyTo(back, 4 + dataLength - 2 + i * dataLength);
            }

            return back;
        }
    }
}
