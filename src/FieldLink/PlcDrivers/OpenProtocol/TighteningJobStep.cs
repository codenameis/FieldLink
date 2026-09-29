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
    /// <summary>JobItem 프로토콜 값입니다.</summary>
    public class TighteningJobStep
    {
        /// <summary>기본 객체를 생성합니다.</summary>
        public TighteningJobStep()
        {
        }

        /// <summary>원본 데이터의 인스턴스화된 객체 정보를 지정합니다.</summary>
        /// <param name = "data">분석을 기다리는 원본 데이터, 예를 들어:15:011:0:22</param>
        public TighteningJobStep(string data)
        {
            if (data.Length == 12)
                data = data.Substring(0, 11);
            string[] splits = data.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
            ChannelID = Convert.ToInt32(splits[0]);
            TypeID = Convert.ToInt32(splits[1]);
            AutoValue = Convert.ToInt32(splits[2]);
            BatchSize = Convert.ToInt32(splits[3]);
        }

        /// <summary>Channel-ID</summary>
        public int ChannelID { get; set; }
        /// <summary>Type-ID</summary>
        public int TypeID { get; set; }
        /// <summary>AutoValue</summary>
        public int AutoValue { get; set; }
        /// <summary>BatchSize</summary>
        public int BatchSize { get; set; }
    }
}
