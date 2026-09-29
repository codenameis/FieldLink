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

namespace FieldLink.PlcDrivers.Knx
{
    /// <summary>Knx 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class KnxResponseParser
    {
        /// <summary>수신 서비스와 cEMI 데이터를 해석하고 필요한 응답 프레임을 반환합니다.</summary>
        /// <param name = "channel">channel에 사용할 입력값입니다.</param>
        /// <param name = "isConnected">isConnected에 사용할 입력값입니다.</param>
        /// <param name = "frame">frame에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <remarks>그룹 데이터의 배열 길이와 비트 표현은 원본의 Read_CEMI_29 규칙을 유지합니다.</remarks>
        public static KnxDecodedMessage Parse(byte channel, bool isConnected, byte[] frame)
        {
            var result = new KnxDecodedMessage
            {
                Channel = channel,
                IsConnected = isConnected
            };
            if (frame[2] == 2)
            {
                if (frame[3] == 6)
                {
                    result.Channel = frame[6];
                    if (frame[5] == 8 & frame[7] == 0x25)
                        result.IsConnected = false;
                    if (result.Channel > 0)
                        result.IsConnected = true;
                }
                else if (frame[3] == 7)
                    result.Reply = KnxCommandBuilder.BuildConnectionStateResponse(channel);
            }
            else if (frame[2] == 4 && frame[3] == 0x20 && frame.Length > 11)
            {
                if (frame[10] == 0x2e)
                    result.Reply = KnxCommandBuilder.BuildTunnelAcknowledgement(channel, frame);
                else if (frame[10] == 0x29)
                {
                    result.Address = BitConverter.ToInt16(new byte[] { frame[17], frame[16] }, 0);
                    result.DataLength = frame[18];
                    if (frame[18] > 1)
                    {
                        // NPDU 길이에는 제어 바이트 1개가 포함된다.
                        result.Data = new byte[frame[18] - 1];
                        for (int i = 0; i < frame[18] - 1; i++)
                            result.Data[i] = frame[21 + i];
                    }
                    else
                        result.Data = BitConverter.GetBytes(frame[20] & 0x3f);
                    result.Reply = KnxCommandBuilder.BuildTunnelAcknowledgement(channel, frame);
                }
            }

            return result;
        }
    }
}
