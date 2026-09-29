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

namespace FieldLink.PlcDrivers.Common
{
    /// <summary>ProtocolBytes 프로토콜 값입니다.</summary>
    public static class ProtocolBytes
    {
        /// <summary>ArrayFormat 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "array">array에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <typeparam name = "T">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static string ArrayFormat<T>(T[] array) => ArrayFormat(array, string.Empty);
        /// <summary>ArrayFormat 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "array">array에 사용할 입력값입니다.</param>
        /// <param name = "format">format에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <typeparam name = "T">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static string ArrayFormat<T>(T[] array, string format)
        {
            if (array == null)
                return "NULL";
            StringBuilder sb = new StringBuilder("[");
            for (int i = 0; i < array.Length; i++)
            {
                sb.Append(string.IsNullOrEmpty(format) ? array[i].ToString() : string.Format(format, array[i]));
                if (i != array.Length - 1)
                    sb.Append(",");
            }

            sb.Append("]");
            return sb.ToString();
        }

        /// <summary>ArrayFormat 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "array">array에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <typeparam name = "T">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static string ArrayFormat<T>(T array) => ArrayFormat(array, string.Empty);
        /// <summary>ArrayFormat 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "array">array에 사용할 입력값입니다.</param>
        /// <param name = "format">format에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <typeparam name = "T">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static string ArrayFormat<T>(T array, string format)
        {
            StringBuilder sb = new StringBuilder("[");
            if (array is Array array1)
            {
                foreach (var item in array1)
                {
                    sb.Append(string.IsNullOrEmpty(format) ? item.ToString() : string.Format(format, item));
                    sb.Append(",");
                }

                if (array1.Length > 0 && sb[sb.Length - 1] == ',')
                    sb.Remove(sb.Length - 1, 1);
            }
            else
            {
                sb.Append(string.IsNullOrEmpty(format) ? array.ToString() : string.Format(format, array));
            }

            sb.Append("]");
            return sb.ToString();
        }

        /// <summary>ArrayExpandToLength 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <typeparam name = "T">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static T[] ArrayExpandToLength<T>(T[] data, int length)
        {
            if (data == null)
                return new T[length];
            if (data.Length == length)
                return data;
            T[] buffer = new T[length];
            Array.Copy(data, buffer, Math.Min(data.Length, buffer.Length));
            return buffer;
        }

        /// <summary>ArrayExpandToLengthEven 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <typeparam name = "T">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static T[] ArrayExpandToLengthEven<T>(T[] data)
        {
            if (data == null)
                return new T[0];
            if (data.Length % 2 == 1)
                return ArrayExpandToLength(data, data.Length + 1);
            else
                return data;
        }

        /// <summary>ArraySplitByLength 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "array">array에 사용할 입력값입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <typeparam name = "T">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static List<T[]> ArraySplitByLength<T>(T[] array, int length)
        {
            if (array == null)
                return new List<T[]>();
            List<T[]> result = new List<T[]>();
            int index = 0;
            while (index < array.Length)
            {
                if (index + length < array.Length)
                {
                    T[] tmp = new T[length];
                    Array.Copy(array, index, tmp, 0, length);
                    index += length;
                    result.Add(tmp);
                }
                else
                {
                    T[] tmp = new T[array.Length - index];
                    Array.Copy(array, index, tmp, 0, tmp.Length);
                    index += length;
                    result.Add(tmp);
                }
            }

            return result;
        }

        /// <summary>SplitIntegerToArray 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "integer">integer에 사용할 입력값입니다.</param>
        /// <param name = "everyLength">everyLength에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static int[] SplitIntegerToArray(int integer, int everyLength)
        {
            int[] result = new int[(integer / everyLength) + ((integer % everyLength) == 0 ? 0 : 1)];
            for (int i = 0; i < result.Length; i++)
            {
                if (i == result.Length - 1)
                {
                    result[i] = (integer % everyLength) == 0 ? everyLength : (integer % everyLength);
                }
                else
                {
                    result[i] = everyLength;
                }
            }

            return result;
        }

