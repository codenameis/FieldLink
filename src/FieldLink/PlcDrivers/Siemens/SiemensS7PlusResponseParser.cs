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
using static FieldLink.PlcDrivers.Siemens.SiemensS7PlusCommandBuilder;
using static FieldLink.PlcDrivers.Siemens.SiemensS7PlusValueConverter;

namespace FieldLink.PlcDrivers.Siemens
{
    /// <summary>SiemensS7Plus 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class SiemensS7PlusResponseParser
    {
        /// <summary>프로토콜 응답에서 데이터와 상태 정보를 추출합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "buffer">프레임 데이터가 담긴 버퍼입니다.</param>
        /// <param name = "index">데이터를 읽거나 기록할 위치입니다.</param>
        /// <param name = "ignoreID">ignoreID에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static S7Value ExtraS7Value(S7PlusCodecOptions context, byte[] buffer, ref int index, bool ignoreID)
        {
            if (!ignoreID)
            {
                uint id = S7Object.GetValueUint32(buffer, ref index);
                if (id == 0)
                    return null;
            }

            byte flag = buffer[index++];
            byte type = buffer[index++];
            S7Value s7Value = new S7Value();
            s7Value.TypeCode = type;
            s7Value.Flag = flag;
            if (flag == 0x00)
            {
                if (type == 0x01)
                {
                    s7Value.Value = Convert.ToBoolean(buffer[index]);
                    s7Value.Buffer = new byte[]
                    {
                        buffer[index]
                    };
                    index += 1;
                    return s7Value;
                }
                else if (type == 0x02 || type == 0x0a)
                {
                    s7Value.Value = buffer[index];
                    s7Value.Buffer = new byte[]
                    {
                        buffer[index]
                    };
                    index += 1;
                    return s7Value;
                }
                else if (type == 0x03 || type == 0x0b)
                {
                    s7Value.Value = context.ValueConverter.ReadUInt16(buffer, index);
                    s7Value.Buffer = buffer.SelectMiddle(index, 2);
                    index += 2;
                    return s7Value;
                }
                else if (type == 0x04 || type == 0x08 || type == 0x13)
                {
                    uint value = S7Object.GetValueUint32(buffer, ref index);
                    if (type == 0x04)
                        s7Value.Value = value;
                    else
                        s7Value.Value = (int)value;
                    s7Value.Buffer = context.ValueConverter.GetBytes(value);
                    return s7Value;
                }
                else if (type == 0x05)
                {
                    ulong value = S7Object.GetValueUint64(buffer, ref index);
                    s7Value.Value = value;
                    s7Value.Buffer = context.ValueConverter.GetBytes(value);
                    return s7Value;
                }
                else if (type == 0x06)
                {
                    s7Value.Value = (sbyte)buffer[index];
                    s7Value.Buffer = new byte[]
                    {
                        buffer[index]
                    };
                    index += 1;
                    return s7Value;
                }
                else if (type == 0x07)
                {
                    s7Value.Value = context.ValueConverter.ReadInt16(buffer, index);
                    s7Value.Buffer = buffer.SelectMiddle(index, 2);
                    index += 2;
                    return s7Value;
                }
                else if (type == 0x09 || type == 0x0D || type == 0x10 || type == 0x11)
                {
                    ulong value = S7Object.GetValueUint64(buffer, ref index);
                    if (type == 0x0d || type == 0x10)
                        s7Value.Value = value;
                    else
                        s7Value.Value = (long)value;
                    s7Value.Buffer = context.ValueConverter.GetBytes(value);
                    return s7Value;
                }
                else if (type == 0x0C || type == 0x12)
                {
                    s7Value.Value = context.ValueConverter.ReadUInt32(buffer, index);
                    s7Value.Buffer = buffer.SelectMiddle(index, 4);
                    index += 4;
                    return s7Value;
                }
                else if (type == 0x0D || type == 0x10)
                {
                    s7Value.Value = context.ValueConverter.ReadUInt64(buffer, index);
                    s7Value.Buffer = buffer.SelectMiddle(index, 8);
                    index += 8;
                    return s7Value;
                }
                else if (type == 0x0E)
                {
                    s7Value.Value = context.ValueConverter.ReadSingle(buffer, index);
                    s7Value.Buffer = buffer.SelectMiddle(index, 4);
                    index += 4;
                    return s7Value;
                }
                else if (type == 0x0F)
                {
                    s7Value.Value = context.ValueConverter.ReadDouble(buffer, index);
                    s7Value.Buffer = buffer.SelectMiddle(index, 8);
                    index += 8;
                    return s7Value;
                }
                else if (type == 0x15)
                {
                    int len = (int)S7Object.GetValueUint32(buffer, ref index);
                    s7Value.Buffer = buffer.SelectMiddle(index, len);
                    s7Value.Value = Encoding.UTF8.GetString(s7Value.Buffer);
                    index += len;
                    return s7Value;
                }
                else if (type == 0x17) // 구조체
                {
                    s7Value.StructID = context.ValueConverter.ReadUInt32(buffer, index);
                    index += 4;
                    index += 8;
                    uint flags = S7Object.GetValueUint32(buffer, ref index);
                    uint bytesCount = S7Object.GetValueUint32(buffer, ref index);
                    if ((flags & 0x400) != 0)
                        bytesCount = S7Object.GetValueUint32(buffer, ref index);
                    s7Value.Buffer = buffer.SelectMiddle(index, (int)bytesCount);
                    index += (int)bytesCount;
                    return s7Value;
                }
            }
            else if (flag == 0x10)
            {
                // 그룹 상태
                if (type == 0x02 || type == 0x0a)
                {
                    int len = (int)S7Object.GetValueUint32(buffer, ref index);
                    s7Value.Buffer = buffer.SelectMiddle(index, len);
                    s7Value.Value = buffer.SelectMiddle(index, len);
                    index += len;
                    return s7Value;
                }
                else if (type == 0x03 || type == 0x0b)
                {
                    int len = (int)S7Object.GetValueUint32(buffer, ref index);
                    s7Value.Value = context.ValueConverter.ReadUInt16(buffer, index, len);
                    s7Value.Buffer = buffer.SelectMiddle(index, 2 * len);
                    index += 2 * len;
                    return s7Value;
                }
                else if (type == 0x04)
                {
                    int len = (int)S7Object.GetValueUint32(buffer, ref index);
                    uint[] values = new uint[len];
                    for (int i = 0; i < len; i++)
                    {
                        values[i] = S7Object.GetValueUint32(buffer, ref index);
                    }

                    s7Value.Value = values;
                    s7Value.Buffer = context.ValueConverter.GetBytes(values);
                    return s7Value;
                }
                else if (type == 0x05)
                {
                    int len = (int)S7Object.GetValueUint32(buffer, ref index);
                    ulong[] values = new ulong[len];
                    for (int i = 0; i < len; i++)
                    {
                        values[i] = S7Object.GetValueUint64(buffer, ref index);
                    }

                    s7Value.Value = values;
                    s7Value.Buffer = context.ValueConverter.GetBytes(values);
                    return s7Value;
                }
                else if (type == 0x06)
                {
                    int len = (int)S7Object.GetValueUint32(buffer, ref index);
                    sbyte[] values = new sbyte[len];
                    for (int i = 0; i < len; i++)
                    {
                        values[i] = (sbyte)buffer[index + i];
                    }

                    s7Value.Value = values;
                    s7Value.Buffer = buffer.SelectMiddle(index, len);
                    index += len;
                    return s7Value;
                }
                else if (type == 0x07)
                {
                    int len = (int)S7Object.GetValueUint32(buffer, ref index);
                    s7Value.Value = context.ValueConverter.ReadInt16(buffer, index, len);
                    s7Value.Buffer = buffer.SelectMiddle(index, 2 * len);
                    index += 2 * len;
                    return s7Value;
                }
                else if (type == 0x08)
                {
                    int len = (int)S7Object.GetValueUint32(buffer, ref index);
                    int[] values = new int[len];
                    for (int i = 0; i < len; i++)
                    {
                        values[i] = (int)S7Object.GetValueUint32(buffer, ref index);
                    }

                    s7Value.Value = values;
                    s7Value.Buffer = context.ValueConverter.GetBytes(values);
                    return s7Value;
                }
                else if (type == 0x09)
                {
                    int len = (int)S7Object.GetValueUint32(buffer, ref index);
                    long[] values = new long[len];
                    for (int i = 0; i < len; i++)
                    {
                        values[i] = (long)S7Object.GetValueUint64(buffer, ref index);
                    }

                    s7Value.Value = values;
                    s7Value.Buffer = context.ValueConverter.GetBytes(values);
                    return s7Value;
                }
                else if (type == 0x0c)
                {
                    int len = (int)S7Object.GetValueUint32(buffer, ref index);
                    s7Value.Value = context.ValueConverter.ReadUInt32(buffer, index, len);
                    s7Value.Buffer = buffer.SelectMiddle(index, 4 * len);
                    index += 4 * len;
                    return s7Value;
                }
                else if (type == 0x0d)
                {
                    int len = (int)S7Object.GetValueUint32(buffer, ref index);
                    s7Value.Value = context.ValueConverter.ReadUInt64(buffer, index, len);
                    s7Value.Buffer = buffer.SelectMiddle(index, 8 * len);
                    index += 8 * len;
                    return s7Value;
                }
                else if (type == 0x0E)
                {
                    int len = (int)S7Object.GetValueUint32(buffer, ref index);
                    s7Value.Value = context.ValueConverter.ReadSingle(buffer, index, len);
                    s7Value.Buffer = buffer.SelectMiddle(index, 4 * len);
                    index += 4 * len;
                    return s7Value;
                }
                else if (type == 0x0F)
                {
                    int len = (int)S7Object.GetValueUint32(buffer, ref index);
                    s7Value.Value = context.ValueConverter.ReadDouble(buffer, index, len);
                    s7Value.Buffer = buffer.SelectMiddle(index, 8 * len);
                    index += 8 * len;
                    return s7Value;
                }
            }

            return s7Value;
        }

        /// <summary>프로토콜 응답에서 데이터와 상태 정보를 추출합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "buffer">프레임 데이터가 담긴 버퍼입니다.</param>
        /// <param name = "attributeId">attributeId에 사용할 입력값입니다.</param>
        /// <param name = "index">데이터를 읽거나 기록할 위치입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> ExtraAttributeData(S7PlusCodecOptions context, byte[] buffer, uint attributeId, ref int index)
        {
            uint id = S7Object.GetValueUint32(buffer, ref index);
            if (id == 0)
                return null;
            byte flag = buffer[index++];
            byte type = buffer[index++];
            if (flag == 0x00)
            {
                if (type == 0x01)
                {
                    index++;
                    if (id == attributeId)
                        return OperationResult.CreateSuccessResult(new byte[] { buffer[index] });
                }
                else if (type == 0x04 || type == 0x08)
                {
                    uint value = S7Object.GetValueUint32(buffer, ref index);
                    if (id == attributeId)
                        return OperationResult.CreateSuccessResult(context.ValueConverter.GetBytes(value));
                }
                else if (type == 0x05)
                {
                    ulong value = S7Object.GetValueUint64(buffer, ref index);
                    if (id == attributeId)
                        return OperationResult.CreateSuccessResult(context.ValueConverter.GetBytes(value));
                }
                else if (type == 0x03)
                {
                    index += 2;
                    if (id == attributeId)
                        return OperationResult.CreateSuccessResult(buffer.SelectMiddle(index - 2, 2));
                }
                else if (type == 0x0B)
                {
                    index += 2;
                    if (id == attributeId)
                        return OperationResult.CreateSuccessResult(buffer.SelectMiddle(index - 2, 2));
                }
                else if (type == 0x0C)
                {
                    index += 4;
                    if (id == attributeId)
                        return OperationResult.CreateSuccessResult(buffer.SelectMiddle(index - 4, 4));
                }
                else if (type == 0x15)
                {
                    int len = (int)S7Object.GetValueUint32(buffer, ref index);
                    index += len;
                    if (id == attributeId)
                        return OperationResult.CreateSuccessResult(buffer.SelectMiddle(index - len, len));
                }
                else if (type == 0x17) // 구조체
                {
                    int startIndex = index - 2;
                    index += 4;
                    OperationResult<byte[]> value = ExtraAttributeData(context, buffer, attributeId, ref index);
                    while (value != null)
                    {
                        if (value.IsSuccess && value.Content != null)
                            return value;
                        value = ExtraAttributeData(context, buffer, attributeId, ref index);
                    }

                    if (id == attributeId)
                        return OperationResult.CreateSuccessResult(buffer.SelectMiddle(startIndex, index - startIndex));
                }
            }
            else if (flag == 0x10)
            {
                // 그룹 상태
                if (type == 0x02)
                {
                    int len = (int)S7Object.GetValueUint32(buffer, ref index);
                    index += len;
                    if (id == attributeId)
                        return OperationResult.CreateSuccessResult(buffer.SelectMiddle(index - len, len));
                }
            }

            byte[] emptyNull = null;
            return OperationResult.CreateSuccessResult(emptyNull);
        }

        /// <summary>프로토콜 응답에서 데이터와 상태 정보를 추출합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "buffer">프레임 데이터가 담긴 버퍼입니다.</param>
        /// <param name = "attributeId">attributeId에 사용할 입력값입니다.</param>
        /// <param name = "index">데이터를 읽거나 기록할 위치입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> ExtraResponseData(S7PlusCodecOptions context, byte[] buffer, uint attributeId, ref int index)
        {
            while (index < buffer.Length)
            {
                byte code = buffer[index++];
                if (code == 0xA3) // 설명은 특성입니다.
                {
                    OperationResult<byte[]> extra = ExtraAttributeData(context, buffer, attributeId, ref index);
                    if (extra.IsSuccess && extra.Content != null)
                        return extra;
                }
                else if (code == 0xA1) // StartOfObject
                {
                    index += 4; // RelationId
                    S7Object.GetValueUint32(buffer, ref index); // ClassId
                    S7Object.GetValueUint32(buffer, ref index); // ClassFlags
                    S7Object.GetValueUint32(buffer, ref index); // AttributeId
                    OperationResult<byte[]> extra = ExtraResponseData(context, buffer, attributeId, ref index);
                    if (extra.IsSuccess == false)
                        return extra;
                    if (extra.IsSuccess && extra.Content != null)
                        return extra;
                }
                else
                {
                    break;
                }
            }

            return new OperationResult<byte[]>($"No attribute id [{attributeId}] data");
        }

        /// <summary>입력 데이터가 프로토콜의 검증 조건을 만족하는지 확인합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "buffer">프레임 데이터가 담긴 버퍼입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult CheckReturnCode(S7PlusCodecOptions context, byte[] buffer)
        {
            try
            {
                int index = 10;
                ulong returnCode = S7Object.GetValueUint64(buffer, ref index);
                if (returnCode == 0)
                    return OperationResult.CreateSuccessResult();
                return new OperationResult("ReturnCode: " + returnCode.ToString() + " Source: " + buffer.ToHexString(' '));
            }
            catch (Exception ex)
            {
                return new OperationResult("ExploreS7Values failed: " + ex.Message);
            }
        }
    }
}
