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
using static FieldLink.PlcDrivers.Keyence.KeyenceNanoCommandBuilder;
using static FieldLink.PlcDrivers.Keyence.KeyenceNanoAddressParser;
using static FieldLink.PlcDrivers.Keyence.KeyenceNanoControlCommandBuilder;
using static FieldLink.PlcDrivers.Keyence.KeyenceNanoControlResponseParser;

namespace FieldLink.PlcDrivers.Keyence
{
    /// <summary>KeyenceNano 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class KeyenceNanoResponseParser
    {
        /// <summary>장치 오류 또는 상태 코드에 대응하는 설명을 반환합니다.</summary>
        /// <param name = "err">err에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string GetErrorText(string err)
        {
            if (err.StartsWith("E0"))
                return ProtocolMessages.KeyenceNanoE0;
            if (err.StartsWith("E1"))
                return ProtocolMessages.KeyenceNanoE1;
            if (err.StartsWith("E2"))
                return ProtocolMessages.KeyenceNanoE2;
            if (err.StartsWith("E4"))
                return ProtocolMessages.KeyenceNanoE4;
            if (err.StartsWith("E5"))
                return ProtocolMessages.KeyenceNanoE5;
            if (err.StartsWith("E6"))
                return ProtocolMessages.KeyenceNanoE6;
            return ProtocolMessages.UnknownError + " " + err;
        }

        /// <summary>입력 데이터가 프로토콜의 검증 조건을 만족하는지 확인합니다.</summary>
        /// <param name = "ack">ack에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult CheckPlcReadResponse(byte[] ack)
        {
            try
            {
                if (ack.Length == 0)
                    return new OperationResult(ProtocolMessages.MelsecFxReceiveZero);
                if (ack[0] == 0x45)
                    return new OperationResult(GetErrorText(Encoding.ASCII.GetString(ack)));
                if ((ack[ack.Length - 1] != AsciiControl.LF) && (ack[ack.Length - 2] != AsciiControl.CR))
                    return new OperationResult(ProtocolMessages.MelsecFxAckWrong + " Actual: " + ProtocolBytes.ByteToHexString(ack, ' '));
                return OperationResult.CreateSuccessResult();
            }
            catch (Exception ex)
            {
                return new OperationResult("CheckPlcReadResponse failed: " + ex.Message + Environment.NewLine + ack.ToHexString(' '));
            }
        }

        /// <summary>입력 데이터가 프로토콜의 검증 조건을 만족하는지 확인합니다.</summary>
        /// <param name = "ack">ack에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult CheckPlcWriteResponse(byte[] ack)
        {
            try
            {
                if (ack.Length == 0)
                    return new OperationResult(ProtocolMessages.MelsecFxReceiveZero);
                if (ack[0] == 0x4F && ack[1] == 0x4B)
                    return OperationResult.CreateSuccessResult();
                return new OperationResult(GetErrorText(Encoding.ASCII.GetString(ack)));
            }
            catch (Exception ex)
            {
                return new OperationResult("CheckPlcWriteResponse failed: " + ex.Message + Environment.NewLine + ack.ToHexString(' '));
            }
        }

        /// <summary>프로토콜 응답에서 데이터와 상태 정보를 추출합니다.</summary>
        /// <param name = "addressType">addressType에 사용할 입력값입니다.</param>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<bool[]> ExtractActualBoolData(string addressType, byte[] response)
        {
            try
            {
                if (string.IsNullOrEmpty(addressType))
                    addressType = "R";
                string strResponse = Encoding.Default.GetString(response.RemoveLast(2));
                if (addressType == "R" || addressType == "CR" || addressType == "MR" || addressType == "LR" || addressType == "B" || addressType == "VB")
                {
                    return OperationResult.CreateSuccessResult(strResponse.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(m => m == "1").ToArray());
                }
                else if (addressType == "T" || addressType == "C" || addressType == "CTH" || addressType == "CTC")
                {
                    return OperationResult.CreateSuccessResult(strResponse.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(m => m.StartsWith("1")).ToArray());
                }
                else
                {
                    return new OperationResult<bool[]>(ProtocolMessages.NotSupportedDataType);
                }
            }
            catch (Exception ex)
            {
                return new OperationResult<bool[]>()
                {
                    Message = "Extract Msg：" + ex.Message + Environment.NewLine + "Data: " + ProtocolBytes.ByteToHexString(response)
                };
            }
        }

        /// <summary>프로토콜 응답에서 데이터와 상태 정보를 추출합니다.</summary>
        /// <param name = "addressType">addressType에 사용할 입력값입니다.</param>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> ExtractActualData(string addressType, byte[] response)
        {
            try
            {
                if (string.IsNullOrEmpty(addressType))
                    addressType = "R";
                string strResponse = Encoding.Default.GetString(response.RemoveLast(2));
                string[] splits = strResponse.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (addressType == "DM" || addressType == "EM" || addressType == "FM" || addressType == "ZF" || addressType == "W" || addressType == "TM" || addressType == "Z" || addressType == "CM" || addressType == "VM")
                {
                    byte[] buffer = new byte[splits.Length * 2];
                    for (int i = 0; i < splits.Length; i++)
                    {
                        BitConverter.GetBytes(ushort.Parse(splits[i])).CopyTo(buffer, i * 2);
                    }

                    return OperationResult.CreateSuccessResult(buffer);
                }
                else if (addressType == "AT" || addressType == "TC" || addressType == "CC" || addressType == "TS" || addressType == "CS")
                {
                    byte[] buffer = new byte[splits.Length * 4];
                    for (int i = 0; i < splits.Length; i++)
                    {
                        BitConverter.GetBytes(uint.Parse(splits[i])).CopyTo(buffer, i * 4);
                    }

                    return OperationResult.CreateSuccessResult(buffer);
                }
                else if (addressType == "T" || addressType == "C" || addressType == "CTH" || addressType == "CTC")
                {
                    byte[] buffer = new byte[splits.Length * 4];
                    for (int i = 0; i < splits.Length; i++)
                    {
                        string[] datas = splits[i].Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        BitConverter.GetBytes(uint.Parse(datas[1])).CopyTo(buffer, i * 4);
                    }

                    return OperationResult.CreateSuccessResult(buffer);
                }
                else
                {
                    return new OperationResult<byte[]>(ProtocolMessages.NotSupportedDataType);
                }
            }
            catch (Exception ex)
            {
                return new OperationResult<byte[]>()
                {
                    Message = "Extract Msg：" + ex.Message + Environment.NewLine + "Data: " + ProtocolBytes.ByteToHexString(response)
                };
            }
        }
    }
}