        /// <summary>ByteToHexString 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "InBytes">InBytes에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string ByteToHexString(byte[] InBytes) => ByteToHexString(InBytes, (char)0);
        /// <summary>ByteToHexString 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "InBytes">InBytes에 사용할 입력값입니다.</param>
        /// <param name = "segment">segment에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string ByteToHexString(byte[] InBytes, char segment) => ByteToHexString(InBytes, segment, 0);
        /// <summary>ByteToHexString 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "InBytes">InBytes에 사용할 입력값입니다.</param>
        /// <param name = "segment">segment에 사용할 입력값입니다.</param>
        /// <param name = "newLineCount">newLineCount에 사용할 입력값입니다.</param>
        /// <param name = "format">format에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string ByteToHexString(byte[] InBytes, char segment, int newLineCount, string format = "{0:X2}")
        {
            if (InBytes == null)
                return string.Empty;
            StringBuilder sb = new StringBuilder();
            long tick = 0;
            foreach (byte InByte in InBytes)
            {
                if (segment == 0)
                    sb.Append(string.Format(format, InByte));
                else
                    sb.Append(string.Format(format + "{1}", InByte, segment));
                tick++;
                if (newLineCount > 0 && tick >= newLineCount)
                {
                    sb.Append(Environment.NewLine);
                    tick = 0;
                }
            }

            if (segment != 0 && sb.Length > 1 && sb[sb.Length - 1] == segment)
            {
                sb.Remove(sb.Length - 1, 1);
            }

            return sb.ToString();
        }

        /// <summary>ByteToHexString 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "InString">InString에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string ByteToHexString(string InString) => ByteToHexString(Encoding.Unicode.GetBytes(InString));
        /// <summary>GetHexCharIndex 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "ch">ch에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static int GetHexCharIndex(char ch)
        {
            switch (ch)
            {
                case '0':
                    return 0;
                case '1':
                    return 1;
                case '2':
                    return 2;
                case '3':
                    return 3;
                case '4':
                    return 4;
                case '5':
                    return 5;
                case '6':
                    return 6;
                case '7':
                    return 7;
                case '8':
                    return 8;
                case '9':
                    return 9;
                case 'A':
                case 'a':
                    return 10;
                case 'B':
                case 'b':
                    return 11;
                case 'C':
                case 'c':
                    return 12;
                case 'D':
                case 'd':
                    return 13;
                case 'E':
                case 'e':
                    return 14;
                case 'F':
                case 'f':
                    return 15;
                default:
                    return -1;
            }
        }

        /// <summary>HexStringToBytes 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "hex">hex에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] HexStringToBytes(string hex)
        {
            MemoryStream ms = new MemoryStream();
            for (int i = 0; i < hex.Length; i++)
            {
                if ((i + 1) < hex.Length)
                {
                    if (GetHexCharIndex(hex[i]) >= 0 && GetHexCharIndex(hex[i + 1]) >= 0)
                    {
                        // 이 숫자는 1바이트입니다
                        ms.WriteByte((byte)(GetHexCharIndex(hex[i]) * 16 + GetHexCharIndex(hex[i + 1])));
                        i++;
                    }
                }
            }

            byte[] result = ms.ToArray();
            ms.Dispose();
            return result;
        }

        /// <summary>BytesReverseByWord 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "inBytes">inBytes에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BytesReverseByWord(byte[] inBytes)
        {
            if (inBytes == null)
                return null;
            if (inBytes.Length == 0)
                return new byte[0];
            byte[] buffer = ArrayExpandToLengthEven(inBytes.CopyArray());
            for (int i = 0; i < buffer.Length / 2; i++)
            {
                byte tmp = buffer[i * 2 + 0];
                buffer[i * 2 + 0] = buffer[i * 2 + 1];
                buffer[i * 2 + 1] = tmp;
            }

            return buffer;
        }

        /// <summary>GetAsciiStringRender 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "content">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string GetAsciiStringRender(byte[] content)
        {
            if (content == null)
                return string.Empty;
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < content.Length; i++)
            {
                if (content[i] < 0x20 || content[i] > 0x7E)
                {
                    sb.Append($"\\{content[i]:X2}");
                }
                else
                {
                    sb.Append((char)content[i]);
                }
            }

            return sb.ToString();
        }

        /// <summary>GetFromAsciiStringRender 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "render">render에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] GetFromAsciiStringRender(string render)
        {
            if (string.IsNullOrEmpty(render))
                return new byte[0];
            MatchEvaluator matchEvaluator = new MatchEvaluator(m => string.Format("{0}", (char)Convert.ToByte(m.Value.Substring(1), 16)));
            return Encoding.ASCII.GetBytes(Regex.Replace(render.Replace("\\r", "\r").Replace("\\n", "\n"), @"\\[0-9A-Fa-f]{2}", matchEvaluator));
        }

