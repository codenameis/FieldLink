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
using static FieldLink.PlcDrivers.Beckhoff.BeckhoffAdsServerDefinitions;

namespace FieldLink.PlcDrivers.Beckhoff
{
    /// <summary>BeckhoffAdsServer 요청 프레임을 생성합니다.</summary>
    public static class BeckhoffAdsServerCommandBuilder
    {
        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "cmd">포장할 명령 바이트입니다.</param>
        /// <param name = "err">err에 사용할 입력값입니다.</param>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackCommand(byte[] cmd, int err, byte[] data)
        {
            if (data == null)
                data = new byte[0];
            byte[] buffer = new byte[32 + data.Length];
            Array.Copy(cmd, 0, buffer, 0, 32);
            byte[] amsTarget = buffer.SelectBegin(8);
            byte[] amsSource = buffer.SelectMiddle(8, 8);
            amsTarget.CopyTo(buffer, 8);
            amsSource.CopyTo(buffer, 0);
            buffer[18] = 0x05;
            buffer[19] = 0x00;
            BitConverter.GetBytes(data.Length).CopyTo(buffer, 20);
            BitConverter.GetBytes(err).CopyTo(buffer, 24);
            buffer[11] = 0x00;
            if (data.Length > 0)
                data.CopyTo(buffer, 32);
            return AdsCommandBuilder.PackAmsTcpHelper(AmsTcpHeaderFlags.Command, buffer);
        }

        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "err">err에 사용할 입력값입니다.</param>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackDataResponse(int err, byte[] data)
        {
            if (data != null)
            {
                byte[] buffer = new byte[8 + data.Length];
                BitConverter.GetBytes(err).CopyTo(buffer, 0);
                BitConverter.GetBytes(data.Length).CopyTo(buffer, 4);
                if (data.Length > 0)
                    data.CopyTo(buffer, 8);
                return buffer;
            }
            else
            {
                return BitConverter.GetBytes(err);
            }
        }
    }
}
