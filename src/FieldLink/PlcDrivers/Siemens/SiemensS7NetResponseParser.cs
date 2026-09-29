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
using static FieldLink.PlcDrivers.Siemens.SiemensS7NetCommandBuilder;
using static FieldLink.PlcDrivers.Siemens.SiemensS7NetAddressParser;
using static FieldLink.PlcDrivers.Siemens.SiemensS7NetDefinitions;

namespace FieldLink.PlcDrivers.Siemens
{
    /// <summary>SiemensS7Net 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class SiemensS7NetResponseParser
    {
        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "s7Addresses">s7Addresses에 사용할 입력값입니다.</param>
        /// <param name = "content">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> AnalysisReadByte(S7DeviceAddress[] s7Addresses, byte[] content)
        {
            /* 이전 구현 보존 — R-002
            아래 구현은 FF 04 등의 패턴을 찾아 요청 길이만큼 복사했다.
            21바이트 응답은 반복문을 건너뛰어 0으로 채운 성공 결과가 되었고,
            미지원 오류와 항목 누락도 성공으로 끝날 수 있었다.
            수정 구현은 외피와 항목의 길이, 반환 코드, 요청 개수를 순서대로 검증한다.
            TPKT 길이 근거: RFC 1006 §6. 타이머·카운터의 3/5바이트 표현은 기존 지원을 유지한다.
            // 분석 결과 -> Analysis results
            try
            {
                int receiveCount = 0;
                for (int i = 0; i < s7Addresses.Length; i++)
                {
                    if (s7Addresses[i].DataCode == 0x1F || s7Addresses[i].DataCode == 0x1E)
                        receiveCount += s7Addresses[i].Length * 2;
                    else
                        receiveCount += s7Addresses[i].Length;
                }

                if (content.Length >= 21 && content[20] == s7Addresses.Length)
                {
                    byte[] buffer = new byte[receiveCount];
                    int kk = 0;
                    int ll = 0;
                    for (int ii = 21; ii < content.Length; ii++)
                    {
                        if ((ii + 1) < content.Length)
                        {
                            if (content[ii] == 0xFF && content[ii + 1] == 0x04)
                            {
                                Array.Copy(content, ii + 4, buffer, ll, s7Addresses[kk].Length);
                                ii += s7Addresses[kk].Length + 3;
                                ll += s7Addresses[kk].Length;
                                kk++;
                            }
                            else if (content[ii] == 0xFF && content[ii + 1] == 0x09)
                            {
                                int count = content[ii + 2] * 256 + content[ii + 3];
                                if (count % 3 == 0)
                                {
                                    for (int i = 0; i < count / 3; i++)
                                    {
                                        Array.Copy(content, ii + 5 + 3 * i, buffer, ll, 2);
                                        ll += 2;
                                    }
                                }
                                else
                                {
                                    for (int i = 0; i < count / 5; i++)
                                    {
                                        Array.Copy(content, ii + 7 + 5 * i, buffer, ll, 2);
                                        ll += 2;
                                    }
                                }

                                ii += count + 4;
                                kk++;
                            }
                            else if (content[ii] == 0x05 && content[ii + 1] == 0x00)
                                return new OperationResult<byte[]>(content[ii], ProtocolMessages.SiemensReadLengthOverPlcAssign);
                            else if (content[ii] == 0x06 && content[ii + 1] == 0x00)
                                return new OperationResult<byte[]>(content[ii], ProtocolMessages.SiemensError0006);
                            else if (content[ii] == 0x0A && content[ii + 1] == 0x00)
                                return new OperationResult<byte[]>(content[ii], ProtocolMessages.SiemensError000A);
                        }
                    }

                    return OperationResult.CreateSuccessResult(buffer);
                }
                else
                {
                    return new OperationResult<byte[]>(ProtocolMessages.SiemensDataLengthCheckFailed + " Msg:" + ProtocolBytes.ByteToHexString(content, ' '));
                }
            }
            catch (Exception ex)
            {
                return new OperationResult<byte[]>("AnalysisReadByte failed: " + ex.Message + Environment.NewLine + " Msg:" + ProtocolBytes.ByteToHexString(content, ' '));
            }
            */
            const int responseHeaderLength = 21;
            if (s7Addresses == null || s7Addresses.Length == 0)
                return InvalidReadResponse("요청 주소가 없습니다.");
            OperationResult validation = S7ResponseValidation.Validate(content, 4);
            if (!validation.IsSuccess)
                return validation.ConvertFailed<byte[]>();
            if (content[20] != s7Addresses.Length)
                return InvalidReadResponse("응답 항목 수가 요청과 일치하지 않습니다.");
            int dataLength = content.Length - responseHeaderLength;

            int expectedLength = 0;
            foreach (S7DeviceAddress address in s7Addresses)
            {
                if (address == null)
                    return InvalidReadResponse("요청 주소가 없습니다.");
                expectedLength += address.Length * (IsTimerOrCounter(address) ? 2 : 1);
            }
            // 응답보다 큰 결과 배열을 만들지 않으며, 항목 오류 코드는 아래에서 먼저 판별한다.
            byte[] buffer = new byte[Math.Min(expectedLength, dataLength)];
            int offset = responseHeaderLength;
            int written = 0;
            for (int item = 0; item < s7Addresses.Length; item++)
            {
                if (content.Length - offset < 4)
                    return InvalidReadResponse("읽기 항목의 상태·길이 필드가 누락되었습니다.");
                byte status = content[offset];
                if (status != 0xFF)
                    return new OperationResult<byte[]>(status, GetReadItemError(status));

                S7DeviceAddress address = s7Addresses[item];
                byte transportSize = content[offset + 1];
                int declaredLength = content[offset + 2] * 256 + content[offset + 3];
                int outputLength = address.Length * (IsTimerOrCounter(address) ? 2 : 1);
                int recordSize = 0;
                int payloadLength;
                if (transportSize == 0x04 && !IsTimerOrCounter(address))
                {
                    // 바이트 읽기 응답의 길이 필드는 비트 단위다.
                    if (declaredLength != address.Length * 8)
                        return InvalidReadResponse("읽기 항목의 비트 길이가 요청과 일치하지 않습니다.");
                    payloadLength = address.Length;
                }
                else if (transportSize == 0x09 && IsTimerOrCounter(address))
                {
                    // Snap7/Sharp7의 C/T 값은 연속 2바이트이며 OCTET 길이는 바이트 단위다.
                    // 이전 3/5바이트 레코드는 기존 장비 호환 경로로만 유지한다.
                    if ((address.DataCode == 0x1C || address.DataCode == 0x1D) && declaredLength == address.Length * 2)
                        recordSize = 0;
                    else if ((address.DataCode == 0x1E || address.DataCode == 0x1F) && declaredLength == address.Length * 3)
                        recordSize = 3;
                    else if ((address.DataCode == 0x1E || address.DataCode == 0x1F) && declaredLength == address.Length * 5)
                        recordSize = 5;
                    else
                        return InvalidReadResponse("타이머·카운터 레코드 길이가 요청과 일치하지 않습니다.");
                    payloadLength = declaredLength;
                }
                else
                    return InvalidReadResponse("요청과 맞지 않거나 지원하지 않는 읽기 데이터 형식입니다.");

                offset += 4;
                if (payloadLength > content.Length - offset || outputLength > buffer.Length - written)
                    return InvalidReadResponse("읽기 항목의 실제 데이터가 부족합니다.");
                if (recordSize == 0)
                    Array.Copy(content, offset, buffer, written, outputLength);
                else
                    for (int record = 0; record < address.Length; record++)
                        Array.Copy(content, offset + record * recordSize + recordSize - 2,
                            buffer, written + record * 2, 2);
                written += outputLength;
                offset += payloadLength;

                // 홀수 길이 항목 사이에는 정렬 바이트가 온다. 마지막 항목의 선택적 0 패딩도 허용한다.
                if ((payloadLength & 1) != 0 && (item < s7Addresses.Length - 1 || offset < content.Length))
                {
                    if (offset >= content.Length || content[offset] != 0)
                        return InvalidReadResponse("읽기 항목의 정렬 바이트가 없거나 올바르지 않습니다.");
                    offset++;
                }
            }
            if (offset != content.Length || written != expectedLength)
                return InvalidReadResponse("요청 데이터가 완성되지 않았거나 응답에 불필요한 데이터가 남았습니다.");
            return OperationResult.CreateSuccessResult(buffer);
        }

        private static bool IsTimerOrCounter(S7DeviceAddress address) => address.DataCode >= 0x1C && address.DataCode <= 0x1F;

        private static OperationResult<byte[]> InvalidReadResponse(string message) => new OperationResult<byte[]>(message);

        private static string GetReadItemError(byte status)
        {
            if (status == 0x05)
                return ProtocolMessages.SiemensReadLengthOverPlcAssign;
            if (status == 0x06)
                return ProtocolMessages.SiemensError0006;
            if (status == 0x0A)
                return ProtocolMessages.SiemensError000A;
            return "S7 읽기 항목 오류: 0x" + status.ToString("X2");
        }

        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "content">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult AnalysisWrite(byte[] content)
        {
            OperationResult validation = S7ResponseValidation.Validate(content, 5);
            if (!validation.IsSuccess)
                return validation;
            int itemCount = content[20];
            if (content.Length - S7ResponseValidation.ItemDataOffset != itemCount)
                return new OperationResult("쓰기 응답의 상태 바이트 수가 항목 수와 일치하지 않습니다.");
            for (int i = 0; i < itemCount; i++)
            {
                byte code = content[S7ResponseValidation.ItemDataOffset + i];
                if (code != 0xFF)
                    return new OperationResult(code, "S7 쓰기 항목 오류: 0x" + code.ToString("X2"));
            }
            return OperationResult.CreateSuccessResult();
        }

        /// <summary>입력 데이터가 프로토콜의 검증 조건을 만족하는지 확인합니다.</summary>
        /// <param name = "content">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult CheckStartResult(byte[] content)
        {
            return CheckControlResult(content, pduStart);
        }

        /// <summary>입력 데이터가 프로토콜의 검증 조건을 만족하는지 확인합니다.</summary>
        /// <param name = "content">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult CheckStopResult(byte[] content)
        {
            return CheckControlResult(content, pduStop);
        }

        private static OperationResult CheckControlResult(byte[] content, byte function)
        {
            OperationResult validation = S7ResponseValidation.ValidateEnvelope(content);
            if (!validation.IsSuccess)
                return validation;
            // Snap7 PerformFunctionControl의 정상 ACK_DATA: 기능 코드 1바이트, 데이터 없음.
            // 20바이트 응답에는 content[20]이 없다. 02/07을 성공 코드로 해석하지 않는다.
            // 근거와 지원 범위: docs/review-fixes-2026-09-29.md (R-030).
            if (content.Length != 20 || content[13] != 0 || content[14] != 1 ||
                content[15] != 0 || content[16] != 0 || content[19] != function)
                return new OperationResult("S7 CPU 제어 응답의 기능 코드 또는 선언 길이가 올바르지 않습니다.");
            return OperationResult.CreateSuccessResult();
        }
    }
}
