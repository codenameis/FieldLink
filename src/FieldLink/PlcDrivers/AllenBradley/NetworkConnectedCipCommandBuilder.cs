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
using static FieldLink.PlcDrivers.AllenBradley.ConnectedCipResponseParser;
using static FieldLink.PlcDrivers.AllenBradley.NetworkConnectedCipValueConverter;

namespace FieldLink.PlcDrivers.AllenBradley
{
    /// <summary>NetworkConnectedCip 요청 프레임을 생성합니다.</summary>
    public static class NetworkConnectedCipCommandBuilder
    {
        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "command">포장할 명령 바이트입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackCommandWithHeader(ConnectedCipOptions context, byte[] command)
        {
            return AllenBradleyCommandBuilder.PackRequestHeader(0x70, context.SessionHandle, AllenBradleyCommandBuilder.PackCommandSpecificData(GetOTConnectionIdService(context), command));
        }

        /// <summary>여러 CIP 명령들을 하나의 서비스 명령으로 패키징하는 방법</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "cip">CIP 명령 목록</param>
        /// <returns>서비스 명령</returns>
        public static byte[] PackCommandService(ConnectedCipOptions context, params byte[][] cip)
        {
            MemoryStream ms = new MemoryStream();
            // 타입 id 0xB2: 연결되지 않은 데이터 항목 0xB1: 연결된 데이터 항목 0xA1: 연결 주소 항목
            ms.WriteByte(0xB1);
            ms.WriteByte(0x00);
            ms.WriteByte(0x00); // 후속 데이터의 길이는
            ms.WriteByte(0x00);
            long messageId = context.SequenceNumber;
            ms.WriteByte(BitConverter.GetBytes(messageId)[0]); // CIP Sequence Count CIP 시퀀스 카운트
            ms.WriteByte(BitConverter.GetBytes(messageId)[1]);
            if (cip.Length == 1)
            {
                ms.Write(cip[0], 0, cip[0].Length);
            }
            else
            {
                ms.Write(new byte[] { 0x0A, 0x02, 0x20, 0x02, 0x24, 0x01 }, 0, 6);
                ms.WriteByte(BitConverter.GetBytes(cip.Length)[0]);
                ms.WriteByte(BitConverter.GetBytes(cip.Length)[1]);
                int offset = 2 + cip.Length * 2;
                for (int i = 0; i < cip.Length; i++)
                {
                    ms.WriteByte(BitConverter.GetBytes(offset)[0]); // 각 데이터의 길이는
                    ms.WriteByte(BitConverter.GetBytes(offset)[1]);
                    offset += cip[i].Length;
                }

                for (int i = 0; i < cip.Length; i++)
                {
                    ms.Write(cip[i], 0, cip[i].Length); // Omron CIP의 구체적인 내용
                }
            }

            byte[] data = ms.ToArray();
            ms.Dispose();
            BitConverter.GetBytes((ushort)(data.Length - 4)).CopyTo(data, 2);
            return data;
        }
    }
}
