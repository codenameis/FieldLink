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
using static FieldLink.PlcDrivers.AllenBradley.AllenBradleyResponseParser;
using static FieldLink.PlcDrivers.AllenBradley.AllenBradleyAddressParser;
using static FieldLink.PlcDrivers.AllenBradley.AllenBradleyDefinitions;

namespace FieldLink.PlcDrivers.AllenBradley
{
    /// <summary>AllenBradley 요청 프레임을 생성합니다.</summary>
    public static class AllenBradleyCommandBuilder
    {
        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "isConnectedAddress">isConnectedAddress에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildRequestPathCommand(string address, bool isConnectedAddress = false)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                // Symbol Instance Addressing 주소라면 class=0x6b;0xf68f를 사용한다.
                int classid = AddressParameters.ExtractParameter(ref address, "class", -1);
                if (classid != -1)
                {
                    int instanceid = address.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? Convert.ToInt32(address.Substring(2), 16) : Convert.ToInt32(address);
                    if (classid < 256)
                    {
                        ms.WriteByte(0x20);
                        ms.WriteByte((byte)classid);
                    }
                    else
                    {
                        ms.WriteByte(0x21);
                        ms.WriteByte(0x00);
                        ms.WriteByte(BitConverter.GetBytes(classid)[0]);
                        ms.WriteByte(BitConverter.GetBytes(classid)[1]);
                    }

                    if (instanceid < 256)
                    {
                        ms.WriteByte(0x24);
                        ms.WriteByte((byte)instanceid);
                    }
                    else
                    {
                        ms.WriteByte(0x25);
                        ms.WriteByte(0x00);
                        ms.WriteByte(BitConverter.GetBytes(instanceid)[0]);
                        ms.WriteByte(BitConverter.GetBytes(instanceid)[1]);
                    }
                }
                else
                {
                    string[] tagNames = address.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < tagNames.Length; i++)
                    {
                        string strIndex = string.Empty;
                        int indexFirst = tagNames[i].IndexOf('[');
                        int indexSecond = tagNames[i].IndexOf(']');
                        if (indexFirst > 0 && indexSecond > 0 && indexSecond > indexFirst)
                        {
                            strIndex = tagNames[i].Substring(indexFirst + 1, indexSecond - indexFirst - 1);
                            tagNames[i] = tagNames[i].Substring(0, indexFirst);
                        }

                        ms.WriteByte(0x91); // 고정, 문자열 태그
                        byte[] nameBytes = Encoding.UTF8.GetBytes(tagNames[i]);
                        ms.WriteByte((byte)nameBytes.Length); // 노드의 길이 값
                        ms.Write(nameBytes, 0, nameBytes.Length);
                        if (nameBytes.Length % 2 == 1)
                            ms.WriteByte(0x00);
                        if (!string.IsNullOrEmpty(strIndex))
                        {
                            string[] indexs = strIndex.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                            for (int j = 0; j < indexs.Length; j++)
                            {
                                int index = Convert.ToInt32(indexs[j]);
                                if (index < 256 && !isConnectedAddress)
                                {
                                    ms.WriteByte(0x28);
                                    ms.WriteByte((byte)index);
                                }
                                else if (index < 65536)
                                {
                                    ms.WriteByte(0x29);
                                    ms.WriteByte(0x00);
                                    ms.WriteByte(BitConverter.GetBytes(index)[0]);
                                    ms.WriteByte(BitConverter.GetBytes(index)[1]);
                                }
                                else
                                {
                                    ms.WriteByte(0x2A);
                                    ms.WriteByte(0x00);
                                    ms.Write(BitConverter.GetBytes(index));
                                }
                            }
                        }
                    }
                }

