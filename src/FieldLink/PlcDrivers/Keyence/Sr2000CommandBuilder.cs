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

namespace FieldLink.PlcDrivers.Keyence
{
    /// <summary>Sr2000 요청 프레임을 생성합니다.</summary>
    public static class Sr2000CommandBuilder
    {
        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildReadBarcode() => BuildCustom("LON");
        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildReset() => BuildCustom("RESET");
        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildOpenIndicator() => BuildCustom("AMON");
        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildCloseIndicator() => BuildCustom("AMOFF");
        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildReadVersion() => BuildCustom("KEYENCE");
        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildReadCommandState() => BuildCustom("CMDSTAT");
        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildReadErrorState() => BuildCustom("ERRSTAT");
        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <param name = "number">number에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildCheckInput(int number) => BuildCustom("INCHK," + number);
        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "number">number에 사용할 입력값입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildSetOutput(int number, bool value) => BuildCustom((value ? "OUTON," : "OUTOFF,") + number);
        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildReadRecord() => BuildCustom("NUM");
        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildLock() => BuildCustom("LOCK");
        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildUnLock() => BuildCustom("UNLOCK");
        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <param name = "command">포장할 명령 바이트입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildCustom(string command) => Encoding.ASCII.GetBytes(command + "\r");
    }
}
