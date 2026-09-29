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
    /// <summary>ProtocolExtensions 프로토콜 값입니다.</summary>
    public static class ProtocolExtensions
    {
        /// <summary>ToHexString 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "InBytes">InBytes에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string ToHexString(this byte[] InBytes) => ProtocolBytes.ByteToHexString(InBytes);
        /// <summary>ToHexString 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "InBytes">InBytes에 사용할 입력값입니다.</param>
        /// <param name = "segment">segment에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string ToHexString(this byte[] InBytes, char segment) => ProtocolBytes.ByteToHexString(InBytes, segment);
        /// <summary>ToHexString 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "InBytes">InBytes에 사용할 입력값입니다.</param>
        /// <param name = "segment">segment에 사용할 입력값입니다.</param>
        /// <param name = "newLineCount">newLineCount에 사용할 입력값입니다.</param>
        /// <param name = "format">format에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string ToHexString(this byte[] InBytes, char segment, int newLineCount, string format = "{0:X2}") => ProtocolBytes.ByteToHexString(InBytes, segment, newLineCount, format);
        /// <summary>ToHexBytes 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] ToHexBytes(this string value) => ProtocolBytes.HexStringToBytes(value);
        /// <summary>ToByteArray 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "array">array에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] ToByteArray(this bool[] array) => ProtocolBytes.BoolArrayToByte(array);
        /// <summary>ToBoolArray 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "InBytes">InBytes에 사용할 입력값입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static bool[] ToBoolArray(this byte[] InBytes, int length) => ProtocolBytes.ByteToBoolArray(InBytes, length);
        /// <summary>ToBoolArray 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "InBytes">InBytes에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static bool[] ToBoolArray(this byte[] InBytes) => ProtocolBytes.ByteToBoolArray(InBytes);
        /// <summary>GetBoolByIndex 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "bytes">bytes에 사용할 입력값입니다.</param>
        /// <param name = "boolIndex">boolIndex에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static bool GetBoolByIndex(this byte[] bytes, int boolIndex)
        {
            return ProtocolBytes.BoolOnByteIndex(bytes[boolIndex / 8], boolIndex % 8);
        }

        /// <summary>GetBoolByIndex 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "byt">byt에 사용할 입력값입니다.</param>
        /// <param name = "boolIndex">boolIndex에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static bool GetBoolByIndex(this byte byt, int boolIndex)
        {
            return ProtocolBytes.BoolOnByteIndex(byt, boolIndex % 8);
        }

        /// <summary>GetBoolByIndex 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "boolIndex">boolIndex에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static bool GetBoolByIndex(this short value, int boolIndex) => BitConverter.GetBytes(value).GetBoolByIndex(boolIndex);
        /// <summary>GetBoolByIndex 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "boolIndex">boolIndex에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static bool GetBoolByIndex(this ushort value, int boolIndex) => BitConverter.GetBytes(value).GetBoolByIndex(boolIndex);
        /// <summary>GetBoolByIndex 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "boolIndex">boolIndex에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static bool GetBoolByIndex(this int value, int boolIndex) => BitConverter.GetBytes(value).GetBoolByIndex(boolIndex);
        /// <summary>GetBoolByIndex 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "boolIndex">boolIndex에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static bool GetBoolByIndex(this uint value, int boolIndex) => BitConverter.GetBytes(value).GetBoolByIndex(boolIndex);
        /// <summary>GetBoolByIndex 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "boolIndex">boolIndex에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static bool GetBoolByIndex(this long value, int boolIndex) => BitConverter.GetBytes(value).GetBoolByIndex(boolIndex);
        /// <summary>GetBoolByIndex 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "boolIndex">boolIndex에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static bool GetBoolByIndex(this ulong value, int boolIndex) => BitConverter.GetBytes(value).GetBoolByIndex(boolIndex);
        /// <summary>SetBoolByIndex 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "byt">byt에 사용할 입력값입니다.</param>
        /// <param name = "boolIndex">boolIndex에 사용할 입력값입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte SetBoolByIndex(this byte byt, int boolIndex, bool value) => ProtocolBytes.SetBoolOnByteIndex(byt, boolIndex, value);
        /// <summary>SetBoolByIndex 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "buffer">프레임 데이터가 담긴 버퍼입니다.</param>
        /// <param name = "boolIndex">boolIndex에 사용할 입력값입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        public static void SetBoolByIndex(this byte[] buffer, int boolIndex, bool value) => buffer[boolIndex / 8] = buffer[boolIndex / 8].SetBoolByIndex(boolIndex % 8, value);
        /// <summary>SetBoolByIndex 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "shortValue">shortValue에 사용할 입력값입니다.</param>
        /// <param name = "boolIndex">boolIndex에 사용할 입력값입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static short SetBoolByIndex(this short shortValue, int boolIndex, bool value)
        {
            byte[] buffer = BitConverter.GetBytes(shortValue);
            buffer.SetBoolByIndex(boolIndex, value);
            return BitConverter.ToInt16(buffer, 0);
        }

        /// <summary>SetBoolByIndex 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "ushortValue">ushortValue에 사용할 입력값입니다.</param>
        /// <param name = "boolIndex">boolIndex에 사용할 입력값입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static ushort SetBoolByIndex(this ushort ushortValue, int boolIndex, bool value)
        {
            byte[] buffer = BitConverter.GetBytes(ushortValue);
            buffer.SetBoolByIndex(boolIndex, value);
            return BitConverter.ToUInt16(buffer, 0);
        }

        /// <summary>SetBoolByIndex 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "intValue">intValue에 사용할 입력값입니다.</param>
        /// <param name = "boolIndex">boolIndex에 사용할 입력값입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static int SetBoolByIndex(this int intValue, int boolIndex, bool value)
        {
            byte[] buffer = BitConverter.GetBytes(intValue);
            buffer.SetBoolByIndex(boolIndex, value);
            return BitConverter.ToInt32(buffer, 0);
        }

        /// <summary>SetBoolByIndex 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "uintValue">uintValue에 사용할 입력값입니다.</param>
        /// <param name = "boolIndex">boolIndex에 사용할 입력값입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static uint SetBoolByIndex(this uint uintValue, int boolIndex, bool value)
        {
            byte[] buffer = BitConverter.GetBytes(uintValue);
            buffer.SetBoolByIndex(boolIndex, value);
            return BitConverter.ToUInt32(buffer, 0);
        }

        /// <summary>SetBoolByIndex 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "longValue">longValue에 사용할 입력값입니다.</param>
        /// <param name = "boolIndex">boolIndex에 사용할 입력값입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static long SetBoolByIndex(this long longValue, int boolIndex, bool value)
        {
            byte[] buffer = BitConverter.GetBytes(longValue);
            buffer.SetBoolByIndex(boolIndex, value);
            return BitConverter.ToInt64(buffer, 0);
        }

        /// <summary>SetBoolByIndex 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "ulongValue">ulongValue에 사용할 입력값입니다.</param>
        /// <param name = "boolIndex">boolIndex에 사용할 입력값입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static ulong SetBoolByIndex(this ulong ulongValue, int boolIndex, bool value)
        {
            byte[] buffer = BitConverter.GetBytes(ulongValue);
            buffer.SetBoolByIndex(boolIndex, value);
            return BitConverter.ToUInt64(buffer, 0);
        }

        /// <summary>RemoveDouble 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "leftLength">leftLength에 사용할 입력값입니다.</param>
        /// <param name = "rightLength">rightLength에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <typeparam name = "T">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static T[] RemoveDouble<T>(this T[] value, int leftLength, int rightLength) => ProtocolBytes.ArrayRemoveDouble(value, leftLength, rightLength);
        /// <summary>RemoveBegin 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <typeparam name = "T">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static T[] RemoveBegin<T>(this T[] value, int length) => ProtocolBytes.ArrayRemoveBegin(value, length);
        /// <summary>RemoveLast 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <typeparam name = "T">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static T[] RemoveLast<T>(this T[] value, int length) => ProtocolBytes.ArrayRemoveLast(value, length);
        /// <summary>SelectMiddle 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "index">데이터를 읽거나 기록할 위치입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <typeparam name = "T">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static T[] SelectMiddle<T>(this T[] value, int index, int length) => ProtocolBytes.ArraySelectMiddle(value, index, length);
        /// <summary>SelectBegin 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <typeparam name = "T">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static T[] SelectBegin<T>(this T[] value, int length) => ProtocolBytes.ArraySelectBegin(value, length);
        /// <summary>SelectLast 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <typeparam name = "T">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static T[] SelectLast<T>(this T[] value, int length) => ProtocolBytes.ArraySelectLast(value, length);
        /// <summary>RemoveLast 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string RemoveLast(this string value, int length)
        {
            if (value == null)
                return null;
            if (value.Length < length)
                return string.Empty;
            return value.Remove(value.Length - length);
        }

        /// <summary>CopyArray 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <typeparam name = "T">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static T[] CopyArray<T>(this T[] value)
        {
            if (value == null)
                return null;
            T[] buffer = new T[value.Length];
            Array.Copy(value, buffer, value.Length);
            return buffer;
        }

        /// <summary>ToArrayString 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <typeparam name = "T">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static string ToArrayString<T>(this T[] value) => ProtocolBytes.ArrayFormat(value);
        /// <summary>ToArrayString 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "format">format에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <typeparam name = "T">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static string ToArrayString<T>(this T[] value, string format) => ProtocolBytes.ArrayFormat(value, format);
        /// <summary>ToStringArray 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "selector">selector에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <typeparam name = "T">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static T[] ToStringArray<T>(this string value, Func<string, T> selector)
        {
            if (value.IndexOf('[') >= 0)
                value = value.Replace("[", "");
            if (value.IndexOf(']') >= 0)
                value = value.Replace("]", "");
            string[] splits = value.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            return splits.Select(selector).ToArray();
        }

        /// <summary>ToStringArray 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <typeparam name = "T">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static T[] ToStringArray<T>(this string value)
        {
            Type type = typeof(T);
            if (type == typeof(byte))
                return (T[])(object)value.ToStringArray(byte.Parse);
            else if (type == typeof(sbyte))
                return (T[])(object)value.ToStringArray(sbyte.Parse);
            else if (type == typeof(bool))
                return (T[])(object)value.ToStringArray(bool.Parse);
            else if (type == typeof(short))
                return (T[])(object)value.ToStringArray(short.Parse);
            else if (type == typeof(ushort))
                return (T[])(object)value.ToStringArray(ushort.Parse);
            else if (type == typeof(int))
                return (T[])(object)value.ToStringArray(int.Parse);
            else if (type == typeof(uint))
                return (T[])(object)value.ToStringArray(uint.Parse);
            else if (type == typeof(long))
                return (T[])(object)value.ToStringArray(long.Parse);
            else if (type == typeof(ulong))
                return (T[])(object)value.ToStringArray(ulong.Parse);
            else if (type == typeof(float))
                return (T[])(object)value.ToStringArray(float.Parse);
            else if (type == typeof(double))
                return (T[])(object)value.ToStringArray(double.Parse);
            else if (type == typeof(DateTime))
                return (T[])(object)value.ToStringArray(DateTime.Parse);
            else if (type == typeof(Guid))
                return (T[])(object)value.ToStringArray(Guid.Parse);
            else if (type == typeof(string))
                return (T[])(object)value.ToStringArray(m => m);
            else
                throw new Exception("use ToArray<T>(Func<string,T>) method instead");
        }

        /// <summary>SplitDot 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "str">str에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string[] SplitDot(this string str) => str.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
        /// <summary>Write 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "ms">ms에 사용할 입력값입니다.</param>
        /// <param name = "buffer">프레임 데이터가 담긴 버퍼입니다.</param>
        public static void Write(this MemoryStream ms, byte[] buffer)
        {
            if (buffer != null)
                ms.Write(buffer, 0, buffer.Length);
        }

        /// <summary>ReverseByWord 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "inBytes">inBytes에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] ReverseByWord(this byte[] inBytes) => ProtocolBytes.BytesReverseByWord(inBytes);
        /// <summary>StartsWithAndNumber 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "code">code에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static bool StartsWithAndNumber(this string address, string code)
        {
            if (address.StartsWith(code, StringComparison.InvariantCultureIgnoreCase))
            {
                if (char.IsNumber(address[code.Length]))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Contains 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "str">str에 사용할 입력값입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static bool Contains(this string str, string[] value)
        {
            if (value == null)
                return false;
            for (int i = 0; i < value.Length; i++)
            {
                if (str.Contains(value[i]))
                    return true;
            }

            return false;
        }

        /// <summary>ReverseNew 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <typeparam name = "T">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static T[] ReverseNew<T>(this T[] value)
        {
            T[] buffer = value.CopyArray();
            Array.Reverse(buffer);
            return buffer;
        }
    }
}
