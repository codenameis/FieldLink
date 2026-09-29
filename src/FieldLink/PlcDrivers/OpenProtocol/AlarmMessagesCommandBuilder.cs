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
    /// <summary>AlarmMessages 요청 프레임을 생성합니다.</summary>
    public static class AlarmMessagesCommandBuilder
    {
        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildAlarmSubscrib()
        {
            return OpenProtocolNetCommandBuilder.BuildReadCommand(70, 1, -1, -1, null);
        }

        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildAlarmUnsubscribe()
        {
            return OpenProtocolNetCommandBuilder.BuildReadCommand(73, 1, -1, -1, null);
        }

        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildAcknowledgeAlarmRemotelyOnController()
        {
            return OpenProtocolNetCommandBuilder.BuildReadCommand(78, 1, -1, -1, null);
        }
    }
}