        /// <summary>BytesToAsciiBytes 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "inBytes">inBytes에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BytesToAsciiBytes(byte[] inBytes) => Encoding.ASCII.GetBytes(ByteToHexString(inBytes));
        /// <summary>AsciiBytesToBytes 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "inBytes">inBytes에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] AsciiBytesToBytes(byte[] inBytes) => HexStringToBytes(Encoding.ASCII.GetString(inBytes));
        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildAsciiBytesFrom(byte value) => Encoding.ASCII.GetBytes(value.ToString("X2"));
        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildAsciiBytesFrom(short value) => Encoding.ASCII.GetBytes(value.ToString("X4"));
        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildAsciiBytesFrom(ushort value) => Encoding.ASCII.GetBytes(value.ToString("X4"));
        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildAsciiBytesFrom(uint value) => Encoding.ASCII.GetBytes(value.ToString("X8"));
        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildAsciiBytesFrom(byte[] value)
        {
            byte[] buffer = new byte[value.Length * 2];
            for (int i = 0; i < value.Length; i++)
            {
                ProtocolBytes.BuildAsciiBytesFrom(value[i]).CopyTo(buffer, 2 * i);
            }

            return buffer;
        }

        /// <summary>GetDataByBitIndex 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "offset">offset에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static byte GetDataByBitIndex(int offset)
        {
            switch (offset)
            {
                case 0:
                    return 0x01;
                case 1:
                    return 0x02;
                case 2:
                    return 0x04;
                case 3:
                    return 0x08;
                case 4:
                    return 0x10;
                case 5:
                    return 0x20;
                case 6:
                    return 0x40;
                case 7:
                    return 0x80;
                default:
                    return 0;
            }
        }

        /// <summary>BoolOnByteIndex 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "offset">offset에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static bool BoolOnByteIndex(byte value, int offset)
        {
            byte temp = GetDataByBitIndex(offset);
            return (value & temp) == temp;
        }

        /// <summary>SetBoolOnByteIndex 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "byt">byt에 사용할 입력값입니다.</param>
        /// <param name = "offset">offset에 사용할 입력값입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte SetBoolOnByteIndex(byte byt, int offset, bool value)
        {
            byte temp = GetDataByBitIndex(offset);
            if (value)
                return (byte)(byt | temp);
            return (byte)(byt & (~temp));
        }

        /// <summary>BoolArrayToByte 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "array">array에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BoolArrayToByte(bool[] array)
        {
            if (array == null)
                return null;
            int length = array.Length % 8 == 0 ? array.Length / 8 : array.Length / 8 + 1;
            byte[] buffer = new byte[length];
            for (int i = 0; i < array.Length; i++)
            {
                if (array[i])
                    buffer[i / 8] += GetDataByBitIndex(i % 8);
            }

            return buffer;
        }

        /// <summary>BoolArrayToString 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "array">array에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string BoolArrayToString(bool[] array)
        {
            if (array == null)
                return string.Empty;
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < array.Length; i++)
            {
                sb.Append(array[i] ? "1" : "0");
            }

