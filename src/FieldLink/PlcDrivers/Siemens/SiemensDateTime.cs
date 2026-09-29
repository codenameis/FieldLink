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
    /// <summary>둘 사이에 변환하는 방법을 포함합니다 <c>T:System.DateTime</c> 그리고 S7은 날짜와 시간의 값을 나타냅니다.</summary>
    /// <remarks>이 부분의 코드는 다른 s7의 라이브러리를 참조합니다.
    /// 
    /// https://github.com/S7NetPlus/s7netplus</remarks>
    public class SiemensDateTime
    {
        /// <summary>최소 <c>T:System.DateTime</c> 사양에서 지원하는 값입니다.</summary>
        public static readonly DateTime SpecMinimumDateTime = new DateTime(1990, 1, 1);
        /// <summary>최대 <c>T:System.DateTime</c> 사양에서 지원하는 값입니다.</summary>
        public static readonly DateTime SpecMaximumDateTime = new DateTime(2089, 12, 31, 23, 59, 59, 999);
        /// <summary>Parses a <c>T:System.DateTime</c> 바이트에서 값.</summary>
        /// <param name = "bytes">입력 바이트는 PLC에서 읽습니다.</param>
        /// <returns>A <c>T:System.DateTime</c> PLC에서 읽은 값을 나타내는 객체입니다.</returns>
        public static OperationResult<DateTime> FromByteArray(byte[] bytes)
        {
            try
            {
                return OperationResult.CreateSuccessResult(FromByteArrayImpl(bytes));
            }
            catch (Exception ex)
            {
                return new OperationResult<DateTime>("Prase DateTime failed: " + ex.Message);
            }
        }

        /// <summary>Siemens의 원본 바이트 데이터에서 DTL 형식의 시간 정보를 추출합니다.</summary>
        /// <param name = "byteTransform">시몬스의 바이트 변환 객체</param>
        /// <param name = "buffer">원시 바이트 데이터</param>
        /// <param name = "index">바이트 편향 인덱스</param>
        /// <returns>시간 정보</returns>
        public static OperationResult<DateTime> GetDTLTime(IProtocolValueConverter byteTransform, byte[] buffer, int index)
        {
            try
            {
                int year = byteTransform.ReadInt16(buffer, index);
                int month = buffer[index + 2];
                int day = buffer[index + 3];
                int hour = buffer[index + 5];
                int minute = buffer[index + 6];
                int second = buffer[index + 7];
                int microsecond = byteTransform.ReadInt32(buffer, index + 8) / 1000 / 1000;
                return OperationResult.CreateSuccessResult(new DateTime(year, month, day, hour, minute, second, microsecond));
            }
            catch (Exception ex)
            {
                return new OperationResult<DateTime>("GetDTLTime failed: " + ex.Message);
            }
        }

        /// <summary>Siemens의 DTL 형식의 시간 데이터로 시간 데이터를 변환</summary>
        /// <param name = "byteTransform">시몬스의 바이트 변환 객체</param>
        /// <param name = "dateTime">정해진 시간 정보</param>
        /// <returns>원본 바이트 데이터 정보</returns>
        public static byte[] GetBytesFromDTLTime(IProtocolValueConverter byteTransform, DateTime dateTime)
        {
            byte[] buffer = new byte[12];
            byteTransform.GetBytes((short)dateTime.Year).CopyTo(buffer, 0);
            buffer[2] = (byte)dateTime.Month;
            buffer[3] = (byte)dateTime.Day;
            buffer[4] = 0x05;
            buffer[5] = (byte)dateTime.Hour;
            buffer[6] = (byte)dateTime.Minute;
            buffer[7] = (byte)dateTime.Second;
            byteTransform.GetBytes(dateTime.Millisecond * 1000 * 1000).CopyTo(buffer, 8);
            return buffer;
        }

        /// <summary>배열을 분석합니다. <c>T:System.DateTime</c> 바이트의 값입니다.</summary>
        /// <param name = "bytes">입력 바이트는 PLC에서 읽습니다.</param>
        /// <returns>배열 <c>T:System.DateTime</c> PLC에서 읽은 값을 나타내는 객체.</returns>
        public static DateTime[] ToArray(byte[] bytes)
        {
            if (bytes.Length % 8 != 0)
                throw new ArgumentOutOfRangeException(nameof(bytes), bytes.Length, $"Parsing an array of DateTime requires a multiple of 8 bytes of input data, input data is '{bytes.Length}' long.");
            var cnt = bytes.Length / 8;
            var result = new System.DateTime[bytes.Length / 8];
            for (var i = 0; i < cnt; i++)
                result[i] = FromByteArrayImpl(new ArraySegment<byte>(bytes, i * 8, 8).Array);
            return result;
        }

        /// <summary>FromByteArrayImpl 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "bytes">bytes에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        private static DateTime FromByteArrayImpl(IList<byte> bytes)
        {
            if (bytes.Count != 8)
                throw new ArgumentOutOfRangeException(nameof(bytes), bytes.Count, $"Parsing a DateTime requires exactly 8 bytes of input data, input data is {bytes.Count} bytes long.");
            int DecodeBcd(byte input) => 10 * (input >> 4) + (input & 0b00001111);
            int ByteToYear(byte bcdYear)
            {
                var input = DecodeBcd(bcdYear);
                if (input < 90)
                    return input + 2000;
                if (input < 100)
                    return input + 1900;
                throw new ArgumentOutOfRangeException(nameof(bcdYear), bcdYear, $"Value '{input}' is higher than the maximum '99' of S7 date and time representation.");
            }

            int AssertRangeInclusive(int input, byte min, byte max, string field)
            {
                if (input < min)
                    throw new ArgumentOutOfRangeException(nameof(input), input, $"Value '{input}' is lower than the minimum '{min}' allowed for {field}.");
                if (input > max)
                    throw new ArgumentOutOfRangeException(nameof(input), input, $"Value '{input}' is higher than the maximum '{max}' allowed for {field}.");
                return input;
            }

            var year = ByteToYear(bytes[0]);
            var month = AssertRangeInclusive(DecodeBcd(bytes[1]), 1, 12, "month");
            var day = AssertRangeInclusive(DecodeBcd(bytes[2]), 1, 31, "day of month");
            var hour = AssertRangeInclusive(DecodeBcd(bytes[3]), 0, 23, "hour");
            var minute = AssertRangeInclusive(DecodeBcd(bytes[4]), 0, 59, "minute");
            var second = AssertRangeInclusive(DecodeBcd(bytes[5]), 0, 59, "second");
            var hsec = AssertRangeInclusive(DecodeBcd(bytes[6]), 0, 99, "first two millisecond digits");
            var msec = AssertRangeInclusive(bytes[7] >> 4, 0, 9, "third millisecond digit");
            var dayOfWeek = AssertRangeInclusive(bytes[7] & 0b00001111, 1, 7, "day of week");
            return new System.DateTime(year, month, day, hour, minute, second, hsec * 10 + msec);
        }

        /// <summary>Converts a <c>T:System.DateTime</c> 바이트 배열의 값입니다.</summary>
        /// <param name = "dateTime">변환할 DateTime 값</param>
        /// <returns>S7 날짜 시간 표현을 포함하는 바이트 배열 <c>dateTime</c>.</returns>
        public static byte[] ToByteArray(DateTime dateTime)
        {
            byte EncodeBcd(int value)
            {
                return (byte)((value / 10 << 4) | value % 10);
            }

            if (dateTime < SpecMinimumDateTime)
                throw new ArgumentOutOfRangeException(nameof(dateTime), dateTime, $"Date time '{dateTime}' is before the minimum '{SpecMinimumDateTime}' supported in S7 date time representation.");
            if (dateTime > SpecMaximumDateTime)
                throw new ArgumentOutOfRangeException(nameof(dateTime), dateTime, $"Date time '{dateTime}' is after the maximum '{SpecMaximumDateTime}' supported in S7 date time representation.");
            byte MapYear(int year) => (byte)(year < 2000 ? year - 1900 : year - 2000);
            int DayOfWeekToInt(DayOfWeek dayOfWeek) => (int)dayOfWeek + 1;
            return new[]
            {
                EncodeBcd(MapYear(dateTime.Year)),
                EncodeBcd(dateTime.Month),
                EncodeBcd(dateTime.Day),
                EncodeBcd(dateTime.Hour),
                EncodeBcd(dateTime.Minute),
                EncodeBcd(dateTime.Second),
                EncodeBcd(dateTime.Millisecond / 10),
                (byte)(dateTime.Millisecond % 10 << 4 | DayOfWeekToInt(dateTime.DayOfWeek))
            };
        }

        /// <summary>배열을 변환합니다. <c>T:System.DateTime</c> 바이트 배열의 값입니다.</summary>
        /// <param name = "dateTimes">변환할 DateTime 값.</param>
        /// <returns>S7 날짜 시간 표현을 포함하는 바이트 배열 <c>dateTimes</c>.</returns>
        public static byte[] ToByteArray(System.DateTime[] dateTimes)
        {
            var bytes = new List<byte>(dateTimes.Length * 8);
            foreach (var dateTime in dateTimes)
                bytes.AddRange(ToByteArray(dateTime));
            return bytes.ToArray();
        }
    }
}
