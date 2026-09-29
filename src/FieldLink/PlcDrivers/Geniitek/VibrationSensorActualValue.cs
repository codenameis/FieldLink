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
    /// <summary>진동 센서의 가속값</summary>
    public struct VibrationSensorActualValue
    {
        /// <summary>X축의 실시간 가속</summary>
        public float AcceleratedSpeedX { get; set; }
        /// <summary>Y축의 실시간 가속</summary>
        public float AcceleratedSpeedY { get; set; }
        /// <summary>Z축의 실시간 가속</summary>
        public float AcceleratedSpeedZ { get; set; }

        /// <summary>현재 프로토콜 값의 문자열 표현을 반환합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public override string ToString() => $"ActualValue[{AcceleratedSpeedX},{AcceleratedSpeedY},{AcceleratedSpeedZ}]";
    }
}
