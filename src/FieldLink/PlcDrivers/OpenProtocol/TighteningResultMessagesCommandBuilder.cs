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
    /// <summary>TighteningResultMessages 요청 프레임을 생성합니다.</summary>
    public static class TighteningResultMessagesCommandBuilder
    {
        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <param name = "revision">revision에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildLastTighteningResultDataSubscribe(int revision)
        {
            return OpenProtocolNetCommandBuilder.BuildReadCommand(60, revision, -1, -1, null);
        }

        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildLastTighteningResultDataUnsubscribe()
        {
            return OpenProtocolNetCommandBuilder.BuildReadCommand(63, 1, -1, -1, null);
        }
    }
}