            return sb.ToString();
        }

        /// <summary>ByteToBoolArray 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "inBytes">inBytes에 사용할 입력값입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static bool[] ByteToBoolArray(byte[] inBytes, int length)
        {
            if (inBytes == null)
                return null;
            if (length > inBytes.Length * 8)
                length = inBytes.Length * 8;
            bool[] buffer = new bool[length];
            for (int i = 0; i < length; i++)
            {
                buffer[i] = BoolOnByteIndex(inBytes[i / 8], i % 8);
            }

            return buffer;
        }

        /// <summary>ByteToBoolArray 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "inBytes">inBytes에 사용할 입력값입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <param name = "trueValue">trueValue에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static bool[] ByteToBoolArray(byte[] inBytes, int length, byte trueValue)
        {
            if (inBytes == null)
                return null;
            if (length > inBytes.Length)
                length = inBytes.Length;
            bool[] buffer = new bool[length];
            for (int i = 0; i < length; i++)
            {
                buffer[i] = inBytes[i] == trueValue;
            }

            return buffer;
        }

        /// <summary>ByteToBoolArray 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "InBytes">InBytes에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static bool[] ByteToBoolArray(byte[] InBytes) => InBytes == null ? null : ByteToBoolArray(InBytes, InBytes.Length * 8);
        /// <summary>ArrayRemoveDouble 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "leftLength">leftLength에 사용할 입력값입니다.</param>
        /// <param name = "rightLength">rightLength에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <typeparam name = "T">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static T[] ArrayRemoveDouble<T>(T[] value, int leftLength, int rightLength)
        {
            if (value == null)
                return null;
            if (value.Length <= (leftLength + rightLength))
                return new T[0];
            T[] buffer = new T[value.Length - leftLength - rightLength];
            Array.Copy(value, leftLength, buffer, 0, buffer.Length);
            return buffer;
        }

        /// <summary>ArrayRemoveBegin 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <typeparam name = "T">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static T[] ArrayRemoveBegin<T>(T[] value, int length) => ArrayRemoveDouble(value, length, 0);
        /// <summary>ArrayRemoveLast 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <typeparam name = "T">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static T[] ArrayRemoveLast<T>(T[] value, int length) => ArrayRemoveDouble(value, 0, length);
        /// <summary>ArraySelectMiddle 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "index">데이터를 읽거나 기록할 위치입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <typeparam name = "T">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static T[] ArraySelectMiddle<T>(T[] value, int index, int length)
        {
            if (value == null)
                return null;
            if (length == 0)
                return new T[0];
            T[] buffer = new T[Math.Min(value.Length, length)];
            Array.Copy(value, index, buffer, 0, buffer.Length);
            return buffer;
        }

        /// <summary>ArraySelectBegin 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <typeparam name = "T">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static T[] ArraySelectBegin<T>(T[] value, int length)
        {
            if (length == 0)
                return new T[0];
            T[] buffer = new T[Math.Min(value.Length, length)];
            if (buffer.Length > 0)
                Array.Copy(value, 0, buffer, 0, buffer.Length);
            return buffer;
        }

        /// <summary>ArraySelectLast 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <typeparam name = "T">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static T[] ArraySelectLast<T>(T[] value, int length)
        {
            if (length == 0)
                return new T[0];
            T[] buffer = new T[Math.Min(value.Length, length)];
            Array.Copy(value, value.Length - length, buffer, 0, buffer.Length);
            return buffer;
        }

        /// <summary>SpliceArray 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "arrays">arrays에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <typeparam name = "T">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static T[] SpliceArray<T>(params T[][] arrays)
        {
            int count = 0;
            for (int i = 0; i < arrays.Length; i++)
            {
                if (arrays[i]?.Length > 0)
                {
                    count += arrays[i].Length;
                }
            }

            int index = 0;
            T[] buffer = new T[count];
            for (int i = 0; i < arrays.Length; i++)
            {
                if (arrays[i]?.Length > 0)
                {
                    arrays[i].CopyTo(buffer, index);
                    index += arrays[i].Length;
                }
            }

            return buffer;
        }

        /// <summary>SpliceStringArray 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "first">first에 사용할 입력값입니다.</param>
        /// <param name = "array">array에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string[] SpliceStringArray(string first, string[] array)
        {
            List<string> list = new List<string>();
            list.Add(first);
            list.AddRange(array);
            return list.ToArray();
        }

        /// <summary>SpliceStringArray 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "first">first에 사용할 입력값입니다.</param>
        /// <param name = "second">second에 사용할 입력값입니다.</param>
        /// <param name = "array">array에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string[] SpliceStringArray(string first, string second, string[] array)
        {
            List<string> list = new List<string>();
            list.Add(first);
            list.Add(second);
            list.AddRange(array);
            return list.ToArray();
        }

        /// <summary>SpliceStringArray 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "first">first에 사용할 입력값입니다.</param>
        /// <param name = "second">second에 사용할 입력값입니다.</param>
        /// <param name = "third">third에 사용할 입력값입니다.</param>
        /// <param name = "array">array에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string[] SpliceStringArray(string first, string second, string third, string[] array)
        {
            List<string> list = new List<string>();
            list.Add(first);
            list.Add(second);
            list.Add(third);
            list.AddRange(array);
            return list.ToArray();
        }

        /// <summary>HexToInt 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "h">h에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static int HexToInt(char h)
        {
            return (h >= '0' && h <= '9') ? h - '0' : (h >= 'a' && h <= 'f') ? h - 'a' + 10 : (h >= 'A' && h <= 'F') ? h - 'A' + 10 : -1;
        }
    }
}
