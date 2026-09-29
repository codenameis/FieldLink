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
using static FieldLink.PlcDrivers.Omron.OmronHostLinkCModeAddressParser;
using static FieldLink.PlcDrivers.Omron.OmronHostLinkCModeCommandBuilder;
using static FieldLink.PlcDrivers.Omron.OmronHostLinkCModeResponseParser;
using static FieldLink.PlcDrivers.Omron.OmronHostLinkCModeControlCommandBuilder;

namespace FieldLink.PlcDrivers.Omron
{
    /// <summary>OmronHostLinkCModeControl 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class OmronHostLinkCModeControlResponseParser
    {
        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <param name = "unitNumber">unitNumber에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<string> ParseReadPlcType(byte[] response, byte unitNumber)
        {
            // 데이터 유효성 분석
            int err = Convert.ToInt32(Encoding.ASCII.GetString(response, 5, 2), 16);
            if (err > 0)
                return new OperationResult<string>(err, "Unknown Error");
            // 성공
            string model = Encoding.ASCII.GetString(response, 7, 2);
            return GetModelText(model);
        }

        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <param name = "unitNumber">unitNumber에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<int> ParseReadPlcMode(byte[] response, byte unitNumber)
        {
            // 데이터 유효성 분석
            int err = Convert.ToInt32(Encoding.ASCII.GetString(response, 5, 2), 16);
            if (err > 0)
                return new OperationResult<int>(err, "Unknown Error");
            // 성공
            byte[] model = Encoding.ASCII.GetString(response, 7, 4).ToHexBytes();
            return OperationResult.CreateSuccessResult(model[0] & 0x03);
        }

        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <param name = "unitNumber">unitNumber에 사용할 입력값입니다.</param>
        /// <param name = "mode">mode에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult ParseChangePlcMode(byte[] response, byte unitNumber, byte mode)
        {
            // 데이터 유효성 분석
            int err = Convert.ToInt32(Encoding.ASCII.GetString(response, 5, 2), 16);
            if (err > 0)
                return new OperationResult<int>(err, "Unknown Error");
            // 성공
            return OperationResult.CreateSuccessResult();
        }
    }
}
