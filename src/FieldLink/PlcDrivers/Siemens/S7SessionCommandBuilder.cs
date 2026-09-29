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
    /// <summary>S7Session 요청 프레임을 생성합니다.</summary>
    public static class S7SessionCommandBuilder
    {
        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildDefaultConnection() => new byte[22]
        {
            0x03,
            0x00,
            0x00,
            0x16,
            0x11,
            0xE0,
            0x00,
            0x00,
            0x00,
            0x01,
            0x00,
            0xC0,
            0x01,
            0x0A,
            0xC1,
            0x02,
            0x01,
            0x02,
            0xC2,
            0x02,
            0x01,
            0x00
        //0x03,0x00,0x00,0x16,0x11,0xE0,0x00,0x00,0x00,0x01,0x00,0xC1,0x02,0x01,0x00,0xC2,
        //0x02,0x01,0x01,0xC0,0x01,0x09
        };
        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildDefaultSetup() => new byte[25]
        {
            0x03,
            0x00,
            0x00,
            0x19,
            0x02,
            0xF0,
            0x80,
            0x32,
            0x01,
            0x00,
            0x00,
            0x04,
            0x00,
            0x00,
            0x08,
            0x00,
            0x00,
            0xF0,
            0x00,
            0x00,
            0x01,
            0x00,
            0x01,
            0x01,
            0xE0
        //0x03,0x00,0x00,0x19,0x02,0xF0,0x80,0x32,0x01,0x00,0x00,0xFF,0xFF,0x00,0x08,0x00,
        //0x00,0xF0,0x00,0x00,0x01,0x00,0x01,0x07,0x80
        };
        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildOrderNumberRequest() => new byte[]
        {
            0x03,
            0x00,
            0x00,
            0x21,
            0x02,
            0xF0,
            0x80,
            0x32,
            0x07,
            0x00,
            0x00,
            0x00,
            0x01,
            0x00,
            0x08,
            0x00,
            0x08,
            0x00,
            0x01,
            0x12,
            0x04,
            0x11,
            0x44,
            0x01,
            0x00,
            0xFF,
            0x09,
            0x00,
            0x04,
            0x00,
            0x11,
            0x00,
            0x00
        };
        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildS200SmartConnection() => new byte[22]
        {
            0x03,
            0x00,
            0x00,
            0x16,
            0x11,
            0xE0,
            0x00,
            0x00,
            0x00,
            0x01,
            0x00,
            0xC1,
            0x02,
            0x10,
            0x00,
            0xC2,
            0x02,
            0x03,
            0x00,
            0xC0,
            0x01,
            0x0A
        };
        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildS200SmartSetup() => new byte[25]
        {
            0x03,
            0x00,
            0x00,
            0x19,
            0x02,
            0xF0,
            0x80,
            0x32,
            0x01,
            0x00,
            0x00,
            0xCC,
            0xC1,
            0x00,
            0x08,
            0x00,
            0x00,
            0xF0,
            0x00,
            0x00,
            0x01,
            0x00,
            0x01,
            0x03,
            0xC0
        };
        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildS200Connection() => new byte[22]
        {
            0x03,
            0x00,
            0x00,
            0x16,
            0x11,
            0xE0,
            0x00,
            0x00,
            0x00,
            0x01,
            0x00,
            0xC1,
            0x02,
            0x4D,
            0x57,
            0xC2,
            0x02,
            0x4D,
            0x57,
            0xC0,
            0x01,
            0x09
        };
        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildS200Setup() => new byte[25]
        {
            0x03,
            0x00,
            0x00,
            0x19,
            0x02,
            0xF0,
            0x80,
            0x32,
            0x01,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x08,
            0x00,
            0x00,
            0xF0,
            0x00,
            0x00,
            0x01,
            0x00,
            0x01,
            0x03,
            0xC0
        };
        /// <summary>운전 정지 명령 프레임을 생성합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildStop() => new byte[]
        {
            0x03,
            0x00,
            0x00,
            0x21,
            0x02,
            0xf0,
            0x80,
            0x32,
            0x01,
            0x00,
            0x00,
            0x0e,
            0x00,
            0x00,
            0x10,
            0x00,
            0x00,
            0x29,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x09,
            0x50,
            0x5f,
            0x50,
            0x52,
            0x4f,
            0x47,
            0x52,
            0x41,
            0x4d
        };
        /// <summary>운전 시작 명령 프레임을 생성합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildHotStart() => new byte[]
        {
            0x03,
            0x00,
            0x00,
            0x25,
            0x02,
            0xf0,
            0x80,
            0x32,
            0x01,
            0x00,
            0x00,
            0x0c,
            0x00,
            0x00,
            0x14,
            0x00,
            0x00,
            0x28,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0xfd,
            0x00,
            0x00,
            0x09,
            0x50,
            0x5f,
            0x50,
            0x52,
            0x4f,
            0x47,
            0x52,
            0x41,
            0x4d
        };
        /// <summary>운전 시작 명령 프레임을 생성합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildColdStart() => new byte[]
        {
            0x03,
            0x00,
            0x00,
            0x27,
            0x02,
            0xf0,
            0x80,
            0x32,
            0x01,
            0x00,
            0x00,
            0x0f,
            0x00,
            0x00,
            0x16,
            0x00,
            0x00,
            0x28,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0xfd,
            0x00,
            0x02,
            0x43,
            0x20,
            0x09,
            0x50,
            0x5f,
            0x50,
            0x52,
            0x4f,
            0x47,
            0x52,
            0x41,
            0x4d
        };
    }
}