                return ms.ToArray();
            }
        }

        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <param name = "startInstance">startInstance에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildEnumeratorCommand(uint startInstance)
        {
            byte[] buffer = new byte[16];
            buffer[0] = 0x55; // Get_Instance_Attribute_List 서비스 (청구)
            buffer[1] = 0x03; // 요청 경로는 3 워드 (6 바이트) 로 길다.
            buffer[2] = 0x20; // 8비트 클래스 ID
            buffer[3] = 0x6B; // 기호 객체
            buffer[4] = 0x25; // 16비트 인스턴스 ID
            buffer[5] = 0x00;
            buffer[6] = BitConverter.GetBytes(startInstance)[0];
            buffer[7] = BitConverter.GetBytes(startInstance)[1];
            buffer[8] = 0x03; // 가져오기 위한 속성 수
            buffer[9] = 0x00;
            buffer[10] = 0x01; // 속성 1  기호 이름
            buffer[11] = 0x00;
            buffer[12] = 0x02; // 속성 2  기호 타입
            buffer[13] = 0x00;
            buffer[14] = 0x08; // Attribute 3 – test
            buffer[15] = 0x00;
            return buffer;
        }

        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <param name = "startInstance">startInstance에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildEnumeratorProgrameMainCommand(uint startInstance)
        {
            byte[] buffer = new byte[38];
            buffer[0] = 0x55; // Get_Instance_Attribute_List 서비스 (청구)
            buffer[1] = 0x0e; // 요청 경로는 14 워드 (28 바이트) 로 길다.
            buffer[2] = 0x91; // Logical Segments: Class 0x6B, Instance 시작 주소 0 x0000 ((돌아온 인스턴스 ID는 여기의 이동량입니다)
            buffer[3] = 0x13;
            Encoding.ASCII.GetBytes("Program:MainProgram").CopyTo(buffer, 4);
            buffer[23] = 0x00;
            buffer[24] = 0x20; // 8비트 클래스 ID
            buffer[25] = 0x6B; // 기호 객체
            buffer[26] = 0x25; // 16비트 인스턴스 ID
            buffer[27] = 0x00;
            buffer[28] = BitConverter.GetBytes(startInstance)[0];
            buffer[29] = BitConverter.GetBytes(startInstance)[1];
            buffer[30] = 0x03; // 가져오기 위한 속성 수
            buffer[31] = 0x00;
            buffer[32] = 0x01; // 속성 1  기호 이름
            buffer[33] = 0x00;
            buffer[34] = 0x02; // 속성 2  기호 타입
            buffer[35] = 0x00;
            buffer[36] = 0x08; // Attribute 3 – test
            buffer[37] = 0x00;
            return buffer;
        }

        /// <summary>GetStructHandleCommand 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "symbolType">symbolType에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] GetStructHandleCommand(ushort symbolType)
        {
            byte[] buffer = new byte[18];
            symbolType = (ushort)(symbolType & 0x0fff);
            buffer[0] = 0x03; // Get Attributes, List Service (청구)
            buffer[1] = 0x03; // 요청 경로는 3 워드 (6 바이트) 로 길다.
            buffer[2] = 0x20; // 8비트 클래스 ID
            buffer[3] = 0x6c; // 템플릿 클래스 (0x6C)
            buffer[4] = 0x25; // 16비트 인스턴스 ID
            buffer[5] = 0x00;
            buffer[6] = BitConverter.GetBytes(symbolType)[0]; // 데이터 타입을 인스턴스 ID로 사용한다
            buffer[7] = BitConverter.GetBytes(symbolType)[1];
            buffer[8] = 0x04; // 속성 카운트
            buffer[9] = 0x00;
            buffer[10] = 0x04; // 속성 목록: 속성 4
            buffer[11] = 0x00;
            buffer[12] = 0x05; // 속성 목록: 속성 5
            buffer[13] = 0x00;
            buffer[14] = 0x02; // 속성 목록: 속성 2
            buffer[15] = 0x00;
            buffer[16] = 0x01; // 속성 목록: 속성 1
            buffer[17] = 0x00;
            return buffer;
        }

        /// <summary>GetStructItemNameType 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "symbolType">symbolType에 사용할 입력값입니다.</param>
        /// <param name = "structHandle">structHandle에 사용할 입력값입니다.</param>
        /// <param name = "offset">offset에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] GetStructItemNameType(ushort symbolType, AbStructHandle structHandle, int offset)
        {
            byte[] buffer = new byte[14];
            symbolType = (ushort)(symbolType & 0x0fff);
            byte[] read_len_buff = BitConverter.GetBytes(structHandle.TemplateObjectDefinitionSize * 4 - 21); // 길이를 가져오기
            buffer[0] = CIP_READ_DATA; // 읽기 서비스 (청구)
            buffer[1] = 0x03; // 요청 경로는 3 워드 (6 바이트) 로 길다.
            buffer[2] = 0x20; // 8비트 클래스 ID
            buffer[3] = 0x6c; // 템플릿 클래스 (0x6C)
            buffer[4] = 0x25; // 16비트 인스턴스 ID
            buffer[5] = 0x00;
            buffer[6] = BitConverter.GetBytes(symbolType)[0]; // 인스턴스 id
            buffer[7] = BitConverter.GetBytes(symbolType)[1];
            buffer[8] = BitConverter.GetBytes(offset)[0]; //이동량
            buffer[9] = BitConverter.GetBytes(offset)[1];
            buffer[10] = BitConverter.GetBytes(offset)[2];
            buffer[11] = BitConverter.GetBytes(offset)[3];
            buffer[12] = read_len_buff[0]; //바이트 길이를 읽어
            buffer[13] = read_len_buff[1];
            return buffer;
        }

        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "command">포장할 명령 바이트입니다.</param>
        /// <param name = "session">session에 사용할 입력값입니다.</param>
        /// <param name = "commandSpecificData">commandSpecificData에 사용할 입력값입니다.</param>
        /// <param name = "senderContext">senderContext에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackRequestHeader(ushort command, uint session, byte[] commandSpecificData, byte[] senderContext = null)
        {
            if (commandSpecificData == null)
                commandSpecificData = new byte[0];
            byte[] buffer = new byte[commandSpecificData.Length + 24];
            Array.Copy(commandSpecificData, 0, buffer, 24, commandSpecificData.Length);
            BitConverter.GetBytes(command).CopyTo(buffer, 0);
            BitConverter.GetBytes(session).CopyTo(buffer, 4);
            if (senderContext != null)
                senderContext.CopyTo(buffer, 12);
            BitConverter.GetBytes((ushort)commandSpecificData.Length).CopyTo(buffer, 2);
            return buffer;
        }

        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "command">포장할 명령 바이트입니다.</param>
        /// <param name = "error">error에 사용할 입력값입니다.</param>
        /// <param name = "session">session에 사용할 입력값입니다.</param>
        /// <param name = "commandSpecificData">commandSpecificData에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackRequestHeader(ushort command, uint error, uint session, byte[] commandSpecificData)
        {
            byte[] buffer = PackRequestHeader(command, session, commandSpecificData);
            BitConverter.GetBytes(error).CopyTo(buffer, 8);
            return buffer;
        }

        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "pccc">pccc에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackExecutePCCC(byte[] pccc)
        {
            MemoryStream ms = new MemoryStream();
            ms.WriteByte(CIP_Execute_PCCC);
            ms.WriteByte(0x02);
            ms.WriteByte(0x20); //
            ms.WriteByte(0x67);
            ms.WriteByte(0x24); // 신청자 ID
            ms.WriteByte(0x01);
            ms.WriteByte(0x07); // CIPCC 대상
            ms.WriteByte(0x09);
            ms.WriteByte(0x10);
            ms.WriteByte(0x0B); // CIP 일련 번호
            ms.WriteByte(0x46);
            ms.WriteByte(0xA5);
            ms.WriteByte(0xC1);
            ms.Write(pccc);
            byte[] buffer = ms.ToArray();
            BitConverter.GetBytes(AllenBradleyDefinitions.OriginatorVendorID).CopyTo(buffer, 7);
            BitConverter.GetBytes(AllenBradleyDefinitions.OriginatorSerialNumber).CopyTo(buffer, 9);
            return buffer;
        }

        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "tns">tns에 사용할 입력값입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> PackExecutePCCCRead(int tns, string address, ushort length)
        {
            OperationResult<byte[]> command = AllenBradleyDF1SerialCommandBuilder.BuildProtectedTypedLogicalReadWithThreeAddressFields(tns, address, length);
            if (!command.IsSuccess)
                return command;
            return OperationResult.CreateSuccessResult(PackExecutePCCC(command.Content));
        }

        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "tns">tns에 사용할 입력값입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> PackExecutePCCCWrite(int tns, string address, byte[] value)
        {
            OperationResult<byte[]> command = AllenBradleyDF1SerialCommandBuilder.BuildProtectedTypedLogicalWriteWithThreeAddressFields(tns, address, value);
            if (!command.IsSuccess)
                return command;
            return OperationResult.CreateSuccessResult(PackExecutePCCC(command.Content));
        }

        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "tns">tns에 사용할 입력값입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "bitIndex">bitIndex에 사용할 입력값입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> PackExecutePCCCWrite(int tns, string address, int bitIndex, bool value)
        {
            OperationResult<byte[]> command = AllenBradleyDF1SerialCommandBuilder.BuildProtectedTypedLogicalMaskWithThreeAddressFields(tns, address, bitIndex, value);
            if (!command.IsSuccess)
                return command;
            return OperationResult.CreateSuccessResult(PackExecutePCCC(command.Content));
        }

        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <param name = "isConnectedAddress">isConnectedAddress에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackRequsetRead(string address, int length, bool isConnectedAddress = false)
        {
            byte[] buffer = new byte[1024];
            int offset = 0;
            buffer[offset++] = CIP_READ_DATA;
            offset++;
            byte[] requestPath = BuildRequestPathCommand(address, isConnectedAddress);
            requestPath.CopyTo(buffer, offset);
            offset += requestPath.Length;
            buffer[1] = (byte)((offset - 2) / 2);
            buffer[offset++] = BitConverter.GetBytes(length)[0];
            buffer[offset++] = BitConverter.GetBytes(length)[1];
            byte[] data = new byte[offset];
            Array.Copy(buffer, 0, data, 0, offset);
            return data;
        }

        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "startIndex">startIndex에 사용할 입력값입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackRequestReadSegment(string address, int startIndex, int length)
        {
            byte[] buffer = new byte[1024];
            int offset = 0;
            buffer[offset++] = CIP_READ_FRAGMENT;
            offset++;
            byte[] requestPath = BuildRequestPathCommand(address);
            requestPath.CopyTo(buffer, offset);
            offset += requestPath.Length;
            buffer[1] = (byte)((offset - 2) / 2);
            buffer[offset++] = BitConverter.GetBytes(length)[0];
            buffer[offset++] = BitConverter.GetBytes(length)[1];
            buffer[offset++] = BitConverter.GetBytes(startIndex)[0];
            buffer[offset++] = BitConverter.GetBytes(startIndex)[1];
            buffer[offset++] = BitConverter.GetBytes(startIndex)[2];
            buffer[offset++] = BitConverter.GetBytes(startIndex)[3];
            byte[] data = new byte[offset];
            Array.Copy(buffer, 0, data, 0, offset);
            return data;
        }

        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "typeCode">typeCode에 사용할 입력값입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <param name = "isConnectedAddress">isConnectedAddress에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackRequestWrite(string address, ushort typeCode, byte[] value, int length = 1, bool isConnectedAddress = false)
        {
            byte[] buffer = new byte[1024];
            int offset = 0;
            buffer[offset++] = CIP_WRITE_DATA;
            offset++;
            byte[] requestPath = BuildRequestPathCommand(address, isConnectedAddress);
            requestPath.CopyTo(buffer, offset);
            offset += requestPath.Length;
            buffer[1] = (byte)((offset - 2) / 2);
            buffer[offset++] = BitConverter.GetBytes(typeCode)[0]; // 데이터 타입
            buffer[offset++] = BitConverter.GetBytes(typeCode)[1];
            buffer[offset++] = BitConverter.GetBytes(length)[0]; // 고정
            buffer[offset++] = BitConverter.GetBytes(length)[1];
            if (value == null)
                value = new byte[0];
            byte[] data = new byte[value.Length + offset];
            Array.Copy(buffer, 0, data, 0, offset);
            value.CopyTo(data, offset); // 수치
            return data;
        }

        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "typeCode">typeCode에 사용할 입력값입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "startIndex">startIndex에 사용할 입력값입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <param name = "isConnectedAddress">isConnectedAddress에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackRequestWriteSegment(string address, ushort typeCode, byte[] value, int startIndex, int length = 1, bool isConnectedAddress = false)
        {
            byte[] buffer = new byte[1024];
            int offset = 0;
            buffer[offset++] = CIP_WRITE_FRAGMENT;
            offset++;
            byte[] requestPath = BuildRequestPathCommand(address, isConnectedAddress);
            requestPath.CopyTo(buffer, offset);
            offset += requestPath.Length;
            buffer[1] = (byte)((offset - 2) / 2);
            buffer[offset++] = BitConverter.GetBytes(typeCode)[0]; // 데이터 타입
            buffer[offset++] = BitConverter.GetBytes(typeCode)[1];
            buffer[offset++] = BitConverter.GetBytes(length)[0]; // 고정
            buffer[offset++] = BitConverter.GetBytes(length)[1];
            buffer[offset++] = BitConverter.GetBytes(startIndex)[0];
            buffer[offset++] = BitConverter.GetBytes(startIndex)[1];
            buffer[offset++] = BitConverter.GetBytes(startIndex)[2];
            buffer[offset++] = BitConverter.GetBytes(startIndex)[3];
            if (value == null)
                value = new byte[0];
            byte[] data = new byte[value.Length + offset];
            Array.Copy(buffer, 0, data, 0, offset);
            value.CopyTo(data, offset); // 수치
            return data;
        }

        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "orMask">orMask에 사용할 입력값입니다.</param>
        /// <param name = "andMask">andMask에 사용할 입력값입니다.</param>
        /// <param name = "isConnectedAddress">isConnectedAddress에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackRequestReadModifyWrite(string address, uint orMask, uint andMask, bool isConnectedAddress = false)
        {
            byte[] buffer = new byte[1024];
            int offset = 0;
            buffer[offset++] = CIP_READ_WRITE_DATA;
            offset++;
            byte[] requestPath = BuildRequestPathCommand(address, isConnectedAddress);
            requestPath.CopyTo(buffer, offset);
            offset += requestPath.Length;
            buffer[1] = (byte)((offset - 2) / 2);
            buffer[offset++] = 0x04; // 마스크의 크기는 4개입니다.
            buffer[offset++] = 0x00;
            BitConverter.GetBytes(orMask).CopyTo(buffer, offset);
            offset += 4;
            BitConverter.GetBytes(andMask).CopyTo(buffer, offset);
            offset += 4;
            return buffer.SelectBegin(offset);
        }

        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "index">데이터를 읽거나 기록할 위치입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "isConnectedAddress">isConnectedAddress에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackRequestReadModifyWrite(string address, int index, bool value, bool isConnectedAddress = false)
        {
            address = address + $"[{(index / 32)}]";
            index = index % 32;
            if (value)
            {
                uint orMask = 0x01;
                orMask = orMask << index;
                return PackRequestReadModifyWrite(address, orMask, 0xffffffff, isConnectedAddress);
            }
            else
            {
                uint andMask = 0x01;
                andMask = andMask << index;
                andMask = ~andMask;
                return PackRequestReadModifyWrite(address, 0x00, andMask, isConnectedAddress);
            }
        }

        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackRequestWrite(string address, bool value)
        {
            address = AnalysisArrayIndex(address, out int bitIndex);
            return PackRequestReadModifyWrite(address, bitIndex, value, isConnectedAddress: false);
        }

        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "portSlot">portSlot에 사용할 입력값입니다.</param>
        /// <param name = "cips">cips에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackCommandService(byte[] portSlot, params byte[][] cips)
        {
            MemoryStream ms = new MemoryStream();
            // 타입 id 0xB2: 연결되지 않은 데이터 항목 0xB1: 연결된 데이터 항목 0xA1: 연결 주소 항목
            ms.WriteByte(0xB2);
            ms.WriteByte(0x00);
            ms.WriteByte(0x00); // 후속 데이터의 길이는
            ms.WriteByte(0x00);
            ms.WriteByte(0x52); // 서비스
            ms.WriteByte(0x02); // 경로 크기를 요청
            ms.WriteByte(0x20); // 경로를 요청
            ms.WriteByte(0x06);
            ms.WriteByte(0x24);
            ms.WriteByte(0x01);
            ms.WriteByte(0x0A); // 제한 시간
            ms.WriteByte(0xF0);
            ms.WriteByte(0x00); // CIP 명령 길이는
            ms.WriteByte(0x00);
            int count = 0;
            if (cips.Length == 1)
            {
                ms.Write(cips[0], 0, cips[0].Length);
                count += cips[0].Length;
                if (cips[0].Length % 2 == 1)
                    ms.WriteByte(0x00);
            }
            else
            {
                ms.WriteByte(0x0A); // 고정
                ms.WriteByte(0x02);
                ms.WriteByte(0x20);
                ms.WriteByte(0x02);
                ms.WriteByte(0x24);
                ms.WriteByte(0x01);
                count += 8;
                ms.Write(BitConverter.GetBytes((ushort)cips.Length), 0, 2); // 기입된 항목
                ushort offect = (ushort)(0x02 + 2 * cips.Length);
                count += 2 * cips.Length;
                for (int i = 0; i < cips.Length; i++)
                {
                    ms.Write(BitConverter.GetBytes(offect), 0, 2);
                    offect = (ushort)(offect + cips[i].Length);
                }

                for (int i = 0; i < cips.Length; i++)
                {
                    ms.Write(cips[i], 0, cips[i].Length);
                    count += cips[i].Length;
                }
            }

            if (portSlot != null)
            {
                ms.WriteByte((byte)((portSlot.Length + 1) / 2)); // 경로 크기
                ms.WriteByte(0x00);
                ms.Write(portSlot, 0, portSlot.Length);
                if (portSlot.Length % 2 == 1)
                    ms.WriteByte(0x00);
            }

            byte[] data = ms.ToArray();
            BitConverter.GetBytes((short)count).CopyTo(data, 12);
            BitConverter.GetBytes((short)(data.Length - 4)).CopyTo(data, 2);
            return data;
        }

        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "portSlot">portSlot에 사용할 입력값입니다.</param>
        /// <param name = "cips">cips에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackCleanCommandService(byte[] portSlot, params byte[][] cips)
        {
            MemoryStream ms = new MemoryStream();
            // 타입 id 0xB2: 연결되지 않은 데이터 항목 0xB1: 연결된 데이터 항목 0xA1: 연결 주소 항목
            ms.WriteByte(0xB2);
            ms.WriteByte(0x00);
            ms.WriteByte(0x00); // 후속 데이터의 길이는
            ms.WriteByte(0x00);
            if (cips.Length == 1)
            {
                ms.Write(cips[0], 0, cips[0].Length);
            }
            else
            {
                ms.WriteByte(0x0A); // 고정
                ms.WriteByte(0x02);
                ms.WriteByte(0x20);
                ms.WriteByte(0x02);
                ms.WriteByte(0x24);
                ms.WriteByte(0x01);
                ms.Write(BitConverter.GetBytes((ushort)cips.Length), 0, 2); // 기입된 항목
                ushort offect = (ushort)(0x02 + 2 * cips.Length);
                for (int i = 0; i < cips.Length; i++)
                {
                    ms.Write(BitConverter.GetBytes(offect), 0, 2);
                    offect = (ushort)(offect + cips[i].Length);
                }

                for (int i = 0; i < cips.Length; i++)
                {
                    ms.Write(cips[i], 0, cips[i].Length);
                }
            }

            ms.WriteByte((byte)((portSlot.Length + 1) / 2)); // 경로 크기
            ms.WriteByte(0x00);
            ms.Write(portSlot, 0, portSlot.Length);
            if (portSlot.Length % 2 == 1)
                ms.WriteByte(0x00);
            byte[] data = ms.ToArray();
            BitConverter.GetBytes((short)(data.Length - 4)).CopyTo(data, 2);
            return data;
        }

        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "portSlot">portSlot에 사용할 입력값입니다.</param>
        /// <param name = "sessionHandle">sessionHandle에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackCommandGetAttributesAll(byte[] portSlot, uint sessionHandle)
        {
            byte[] commandSpecificData = AllenBradleyCommandBuilder.PackCommandSpecificData(new byte[4], AllenBradleyCommandBuilder.PackCommandService(portSlot, new byte[] { 0x01, 0x02, 0x20, 0x01, 0x24, 0x01 }));
            return AllenBradleyCommandBuilder.PackRequestHeader(0x6F, sessionHandle, commandSpecificData);
        }

        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "isRead">isRead에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackCommandResponse(byte[] data, bool isRead)
        {
            if (data == null)
            {
                return new byte[]
                {
                    0x00,
                    0x00,
                    0x04,
                    0x00,
                    0x00,
                    0x00
                };
            }
            else
            {
                return ProtocolBytes.SpliceArray(new byte[] { (byte)(isRead ? 0xCC : 0xCD), 0x00, 0x00, 0x00, 0x00, 0x00 }, data);
            }
        }

        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "service">service에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackCommandSpecificData(params byte[][] service)
        {
            MemoryStream ms = new MemoryStream();
            ms.WriteByte(0x00);
            ms.WriteByte(0x00);
            ms.WriteByte(0x00);
            ms.WriteByte(0x00);
            ms.WriteByte(0x0A); // 시간 초과
            ms.WriteByte(0x00);
            ms.WriteByte(BitConverter.GetBytes(service.Length)[0]); // 항목 수
            ms.WriteByte(BitConverter.GetBytes(service.Length)[1]);
            for (int i = 0; i < service.Length; i++)
            {
                ms.Write(service[i], 0, service[i].Length);
            }

            return ms.ToArray();
        }

        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "command">포장할 명령 바이트입니다.</param>
        /// <param name = "code">code에 사용할 입력값입니다.</param>
        /// <param name = "isConnected">isConnected에 사용할 입력값입니다.</param>
        /// <param name = "sequence">sequence에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackCommandSingleService(byte[] command, ushort code = 0xB2, bool isConnected = false, ushort sequence = 0)
        {
            if (command == null)
                command = new byte[0];
            byte[] buffer = isConnected ? new byte[6 + command.Length] : new byte[4 + command.Length];
            buffer[0] = BitConverter.GetBytes(code)[0];
            buffer[1] = BitConverter.GetBytes(code)[1];
            buffer[2] = BitConverter.GetBytes(buffer.Length - 4)[0];
            buffer[3] = BitConverter.GetBytes(buffer.Length - 4)[1];
            command.CopyTo(buffer, isConnected ? 6 : 4);
            if (isConnected)
                BitConverter.GetBytes(sequence).CopyTo(buffer, 4);
            return buffer;
        }

        /// <summary>RegisterSessionHandle 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "senderContext">senderContext에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] RegisterSessionHandle(byte[] senderContext = null)
        {
            byte[] commandSpecificData = new byte[]
            {
                0x01,
                0x00,
                0x00,
                0x00,
            };
            return AllenBradleyCommandBuilder.PackRequestHeader(0x65, 0, commandSpecificData, senderContext);
        }

        /// <summary>UnRegisterSessionHandle 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "sessionHandle">sessionHandle에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] UnRegisterSessionHandle(uint sessionHandle)
        {
            return AllenBradleyCommandBuilder.PackRequestHeader(0x66, sessionHandle, new byte[0]);
        }
    }
}
