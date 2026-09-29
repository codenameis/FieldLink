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
    /// <summary>ParameterSetMessages 요청 프레임을 생성합니다.</summary>
    public static class ParameterSetMessagesCommandBuilder
    {
        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildParameterSetIDUpload()
        {
            return OpenProtocolNetCommandBuilder.BuildReadCommand(10, 1, -1, -1, null);
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "id">id에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildParameterSetDataUpload(int id)
        {
            return OpenProtocolNetCommandBuilder.BuildReadCommand(12, 1, -1, -1, new List<string>() { id.ToString("D3") });
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildParameterSetSelectedSubscribe()
        {
            return OpenProtocolNetCommandBuilder.BuildReadCommand(14, 1, -1, -1, null);
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildParameterSetSelectedUnsubscribe()
        {
            return OpenProtocolNetCommandBuilder.BuildReadCommand(17, 1, -1, -1, null);
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "id">id에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildSelectParameterSet(int id)
        {
            return OpenProtocolNetCommandBuilder.BuildReadCommand(18, 1, -1, -1, new List<string>() { id.ToString("D3") });
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "id">id에 사용할 입력값입니다.</param>
        /// <param name = "batchSize">batchSize에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildSetParameterSetBatchSize(int id, int batchSize)
        {
            return OpenProtocolNetCommandBuilder.BuildReadCommand(19, 1, -1, -1, new List<string>() { id.ToString("D3"), batchSize.ToString("D2") });
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "id">id에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildResetParameterSetBatchCounter(int id)
        {
            return OpenProtocolNetCommandBuilder.BuildReadCommand(20, 1, -1, -1, new List<string>() { id.ToString("D3") });
        }
    }
}
