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
using static FieldLink.PlcDrivers.Beckhoff.BeckhoffAdsNetCommandBuilder;

namespace FieldLink.PlcDrivers.Beckhoff
{
    /// <summary>BeckhoffAdsNet 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class BeckhoffAdsNetResponseParser
    {
        /// <summary>UnpackResponseContent 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "send">send에 사용할 입력값입니다.</param>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> UnpackResponseContent(AdsFrameOptions context, byte[] send, byte[] response)
        {
            if (response == null || response.Length < 38)
                return new OperationResult<byte[]>("Incomplete AMS/TCP response header.");
            if (send != null && send.Length > 0)
            {
                if (send.Length < 38)
                    return new OperationResult<byte[]>("Incomplete AMS/TCP request header.");
                if (BitConverter.ToUInt16(send, 22) != BitConverter.ToUInt16(response, 22) ||
                    BitConverter.ToUInt32(send, 34) != BitConverter.ToUInt32(response, 34))
                    return new OperationResult<byte[]>("ADS command or invoke ID does not match the request.");
                for (int i = 0; i < 8; i++)
                {
                    if (response[6 + i] != send[14 + i] || response[14 + i] != send[6 + i])
                        return new OperationResult<byte[]>("ADS source or target does not match the request.");
                }
            }

            OperationResult check = AdsResponseParser.CheckResponse(response);
            if (!check.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(check);

            ushort commandId = BitConverter.ToUInt16(response, 22);
            if (commandId == BeckhoffCommandId.Read || commandId == BeckhoffCommandId.ReadWrite)
            {
                if (response.Length < 46 || BitConverter.ToUInt32(response, 42) != response.Length - 46)
                    return new OperationResult<byte[]>("ADS read data length does not match the response.");
                return OperationResult.CreateSuccessResult(response.RemoveBegin(46));
            }

            int expectedLength;
            switch (commandId)
            {
                case BeckhoffCommandId.ReadDeviceInfo:
                    expectedLength = 62;
                    break;
                case BeckhoffCommandId.ReadState:
                case BeckhoffCommandId.AddDeviceNotification:
                    expectedLength = 46;
                    break;
                case BeckhoffCommandId.Write:
                case BeckhoffCommandId.WriteControl:
                case BeckhoffCommandId.DeleteDeviceNotification:
                    expectedLength = 42;
                    break;
                default:
                    return new OperationResult<byte[]>(ProtocolMessages.NotSupportedFunction);
            }
            if (response.Length != expectedLength)
                return new OperationResult<byte[]>("ADS response size does not match its command.");
            return OperationResult.CreateSuccessResult(response.RemoveBegin(42));
        }
    }
}
