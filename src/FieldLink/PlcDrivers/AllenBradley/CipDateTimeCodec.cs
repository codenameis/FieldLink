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

namespace FieldLink.PlcDrivers.AllenBradley
{
    /// <summary>CipDateTime 데이터의 프로토콜 표현을 생성하고 해석합니다.</summary>
    public static class CipDateTimeCodec
    {
        /// <summary>1970년 1월 1일부터 지난 나노초를 DateTime으로 변환합니다. 100나노초 미만은 버립니다.</summary>
        /// <param name = "nanoseconds">장치가 반환한 나노초 단위 값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static DateTime ParseDate(long nanoseconds) => new DateTime(1970, 1, 1).AddTicks(nanoseconds / 100);
        /// <summary>나노초 단위 TIME 또는 TimeOfDate 값을 TimeSpan으로 변환합니다.</summary>
        /// <param name = "nanoseconds">장치가 반환한 나노초 단위 값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static TimeSpan ParseTime(long nanoseconds) => TimeSpan.FromTicks(nanoseconds / 100);
        /// <summary>DATE 값을 만듭니다. 시각을 제외하고 날짜만 나노초 단위로 기록합니다.</summary>
        /// <param name = "date">date에 사용할 입력값입니다.</param>
        /// <param name = "transform">프로토콜 값에 적용할 바이트 순서 변환입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildDate(DateTime date, IProtocolValueConverter transform) => transform.GetBytes((date.Date - new DateTime(1970, 1, 1)).Ticks * 100);
        /// <summary>시각을 포함한 TimeAndDate 값을 나노초 단위로 기록합니다.</summary>
        /// <param name = "date">date에 사용할 입력값입니다.</param>
        /// <param name = "transform">프로토콜 값에 적용할 바이트 순서 변환입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildTimeAndDate(DateTime date, IProtocolValueConverter transform) => transform.GetBytes((date - new DateTime(1970, 1, 1)).Ticks * 100);
        /// <summary>TIME 또는 TimeOfDate 값을 나노초 단위로 기록합니다.</summary>
        /// <param name = "time">time에 사용할 입력값입니다.</param>
        /// <param name = "transform">프로토콜 값에 적용할 바이트 순서 변환입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildTime(TimeSpan time, IProtocolValueConverter transform) => transform.GetBytes(time.Ticks * 100);
    }
}
