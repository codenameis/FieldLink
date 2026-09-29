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
    /// <summary>장치 주소 데이터에 대한 정보, 일반적으로 시작 주소, 데이터 유형, 길이</summary>
    public class DeviceAddress
    {
        /// <summary>숫자의 시작 주소, 즉 이동 주소</summary>
        public int AddressStart { get; set; }
        /// <summary>읽은 데이터의 길이는 장치에 따라 바이트 또는 워드로 나타납니다.</summary>
        public ushort Length { get; set; }

        /// <summary>정해진 주소 정보에서 실제 장치 주소 정보로 분해</summary>
        /// <param name = "address">주소 정보</param>
        /// <param name = "length">데이터 길이</param>
        public virtual void Parse(string address, ushort length)
        {
            AddressStart = int.Parse(address);
            Length = length;
        }

        /// <summary>현재 프로토콜 값의 문자열 표현을 반환합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public override string ToString() => AddressStart.ToString();
    }
}
