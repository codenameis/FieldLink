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
    /// <summary>ToolData 프로토콜 값입니다.</summary>
    public class TighteningToolInfo
    {
        /// <summary>도구 시리얼 번호</summary>
        public string ToolSerialNumber { get; set; }
        /// <summary>꽉 잡아주는 도구 번호</summary>
        public uint ToolNumberOfTightening { get; set; }
        /// <summary>마지막 캘리브레이션 날짜</summary>
        public DateTime LastCalibrationDate { get; set; }
        /// <summary>컨트롤러 일련 번호</summary>
        public string ControllerSerialNumber { get; set; }
        /// <summary>캘리브레이션 값</summary>
        public double CalibrationValue { get; set; }
        /// <summary>마지막 서비스 날짜</summary>
        public DateTime LastServiceDate { get; set; }
        /// <summary>서비스 이후의 강화</summary>
        public uint TighteningsSinceService { get; set; }
        /// <summary>도구 유형: 01=S-도구, 02=DS-도구, 03=Ref.transducer, 04=ST-도구, 05=EPtool, 06=ETX-도구, 07=SL-도구, 08=DL-도구, 09=STB (오프라인), 10=STB (온라인), 11=QST-도구</summary>
        public int ToolType { get; set; }
        /// <summary>모터 크기</summary>
        public int MotorSize { get; set; }
        /// <summary>개방된 사용 끝</summary>
        public bool UseOpenEnd { get; set; }
        /// <summary>tightening direction: CW=전시, CCW=반시</summary>
        public string TighteningDirection { get; set; }
        /// <summary>모터 회전: 0=정상, 1=반전.</summary>
        public int MotorRotation { get; set; }
        /// <summary>컨트롤러 소프트웨어 버전</summary>
        public string ControllerSoftwareVersion { get; set; }
    }
}
