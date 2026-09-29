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
    /// <summary>오픈 프로토콜의 메시지 객체 정보</summary>
    public class OpenProtocolEventArgs : EventArgs
    {
        /// <summary>기본 객체를 생성합니다.</summary>
        public OpenProtocolEventArgs()
        {
        }

        /// <summary>Open의 메시지를 인스턴스 객체로 지정합니다.</summary>
        /// <param name = "content">오픈 콘텐츠</param>
        public OpenProtocolEventArgs(string content)
        {
            Content = content;
        }

        /// <summary>오픈 프로토콜의 메시지 내용</summary>
        public string Content { get; set; }
    }
}
