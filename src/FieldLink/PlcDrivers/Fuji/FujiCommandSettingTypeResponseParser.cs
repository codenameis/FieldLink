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
using static FieldLink.PlcDrivers.Fuji.FujiCommandSettingTypeCommandBuilder;

namespace FieldLink.PlcDrivers.Fuji
{
    /// <summary>FujiCommandSettingType 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class FujiCommandSettingTypeResponseParser
    {
        /// <summary>장치 오류 또는 상태 코드에 대응하는 설명을 반환합니다.</summary>
        /// <param name = "error">error에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string GetErrorText(int error)
        {
            switch (error)
            {
                case 0x12:
                    return "Write of data to the program area";
                case 0x20:
                    return "Non-existing CMND code";
                case 0x21:
                    return "Input data is not in the order of data corresponding to CMND";
                case 0x22:
                    return "Operation only from the loader is effective. Operation from any other node is disabled";
                case 0x24:
                    return "A non-existing module has been specified";
                case 0x32:
                    return "An address out of the memory size has been specified";
                default:
                    return ProtocolMessages.UnknownError;
            }
        }

        /// <summary>UnpackResponseContentHelper 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "send">send에 사용할 입력값입니다.</param>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> UnpackResponseContentHelper(byte[] send, byte[] response)
        {
            try
            {
                if (response[1] != 0x00)
                    return new OperationResult<byte[]>(GetErrorText(response[1]));
                if (response[0] == 0x01)
                    return OperationResult.CreateSuccessResult(new byte[0]); // 쓰기 작업은 성공으로 돌아갑니다.
                if (response.Length < 10)
                    return new OperationResult<byte[]>(ProtocolMessages.ReceiveDataLengthTooShort + "10, Source: " + response.ToHexString(' '));
                return OperationResult.CreateSuccessResult(response.RemoveBegin(10));
            }
            catch (Exception ex)
            {
                return new OperationResult<byte[]>("UnpackResponseContentHelper failed: " + ex.Message + " Source: " + response.ToHexString(' '));
            }
        }
    }
}
