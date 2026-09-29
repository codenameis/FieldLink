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
    /// <summary>ToolMessages 요청 프레임을 생성합니다.</summary>
    public static class ToolMessagesCommandBuilder
    {
        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <param name = "revision">revision에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildToolDataUpload(int revision = 1)
        {
            return OpenProtocolNetCommandBuilder.BuildReadCommand(40, revision, -1, -1, null);
        }

        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildDisableTool()
        {
            return OpenProtocolNetCommandBuilder.BuildReadCommand(42, 1, -1, -1, null);
        }

        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildEnableTool()
        {
            return OpenProtocolNetCommandBuilder.BuildReadCommand(43, 1, -1, -1, null);
        }

        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildDisconnectToolRequest()
        {
            return OpenProtocolNetCommandBuilder.BuildReadCommand(44, 1, -1, -1, null);
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "calibrationValueUnit">calibrationValueUnit에 사용할 입력값입니다.</param>
        /// <param name = "calibrationValue">calibrationValue에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildSetCalibrationValueRequest(int calibrationValueUnit, double calibrationValue)
        {
            string value1 = "01" + calibrationValueUnit.ToString();
            string value2 = "02" + Convert.ToInt32(calibrationValue * 100d).ToString("D6");
            return OpenProtocolNetCommandBuilder.BuildReadCommand(45, 1, -1, -1, new List<string>() { value1, value2 });
        }
    }
}
