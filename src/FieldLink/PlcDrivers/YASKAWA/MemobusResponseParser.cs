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
using static FieldLink.PlcDrivers.YASKAWA.MemobusCommandBuilder;
using static FieldLink.PlcDrivers.YASKAWA.MemobusValueConverter;

namespace FieldLink.PlcDrivers.YASKAWA
{
    /// <summary>Memobus 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class MemobusResponseParser
    {
        /// <summary>장치 오류 또는 상태 코드에 대응하는 설명을 반환합니다.</summary>
        /// <param name = "err">err에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string GetErrorText(byte err)
        {
            switch (err)
            {
                case 0x01:
                    return ProtocolMessages.Memobus01;
                case 0x02:
                    return ProtocolMessages.Memobus02;
                case 0x03:
                    return ProtocolMessages.Memobus03;
                case 0x40:
                    return ProtocolMessages.Memobus40;
                case 0x41:
                    return ProtocolMessages.Memobus41;
                case 0x42:
                    return ProtocolMessages.Memobus42;
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
            // SIEP C88070004Q Appendix B: 12-byte 218 header and length-prefixed MEMOBUS data.
            if (response == null || response.Length < 17)
                return new OperationResult<byte[]>("Incomplete MEMOBUS response.");
            if (response[0] != 0x19 || BitConverter.ToUInt16(response, 6) != response.Length ||
                BitConverter.ToUInt16(response, 12) != response.Length - 14 || response[14] != 0x20)
                return new OperationResult<byte[]>("Invalid MEMOBUS response header or length.");
            try
            {
                if (send != null && send.Length > 0)
                {
                    if (send.Length < 17)
                        return new OperationResult<byte[]>("Incomplete MEMOBUS request.");
                    if (response[1] != send[1] || response[2] != send[3] || response[3] != send[2] ||
                        response[16] != (byte)((send[16] << 4) | (send[16] >> 4)))
                        return new OperationResult<byte[]>("MEMOBUS response identity does not match the request.");
                    if (send[15] != response[15] && (send[15] | 0x80) != response[15])
                        return new OperationResult<byte[]>(response[15], "Send SFC not same as back SFC:" + response.ToHexString());
                }
                if ((response[15] & 0x80) != 0)
                {
                    if (response.Length != 18)
                        return new OperationResult<byte[]>("Invalid MEMOBUS error response length.");
                    return new OperationResult<byte[]>(response[17], GetErrorText(response[17]) + " Source: " + response.ToHexString(' '));
                }

                return OperationResult.CreateSuccessResult(response.RemoveBegin(12));
            }
            catch (Exception ex)
            {
                return new OperationResult<byte[]>("UnpackResponseContent failed: " + ex.Message + "  Source: " + response.ToHexString(' '));
            }
        }
    }
}
