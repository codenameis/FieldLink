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
using static FieldLink.PlcDrivers.Melsec.MelsecFxSerialCommandBuilder;
using static FieldLink.PlcDrivers.Melsec.MelsecFxSerialAddressParser;

namespace FieldLink.PlcDrivers.Melsec
{
    /// <summary>MelsecFxSerial 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class MelsecFxSerialResponseParser
    {
        /// <summary>누적된 바이트가 완전한 프로토콜 프레임인지 판별합니다.</summary>
        /// <param name = "buffer">프레임 데이터가 담긴 버퍼입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static bool CheckReceiveDataComplete(byte[] buffer)
        {
            if (buffer.Length == 0)
                return false;
            if (buffer.Length == 1)
            {
                if (buffer[0] == AsciiControl.NAK)
                    return true;
                if (buffer[0] == AsciiControl.ACK)
                    return true;
            }
            else if (buffer[0] == AsciiControl.STX && buffer.Length >= 5)
            {
                if (buffer[buffer.Length - 3] == AsciiControl.ETX && MelsecResponseParser.CheckCRC(buffer))
                    return true;
            }

            return false;
        }

        /// <summary>입력 데이터가 프로토콜의 검증 조건을 만족하는지 확인합니다.</summary>
        /// <param name = "ack">ack에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult CheckPlcReadResponse(byte[] ack)
        {
            if (ack.Length == 0)
                return new OperationResult(ProtocolMessages.MelsecFxReceiveZero);
            if (ack[0] == AsciiControl.NAK)
                return new OperationResult(ProtocolMessages.MelsecFxAckNagative + " Actual: " + ProtocolBytes.ByteToHexString(ack, ' '));
            if (ack[0] != AsciiControl.STX)
                return new OperationResult(ProtocolMessages.MelsecFxAckWrong + ack[0] + " Actual: " + ProtocolBytes.ByteToHexString(ack, ' '));
            try
            {
                if (!MelsecResponseParser.CheckCRC(ack))
                    return new OperationResult(ProtocolMessages.MelsecFxCrcCheckFailed + " Actual: " + ProtocolBytes.ByteToHexString(ack, ' '));
            }
            catch (Exception ex)
            {
                return new OperationResult(ProtocolMessages.MelsecFxCrcCheckFailed + ex.Message + Environment.NewLine + "Actual: " + ProtocolBytes.ByteToHexString(ack, ' '));
            }

            return OperationResult.CreateSuccessResult();
        }

        /// <summary>입력 데이터가 프로토콜의 검증 조건을 만족하는지 확인합니다.</summary>
        /// <param name = "ack">ack에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult CheckPlcWriteResponse(byte[] ack)
        {
            if (ack.Length == 0)
                return new OperationResult(ProtocolMessages.MelsecFxReceiveZero);
            if (ack[0] == AsciiControl.NAK)
                return new OperationResult(ProtocolMessages.MelsecFxAckNagative + " Actual: " + ProtocolBytes.ByteToHexString(ack, ' '));
            if (ack[0] != AsciiControl.ACK)
                return new OperationResult(ProtocolMessages.MelsecFxAckWrong + ack[0] + " Actual: " + ProtocolBytes.ByteToHexString(ack, ' '));
            return OperationResult.CreateSuccessResult();
        }

        /// <summary>프로토콜 응답에서 데이터와 상태 정보를 추출합니다.</summary>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> ExtractActualData(byte[] response)
        {
            try
            {
                byte[] data = new byte[(response.Length - 4) / 2];
                for (int i = 0; i < data.Length; i++)
                {
                    byte[] buffer = new byte[2];
                    buffer[0] = response[i * 2 + 1];
                    buffer[1] = response[i * 2 + 2];
                    data[i] = Convert.ToByte(Encoding.ASCII.GetString(buffer), 16);
                }

                return OperationResult.CreateSuccessResult(data);
            }
            catch (Exception ex)
            {
                return new OperationResult<byte[]>()
                {
                    Message = "Extract Msg：" + ex.Message + Environment.NewLine + "Data: " + ProtocolBytes.ByteToHexString(response)
                };
            }
        }

        /// <summary>프로토콜 응답에서 데이터와 상태 정보를 추출합니다.</summary>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <param name = "start">start에 사용할 입력값입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<bool[]> ExtractActualBoolData(byte[] response, int start, int length)
        {
            OperationResult<byte[]> extraResult = ExtractActualData(response);
            if (!extraResult.IsSuccess)
                return OperationResult.CreateFailedResult<bool[]>(extraResult);
            // bool 배열 변환
            try
            {
                bool[] data = new bool[length];
                bool[] array = ProtocolBytes.ByteToBoolArray(extraResult.Content, extraResult.Content.Length * 8);
                for (int i = 0; i < length; i++)
                {
                    data[i] = array[i + start];
                }

                return OperationResult.CreateSuccessResult(data);
            }
            catch (Exception ex)
            {
                return new OperationResult<bool[]>()
                {
                    Message = "Extract Msg：" + ex.Message + Environment.NewLine + "Data: " + ProtocolBytes.ByteToHexString(response)
                };
            }
        }
    }
}
