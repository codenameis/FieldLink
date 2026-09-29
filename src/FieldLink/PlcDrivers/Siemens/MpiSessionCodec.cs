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

namespace FieldLink.PlcDrivers.Siemens
{
    /// <summary>MpiSession 데이터의 프로토콜 표현을 생성하고 해석합니다.</summary>
    public static class MpiSessionCodec
    {
        /// <summary>원본 MPI 구현의 핸드셰이크 입력에 대응하는 다음 프레임을 생성합니다.</summary>
        /// <param name = "received">received에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <remarks>원본 SiemensMPI는 장치 검증 미완료로 표시되어 있습니다.</remarks>
        public static byte[] BuildHandshakeReply(byte[] received)
        {
            if (received[0] == 0xdc && received[1] == 2 && received[2] == 2)
                return new byte[]
                {
                    0xdc,
                    0,
                    0
                };
            if (received[0] == 0xdc && received[1] == 0 && received[2] == 2)
                return new byte[]
                {
                    0xdc,
                    2,
                    0
                };
            return null;
        }

        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "write">write에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildConfirmation(byte station, bool write)
        {
            byte[] frame = write ? MpiConfirmationCommandBuilder.BuildWriteTemplate() : MpiConfirmationCommandBuilder.BuildReadTemplate();
            frame[4] = (byte)(station + 0x80);
            int checksum = 0;
            for (int i = 4; i < 12; i++)
                checksum += frame[i];
            frame[12] = (byte)checksum;
            return frame;
        }

        /// <summary>입력 데이터가 프로토콜의 검증 조건을 만족하는지 확인합니다.</summary>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult CheckAcknowledgement(byte[] response) => response[14] == 0xe5 ? OperationResult.CreateSuccessResult() : new OperationResult("MPI 확인 응답이 E5가 아닙니다.");
        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> ParseRead(byte[] response, ushort length)
        {
            if (response[19] != 0)
                return new OperationResult<byte[]>("MPI 읽기 응답 오류: " + response[19]);
            byte[] data = new byte[length];
            if (response[25] == 0xff && response[26] == 4)
                Array.Copy(response, 29, data, 0, length);
            return OperationResult.CreateSuccessResult(data);
        }

        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<bool[]> ParseReadBits(byte[] response, ushort length)
        {
            if (response[19] != 0)
                return new OperationResult<bool[]>("MPI 비트 읽기 응답 오류: " + response[19]);
            byte[] data = new byte[response.Length - 31];
            if (response[21] == 0xff && response[22] == 3)
                Array.Copy(response, 28, data, 0, data.Length);
            return OperationResult.CreateSuccessResult(ProtocolBytes.ByteToBoolArray(data, length));
        }

        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult ParseWrite(byte[] response) => response[25] == 0xff ? OperationResult.CreateSuccessResult() : new OperationResult("MPI 쓰기 응답 오류: " + response[25]);
    }
}
