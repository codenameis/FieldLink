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

namespace FieldLink.PlcDrivers.Melsec
{
    /// <summary>McControl 데이터의 프로토콜 표현을 생성하고 해석합니다.</summary>
    public static class McControlCodec
    {
        /// <summary>운전 시작 명령 프레임을 생성합니다.</summary>
        /// <param name = "type">type에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildRemoteRun(McType type) => type == McType.McBinary ? OperationResult.CreateSuccessResult(new byte[] { 0x01, 0x10, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00 }) : type == McType.MCAscii ? OperationResult.CreateSuccessResult(Encoding.ASCII.GetBytes("1001000000010000")) : new OperationResult<byte[]>(ProtocolMessages.NotSupportedFunction);
        /// <summary>운전 정지 명령 프레임을 생성합니다.</summary>
        /// <param name = "type">type에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildRemoteStop(McType type) => type == McType.McBinary ? OperationResult.CreateSuccessResult(new byte[] { 0x02, 0x10, 0x00, 0x00, 0x01, 0x00 }) : type == McType.MCAscii ? OperationResult.CreateSuccessResult(Encoding.ASCII.GetBytes("100200000001")) : new OperationResult<byte[]>(ProtocolMessages.NotSupportedFunction);
        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <param name = "type">type에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildRemoteReset(McType type) => type == McType.McBinary ? OperationResult.CreateSuccessResult(new byte[] { 0x06, 0x10, 0x00, 0x00, 0x01, 0x00 }) : type == McType.MCAscii ? OperationResult.CreateSuccessResult(Encoding.ASCII.GetBytes("100600000001")) : new OperationResult<byte[]>(ProtocolMessages.NotSupportedFunction);
        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "type">type에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildReadPlcType(McType type) => type == McType.McBinary ? OperationResult.CreateSuccessResult(new byte[] { 0x01, 0x01, 0x00, 0x00 }) : type == McType.MCAscii ? OperationResult.CreateSuccessResult(Encoding.ASCII.GetBytes("01010000")) : new OperationResult<byte[]>(ProtocolMessages.NotSupportedFunction);
        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <param name = "type">type에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildErrorStateReset(McType type) => type == McType.McBinary ? OperationResult.CreateSuccessResult(new byte[] { 0x17, 0x16, 0x00, 0x00 }) : type == McType.MCAscii ? OperationResult.CreateSuccessResult(Encoding.ASCII.GetBytes("16170000")) : new OperationResult<byte[]>(ProtocolMessages.NotSupportedFunction);
        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string ParsePlcType(byte[] response) => Encoding.ASCII.GetString(response, 0, 16).TrimEnd();
    }
}
