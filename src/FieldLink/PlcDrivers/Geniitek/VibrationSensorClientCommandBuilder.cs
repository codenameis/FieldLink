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
using static FieldLink.PlcDrivers.Geniitek.VibrationSensorClientResponseParser;

namespace FieldLink.PlcDrivers.Geniitek
{
    /// <summary>VibrationSensorClient 요청 프레임을 생성합니다.</summary>
    public static class VibrationSensorClientCommandBuilder
    {
        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "cmd">포장할 명령 바이트입니다.</param>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BulidLongMessage(ushort address, byte cmd, byte[] data)
        {
            if (data == null)
                data = new byte[0];
            byte[] buffer = new byte[16 + data.Length];
            buffer[0] = 0xAA; // 고정 헤더
            buffer[1] = 0x55;
            buffer[2] = 0x7F;
            buffer[3] = BitConverter.GetBytes(address)[1]; // 디바이스 주소
            buffer[4] = BitConverter.GetBytes(address)[0];
            buffer[5] = cmd; // 프레임 명령
            buffer[6] = 0x01; // 센서가 수신한 데이터
            buffer[7] = 0x00; // 버전 번호
            buffer[8] = 0x01;
            buffer[9] = 0x01; // 프레임 상태
            buffer[10] = BitConverter.GetBytes(data.Length)[1]; // 데이터 길이
            buffer[11] = BitConverter.GetBytes(data.Length)[0];
            data.CopyTo(buffer, 12);
            int xor = buffer[3];
            for (int i = 4; i < buffer.Length - 4; i++)
            {
                xor ^= buffer[i];
            }

            buffer[buffer.Length - 4] = (byte)xor;
            buffer[buffer.Length - 3] = 0x7F;
            buffer[buffer.Length - 2] = 0xAA;
            buffer[buffer.Length - 1] = 0xED;
            return buffer;
        }
    }
}
