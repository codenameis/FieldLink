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
using static FieldLink.PlcDrivers.AllenBradley.AllenBradleySLCNetResponseParser;

namespace FieldLink.PlcDrivers.AllenBradley
{
    /// <summary>AllenBradleySLCNet 요청 프레임을 생성합니다.</summary>
    public static class AllenBradleySLCNetCommandBuilder
    {
        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildReadCommand(string address, ushort length)
        {
            var analysis = AllenBradleySlcAddress.ParseFrom(address);
            if (!analysis.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(analysis);
            if (length < 2)
                length = 2;
            if (analysis.Content.DataCode == 0x8E)
            {
                analysis.Content.AddressStart /= 2;
            }

            byte[] command = new byte[14];
            command[0] = 0x00;
            command[1] = 0x05;
            command[2] = 0x00;
            command[3] = 0x00;
            command[4] = 0x0F;
            command[5] = 0x00;
            command[6] = 0x00; // ID 정보
            command[7] = 0x01;
            command[8] = 0xA2;
            command[9] = (byte)length; // 바이트 수를 읽어
            command[10] = (byte)analysis.Content.DbBlock; // 데이터 블록 번호
            command[11] = analysis.Content.DataCode; // 데이터 타입
            BitConverter.GetBytes((ushort)analysis.Content.AddressStart).CopyTo(command, 12); // 시작 주소
            return OperationResult.CreateSuccessResult(command);
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteCommand(string address, byte[] value)
        {
            var analysis = AllenBradleySlcAddress.ParseFrom(address);
            if (!analysis.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(analysis);
            if (analysis.Content.DataCode == 0x8E)
            {
                analysis.Content.AddressStart /= 2;
            }

            byte[] command = new byte[18 + value.Length];
            command[0] = 0x00;
            command[1] = 0x05;
            command[2] = 0x00;
            command[3] = 0x00;
            command[4] = 0x0F;
            command[5] = 0x00;
            command[6] = 0x00; // ID 정보
            command[7] = 0x01;
            command[8] = 0xAB;
            command[9] = 0xFF;
            command[10] = BitConverter.GetBytes(value.Length)[0]; // 이 숫자는
            command[11] = BitConverter.GetBytes(value.Length)[1]; //
            command[12] = (byte)analysis.Content.DbBlock; // 데이터 블록 번호
            command[13] = analysis.Content.DataCode; // 데이터 타입
            BitConverter.GetBytes((ushort)analysis.Content.AddressStart).CopyTo(command, 14); // 시작 주소
            command[16] = 0xFF;
            command[17] = 0xFF;
            value.CopyTo(command, 18);
            return OperationResult.CreateSuccessResult(command);
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteCommand(string address, bool value)
        {
            address = AnalysisBitIndex(address, out int bitIndex);
            var analysis = AllenBradleySlcAddress.ParseFrom(address);
            if (!analysis.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(analysis);
            if (analysis.Content.DataCode == 0x8E)
            {
                analysis.Content.AddressStart /= 2;
            }

            int bitPow = 0x01 << bitIndex;
            byte[] command = new byte[20];
            command[0] = 0x00;
            command[1] = 0x05;
            command[2] = 0x00;
            command[3] = 0x00;
            command[4] = 0x0F;
            command[5] = 0x00;
            command[6] = 0x00; // ID 정보
            command[7] = 0x01;
            command[8] = 0xAB;
            command[9] = 0xFF;
            command[10] = 0x02; // 이 숫자는
            command[11] = 0x00;
            command[12] = (byte)analysis.Content.DbBlock; // 데이터 블록 번호
            command[13] = analysis.Content.DataCode; // 데이터 타입
            BitConverter.GetBytes((ushort)analysis.Content.AddressStart).CopyTo(command, 14); // 시작 주소
            command[16] = BitConverter.GetBytes(bitPow)[0];
            command[17] = BitConverter.GetBytes(bitPow)[1];
            if (value)
            {
                command[18] = BitConverter.GetBytes(bitPow)[0];
                command[19] = BitConverter.GetBytes(bitPow)[1];
            }

            return OperationResult.CreateSuccessResult(command);
        }

        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "coreCmd">coreCmd에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackCommand(SlcFrameOptions context, byte[] coreCmd)
        {
            byte[] cmd = new byte[28 + coreCmd.Length];
            cmd[0] = 0x01;
            cmd[1] = 0x07;
            cmd[2] = (byte)(coreCmd.Length / 256);
            cmd[3] = (byte)(coreCmd.Length % 256);
            BitConverter.GetBytes(context.SessionHandle).CopyTo(cmd, 4);
            coreCmd.CopyTo(cmd, 28);
            return cmd;
        }
    }
}
