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

namespace FieldLink.PlcDrivers.Siemens
{
    /// <summary>S7 객체 정보</summary>
    public interface IS7Object
    {
        /// <summary>객체를 메시지에 입력하는 메서드</summary>
        /// <param name = "ms">메시지의 데이터 흐름</param>
        void WriteMessgae(MemoryStream ms);
        /// <summary>필드 수를 가져오기</summary>
        /// <returns></returns>
        int GetNumberOfFields();
    }
}
