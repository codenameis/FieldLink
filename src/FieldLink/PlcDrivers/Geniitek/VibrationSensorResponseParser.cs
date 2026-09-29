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
    /// <summary>VibrationSensor 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class VibrationSensorResponseParser
    {
        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "body">body에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static VibrationPeakValues ParsePeak(byte[] body)
        {
            VibrationPeakValues peekValue = new VibrationPeakValues();
            peekValue.AcceleratedSpeedX = BitConverter.ToInt16(body, 0) / 100f;
            peekValue.AcceleratedSpeedY = BitConverter.ToInt16(body, 2) / 100f;
            peekValue.AcceleratedSpeedZ = BitConverter.ToInt16(body, 4) / 100f;
            peekValue.SpeedX = BitConverter.ToInt16(body, 6) / 100f;
            peekValue.SpeedY = BitConverter.ToInt16(body, 8) / 100f;
            peekValue.SpeedZ = BitConverter.ToInt16(body, 10) / 100f;
            peekValue.OffsetX = BitConverter.ToInt16(body, 12);
            peekValue.OffsetY = BitConverter.ToInt16(body, 14);
            peekValue.OffsetZ = BitConverter.ToInt16(body, 16);
            peekValue.Temperature = BitConverter.ToInt16(body, 18) * 0.02f - 273.15f;
            peekValue.Voltage = BitConverter.ToInt16(body, 20) / 100f;
            peekValue.SendingInterval = BitConverter.ToInt32(body, 22);
            return peekValue;
        }

        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "frame">frame에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static VibrationSensorActualValue ParseActual(byte[] frame) => new VibrationSensorActualValue()
        {
            AcceleratedSpeedX = new ProtocolValueConverter(ByteOrder.BigEndian).ReadInt16(frame, 1) / 100f,
            AcceleratedSpeedY = new ProtocolValueConverter(ByteOrder.BigEndian).ReadInt16(frame, 3) / 100f,
            AcceleratedSpeedZ = new ProtocolValueConverter(ByteOrder.BigEndian).ReadInt16(frame, 5) / 100f,
        };
        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "header">앞서 읽은 프로토콜 헤더입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static ushort ParseStation(byte[] header) => new ProtocolValueConverter(ByteOrder.BigEndian).ReadUInt16(header, 3);
    }
}
