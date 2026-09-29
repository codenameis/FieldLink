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
using static FieldLink.PlcDrivers.Vigor.VigorVsCommandBuilder;
using static FieldLink.PlcDrivers.Vigor.VigorVsAddressParser;

namespace FieldLink.PlcDrivers.Vigor
{
    /// <summary>VigorVs 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class VigorVsResponseParser
    {
        /// <summary>UnPackCommand 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "command">포장할 명령 바이트입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] UnPackCommand(byte[] command)
        {
            if (command == null)
                command = new byte[0];
            MemoryStream ms = new MemoryStream();
            for (int i = 0; i < command.Length; i++)
            {
                ms.WriteByte(command[i]);
                if (command[i] == 0x10)
                {
                    if (i + 1 < command.Length && command[i + 1] == 0x10)
                    {
                        i++;
                    }
                }
            }

            return ms.ToArray();
        }

        /// <summary>누적된 바이트가 완전한 프로토콜 프레임인지 판별합니다.</summary>
        /// <param name = "buffer">프레임 데이터가 담긴 버퍼입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static bool CheckReceiveDataComplete(byte[] buffer, int length)
        {
            int index = 0;
            if (length < 10)
                return false;
            for (int i = 0; i < length; i++)
            {
                if (buffer[i] == 0x10 && (i + 1 < length))
                {
                    if (buffer[i + 1] == 0x10)
                        i++;
                    else if (buffer[i + 1] == 0x03)
                    {
                        index = i;
                        break;
                    }
                }
            }

            if (index == length - 4)
                return true;
            return false;
        }

        /// <summary>입력 데이터가 프로토콜의 검증 조건을 만족하는지 확인합니다.</summary>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> CheckResponseContent(byte[] response)
        {
            response = UnPackCommand(response);
            if (response.Length < 6)
                return new OperationResult<byte[]>(ProtocolMessages.ReceiveDataLengthTooShort + " Source: " + response.ToHexString(' '));
            if (response[5] != 0x00)
                return new OperationResult<byte[]>(response[5], GetErrorText(response[5]) + " Source: " + response.ToHexString(' '));
            try
            {
                int len = BitConverter.ToUInt16(response, 3);
                if (len + 9 == response.Length)
                {
                    if (len == 1)
                        return OperationResult.CreateSuccessResult(new byte[0]);
                    return OperationResult.CreateSuccessResult(response.SelectMiddle(6, len - 1));
                }

                return new OperationResult<byte[]>(response[5], "Length check failed, Source: " + response.ToHexString(' '));
            }
            catch (Exception ex)
            {
                return new OperationResult<byte[]>("CheckResponseContent failed: " + ex.Message + Environment.NewLine + "Source: " + response.ToHexString(' '));
            }
        }

        /// <summary>장치 오류 또는 상태 코드에 대응하는 설명을 반환합니다.</summary>
        /// <param name = "status">status에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string GetErrorText(byte status)
        {
            switch (status)
            {
                case 0x02:
                    return ProtocolMessages.Vigor02;
                case 0x04:
                    return ProtocolMessages.Vigor04;
                case 0x06:
                    return ProtocolMessages.Vigor06;
                case 0x08:
                    return ProtocolMessages.Vigor08;
                case 0x31:
                    return ProtocolMessages.Vigor31;
                default:
                    return ProtocolMessages.UnknownError;
            }
        }
    }
}
