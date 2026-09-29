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
    /// <summary>JobMessage 요청 프레임을 생성합니다.</summary>
    public static class JobMessageCommandBuilder
    {
        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <param name = "revision">revision에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildJobIDUpload(int revision = 1)
        {
            return OpenProtocolNetCommandBuilder.BuildReadCommand(30, revision, -1, -1, null);
        }

        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <param name = "id">id에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildJobDataUpload(int id)
        {
            return OpenProtocolNetCommandBuilder.BuildReadCommand(32, 1, -1, -1, new List<string>() { id.ToString("D2") });
        }

        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildJobInfoSubscribe()
        {
            return OpenProtocolNetCommandBuilder.BuildReadCommand(34, 1, -1, -1, null);
        }

        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildJobInfoUnsubscribe()
        {
            return OpenProtocolNetCommandBuilder.BuildReadCommand(37, 1, -1, -1, null);
        }

        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <param name = "id">id에 사용할 입력값입니다.</param>
        /// <param name = "revision">revision에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildSelectJob(int id, int revision = 1)
        {
            return OpenProtocolNetCommandBuilder.BuildReadCommand(38, revision, -1, -1, new List<string>() { (revision == 1 ? id.ToString("D2") : id.ToString("D4")) });
        }

        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <param name = "id">id에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildJobRestart(int id)
        {
            return OpenProtocolNetCommandBuilder.BuildReadCommand(39, 1, -1, -1, new List<string>() { id.ToString("D2") });
        }
    }
}
