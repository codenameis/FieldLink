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

namespace FieldLink.PlcDrivers.OpenProtocol
{
    /// <summary>OpenProtocolSession 요청 프레임을 생성합니다.</summary>
    public static class OpenProtocolSessionCommandBuilder
    {
        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <param name = "revision">revision에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildConnect(int revision) => OpenProtocolNetCommandBuilder.BuildReadCommand(1, revision, -1, -1, null);
        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildDisconnect() => OpenProtocolNetCommandBuilder.BuildReadCommand(3, 1, -1, -1, null);
        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildHeartbeat() => OpenProtocolNetCommandBuilder.BuildReadCommand(9999, 1, -1, -1, null);
        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <param name = "frame">frame에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildEventAcknowledgement(byte[] frame)
        {
            if (frame.Length < 20)
                return null;
            int mid = Convert.ToInt32(Encoding.ASCII.GetString(frame, 4, 4));
            if (mid == 9999)
                return null;
            int acknowledgement = OpenProtocolNetValueConverter.DecideSubscribeData(mid);
            return acknowledgement > 0 && frame[11] == 0x30 ? OpenProtocolNetCommandBuilder.BuildReadCommand(acknowledgement, 1, -1, -1, null).Content : null;
        }
    }
}
