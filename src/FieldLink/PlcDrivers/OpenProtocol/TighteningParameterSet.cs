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

namespace FieldLink.PlcDrivers.OpenProtocol
{
    /// <summary>ParameterSetData 프로토콜 값입니다.</summary>
    public class TighteningParameterSet
    {
        /// <summary>매개 변수 ID</summary>
        public int ParameterSetID { get; set; }
        /// <summary>매개 변수 세트 이름</summary>
        public string ParameterSetName { get; set; }
        /// <summary>Rotation direction, CW: 시계를 따라, CCW: 시계를 거꾸로</summary>
        public string RotationDirection { get; set; }
        /// <summary>배치 크기</summary>
        public int BatchSize { get; set; }
        /// <summary>최소 토크</summary>
        public double TorqueMin { get; set; }
        /// <summary>최대 토크</summary>
        public double TorqueMax { get; set; }
        /// <summary>토크 최종 목표</summary>
        public double TorqueFinalTarget { get; set; }
        /// <summary>최소 각도</summary>
        public int AngleMin { get; set; }
        /// <summary>최대 각도</summary>
        public int AngleMax { get; set; }
        /// <summary>목표 각은 정도에 지정됩니다</summary>
        public int AngleFinalTarget { get; set; }
    }
}
