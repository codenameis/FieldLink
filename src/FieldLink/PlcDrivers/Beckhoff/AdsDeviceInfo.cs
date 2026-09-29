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

namespace FieldLink.PlcDrivers.Beckhoff
{
    /// <summary>Ads 장치의 관련 정보, 주로 버전 번호, 장치 이름</summary>
    public class AdsDeviceInfo
    {
        /// <summary>기본 객체를 생성합니다.</summary>
        public AdsDeviceInfo()
        {
        }

        /// <summary>원시 데이터 컨텐츠에 따라 객체를 인스턴스화합니다.</summary>
        /// <param name = "data">원본 데이터</param>
        public AdsDeviceInfo(byte[] data)
        {
            Major = data[0];
            Minor = data[1];
            Build = BitConverter.ToUInt16(data, 2);
            DeviceName = Encoding.ASCII.GetString(data.RemoveBegin(4)).Trim('\0', ' ');
        }

        /// <summary>주 버전 번호</summary>
        public byte Major { get; set; }
        /// <summary>부 버전 번호</summary>
        public byte Minor { get; set; }
        /// <summary>빌드 버전 번호</summary>
        public ushort Build { get; set; }
        /// <summary>장치의 이름</summary>
        public string DeviceName { get; set; }
    }
}
