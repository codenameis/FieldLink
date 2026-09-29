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
using static FieldLink.PlcDrivers.Panasonic.PanasonicValueConverter;
using static FieldLink.PlcDrivers.Panasonic.PanasonicAddressParser;
using static FieldLink.PlcDrivers.Panasonic.PanasonicResponseParser;

namespace FieldLink.PlcDrivers.Panasonic
{
    /// <summary>Panasonic 요청 프레임을 생성합니다.</summary>
    public static class PanasonicCommandBuilder
    {
        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "cmd">포장할 명령 바이트입니다.</param>
        /// <param name = "useExpandedHeader">useExpandedHeader에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> PackPanasonicCommand(byte station, string cmd, bool useExpandedHeader)
        {
            StringBuilder sb = new StringBuilder(useExpandedHeader ? "<" : "%");
            sb.Append(station.ToString("X2"));
            sb.Append(cmd); // RCSR100F, RCP2R100F와 같은 추가 명령
            sb.Append(CalculateCrc(sb));
            sb.Append('\u000D');
            return OperationResult.CreateSuccessResult(Encoding.ASCII.GetBytes(sb.ToString()));
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildReadOneCoil(byte station, string address)
        {
            // 매개 변수 확인
            if (address == null)
                return new OperationResult<byte[]>("address is not allowed null");
            if (address.Length < 1 || address.Length > 8)
                return new OperationResult<byte[]>("length must be 1-8");
            StringBuilder sb = new StringBuilder("#RCS");
            OperationResult append = AppendCoil(sb, address);
            if (!append.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(append);
            return PackPanasonicCommand(station, sb.ToString(), false);
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<List<byte[]>> BuildReadCoils(byte station, string[] address)
        {
            List<byte[]> list = new List<byte[]>();
            // 8개의 길이의 단위로 나누는 명령
            List<string[]> splits = ProtocolBytes.ArraySplitByLength(address, 8);
            for (int i = 0; i < splits.Count; i++)
            {
                StringBuilder sb = new StringBuilder("#RCP");
                sb.Append(splits[i].Length.ToString()); // 다중 터치 포인트를 지정
                for (int j = 0; j < splits[i].Length; j++)
                {
                    OperationResult append = AppendCoil(sb, splits[i][j]);
                    if (!append.IsSuccess)
                        return OperationResult.CreateFailedResult<List<byte[]>>(append);
                }

                list.Add(PackPanasonicCommand(station, sb.ToString(), false).Content);
            }

            return OperationResult.CreateSuccessResult(list);
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteOneCoil(byte station, string address, bool value)
        {
            StringBuilder sb = new StringBuilder("#WCS");
            OperationResult append = AppendCoil(sb, address);
            if (!append.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(append);
            sb.Append(value ? '1' : '0');
            return PackPanasonicCommand(station, sb.ToString(), false);
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<List<byte[]>> BuildWriteCoils(byte station, string[] address, bool[] value)
        {
            if (address == null)
                return new OperationResult<List<byte[]>>("Parameter address can't be null");
            if (value == null)
                return new OperationResult<List<byte[]>>("Parameter value can't be null");
            if (address.Length != value.Length)
                return new OperationResult<List<byte[]>>("Parameter address and parameter value, length is not same!");
            List<byte[]> list = new List<byte[]>();
            List<string[]> splits_add = ProtocolBytes.ArraySplitByLength(address, 8);
            List<bool[]> splits_value = ProtocolBytes.ArraySplitByLength(value, 8);
            for (int i = 0; i < splits_add.Count; i++)
            {
                StringBuilder sb = new StringBuilder("#WCP");
                sb.Append(splits_add[i].Length.ToString()); // 다중 터치 포인트를 지정
                for (int j = 0; j < splits_add[i].Length; j++)
                {
                    OperationResult append = AppendCoil(sb, splits_add[i][j]);
                    if (!append.IsSuccess)
                        return OperationResult.CreateFailedResult<List<byte[]>>(append);
                    sb.Append(splits_value[i][j] ? '1' : '0');
                }

                list.Add(PackPanasonicCommand(station, sb.ToString(), false).Content);
            }

            return OperationResult.CreateSuccessResult(list);
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <param name = "isBit">비트 단위 요청이면 true입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<List<byte[]>> BuildReadCommand(byte station, string address, ushort length, bool isBit)
        {
            // 매개 변수 확인
            if (address == null)
                return new OperationResult<List<byte[]>>(ProtocolMessages.PanasonicAddressParameterCannotBeNull);
            // 주소 분석
            OperationResult<string, int> analysis = AnalysisAddress(address);
            if (!analysis.IsSuccess)
                return OperationResult.CreateFailedResult<List<byte[]>>(analysis);
            List<byte[]> list = new List<byte[]>();
            if (isBit)
            {
                length += (ushort)(analysis.Content2 % 16);
                analysis.Content2 -= analysis.Content2 % 16;
                int[] splits = ProtocolBytes.SplitIntegerToArray(length, 25 * 16);
                for (int i = 0; i < splits.Length; i++)
                {
                    int len = splits[i];
                    StringBuilder sb = new StringBuilder("#");
                    if (analysis.Content1 == "X" || analysis.Content1 == "Y" || analysis.Content1 == "R" || analysis.Content1 == "L")
                    {
                        sb.Append("RCC");
                        sb.Append(analysis.Content1);
                        int wordStart = analysis.Content2 / 16;
                        int wordFinish = (analysis.Content2 + len - 1) / 16;
                        sb.Append(wordStart.ToString("D4"));
                        sb.Append(wordFinish.ToString("D4"));
                        analysis.Content2 += len;
                    }
                    else
                        return new OperationResult<List<byte[]>>("Bit read only support X,Y,R,L");
                    list.Add(PackPanasonicCommand(station, sb.ToString(), false).Content);
                }

                return OperationResult.CreateSuccessResult(list);
            }
            else
            {
                int[] splits = ProtocolBytes.SplitIntegerToArray(length, 500);
                for (int i = 0; i < splits.Length; i++)
                {
                    int len = splits[i];
                    StringBuilder sb = new StringBuilder("#");
                    if (analysis.Content1 == "X" || analysis.Content1 == "Y" || analysis.Content1 == "R" || analysis.Content1 == "L")
                    {
                        sb.Append("RCC");
                        sb.Append(analysis.Content1);
                        int wordStart = analysis.Content2 / 16;
                        int wordFinish = (analysis.Content2 + (len - 1) * 16) / 16;
                        sb.Append(wordStart.ToString("D4"));
                        sb.Append(wordFinish.ToString("D4"));
                        analysis.Content2 += len * 16;
                    }
                    else if (analysis.Content1 == "D" || analysis.Content1 == "LD" || analysis.Content1 == "F")
                    {
                        sb.Append("RD");
                        sb.Append(analysis.Content1.Substring(0, 1));
                        sb.Append(analysis.Content2.ToString("D5"));
                        sb.Append((analysis.Content2 + len - 1).ToString("D5"));
                        analysis.Content2 += len;
                    }
                    else if (analysis.Content1 == "IX" || analysis.Content1 == "IY" || analysis.Content1 == "ID")
                    {
                        sb.Append("RD");
                        sb.Append(analysis.Content1);
                        sb.Append("000000000");
                        analysis.Content2 += len;
                    }
                    else if (analysis.Content1 == "C" || analysis.Content1 == "T")
                    {
                        sb.Append("RS");
                        sb.Append(analysis.Content2.ToString("D4"));
                        sb.Append((analysis.Content2 + len - 1).ToString("D4"));
                        analysis.Content2 += len;
                    }
                    else if (analysis.Content1 == "K" || analysis.Content1 == "S")
                    {
                        // 지나친 값과 목표값
                        sb.Append("R");
                        sb.Append(analysis.Content1);
                        sb.Append(analysis.Content2.ToString("D4"));
                        sb.Append((analysis.Content2 + len - 1).ToString("D4"));
                        analysis.Content2 += len;
                    }
                    else
                    {
                        return new OperationResult<List<byte[]>>(ProtocolMessages.NotSupportedDataType);
                    }

                    list.Add(PackPanasonicCommand(station, sb.ToString(), len > 27).Content);
                }
            }

            return OperationResult.CreateSuccessResult(list);
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "values">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteCommand(byte station, string address, byte[] values)
        {
            // 매개 변수 확인
            if (address == null)
                return new OperationResult<byte[]>(ProtocolMessages.PanasonicAddressParameterCannotBeNull);
            // 주소 분석
            OperationResult<string, int> analysis = AnalysisAddress(address);
            if (!analysis.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(analysis);
            // 짝수 길이를 확인합니다.
            values = ProtocolBytes.ArrayExpandToLengthEven(values);
            short length = (short)(values.Length / 2);
            StringBuilder sb = new StringBuilder("#");
            if (analysis.Content1 == "X" || analysis.Content1 == "Y" || analysis.Content1 == "R" || analysis.Content1 == "L")
            {
                sb.Append("WCC");
                sb.Append(analysis.Content1);
                int wordStart = analysis.Content2 / 16;
                int wordFinish = wordStart + length - 1;
                sb.Append(wordStart.ToString("D4"));
                sb.Append(wordFinish.ToString("D4"));
            }
            else if (analysis.Content1 == "D" || analysis.Content1 == "LD" || analysis.Content1 == "F")
            {
                sb.Append("WD");
                sb.Append(analysis.Content1.Substring(0, 1));
                sb.Append(analysis.Content2.ToString("D5"));
                sb.Append((analysis.Content2 + length - 1).ToString("D5"));
            }
            else if (analysis.Content1 == "IX" || analysis.Content1 == "IY" || analysis.Content1 == "ID")
            {
                sb.Append("WD");
                sb.Append(analysis.Content1);
                sb.Append(analysis.Content2.ToString("D9"));
                sb.Append((analysis.Content2 + length - 1).ToString("D9"));
            }
            else if (analysis.Content1 == "C" || analysis.Content1 == "T")
            {
                sb.Append("WS");
                sb.Append(analysis.Content2.ToString("D4"));
                sb.Append((analysis.Content2 + length - 1).ToString("D4"));
            }
            else if (analysis.Content1 == "K" || analysis.Content1 == "S")
            {
                // 초과
                sb.Append("W");
                sb.Append(analysis.Content1);
                sb.Append(analysis.Content2.ToString("D4"));
                sb.Append((analysis.Content2 + length - 1).ToString("D4"));
            }

            sb.Append(ProtocolBytes.ByteToHexString(values));
            return PackPanasonicCommand(station, sb.ToString(), sb.Length > 112);
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildReadPlcModel(byte station)
        {
            StringBuilder sb = new StringBuilder("#");
            sb.Append("RT");
            return PackPanasonicCommand(station, sb.ToString(), sb.Length > 112);
        }
    }
}
