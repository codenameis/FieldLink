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
    /// <summary>FxSerialActivation 요청 프레임을 생성합니다.</summary>
    public static class FxSerialActivationCommandBuilder
    {
        /// <summary>ENQ/ACK 뒤에 두 번 보내는 PLC 활성화 프레임입니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildActivation() => new byte[]
        {
            0x02,
            0x30,
            0x30,
            0x45,
            0x30,
            0x32,
            0x30,
            0x32,
            0x03,
            0x36,
            0x43
        };
        /// <summary>PLC 활성화 응답이 ACK인지 확인합니다.</summary>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult CheckActivationAck(byte[] response) => response[0] == AsciiControl.ACK ? OperationResult.CreateSuccessResult() : new OperationResult("ENQ(0x05) 요청에 대한 ACK(0x06) 응답 확인에 실패했습니다.");
    }
}
