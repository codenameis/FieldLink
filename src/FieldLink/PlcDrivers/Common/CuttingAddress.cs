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

namespace FieldLink.PlcDrivers.Common
{
    /// <summary>CuttingAddress 프로토콜 값입니다.</summary>
    public class CuttingAddress
    {
        /// <summary>기본 객체를 생성합니다.</summary>
        public CuttingAddress()
        {
        }

        /// <summary>객체를 인스턴스화하기 위해 주소 유형, 임계 주소, 주소의 반복을 지정합니다.</summary>
        /// <param name = "type">주소 유형</param>
        /// <param name = "address">임상 주소</param>
        /// <param name = "fromBase">주소의 진화</param>
        public CuttingAddress(string type, int address, int fromBase = 10)
        {
            this.DataType = type;
            this.Address = address;
            this.FromBase = fromBase;
        }

        /// <summary>URL의 절단 유형</summary>
        public string DataType { get; set; }
        /// <summary>절단 지점을 기다리고 있습니다.</summary>
        public int Address { get; set; }
        /// <summary>주소 정보</summary>
        public int FromBase { get; set; } = 10;
    }
}
