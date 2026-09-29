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

namespace FieldLink.PlcDrivers.Geniitek
{
    /// <summary>VibrationSensor 요청 프레임을 생성합니다.</summary>
    public static class VibrationSensorCommandBuilder
    {
        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildReadStatus(ushort address) => VibrationSensorClientCommandBuilder.BulidLongMessage(address, 0x01, null);
        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildReadActual(ushort address) => VibrationSensorClientCommandBuilder.BulidLongMessage(address, 0x02, null);
        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "seconds">seconds에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildSetInterval(ushort address, int seconds)
        {
            byte[] data = new byte[6];
            data[0] = BitConverter.GetBytes(address)[0];
            data[1] = BitConverter.GetBytes(address)[1];
            BitConverter.GetBytes(seconds).CopyTo(data, 2);
            return VibrationSensorClientCommandBuilder.BulidLongMessage(address, 0x10, data);
        }
    }
}
