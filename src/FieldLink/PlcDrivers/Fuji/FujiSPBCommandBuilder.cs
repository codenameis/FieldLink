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
using static FieldLink.PlcDrivers.Fuji.FujiSpbAddressParser;
using static FieldLink.PlcDrivers.Fuji.FujiSPBValueConverter;
using static FieldLink.PlcDrivers.Fuji.FujiSPBResponseParser;

namespace FieldLink.PlcDrivers.Fuji
{
    /// <summary>FujiSPB 요청 프레임을 생성합니다.</summary>
    public static class FujiSPBCommandBuilder
    {
        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildReadCommand(byte station, string address, ushort length)
        {
            station = (byte)AddressParameters.ExtractParameter(ref address, "s", station);
            OperationResult<FujiSpbAddress> addressAnalysis = FujiSpbAddress.ParseFrom(address);
            if (!addressAnalysis.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(addressAnalysis);
            return BuildReadCommand(station, addressAnalysis.Content, length);
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildReadCommand(byte station, FujiSpbAddress address, ushort length)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(':');
            stringBuilder.Append(station.ToString("X2"));
            stringBuilder.Append("09");
            stringBuilder.Append("FFFF");
            stringBuilder.Append("00");
            stringBuilder.Append("00");
            stringBuilder.Append(address.GetWordAddress());
            stringBuilder.Append(AnalysisIntegerAddress(length));
            stringBuilder.Append("\r\n");
            return OperationResult.CreateSuccessResult(Encoding.ASCII.GetBytes(stringBuilder.ToString()));
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <param name = "isBool">isBool에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildReadCommand(byte station, string[] address, ushort[] length, bool isBool)
        {
            if (address == null || length == null)
                return new OperationResult<byte[]>("Parameter address or length can't be null");
            if (address.Length != length.Length)
                return new OperationResult<byte[]>(ProtocolMessages.TwoParametersLengthIsNotSame);
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(':');
            stringBuilder.Append(station.ToString("X2"));
            stringBuilder.Append((6 + address.Length * 4).ToString("X2"));
            stringBuilder.Append("FFFF");
            stringBuilder.Append("00");
            stringBuilder.Append("04");
            stringBuilder.Append("00");
            stringBuilder.Append(address.Length.ToString("X2"));
            for (int i = 0; i < address.Length; i++)
            {
                station = (byte)AddressParameters.ExtractParameter(ref address[i], "s", station);
                OperationResult<FujiSpbAddress> addressAnalysis = FujiSpbAddress.ParseFrom(address[i]);
                if (!addressAnalysis.IsSuccess)
                    return OperationResult.CreateFailedResult<byte[]>(addressAnalysis);
                stringBuilder.Append(addressAnalysis.Content.TypeCode);
                stringBuilder.Append(length[i].ToString("X2"));
                stringBuilder.Append(AnalysisIntegerAddress(addressAnalysis.Content.AddressStart));
            }

            stringBuilder[1] = station.ToString("X2")[0];
            stringBuilder[2] = station.ToString("X2")[1];
            stringBuilder.Append("\r\n");
            return OperationResult.CreateSuccessResult(Encoding.ASCII.GetBytes(stringBuilder.ToString()));
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteByteCommand(byte station, string address, byte[] value)
        {
            station = (byte)AddressParameters.ExtractParameter(ref address, "s", station);
            OperationResult<FujiSpbAddress> addressAnalysis = FujiSpbAddress.ParseFrom(address);
            if (!addressAnalysis.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(addressAnalysis);
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(':');
            stringBuilder.Append(station.ToString("X2"));
            stringBuilder.Append("00");
            stringBuilder.Append("FFFF");
            stringBuilder.Append("01");
            stringBuilder.Append("00");
            stringBuilder.Append(addressAnalysis.Content.GetWordAddress());
            stringBuilder.Append(AnalysisIntegerAddress(value.Length / 2));
            stringBuilder.Append(value.ToHexString());
            stringBuilder[3] = ((stringBuilder.Length - 5) / 2).ToString("X2")[0];
            stringBuilder[4] = ((stringBuilder.Length - 5) / 2).ToString("X2")[1];
            stringBuilder.Append("\r\n");
            return OperationResult.CreateSuccessResult(Encoding.ASCII.GetBytes(stringBuilder.ToString()));
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteBoolCommand(byte station, string address, bool value)
        {
            station = (byte)AddressParameters.ExtractParameter(ref address, "s", station);
            OperationResult<FujiSpbAddress> addressAnalysis = FujiSpbAddress.ParseFrom(address);
            if (!addressAnalysis.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(addressAnalysis);
            if (address.StartsWith("X") || address.StartsWith("Y") || address.StartsWith("M") || address.StartsWith("L") || address.StartsWith("TC") || address.StartsWith("CC"))
            {
                if (address.IndexOf('.') < 0)
                {
                    // M1000의 경우, 주소 변환이 필요합니다.
                    addressAnalysis.Content.BitIndex = addressAnalysis.Content.AddressStart % 16;
                    addressAnalysis.Content.AddressStart = (ushort)(addressAnalysis.Content.AddressStart / 16);
                }
            }

            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(':');
            stringBuilder.Append(station.ToString("X2"));
            stringBuilder.Append("00");
            stringBuilder.Append("FFFF");
            stringBuilder.Append("01");
            stringBuilder.Append("02");
            stringBuilder.Append(addressAnalysis.Content.GetWriteBoolAddress());
            stringBuilder.Append(value ? "01" : "00");
            stringBuilder[3] = ((stringBuilder.Length - 5) / 2).ToString("X2")[0];
            stringBuilder[4] = ((stringBuilder.Length - 5) / 2).ToString("X2")[1];
            stringBuilder.Append("\r\n");
            return OperationResult.CreateSuccessResult(Encoding.ASCII.GetBytes(stringBuilder.ToString()));
        }
    }
}
