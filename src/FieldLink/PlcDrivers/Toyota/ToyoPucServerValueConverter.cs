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
using static FieldLink.PlcDrivers.Toyota.ToyoPucServerDefinitions;

namespace FieldLink.PlcDrivers.Toyota
{
    /// <summary>ToyoPucServer 프로토콜의 값과 바이트 표현을 변환합니다.</summary>
    public static class ToyoPucServerValueConverter
    {
        /// <summary>CreateResponseBack 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "request">request에 사용할 입력값입니다.</param>
        /// <param name = "err">err에 사용할 입력값입니다.</param>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] CreateResponseBack(byte[] request, byte err, byte[] data)
        {
            if (err != 0x00)
            {
                byte[] buffer = new byte[5];
                buffer[0] = 0x80;
                buffer[1] = 0x10;
                buffer[2] = 0x01;
                buffer[3] = 0x00;
                buffer[4] = err;
                return buffer;
            }
            else
            {
                if (data == null)
                    data = new byte[0];
                byte[] buffer = new byte[5 + data.Length];
                buffer[0] = 0x80;
                buffer[1] = 0x00;
                buffer[2] = BitConverter.GetBytes(data.Length + 1)[0];
                buffer[3] = BitConverter.GetBytes(data.Length + 1)[1];
                buffer[4] = request[4];
                if (data.Length > 0)
                    data.CopyTo(buffer, 5);
                return buffer;
            }
        }
    }
}
