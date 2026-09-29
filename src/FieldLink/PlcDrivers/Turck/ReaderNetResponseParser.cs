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
using static FieldLink.PlcDrivers.Turck.ReaderNetAddressParser;
using static FieldLink.PlcDrivers.Turck.ReaderNetValueConverter;
using static FieldLink.PlcDrivers.Turck.ReaderNetCommandBuilder;

namespace FieldLink.PlcDrivers.Turck
{
    /// <summary>ReaderNet 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class ReaderNetResponseParser
    {
        /// <summary>장치 오류 또는 상태 코드에 대응하는 설명을 반환합니다.</summary>
        /// <param name = "err1">err1에 사용할 입력값입니다.</param>
        /// <param name = "err2">err2에 사용할 입력값입니다.</param>
        /// <param name = "err3">err3에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string GetErrorText(byte err1, byte err2, byte err3)
        {
            if (err1 == 0x01)
                return "Command not supported";
            if (err1 == 0x02)
                return "Command not correctly detected, e.g. wrong format";
            if (err1 == 0x03)
                return "Command option not supportde";
            if (err1 == 0x0f)
                return "Undefined/General error";
            if (err1 == 0x10)
                return "Requested memory block not available";
            if (err1 == 0x11)
                return "Requested memory block is already locked";
            if (err1 == 0x12)
                return "Requested memory block is locked and cannot be written";
            if (err1 == 0x13)
                return "Writing of requested memory block not successful";
            if (err1 == 0x14)
                return "Requested memory block could not be locked";
            if (err1 != 0x00)
                return "Customer specific error codes";
            if (err2 == 0x01)
                return "CRC_ERR, telegram fault in the tag-response";
            if (err2 == 0x02)
                return "TimeOut_ERR, no tag-response in the given time";
            if (err2 == 0x04)
                return "Tag_ERR, tag defect, e.g. multiple crc-faults on the air interface";
            if (err2 == 0x08)
                return "CHAIN_ERR, Tag has left the air interface before executing all commands";
            if (err2 == 0x10)
                return "UID_ERR, other UID as expected was detected during addressed mode";
            if (err2 != 0x00)
                return ProtocolMessages.UnknownError;
            if (err3 == 0x01)
                return "TRANS_ERR, transceiver defect, e.g. Flash-checksum";
            if (err3 == 0x02)
                return "CMD_ERR, fault during execution of a command";
            if (err3 == 0x04)
                return "syntax_ERR, telegram content not valid, e.g. requested tag-memory address not available";
            if (err3 == 0x08)
                return "PS_ERR, power supply too low";
            if (err3 == 0x10)
                return "CMD_UNKNOWN, unknown command code";
            return ProtocolMessages.UnknownError;
        }

        /// <summary>입력 데이터가 프로토콜의 검증 조건을 만족하는지 확인합니다.</summary>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "len">len에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static bool CheckCRC(byte[] data, int len)
        {
            byte[] crc = CalculateCRC(data, len);
            return data[len + 0] == crc[0] && data[len + 1] == crc[1];
        }

        /// <summary>입력 데이터가 프로토콜의 검증 조건을 만족하는지 확인합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "content">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> CheckResponseContent(TurckTagInfo context, byte[] content)
        {
            if (content[1] == 0x0a && content[2] == 0x0a)
            {
                if (content[5] == 0x00 && content[6] == 0x02 && content[7] == 0x00)
                    context.successfullyInitialized = false;
                return new OperationResult<byte[]>(msg: GetErrorText(content[5], content[6], content[7]) + " Source: " + content.ToHexString(' '));
            }

            if (content[1] == 0x07 && content[2] == 0x07)
                return OperationResult.CreateSuccessResult(new byte[0]);
            if (content.Length > 7)
                return OperationResult.CreateSuccessResult(content.SelectMiddle(5, content.Length - 7));
            return new OperationResult<byte[]>("Error message: " + content.ToHexString(' '));
        }

        /// <summary>프로토콜 응답에서 데이터와 상태 정보를 추출합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "content">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<string> ExtraUID(TurckTagInfo context, byte[] content)
        {
            OperationResult<byte[]> check = CheckResponseContent(context, content);
            if (check.IsSuccess)
            {
                context.UID = content.SelectMiddle(5, 8).ToHexString();
                context.NumberOfBlock = content[15];
                context.BytesOfBlock = (byte)(content[16] + 1);
                context.successfullyInitialized = true;
                return OperationResult.CreateSuccessResult(context.UID);
            }
            else
            {
                context.successfullyInitialized = false;
                return OperationResult.CreateFailedResult<string>(check);
            }
        }
    }
}
