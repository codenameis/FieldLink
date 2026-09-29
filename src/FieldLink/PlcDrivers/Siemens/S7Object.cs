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
    /// <summary>시몬스의 데이터 객체 정보</summary>
    public class S7Object : IS7Object
    {
        /// <summary>RelationId</summary>
        public uint RelationId { get; set; }
        /// <summary>두 번째 관련 정보</summary>
        public uint RelationId2 { get; set; }
        /// <summary>ClassId</summary>
        public uint ClassId { get; set; }
        /// <summary>ClassFlags</summary>
        public uint ClassFlags { get; set; }
        /// <summary>AttributeId</summary>
        public uint AttributeId { get; set; }
        /// <summary>이름 정보</summary>
        public string Name { get; set; }
        /// <summary>데이터 객체와 연관된 하위 객체 정보</summary>
        public List<S7Object> SubObjects { get; set; }
        /// <summary>연결된 노드 정보</summary>
        public List<S7Tag> S7Tags { get; set; }

        /// <summary>WriteMessgae 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "ms">ms에 사용할 입력값입니다.</param>
        public void WriteMessgae(MemoryStream ms)
        {
            S7Object.WriteUint32(ms, 0x00); // symbocrc
            S7Object.WriteUint32(ms, RelationId); // 접근 영역
            S7Object.WriteUint32(ms, 0x02); // LID 개수
            S7Object.WriteUint32(ms, 2550); // 하위 영역
            S7Object.WriteUint32(ms, 1); // id
        }

        /// <summary>GetNumberOfFields 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public int GetNumberOfFields()
        {
            return 5;
        }

        /// <summary>현재 프로토콜 값의 문자열 표현을 반환합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public override string ToString() => $"S7Object[{Name}]";
        /// <summary>GetLongLength 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        private static int GetLongLength(ulong value)
        {
            ulong compare = 0x80;
            int len = 0;
            while (true)
            {
                len++;
                if (len >= 9)
                    return len;
                if (value < compare)
                    return len;
                compare <<= 7;
            }
        }

        /// <summary>WriteAutoLength 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "ms">ms에 사용할 입력값입니다.</param>
        /// <param name = "len">len에 사용할 입력값입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        private static void WriteAutoLength(MemoryStream ms, int len, ulong value)
        {
            byte[] tmp = new byte[len];
            for (int i = len - 1; i >= 0; i--)
            {
                if (i == len - 1)
                {
                    if (i == 8)
                    {
                        tmp[i] = (byte)value;
                        value >>= 8;
                    }
                    else
                    {
                        tmp[i] = (byte)(value & 0x7f);
                        value >>= 7;
                    }
                }
                else
                {
                    tmp[i] = (byte)(value | 0x80);
                    value >>= 7;
                }
            }

            ms.Write(tmp, 0, tmp.Length);
        }

        /// <summary>이 데이터의 크기를 측정하기 위해, uint 타입의 데이터를 입력합니다.</summary>
        /// <param name = "ms">바이트 스트림</param>
        /// <param name = "value">값 정보</param>
        public static void WriteUint32(MemoryStream ms, uint value)
        {
            WriteAutoLength(ms, GetLongLength(value), value);
        }

        /// <summary>동적 길이 표현법을 사용하여, 한 롱 타입의 데이터를 바이트 스트림에 기록합니다.</summary>
        /// <param name = "ms">바이트 스트림</param>
        /// <param name = "value">값 정보</param>
        public static void WriteUint64(MemoryStream ms, ulong value)
        {
            WriteAutoLength(ms, GetLongLength(value), value);
        }

        /// <summary>버퍼에서 동적 길이를 얻는 uint 타입 데이터</summary>
        /// <param name = "buffer">버퍼 값</param>
        /// <param name = "index">인덱스 정보</param>
        /// <returns>결과값</returns>
        public static uint GetValueUint32(byte[] buffer, ref int index)
        {
            int value = 0;
            int len = 0;
            for (int i = 0; i < 5; i++)
            {
                len++;
                value <<= 7;
                value += (byte)(buffer[index + i] & 0x7f);
                if (buffer[index + i].GetBoolByIndex(7) == false)
                {
                    break;
                }
            }

            index += len;
            return (uint)value;
        }

        /// <summary>버퍼에서 동적 길이의ulong 타입 데이터를 가져옵니다.</summary>
        /// <param name = "buffer">버퍼 값</param>
        /// <param name = "index">인덱스 정보</param>
        /// <returns>결과값</returns>
        public static ulong GetValueUint64(byte[] buffer, ref int index)
        {
            ulong value = 0;
            int len = 0;
            for (int i = 0; i < 9; i++)
            {
                len++;
                if (i == 8)
                {
                    value <<= 8;
                    value += buffer[index + i];
                }
                else
                {
                    value <<= 7;
                    value += (byte)(buffer[index + i] & 0x7f);
                    if (buffer[index + i].GetBoolByIndex(7) == false)
                    {
                        break;
                    }
                }
            }

            index += len;
            return value;
        }
    }
}
