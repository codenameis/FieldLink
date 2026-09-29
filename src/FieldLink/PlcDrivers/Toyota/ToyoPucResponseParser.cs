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
using static FieldLink.PlcDrivers.Toyota.ToyoPucCommandBuilder;
using static FieldLink.PlcDrivers.Toyota.ToyoPucAddressParser;

namespace FieldLink.PlcDrivers.Toyota
{
    /// <summary>ToyoPuc 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class ToyoPucResponseParser
    {
        /// <summary>장치 오류 또는 상태 코드에 대응하는 설명을 반환합니다.</summary>
        /// <param name = "code">code에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string GetErrorText(byte code)
        {
            switch (code)
            {
                case 0x11:
                    return ProtocolMessages.ToyoPuc11;
                case 0x20:
                    return ProtocolMessages.ToyoPuc20;
                case 0x21:
                    return ProtocolMessages.ToyoPuc21;
                case 0x23:
                    return ProtocolMessages.ToyoPuc23;
                case 0x24:
                    return ProtocolMessages.ToyoPuc24;
                case 0x25:
                    return ProtocolMessages.ToyoPuc25;
                case 0x34:
                    return ProtocolMessages.ToyoPuc34;
                case 0x3E:
                    return ProtocolMessages.ToyoPuc3E;
                case 0x3F:
                    return ProtocolMessages.ToyoPuc3F;
                case 0x40:
                    return ProtocolMessages.ToyoPuc40;
                case 0x41:
                    return ProtocolMessages.ToyoPuc41;
                default:
                    return ProtocolMessages.UnknownError;
            }
        }

        /// <summary>UnpackResponseContent 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "send">send에 사용할 입력값입니다.</param>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> UnpackResponseContent(byte[] send, byte[] response)
        {
            if (response == null || response.Length < 4)
                return new OperationResult<byte[]>("Receive data too short: " + response.ToHexString(' '));
            if (response[0] != 0x80)
                return new OperationResult<byte[]>("FT check failed: " + response.ToHexString(' '));
            if (response[1] != 0x00)
                return new OperationResult<byte[]>(response.Length == 4 ? GetErrorText(response[1]) : GetErrorText(response[4]));
            if (response.Length > 5)
                return OperationResult.CreateSuccessResult(response.RemoveBegin(5));
            else
                return OperationResult.CreateSuccessResult(new byte[0]);
        }
    }
}
