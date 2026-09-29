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
    /// <summary>진동 센서의 피크 데이터 클래스</summary>
    public class VibrationPeakValues
    {
        /// <summary>X축의 가속도, 단위 m/s2</summary>
        public float AcceleratedSpeedX { get; set; }
        /// <summary>y축의 가속도, 단위 m/s2</summary>
        public float AcceleratedSpeedY { get; set; }
        /// <summary>Z축의 가속도, 단위 m/s2</summary>
        public float AcceleratedSpeedZ { get; set; }
        /// <summary>X축의 속도, 단위 mm/s</summary>
        public float SpeedX { get; set; }
        /// <summary>Y축의 속도, 단위 mm/s</summary>
        public float SpeedY { get; set; }
        /// <summary>Z축의 속도, 단위 mm/s</summary>
        public float SpeedZ { get; set; }
        /// <summary>x축의 위치, 단위 um</summary>
        public int OffsetX { get; set; }
        /// <summary>y축의 이동, 단위 um</summary>
        public int OffsetY { get; set; }
        /// <summary>Z축의 이동, 단위 um</summary>
        public int OffsetZ { get; set; }
        /// <summary>온도, 단위</summary>
        public float Temperature { get; set; }
        /// <summary>전압, 단위</summary>
        public float Voltage { get; set; }
        /// <summary>데이터 전송 간격, 단위 초</summary>
        public int SendingInterval { get; set; }
    }
}
