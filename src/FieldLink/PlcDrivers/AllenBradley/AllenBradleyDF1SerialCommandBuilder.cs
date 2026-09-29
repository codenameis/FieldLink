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
using static FieldLink.PlcDrivers.AllenBradley.AllenBradleyDF1SerialValueConverter;
using static FieldLink.PlcDrivers.AllenBradley.AllenBradleyDF1SerialResponseParser;

namespace FieldLink.PlcDrivers.AllenBradley
{
    /// <summary>AllenBradleyDF1Serial 요청 프레임을 생성합니다.</summary>
    public static class AllenBradleyDF1SerialCommandBuilder
    {
        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "tns">tns에 사용할 입력값입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildProtectedTypedLogicalReadWithThreeAddressFields(int tns, string address, ushort length)
        {
            var analysis = AllenBradleySlcAddress.ParseFrom(address);
            if (!analysis.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(analysis);
            // AB PLC DF1 매뉴얼 페이지 104
            MemoryStream ms = new MemoryStream();
            ms.WriteByte(0x0F); // 명령
            ms.WriteByte(0x00); // STS
            ms.WriteByte(BitConverter.GetBytes(tns)[0]);
            ms.WriteByte(BitConverter.GetBytes(tns)[1]);
            ms.WriteByte(0xA2); // 기능
            ms.WriteByte(BitConverter.GetBytes(length)[0]); // 바이트 길이
            AddLengthToMemoryStream(ms, analysis.Content.DbBlock); // 파일 번호
            ms.WriteByte(analysis.Content.DataCode); // 파일 형식
            AddLengthToMemoryStream(ms, (ushort)analysis.Content.AddressStart); // 요소 번호
            AddLengthToMemoryStream(ms, 0x00); // 하위 원소 번호
            return OperationResult.CreateSuccessResult(ms.ToArray());
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "dstNode">dstNode에 사용할 입력값입니다.</param>
        /// <param name = "srcNode">srcNode에 사용할 입력값입니다.</param>
        /// <param name = "tns">tns에 사용할 입력값입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildProtectedTypedLogicalReadWithThreeAddressFields(byte dstNode, byte srcNode, int tns, string address, ushort length)
        {
            var build = BuildProtectedTypedLogicalReadWithThreeAddressFields(tns, address, length);
            if (!build.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(build);
            return OperationResult.CreateSuccessResult(ProtocolBytes.SpliceArray(new byte[] { dstNode, srcNode }, build.Content));
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "tns">tns에 사용할 입력값입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildProtectedTypedLogicalWriteWithThreeAddressFields(int tns, string address, byte[] data)
        {
            var analysis = AllenBradleySlcAddress.ParseFrom(address);
            if (!analysis.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(analysis);
            // AB PLC DF1 매뉴얼 페이지 104
            MemoryStream ms = new MemoryStream();
            ms.WriteByte(0x0F); // 명령
            ms.WriteByte(0x00); // STS
            ms.WriteByte(BitConverter.GetBytes(tns)[0]);
            ms.WriteByte(BitConverter.GetBytes(tns)[1]);
            ms.WriteByte(0xAA); // 기능
            ms.WriteByte(BitConverter.GetBytes(data.Length)[0]); // 바이트 길이
            AddLengthToMemoryStream(ms, analysis.Content.DbBlock); // 파일 번호
            ms.WriteByte(analysis.Content.DataCode); // 파일 형식
            AddLengthToMemoryStream(ms, (ushort)analysis.Content.AddressStart); // 요소 번호
            AddLengthToMemoryStream(ms, 0x00); // 하위 원소 번호
            ms.Write(data);
            return OperationResult.CreateSuccessResult(ms.ToArray());
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "dstNode">dstNode에 사용할 입력값입니다.</param>
        /// <param name = "srcNode">srcNode에 사용할 입력값입니다.</param>
        /// <param name = "tns">tns에 사용할 입력값입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildProtectedTypedLogicalWriteWithThreeAddressFields(byte dstNode, byte srcNode, int tns, string address, byte[] data)
        {
            var build = BuildProtectedTypedLogicalWriteWithThreeAddressFields(tns, address, data);
            if (!build.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(build);
            return OperationResult.CreateSuccessResult(ProtocolBytes.SpliceArray(new byte[] { dstNode, srcNode }, build.Content));
        }

        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <param name = "tns">tns에 사용할 입력값입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "bitIndex">bitIndex에 사용할 입력값입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildProtectedTypedLogicalMaskWithThreeAddressFields(int tns, string address, int bitIndex, bool value)
        {
            int mask = 1 << bitIndex;
            var analysis = AllenBradleySlcAddress.ParseFrom(address);
            if (!analysis.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(analysis);
            MemoryStream ms = new MemoryStream();
            ms.WriteByte(0x0F); // 명령
            ms.WriteByte(0x00); // STS
            ms.WriteByte(BitConverter.GetBytes(tns)[0]);
            ms.WriteByte(BitConverter.GetBytes(tns)[1]);
            ms.WriteByte(0xAB); // 기능
            ms.WriteByte(0x02); // 바이트 길이
            AddLengthToMemoryStream(ms, analysis.Content.DbBlock); // 파일 번호
            ms.WriteByte(analysis.Content.DataCode); // 파일 형식
            AddLengthToMemoryStream(ms, (ushort)analysis.Content.AddressStart); // 요소 번호
            AddLengthToMemoryStream(ms, 0x00); // 하위 원소 번호
            ms.WriteByte(BitConverter.GetBytes(mask)[0]);
            ms.WriteByte(BitConverter.GetBytes(mask)[1]);
            if (value)
            {
                ms.WriteByte(BitConverter.GetBytes(mask)[0]);
                ms.WriteByte(BitConverter.GetBytes(mask)[1]);
            }
            else
            {
                ms.WriteByte(0x00);
                ms.WriteByte(0x00);
            }

            return OperationResult.CreateSuccessResult(ms.ToArray());
        }

        /// <summary>패키지 명령의 동작, 추가 국번은 완전한 데이터 콘텐츠로 패키지, 명령 내용은 원본 명령으로, 패키지 후 0x10의 값을 자동으로 보충</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "station">국번 정보</param>
        /// <param name = "command">명령이 발송될 때까지 기다립니다.</param>
        /// <returns>패키지된 데이터의 내용</returns>
        public static byte[] PackCommand(Df1FrameOptions context, byte station, byte[] command)
        {
            byte[] check = CalculateCheckResult(context, station, command);
            MemoryStream ms = new MemoryStream();
            ms.WriteByte(0x10);
            ms.WriteByte(0x01);
            ms.WriteByte(station);
            if (station == 0x10)
                ms.WriteByte(station);
            ms.WriteByte(0x10);
            ms.WriteByte(0x02);
            for (int i = 0; i < command.Length; i++)
            {
                ms.WriteByte(command[i]);
                if (command[i] == 0x10)
                    ms.WriteByte(command[i]);
            }

            ms.WriteByte(0x10);
            ms.WriteByte(0x03);
            ms.Write(check, 0, check.Length);
            return ms.ToArray();
        }
    }
}
