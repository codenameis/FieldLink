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
using static FieldLink.PlcDrivers.Beckhoff.BeckhoffAdsNetResponseParser;

namespace FieldLink.PlcDrivers.Beckhoff
{
    /// <summary>BeckhoffAdsNet 요청 프레임을 생성합니다.</summary>
    public static class BeckhoffAdsNetCommandBuilder
    {
        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "command">포장할 명령 바이트입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackCommandWithHeader(AdsFrameOptions context, byte[] command)
        {
            uint invokeId = (uint)context.InvokeId;
            context.targetAMSNetId.CopyTo(command, 6 + 0);
            context.sourceAMSNetId.CopyTo(command, 6 + 8);
            command[6 + 28] = BitConverter.GetBytes(invokeId)[0];
            command[6 + 29] = BitConverter.GetBytes(invokeId)[1];
            command[6 + 30] = BitConverter.GetBytes(invokeId)[2];
            command[6 + 31] = BitConverter.GetBytes(invokeId)[3];
            return command;
        }
    }
}
