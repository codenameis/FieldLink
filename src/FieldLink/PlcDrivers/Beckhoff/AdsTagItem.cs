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
    /// <summary>광고 태그 정보</summary>
    public class AdsTagItem
    {
        /// <summary>기본 객체를 생성합니다.</summary>
        /// <param name = "name">이름</param>
        /// <param name = "buffer">버퍼된 데이터 객체</param>
        public AdsTagItem(string name, byte[] buffer)
        {
            this.TagName = name;
            this.Buffer = buffer;
        }

        /// <summary>표지 이름</summary>
        public string TagName { get; set; }
        /// <summary>표기된 데이터 버퍼 정보</summary>
        public byte[] Buffer { get; set; }
        /// <summary>절대 주소 정보</summary>
        public uint Location { get; set; }
    }
}
