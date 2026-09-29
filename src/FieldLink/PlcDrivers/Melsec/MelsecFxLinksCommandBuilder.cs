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
using static FieldLink.PlcDrivers.Melsec.MelsecFxLinksResponseParser;

namespace FieldLink.PlcDrivers.Melsec
{
    /// <summary>MelsecFxLinks 요청 프레임을 생성합니다.</summary>
    public static class MelsecFxLinksCommandBuilder
    {
        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "plc">plc에 사용할 입력값입니다.</param>
        /// <param name = "command">포장할 명령 바이트입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackCommandWithHeader(FxLinksFrameOptions plc, byte[] command)
        {
            // 이 문서는 이미 포장된 것으로 확인되면 처리되지 않습니다.
            if (command.Length > 3 && command[0] == AsciiControl.ENQ)
                return command;
            byte[] core = command;
            if (plc.SumCheck)
            {
                core = new byte[command.Length + 2];
                command.CopyTo(core, 0);
                AdditiveChecksum.WriteAscii(core, 0, core.Length - 2);
            }

            if (plc.Format == 1)
                return ProtocolBytes.SpliceArray(new byte[] { AsciiControl.ENQ }, core);
            else if (plc.Format == 4)
                return ProtocolBytes.SpliceArray(new byte[] { AsciiControl.ENQ }, core, new byte[] { AsciiControl.CR, AsciiControl.LF });
            return ProtocolBytes.SpliceArray(new byte[] { AsciiControl.ENQ }, core);
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <param name = "isBool">isBool에 사용할 입력값입니다.</param>
        /// <param name = "waitTime">waitTime에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<List<byte[]>> BuildReadCommand(byte station, string address, ushort length, bool isBool, byte waitTime = 0x00)
        {
            OperationResult<MelsecFxLinksAddress> addressAnalysis = MelsecFxLinksAddress.ParseFrom(address);
            if (!addressAnalysis.IsSuccess)
                return OperationResult.CreateFailedResult<List<byte[]>>(addressAnalysis);
            int[] lens = ProtocolBytes.SplitIntegerToArray(length, isBool ? 256 : 64);
            List<byte[]> list = new List<byte[]>();
            for (int i = 0; i < lens.Length; i++)
            {
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.Append(station.ToString("X2"));
                stringBuilder.Append("FF");
                if (isBool)
                    stringBuilder.Append("BR");
                else
                {
                    if (addressAnalysis.Content.AddressStart >= 10000)
                        stringBuilder.Append("QR");
                    else
                        stringBuilder.Append("WR");
                }

                stringBuilder.Append(waitTime.ToString("X"));
                stringBuilder.Append(addressAnalysis.Content.ToString());
                // 길이가 256인 경우 00을 사용한다.
                if (lens[i] == 256)
                    stringBuilder.Append("00");
                else
                    stringBuilder.Append(lens[i].ToString("X2"));
                list.Add(Encoding.ASCII.GetBytes(stringBuilder.ToString()));
                addressAnalysis.Content.AddressStart += lens[i];
            }

            return OperationResult.CreateSuccessResult(list);
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "waitTime">waitTime에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteBoolCommand(byte station, string address, bool[] value, byte waitTime = 0x00)
        {
            OperationResult<MelsecFxLinksAddress> addressAnalysis = MelsecFxLinksAddress.ParseFrom(address);
            if (!addressAnalysis.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(addressAnalysis);
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(station.ToString("X2"));
            stringBuilder.Append("FF");
            stringBuilder.Append("BW");
            stringBuilder.Append(waitTime.ToString("X"));
            stringBuilder.Append(addressAnalysis.Content.ToString());
            stringBuilder.Append(value.Length.ToString("X2"));
            for (int i = 0; i < value.Length; i++)
            {
                stringBuilder.Append(value[i] ? "1" : "0");
            }

            return OperationResult.CreateSuccessResult(Encoding.ASCII.GetBytes(stringBuilder.ToString()));
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "waitTime">waitTime에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteByteCommand(byte station, string address, byte[] value, byte waitTime = 0x00)
        {
            OperationResult<MelsecFxLinksAddress> addressAnalysis = MelsecFxLinksAddress.ParseFrom(address);
            if (!addressAnalysis.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(addressAnalysis);
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(station.ToString("X2"));
            stringBuilder.Append("FF");
            if (addressAnalysis.Content.AddressStart >= 10000)
                stringBuilder.Append("QW");
            else
                stringBuilder.Append("WW");
            stringBuilder.Append(waitTime.ToString("X"));
            stringBuilder.Append(addressAnalysis.Content.ToString());
            stringBuilder.Append((value.Length / 2).ToString("X2"));
            // 글쓰기
            byte[] buffer = new byte[value.Length * 2];
            for (int i = 0; i < value.Length / 2; i++)
            {
                ProtocolBytes.BuildAsciiBytesFrom(BitConverter.ToUInt16(value, i * 2)).CopyTo(buffer, 4 * i);
            }

            stringBuilder.Append(Encoding.ASCII.GetString(buffer));
            return OperationResult.CreateSuccessResult(Encoding.ASCII.GetBytes(stringBuilder.ToString()));
        }

        /// <summary>운전 시작 명령 프레임을 생성합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "waitTime">waitTime에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildStart(byte station, byte waitTime = 0x00)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(station.ToString("X2"));
            stringBuilder.Append("FF");
            stringBuilder.Append("RR");
            stringBuilder.Append(waitTime.ToString("X"));
            return OperationResult.CreateSuccessResult(Encoding.ASCII.GetBytes(stringBuilder.ToString()));
        }

        /// <summary>운전 정지 명령 프레임을 생성합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "waitTime">waitTime에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildStop(byte station, byte waitTime = 0x00)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(station.ToString("X2"));
            stringBuilder.Append("FF");
            stringBuilder.Append("RS");
            stringBuilder.Append(waitTime.ToString("X"));
            return OperationResult.CreateSuccessResult(Encoding.ASCII.GetBytes(stringBuilder.ToString()));
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "waitTime">waitTime에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildReadPlcType(byte station, byte waitTime = 0x00)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(station.ToString("X2"));
            stringBuilder.Append("FF");
            stringBuilder.Append("PC");
            stringBuilder.Append(waitTime.ToString("X"));
            return OperationResult.CreateSuccessResult(Encoding.ASCII.GetBytes(stringBuilder.ToString()));
        }
    }
}
