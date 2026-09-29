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
    /// <summary>S7 객체의 클래스, 하나의 객체 정보를 나타내는</summary>
    public class S7Value
    {
        /// <summary>타입 코드</summary>
        public byte TypeCode { get; set; }
        /// <summary>메모리 데이터</summary>
        public byte[] Buffer { get; set; }
        /// <summary>값 객체</summary>
        public object Value { get; set; }
        /// <summary>표기 정보</summary>
        public byte Flag { get; set; }
        /// <summary>연관된 구조의 식별 정보</summary>
        public uint StructID { get; set; }

        /// <summary>GetBufferBool 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "byteTransform">byteTransform에 사용할 입력값입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static byte[] GetBufferBool(IProtocolValueConverter byteTransform, bool value)
        {
            byte[] buffer = new byte[3];
            buffer[1] = 0x01;
            if (value)
                buffer[2] = 0x01;
            else
                buffer[2] = 0x00;
            return buffer;
        }

        /// <summary>GetBufferInt8 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "byteTransform">byteTransform에 사용할 입력값입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static byte[] GetBufferInt8(IProtocolValueConverter byteTransform, sbyte value)
        {
            byte[] buffer = new byte[3];
            buffer[1] = 0x06;
            buffer[2] = (byte)value;
            return buffer;
        }

        /// <summary>GetBufferUInt8 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "byteTransform">byteTransform에 사용할 입력값입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static byte[] GetBufferUInt8(IProtocolValueConverter byteTransform, byte value)
        {
            byte[] buffer = new byte[3];
            buffer[1] = 0x0a;
            buffer[2] = value;
            return buffer;
        }

        /// <summary>GetBufferUInt8 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "byteTransform">byteTransform에 사용할 입력값입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static byte[] GetBufferUInt8(IProtocolValueConverter byteTransform, byte[] value)
        {
            MemoryStream ms = new MemoryStream();
            ms.WriteByte(0x10);
            ms.WriteByte(0x0a);
            S7Object.WriteUint32(ms, (uint)value.Length);
            ms.Write(value);
            return ms.ToArray();
            ;
        }

        /// <summary>GetBufferInt16 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "byteTransform">byteTransform에 사용할 입력값입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static byte[] GetBufferInt16(IProtocolValueConverter byteTransform, short value)
        {
            byte[] buffer = new byte[4];
            buffer[1] = 0x07;
            byteTransform.GetBytes(value).CopyTo(buffer, 2);
            return buffer;
        }

        /// <summary>GetBufferInt16 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "byteTransform">byteTransform에 사용할 입력값입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static byte[] GetBufferInt16(IProtocolValueConverter byteTransform, short[] value)
        {
            MemoryStream ms = new MemoryStream();
            ms.WriteByte(0x10);
            ms.WriteByte(0x07);
            S7Object.WriteUint32(ms, (uint)value.Length);
            ms.Write(byteTransform.GetBytes(value));
            return ms.ToArray();
            ;
        }

        /// <summary>GetBufferUInt16 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "byteTransform">byteTransform에 사용할 입력값입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static byte[] GetBufferUInt16(IProtocolValueConverter byteTransform, ushort value)
        {
            byte[] buffer = new byte[4];
            buffer[1] = 0x0b;
            byteTransform.GetBytes(value).CopyTo(buffer, 2);
            return buffer;
        }

        /// <summary>GetBufferUInt16 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "byteTransform">byteTransform에 사용할 입력값입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static byte[] GetBufferUInt16(IProtocolValueConverter byteTransform, ushort[] value)
        {
            MemoryStream ms = new MemoryStream();
            ms.WriteByte(0x10);
            ms.WriteByte(0x0b);
            S7Object.WriteUint32(ms, (uint)value.Length);
            ms.Write(byteTransform.GetBytes(value));
            return ms.ToArray();
        }

        /// <summary>GetBufferInt32 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "byteTransform">byteTransform에 사용할 입력값입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static byte[] GetBufferInt32(IProtocolValueConverter byteTransform, int value)
        {
            MemoryStream ms = new MemoryStream();
            ms.WriteByte(0x00);
            ms.WriteByte(0x08);
            S7Object.WriteUint32(ms, (uint)value);
            return ms.ToArray();
        }

        /// <summary>GetBufferInt32 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "byteTransform">byteTransform에 사용할 입력값입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static byte[] GetBufferInt32(IProtocolValueConverter byteTransform, int[] value)
        {
            MemoryStream ms = new MemoryStream();
            ms.WriteByte(0x10);
            ms.WriteByte(0x08);
            S7Object.WriteUint32(ms, (uint)value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                S7Object.WriteUint32(ms, (uint)value[i]);
            }

            return ms.ToArray();
        }

        /// <summary>GetBufferUInt32 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "byteTransform">byteTransform에 사용할 입력값입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static byte[] GetBufferUInt32(IProtocolValueConverter byteTransform, uint value)
        {
            byte[] buffer = new byte[6];
            buffer[1] = 0x0c;
            byteTransform.GetBytes(value).CopyTo(buffer, 2);
            return buffer;
        }

        /// <summary>GetBufferUInt32 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "byteTransform">byteTransform에 사용할 입력값입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static byte[] GetBufferUInt32(IProtocolValueConverter byteTransform, uint[] value)
        {
            MemoryStream ms = new MemoryStream();
            ms.WriteByte(0x10);
            ms.WriteByte(0x0c);
            S7Object.WriteUint32(ms, (uint)value.Length);
            ms.Write(byteTransform.GetBytes(value));
            return ms.ToArray();
        }

        /// <summary>GetBufferInt64 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "byteTransform">byteTransform에 사용할 입력값입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static byte[] GetBufferInt64(IProtocolValueConverter byteTransform, long value)
        {
            MemoryStream ms = new MemoryStream();
            ms.WriteByte(0x00);
            ms.WriteByte(0x09);
            S7Object.WriteUint64(ms, (ulong)value);
            return ms.ToArray();
        }

        /// <summary>GetBufferInt64 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "byteTransform">byteTransform에 사용할 입력값입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static byte[] GetBufferInt64(IProtocolValueConverter byteTransform, long[] value)
        {
            MemoryStream ms = new MemoryStream();
            ms.WriteByte(0x10);
            ms.WriteByte(0x09);
            S7Object.WriteUint32(ms, (uint)value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                S7Object.WriteUint64(ms, (ulong)value[i]);
            }

            return ms.ToArray();
        }

        /// <summary>GetBufferUInt64 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "byteTransform">byteTransform에 사용할 입력값입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static byte[] GetBufferUInt64(IProtocolValueConverter byteTransform, ulong value)
        {
            byte[] buffer = new byte[10];
            buffer[1] = 0x0d;
            byteTransform.GetBytes(value).CopyTo(buffer, 2);
            return buffer;
        }

        /// <summary>GetBufferUInt64 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "byteTransform">byteTransform에 사용할 입력값입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static byte[] GetBufferUInt64(IProtocolValueConverter byteTransform, ulong[] value)
        {
            MemoryStream ms = new MemoryStream();
            ms.WriteByte(0x10);
            ms.WriteByte(0x0d);
            S7Object.WriteUint32(ms, (uint)value.Length);
            ms.Write(byteTransform.GetBytes(value));
            return ms.ToArray();
        }

        /// <summary>GetBufferFloat 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "byteTransform">byteTransform에 사용할 입력값입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static byte[] GetBufferFloat(IProtocolValueConverter byteTransform, float value)
        {
            byte[] buffer = new byte[6];
            buffer[1] = 0x0e;
            byteTransform.GetBytes(value).CopyTo(buffer, 2);
            return buffer;
        }

        /// <summary>GetBufferFloat 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "byteTransform">byteTransform에 사용할 입력값입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static byte[] GetBufferFloat(IProtocolValueConverter byteTransform, float[] value)
        {
            MemoryStream ms = new MemoryStream();
            ms.WriteByte(0x10);
            ms.WriteByte(0x0e);
            S7Object.WriteUint32(ms, (uint)value.Length);
            ms.Write(byteTransform.GetBytes(value));
            return ms.ToArray();
        }

        /// <summary>GetBufferDouble 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "byteTransform">byteTransform에 사용할 입력값입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static byte[] GetBufferDouble(IProtocolValueConverter byteTransform, double value)
        {
            byte[] buffer = new byte[10];
            buffer[1] = 0x0f;
            byteTransform.GetBytes(value).CopyTo(buffer, 2);
            return buffer;
        }

        /// <summary>GetBufferDouble 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "byteTransform">byteTransform에 사용할 입력값입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static byte[] GetBufferDouble(IProtocolValueConverter byteTransform, double[] value)
        {
            MemoryStream ms = new MemoryStream();
            ms.WriteByte(0x10);
            ms.WriteByte(0x0f);
            S7Object.WriteUint32(ms, (uint)value.Length);
            ms.Write(byteTransform.GetBytes(value));
            return ms.ToArray();
        }
    }
}
