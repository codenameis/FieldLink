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
using static FieldLink.PlcDrivers.Geniitek.VibrationSensorClientCommandBuilder;

namespace FieldLink.PlcDrivers.Geniitek
{
    /// <summary>VibrationSensorClient 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class VibrationSensorClientResponseParser
    {
        /// <summary>입력 데이터가 프로토콜의 검증 조건을 만족하는지 확인합니다.</summary>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static bool CheckXor(byte[] data)
        {
            int xor = data[3];
            for (int i = 4; i < data.Length - 4; i++)
            {
                xor ^= data[i];
            }

            return BitConverter.GetBytes(xor)[0] == data[data.Length - 4];
        }
    }
}
