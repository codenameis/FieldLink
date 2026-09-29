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
using static FieldLink.PlcDrivers.Siemens.SiemensS7PlusValueConverter;
using static FieldLink.PlcDrivers.Siemens.SiemensS7PlusResponseParser;

namespace FieldLink.PlcDrivers.Siemens
{
    /// <summary>SiemensS7Plus 요청 프레임을 생성합니다.</summary>
    public static class SiemensS7PlusCommandBuilder
    {
        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <param name = "pdu">pdu에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildWithTPKTAndISO(byte[] pdu)
        {
            // TPKT + ISO COTP 헤더 (연결 지향 전송 프로토콜)
            byte[] buffer = ProtocolBytes.SpliceArray(new byte[] { 0x03, 0x00, 0x00, 0x1f, 0x02, 0xf0, 0x80 }, pdu);
            buffer[2] = BitConverter.GetBytes(buffer.Length)[1];
            buffer[3] = BitConverter.GetBytes(buffer.Length)[0];
            return buffer;
        }

        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "buffer">프레임 데이터가 담긴 버퍼입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackSendBuffer(S7PlusCodecOptions context, byte[] buffer)
        {
            context.ValueConverter.GetBytes(context.SessionID).CopyTo(buffer, 9);
            MemoryStream ms = new MemoryStream();
            ms.Write(buffer);
            ms.Write("00 00 04 E8 89 69 00 12 00 00 00 00 89 6A 00 13 00 89 6B 00 04 00 00".ToHexBytes());
            uint integrityId = (uint)context.IntegrityId;
            S7Object.WriteUint32(ms, integrityId);
            ms.Write("00 00 00 00".ToHexBytes());
            buffer = ms.ToArray();
            int seq = (int)context.SequenceNumber;
            buffer[7] = BitConverter.GetBytes(seq)[1];
            buffer[8] = BitConverter.GetBytes(seq)[0];
            return buffer;
        }

        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "pduData">pduData에 사용할 입력값입니다.</param>
        /// <param name = "bytesToSend">bytesToSend에 사용할 입력값입니다.</param>
        /// <param name = "protoVersion">protoVersion에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static List<byte[]> BuildS7PlusPdu(S7PlusCodecOptions context, byte[] pduData, int bytesToSend, byte protoVersion)
        {
            List<byte[]> array = new List<byte[]>();
            int curSize;
            int sourcePos = 0;
            int sendLen;
            int NegotiatedIsoPduSize = 1024;
            byte[] packet = new byte[NegotiatedIsoPduSize];
            int MaxSize = NegotiatedIsoPduSize - 4 - 3 - 5 - 17 - 4 - 4;
            while (bytesToSend > 0)
            {
                if (bytesToSend > MaxSize)
                {
                    curSize = MaxSize;
                    bytesToSend -= MaxSize;
                }
                else
                {
                    curSize = bytesToSend;
                    bytesToSend -= curSize;
                }

                // 메시지 헤더
                packet[0] = 0x72;
                packet[1] = protoVersion;
                packet[2] = BitConverter.GetBytes(curSize)[1];
                packet[3] = BitConverter.GetBytes(curSize)[0];
                // 데이터 부분
                Array.Copy(pduData, sourcePos, packet, 4, curSize);
                sourcePos += curSize;
                sendLen = 4 + curSize;
                // 마지막 패킷에 00이 붙습니다.
                if (bytesToSend == 0)
                {
                    packet[sendLen] = 0x72;
                    sendLen++;
                    packet[sendLen] = protoVersion;
                    sendLen++;
                    packet[sendLen] = 0;
                    sendLen++;
                    packet[sendLen] = 0;
                    sendLen++;
                }

                Array.Resize(ref packet, sendLen);
                array.Add(packet);
            }

            return array;
        }

        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "pduData">pduData에 사용할 입력값입니다.</param>
        /// <param name = "protoVersion">protoVersion에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static List<byte[]> BuildS7PlusPdu(S7PlusCodecOptions context, byte[] pduData, byte protoVersion) => BuildS7PlusPdu(context, pduData, pduData.Length, protoVersion);
        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildInitSslCommand(S7PlusCodecOptions context)
        {
            byte[] buffer = new byte[18];
            buffer[0] = 0x31; // Opcode: Request
            buffer[3] = 0x05; // Functioncode: InitSs
            buffer[4] = 0xb3;
            int seq = (int)context.SequenceNumber;
            buffer[7] = BitConverter.GetBytes(seq)[1];
            buffer[8] = BitConverter.GetBytes(seq)[0];
            buffer[13] = 0x30; // TransportFlags
            return BuildWithTPKTAndISO(BuildS7PlusPdu(context, buffer, buffer.Length, protoVersion: 0x01)[0]);
        }

        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildCreateObjectRequest(S7PlusCodecOptions context)
        {
            byte[] buffer = ("31 00 00 04 CA 00 00 00 02 00 00 01 20 36 00 00 01 1D 00 04 00 00 00 00 00 A1 00 00 00 D3 " + "82 1F 00 00 A3 82 2C 00 12 80 C3 C9 01 A1 00 00 00 D3 81 7F 00 00 A2 A2 00 00 00 00").ToHexBytes();
            int seq = (int)context.SequenceNumber;
            buffer[7] = BitConverter.GetBytes(seq)[1];
            buffer[8] = BitConverter.GetBytes(seq)[0];
            return BuildS7PlusPdu(context, buffer, buffer.Length, protoVersion: 0x01)[0];
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "structValue">structValue에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildMultiSetRequest(S7PlusCodecOptions context, byte[] structValue)
        {
            MemoryStream ms = new MemoryStream();
            byte[] buffer = "31 00 00 05 42 00 00 00 03 70 40 00 04 34 70 40 00 04 01 01 82 32 01".ToHexBytes();
            context.ValueConverter.GetBytes(context.SessionID).CopyTo(buffer, 9);
            context.ValueConverter.GetBytes(context.SessionID).CopyTo(buffer, 14);
            ms.Write(buffer);
            ms.Write(structValue);
            ms.Write("00 00 00 04 E8 89 69 00 12 00 00 00 00 89 6A 00 13 00 89 6B 00 04 00 ".ToHexBytes());
            uint integrityId = 0x00;
            S7Object.WriteUint32(ms, integrityId);
            ms.Write("00 00 00 00".ToHexBytes());
            buffer = ms.ToArray();
            int seq = (int)context.SequenceNumber;
            buffer[7] = BitConverter.GetBytes(seq)[1];
            buffer[8] = BitConverter.GetBytes(seq)[0];
            return BuildS7PlusPdu(context, buffer, protoVersion: 0x02)[0];
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "s7Object">s7Object에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildMultiGetRequest(S7PlusCodecOptions context, IS7Object[] s7Object)
        {
            MemoryStream ms = new MemoryStream();
            //ValueConverter.GetBytes( SessionID ).CopyTo( buffer, 9 );
            ms.Write("31 00 00 05 4C 00 00 00 03 70 40 00 04 34 00 00 00 00".ToHexBytes());
            S7Object.WriteUint32(ms, (uint)s7Object.Length); // 배열 길이
            S7Object.WriteUint32(ms, (uint)s7Object.Sum(m => m.GetNumberOfFields())); // fieldCount
            for (int i = 0; i < s7Object.Length; i++)
            {
                s7Object[i].WriteMessgae(ms);
            }

            return ms.ToArray();
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "s7Object">s7Object에 사용할 입력값입니다.</param>
        /// <param name = "buffers">buffers에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildMultiSetRequest(S7PlusCodecOptions context, IS7Object[] s7Object, List<byte[]> buffers)
        {
            MemoryStream ms = new MemoryStream();
            //ValueConverter.GetBytes( SessionID ).CopyTo( buffer, 9 );
            ms.Write("31 00 00 05 42 00 00 00 03 70 40 00 04 34 00 00 00 00".ToHexBytes());
            S7Object.WriteUint32(ms, (uint)s7Object.Length); // 배열 길이
            S7Object.WriteUint32(ms, (uint)s7Object.Sum(m => m.GetNumberOfFields())); // fieldCount
            for (int i = 0; i < s7Object.Length; i++)
            {
                s7Object[i].WriteMessgae(ms);
            }

            // 쓸 값
            for (int i = 0; i < buffers.Count; i++)
            {
                S7Object.WriteUint32(ms, (uint)(i + 1));
                ms.Write(buffers[i]);
            }

            S7Object.WriteUint32(ms, (uint)0);
            return ms.ToArray();
        }

        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "exploreId">exploreId에 사용할 입력값입니다.</param>
        /// <param name = "attributes">attributes에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static List<byte[]> BuildBrowseRequest(S7PlusCodecOptions context, uint exploreId, uint[] attributes)
        {
            // 31 00 00 04 BB 00 00 00 07 70 40 00 04 34 00 00 02 19 00 01 01 00 00 00 04 00 00 00 00 00
            MemoryStream ms = new MemoryStream();
            ms.Write(("31 00 00 04 BB 00 00 00 07 70 40 00 04 34 00 00 02 19 00 01 01 00 00").ToHexBytes());
            S7Object.WriteUint32(ms, (uint)attributes.Length);
            for (int i = 0; i < attributes.Length; i++)
            {
                S7Object.WriteUint32(ms, attributes[i]);
            }

            uint integrityId = (uint)context.IntegrityId;
            S7Object.WriteUint32(ms, integrityId);
            ms.Write("00 00 00 00 00".ToHexBytes());
            byte[] buffer = ms.ToArray();
            int seq = (int)context.SequenceNumber;
            buffer[7] = BitConverter.GetBytes(seq)[1];
            buffer[8] = BitConverter.GetBytes(seq)[0];
            buffer[9] = BitConverter.GetBytes(context.SessionID)[3];
            buffer[10] = BitConverter.GetBytes(context.SessionID)[2];
            buffer[11] = BitConverter.GetBytes(context.SessionID)[1];
            buffer[12] = BitConverter.GetBytes(context.SessionID)[0];
            buffer[14] = BitConverter.GetBytes(exploreId)[3];
            buffer[15] = BitConverter.GetBytes(exploreId)[2];
            buffer[16] = BitConverter.GetBytes(exploreId)[1];
            buffer[17] = BitConverter.GetBytes(exploreId)[0];
            return BuildS7PlusPdu(context, buffer, buffer.Length, protoVersion: 0x02);
        }
    }
}
