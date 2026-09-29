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
using static FieldLink.PlcDrivers.Keyence.KeyenceNanoAddressParser;
using static FieldLink.PlcDrivers.Keyence.KeyenceNanoResponseParser;
using static FieldLink.PlcDrivers.Keyence.KeyenceNanoControlCommandBuilder;
using static FieldLink.PlcDrivers.Keyence.KeyenceNanoControlResponseParser;

namespace FieldLink.PlcDrivers.Keyence
{
    /// <summary>KeyenceNano 요청 프레임을 생성합니다.</summary>
    public static class KeyenceNanoCommandBuilder
    {
        /// <summary>GetConnectCmd 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "useStation">useStation에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] GetConnectCmd(byte station, bool useStation) => useStation ? Encoding.ASCII.GetBytes($"CR {station:D2}\r") : Encoding.ASCII.GetBytes($"CR\r");
        /// <summary>GetDisConnectCmd 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "useStation">useStation에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] GetDisConnectCmd(byte station, bool useStation) => Encoding.ASCII.GetBytes($"CQ\r");
        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<List<byte[]>> BuildReadCommand(string address, ushort length)
        {
            var addressResult = KeyenceNanoAddress.ParseFrom(address, length);
            if (!addressResult.IsSuccess)
                return OperationResult.CreateFailedResult<List<byte[]>>(addressResult);
            return BuildReadCommand(addressResult.Content, length);
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<List<byte[]>> BuildReadCommand(KeyenceNanoAddress address, ushort length)
        {
            if (length > 1)
                length = (ushort)(length / GetWordAddressMultiple(address.DataCode));
            int[] splits = ProtocolBytes.SplitIntegerToArray(length, address.SplitLength);
            List<byte[]> array = new List<byte[]>();
            for (int i = 0; i < splits.Length; i++)
            {
                StringBuilder cmd = new StringBuilder();
                cmd.Append("RDS"); // 대량 읽기
                cmd.Append(" "); // 공백 문자
                cmd.Append(address.DataCode); // DM와 같은 소프트웨어의 종류
                cmd.Append(address.GetAddressStartFormat()); // 1000, 100015과 같은 소프트웨어의 주소
                cmd.Append(" "); // 공백 문자
                cmd.Append(splits[i].ToString());
                cmd.Append("\r"); //종료 문자
                array.Add(Encoding.ASCII.GetBytes(cmd.ToString()));
                address.AddressStart += splits[i];
            }

            return OperationResult.CreateSuccessResult(array);
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteCommand(string address, byte[] value)
        {
            var addressResult = KeyenceNanoAddress.ParseFrom(address, 0);
            if (!addressResult.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(addressResult);
            StringBuilder cmd = new StringBuilder();
            cmd.Append("WRS"); // 대량 읽기
            cmd.Append(" "); // 공백 문자
            cmd.Append(addressResult.Content.DataCode); // 디바이스 주소
            cmd.Append(addressResult.Content.GetAddressStartFormat()); // 디바이스 주소
            cmd.Append(" "); // 공백 문자
            int length = value.Length / (GetWordAddressMultiple(addressResult.Content.DataCode) * 2);
            cmd.Append(length.ToString());
            for (int i = 0; i < length; i++)
            {
                cmd.Append(" ");
                cmd.Append(BitConverter.ToUInt16(value, i * GetWordAddressMultiple(addressResult.Content.DataCode) * 2));
            }

            cmd.Append("\r");
            return OperationResult.CreateSuccessResult(Encoding.ASCII.GetBytes(cmd.ToString()));
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "unit">unit에 사용할 입력값입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteExpansionMemoryCommand(byte unit, ushort address, byte[] value)
        {
            StringBuilder cmd = new StringBuilder();
            cmd.Append("UWR"); // 대량 읽기
            cmd.Append(" "); // 공백 문자
            cmd.Append(unit); // 단위 번호
            cmd.Append(" "); // 공백 문자
            cmd.Append(address); // 주소
            cmd.Append(".U"); // 데이터 형식
            cmd.Append(" "); // 공백 문자
            int length = value.Length / 2;
            cmd.Append(length.ToString());
            for (int i = 0; i < length; i++)
            {
                cmd.Append(" ");
                cmd.Append(BitConverter.ToUInt16(value, i * 2));
            }

            cmd.Append("\r");
            return OperationResult.CreateSuccessResult(Encoding.ASCII.GetBytes(cmd.ToString()));
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteCommand(string address, bool value)
        {
            var addressResult = KeyenceNanoAddress.ParseFrom(address, 0);
            if (!addressResult.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(addressResult);
            StringBuilder cmd = new StringBuilder();
            if (value)
                cmd.Append("ST"); // 비트 설정
            else
                cmd.Append("RS"); // 리셋
            cmd.Append(" "); // 공백 문자
            cmd.Append(addressResult.Content.DataCode); // 디바이스 주소
            cmd.Append(addressResult.Content.GetAddressStartFormat()); // 디바이스 주소
            cmd.Append("\r"); // 공백 문자
            return OperationResult.CreateSuccessResult(Encoding.ASCII.GetBytes(cmd.ToString()));
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteCommand(string address, bool[] value)
        {
            var addressResult = KeyenceNanoAddress.ParseFrom(address, 0);
            if (!addressResult.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(addressResult);
            StringBuilder cmd = new StringBuilder();
            cmd.Append("WRS");
            cmd.Append(" "); // 공백 문자
            cmd.Append(addressResult.Content.DataCode); // 디바이스 주소
            cmd.Append(addressResult.Content.GetAddressStartFormat()); // 디바이스 주소
            cmd.Append(" "); // 공백 문자
            cmd.Append(value.Length.ToString()); // 입력된 데이터의 길이는
            for (int i = 0; i < value.Length; i++)
            {
                cmd.Append(" "); // 공백 문자
                cmd.Append(value[i] ? "1" : "0");
            }

            cmd.Append("\r"); // 공백 문자
            return OperationResult.CreateSuccessResult(Encoding.ASCII.GetBytes(cmd.ToString()));
        }
    }
}
