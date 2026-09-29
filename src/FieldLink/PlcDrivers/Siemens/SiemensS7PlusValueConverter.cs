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
using static FieldLink.PlcDrivers.Siemens.SiemensS7PlusResponseParser;

namespace FieldLink.PlcDrivers.Siemens
{
    /// <summary>SiemensS7Plus 프로토콜의 값과 바이트 표현을 변환합니다.</summary>
    public static class SiemensS7PlusValueConverter
    {
        /// <summary>GetISOTelegrams 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] GetISOTelegrams(S7PlusCodecOptions context)
        {
            context.ValueConverter.GetBytes(context.LocalTSAP).CopyTo(context.iso_head, 16);
            context.iso_head[3] = (byte)(20 + context.destTSAP.Length);
            context.iso_head[4] = (byte)(15 + context.destTSAP.Length);
            context.iso_head[19] = (byte)context.destTSAP.Length;
            return ProtocolBytes.SpliceArray(context.iso_head, context.destTSAP);
        }

        /// <summary>ExploreResponse 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "buffer">프레임 데이터가 담긴 버퍼입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static List<S7Object> ExploreResponse(S7PlusCodecOptions context, byte[] buffer)
        {
            int index = 10;
            S7Object.GetValueUint64(buffer, ref index);
            index += 4;
            S7Object.GetValueUint32(buffer, ref index);
            List<S7Object> list = new List<S7Object>();
            while (true)
            {
                if (buffer[index] == 0xA1)
                {
                    list.Add(CreateS7Object(context, buffer, ref index));
                }
                else
                {
                    break;
                }
            }

            return list;
        }

        /// <summary>ExploreS7Values 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "s7Tags">s7Tags에 사용할 입력값입니다.</param>
        /// <param name = "buffer">프레임 데이터가 담긴 버퍼입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<List<S7Value>> ExploreS7Values(S7PlusCodecOptions context, S7Tag[] s7Tags, byte[] buffer)
        {
            try
            {
                int index = 10;
                ulong returnCode = S7Object.GetValueUint64(buffer, ref index);
                int moreData = 0;
                int i = 0;
                List<S7Value> list = new List<S7Value>();
                while (true)
                {
                    moreData = (int)S7Object.GetValueUint32(buffer, ref index);
                    if (moreData == 0x00)
                        break;
                    S7Value s7Value = ExtraS7Value(context, buffer, ref index, true);
                    if (s7Value == null)
                        break;
                    if (i < s7Tags.Length)
                    {
                        if (s7Tags[i].TypeCode == 0x13 && s7Tags[i].ArrayLength < 0)
                        {
                            s7Value.Value = Encoding.Default.GetString(s7Value.Buffer, 2, s7Value.Buffer[1]);
                        }
                    }

                    list.Add(s7Value);
                    i++;
                }

                return OperationResult.CreateSuccessResult(list);
            }
            catch (Exception ex)
            {
                return new OperationResult<List<S7Value>>("ExploreS7Values failed: " + ex.Message);
            }
        }

