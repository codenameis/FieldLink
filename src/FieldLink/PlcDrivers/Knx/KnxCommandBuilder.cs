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
    /// <summary>Knx 요청 프레임을 생성합니다.</summary>
    public static class KnxCommandBuilder
    {
        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "channel">channel에 사용할 입력값입니다.</param>
        /// <param name = "sequenceNumber">sequenceNumber에 사용할 입력값입니다.</param>
        /// <param name = "addr">addr에 사용할 입력값입니다.</param>
        /// <param name = "len">len에 사용할 입력값입니다.</param>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildWrite(byte channel, byte sequenceNumber, short addr, byte len, byte[] data)
        {
            Byte[] addr_base = BitConverter.GetBytes(addr);
            byte[] out_buff = new byte[(20 + len)];
            byte[] out_buff_len = BitConverter.GetBytes(out_buff.Length);
            out_buff[0] = 0x06;
            out_buff[1] = 0x10;
            out_buff[2] = 0x04;
            out_buff[3] = 0x20;
            out_buff[4] = out_buff_len[1];
            out_buff[5] = out_buff_len[0];
            out_buff[6] = 0x04;
            out_buff[7] = channel;
            out_buff[8] = sequenceNumber;
            out_buff[9] = 0x00;
            out_buff[10] = 0x11;
            out_buff[11] = 0x00;
            out_buff[12] = 0xbc;
            out_buff[13] = 0xe0;
            out_buff[14] = 0x00;
            out_buff[15] = 0x00;
            out_buff[16] = addr_base[1];
            out_buff[17] = addr_base[0];
            out_buff[18] = len;
            out_buff[19] = 0x00;
            if (len == 1)
            {
                var x = BitConverter.GetBytes((data[0] & 0x3f) | 0x80);
                out_buff[20] = x[0];
            }
            else
            {
                out_buff[20] = 0x80;
                for (int i = 2; i <= len; i++)
                {
                    out_buff[(len - 1 + 20)] = data[(i - 2)];
                }
            }

            return out_buff;
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "channel">channel에 사용할 입력값입니다.</param>
        /// <param name = "sequenceNumber">sequenceNumber에 사용할 입력값입니다.</param>
        /// <param name = "addr">addr에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildRead(byte channel, byte sequenceNumber, short addr)
        {
            Byte[] addr_base = BitConverter.GetBytes(addr);
            byte[] out_buff = new byte[21];
            byte[] out_buff_len = BitConverter.GetBytes(out_buff.Length);
            out_buff[0] = 0x06;
            out_buff[1] = 0x10;
            out_buff[2] = 0x04;
            out_buff[3] = 0x20;
            out_buff[4] = out_buff_len[1];
            out_buff[5] = out_buff_len[0];
            out_buff[6] = 0x04;
            out_buff[7] = channel;
            out_buff[8] = sequenceNumber;
            out_buff[9] = 0x00;
            out_buff[10] = 0x11;
            out_buff[11] = 0x00;
            out_buff[12] = 0xbc;
            out_buff[13] = 0xe0;
            out_buff[14] = 0x00;
            out_buff[15] = 0x00;
            out_buff[16] = addr_base[1];
            out_buff[17] = addr_base[0];
            out_buff[18] = 0x01;
            out_buff[19] = 0x00;
            out_buff[20] = 0x00;
            return out_buff;
        }

        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <param name = "channel">channel에 사용할 입력값입니다.</param>
        /// <param name = "IP_PROT">IP_PROT에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildConnectionStateRequest(byte channel, IPEndPoint IP_PROT)
        {
            byte[] out_buff = new byte[16];
            var IP = IP_PROT.Address.GetAddressBytes();
            var Prot = BitConverter.GetBytes(IP_PROT.Port);
            out_buff[0] = 0x06;
            out_buff[1] = 0x10;
            out_buff[2] = 0x02;
            out_buff[3] = 0x07;
            out_buff[4] = 0x00;
            out_buff[5] = 0x10;
            out_buff[6] = channel;
            out_buff[7] = 0x00;
            out_buff[8] = 0x08;
            out_buff[9] = 0x01;
            out_buff[10] = IP[0];
            out_buff[11] = IP[1];
            out_buff[12] = IP[2];
            out_buff[13] = IP[3];
            out_buff[14] = Prot[1];
            out_buff[15] = Prot[0];
            return out_buff;
        }

        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <param name = "channel">channel에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildConnectionStateResponse(byte channel)
        {
            byte[] out_buff = new byte[8];
            out_buff[0] = 0x06;
            out_buff[1] = 0x10;
            out_buff[2] = 0x02;
            out_buff[3] = 0x08;
            out_buff[4] = 0x00;
            out_buff[5] = 0x08;
            out_buff[6] = channel;
            out_buff[7] = 0x00;
            return out_buff;
        }

        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <param name = "channel">channel에 사용할 입력값입니다.</param>
        /// <param name = "in_data">in_data에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildTunnelAcknowledgement(byte channel, byte[] in_data)
        {
            byte[] out_buff = new byte[10];
            out_buff[0] = 0x06;
            out_buff[1] = 0x10;
            out_buff[2] = 0x04;
            out_buff[3] = 0x21;
            out_buff[4] = 0x00;
            out_buff[5] = 0x0A;
            out_buff[6] = 0x04;
            out_buff[7] = channel;
            out_buff[8] = in_data[8];
            out_buff[9] = 0x00;
            return out_buff;
        }
    }
}
