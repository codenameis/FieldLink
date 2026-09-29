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
    /// <summary>KnxCode 프로토콜의 값과 바이트 표현을 변환합니다.</summary>
    public static class KnxCodeValueConverter
    {
        /// <summary>Disconnect_knx 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "channel">channel에 사용할 입력값입니다.</param>
        /// <param name = "IP_PROT">IP_PROT에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] Disconnect_knx(byte channel, IPEndPoint IP_PROT)
        {
            var IP = IP_PROT.Address.GetAddressBytes();
            var Prot = BitConverter.GetBytes(IP_PROT.Port);
            byte[] out_buff = new byte[16];
            out_buff[0] = 0X6; //머리 길이
            out_buff[1] = 0X10;
            out_buff[2] = 0X02;
            out_buff[3] = 0X09;
            out_buff[4] = 0X00;
            out_buff[5] = 0X10;
            out_buff[6] = channel;
            out_buff[7] = 0X00;
            out_buff[8] = 0X08;
            out_buff[9] = 0X01;
            out_buff[10] = IP[0];
            out_buff[11] = IP[1];
            out_buff[12] = IP[2];
            out_buff[13] = IP[3];
            out_buff[14] = Prot[1];
            out_buff[15] = Prot[0];
            return out_buff;
        }

        /// <summary>Handshake 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "IP_PROT">IP_PROT에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] Handshake(IPEndPoint IP_PROT)
        {
            var IP = IP_PROT.Address.GetAddressBytes();
            var Prot = BitConverter.GetBytes(IP_PROT.Port);
            byte[] out_buff = new byte[26];
            out_buff[0] = 0x06;
            out_buff[1] = 0x10;
            out_buff[2] = 0x02;
            out_buff[3] = 0x05;
            out_buff[4] = 0x00;
            out_buff[5] = 0x1a;
            out_buff[6] = 0x08; //****
            out_buff[7] = 0x01;
            out_buff[8] = IP[0];
            out_buff[9] = IP[1];
            out_buff[10] = IP[2];
            out_buff[11] = IP[3];
            out_buff[12] = Prot[1];
            out_buff[13] = Prot[0];
            out_buff[14] = 0x08;
            out_buff[15] = 0x01;
            out_buff[16] = IP[0];
            out_buff[17] = IP[1];
            out_buff[18] = IP[2];
            out_buff[19] = IP[3];
            out_buff[20] = Prot[1];
            out_buff[21] = Prot[0];
            out_buff[22] = 0x04;
            out_buff[23] = 0x04;
            out_buff[24] = 0x02;
            out_buff[25] = 0x00;
            return out_buff;
        }
    }
}