        /// <summary>CreateS7Object 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "buffer">프레임 데이터가 담긴 버퍼입니다.</param>
        /// <param name = "index">데이터를 읽거나 기록할 위치입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static S7Object CreateS7Object(S7PlusCodecOptions context, byte[] buffer, ref int index)
        {
            if (buffer[index] != 0xA1)
                throw new Exception("Not S7 object: 0x" + buffer[index].ToString("X"));
            index++;
            S7Object obj = new S7Object();
            obj.RelationId = context.ValueConverter.ReadUInt32(buffer, index);
            if (obj.RelationId == 80)
                obj.RelationId2 = 0x90010000;
            if (obj.RelationId == 81)
                obj.RelationId2 = 0x90020000;
            if (obj.RelationId == 82)
                obj.RelationId2 = 0x90030000;
            if (obj.RelationId == 83)
                obj.RelationId2 = 0x90060000;
            if (obj.RelationId == 84)
                obj.RelationId2 = 0x90050000;
            index += 4;
            obj.ClassId = S7Object.GetValueUint32(buffer, ref index);
            obj.ClassFlags = S7Object.GetValueUint32(buffer, ref index);
            obj.AttributeId = S7Object.GetValueUint32(buffer, ref index);
            if (buffer[index] == 0xA3)
            {
                // 이것은 객체의 이름 정보입니다.
                index++;
                S7Object.GetValueUint32(buffer, ref index);
                uint flag = S7Object.GetValueUint32(buffer, ref index);
                uint type = S7Object.GetValueUint32(buffer, ref index);
                if (type != 0x15)
                    throw new Exception("Name get failed, not 0x15, actual: " + type.ToString("X"));
                uint len = S7Object.GetValueUint32(buffer, ref index);
                obj.Name = Encoding.UTF8.GetString(buffer, index, (int)len);
                index += (int)len;
            }

            while (true)
            {
                byte code = buffer[index];
                if (code == 0xA1)
                {
                    if (obj.SubObjects == null)
                        obj.SubObjects = new List<S7Object>();
                    S7Object s7Object = CreateS7Object(context, buffer, ref index);
                    obj.SubObjects.Add(s7Object);
                }
                else if (code == 0xA3)
                {
                    index++;
                    OperationResult<byte[]> extra = ExtraAttributeData(context, buffer, 0x00, ref index);
                    if (!extra.IsSuccess)
                        throw new Exception("ExtraAttributeData failed: " + extra.Message);
                }
                else if (code == 0xA2)
                {
                    index++;
                    break;
                }
                else if (code == 0xAB)
                {
                    index++;
                    int len = context.ValueConverter.ReadUInt16(buffer, index);
                    index += 2;
                    obj.S7Tags = new List<S7Tag>();
                    for (int i = index + 4; i < index + len;)
                    {
                        int itemLen = 0;
                        S7Tag tag = new S7Tag();
                        tag.LID = new List<uint>()
                        {
                            BitConverter.ToUInt32(buffer, i)
                        };
                        tag.TypeCode = buffer[i + 8];
                        int offsetType = buffer[i + 9] >> 4;
                        switch (offsetType)
                        {
                            case 0:
                                itemLen = 104;
                                break;
                            case 1:
                            case 8:
                            {
                                itemLen = 4;
                                break;
                            }

                            case 2:
                            case 9:
                            {
                                itemLen = 12;
                                break;
                            }

                            case 3:
                            case 10:
                            {
                                tag.ArrayLength = BitConverter.ToInt32(buffer, i + 28);
                                itemLen = 20;
                                break;
                            }

                            case 4:
                            case 11:
                            {
                                itemLen = 68;
                                break;
                            }

                            case 5:
                            case 12:
                            {
                                tag.StructID = BitConverter.ToUInt32(buffer, i + 24);
                                itemLen = 32;
                                break;
                            }

                            case 6:
                            case 13:
                            {
                                itemLen = 48;
                                break;
                            }

                            case 7:
                            case 14:
                            {
                                itemLen = 96;
                                break;
                            }

                            default:
                                itemLen = 60;
                                break;
                        }

                        if (itemLen == 4)
                            tag.StructOffset = BitConverter.ToUInt16(buffer, i + 14);
                        else
                            tag.StructOffset = BitConverter.ToInt32(buffer, i + 16);
                        i += 12 + itemLen;
                        obj.S7Tags.Add(tag);
                    }

                    index += len;
                    index += 2;
                }
                else if (code == 0xAC)
                {
                    index++;
                    int len = context.ValueConverter.ReadUInt16(buffer, index);
                    index += 2;
                    int objIndex = 0;
                    for (int i = index; i < index + len;)
                    {
                        byte nameLen = buffer[i];
                        if (obj.S7Tags != null && objIndex < obj.S7Tags.Count)
                        {
                            obj.S7Tags[objIndex].Name = Encoding.UTF8.GetString(buffer, i + 1, nameLen);
                        }

                        i += 2 + nameLen;
                        objIndex++;
                    }

                    index += len;
                    index += 2;
                }
                else
                {
                    break;
                }
            }

            return obj;
        }
    }
}
