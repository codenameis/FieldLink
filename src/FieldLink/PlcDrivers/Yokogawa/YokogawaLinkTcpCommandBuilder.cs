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
using static FieldLink.PlcDrivers.Yokogawa.YokogawaLinkTcpResponseParser;

namespace FieldLink.PlcDrivers.Yokogawa
{
    /// <summary>YokogawaLinkTcp 요청 프레임을 생성합니다.</summary>
    public static class YokogawaLinkTcpCommandBuilder
    {
        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "cpu">cpu에 사용할 입력값입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <param name = "isBit">비트 단위 요청이면 true입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<List<byte[]>> BuildReadCommand(byte cpu, string address, ushort length, bool isBit)
        {
            cpu = (byte)AddressParameters.ExtractParameter(ref address, "cpu", cpu);
            OperationResult<YokogawaLinkAddress> analysis = YokogawaLinkAddress.ParseFrom(address, length);
            if (!analysis.IsSuccess)
                return OperationResult.CreateFailedResult<List<byte[]>>(analysis);
            OperationResult<int[], int[]> splits;
            if (isBit)
                splits = AddressParameters.SplitReadLength(analysis.Content.AddressStart, length, 256);
            else
                splits = AddressParameters.SplitReadLength(analysis.Content.AddressStart, length, 64);
            List<byte[]> lists = new List<byte[]>();
            for (int i = 0; i < splits.Content1.Length; i++)
            {
                analysis.Content.AddressStart = splits.Content1[i];
                byte[] buffer = new byte[12];
                buffer[0] = isBit ? (byte)0x01 : (byte)0x11;
                buffer[1] = cpu;
                buffer[2] = 0x00;
                buffer[3] = 0x08;
                analysis.Content.GetAddressBinaryContent().CopyTo(buffer, 4);
                buffer[10] = BitConverter.GetBytes(splits.Content2[i])[1];
                buffer[11] = BitConverter.GetBytes(splits.Content2[i])[0];
                lists.Add(buffer);
            }

            return OperationResult.CreateSuccessResult(lists);
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "cpu">cpu에 사용할 입력값입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "isBit">비트 단위 요청이면 true입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<List<byte[]>> BuildReadRandomCommand(byte cpu, string[] address, bool isBit)
        {
            List<string[]> splits = ProtocolBytes.ArraySplitByLength(address, 32);
            List<byte[]> lists = new List<byte[]>();
            foreach (string[] addressSplit in splits)
            {
                byte[] buffer = new byte[6 + 6 * addressSplit.Length];
                buffer[0] = isBit ? (byte)0x04 : (byte)0x14;
                buffer[1] = cpu;
                buffer[2] = BitConverter.GetBytes(buffer.Length - 4)[1];
                buffer[3] = BitConverter.GetBytes(buffer.Length - 4)[0];
                buffer[4] = BitConverter.GetBytes(addressSplit.Length)[1];
                buffer[5] = BitConverter.GetBytes(addressSplit.Length)[0];
                for (int i = 0; i < addressSplit.Length; i++)
                {
                    buffer[1] = (byte)AddressParameters.ExtractParameter(ref addressSplit[i], "cpu", cpu);
                    OperationResult<YokogawaLinkAddress> analysis = YokogawaLinkAddress.ParseFrom(addressSplit[i], 1);
                    if (!analysis.IsSuccess)
                        return OperationResult.CreateFailedResult<List<byte[]>>(analysis);
                    analysis.Content.GetAddressBinaryContent().CopyTo(buffer, 6 * i + 6);
                }

                lists.Add(buffer);
            }

            return OperationResult.CreateSuccessResult(lists);
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "cpu">cpu에 사용할 입력값입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteBoolCommand(byte cpu, string address, bool[] value)
        {
            cpu = (byte)AddressParameters.ExtractParameter(ref address, "cpu", cpu);
            OperationResult<YokogawaLinkAddress> analysis = YokogawaLinkAddress.ParseFrom(address, 0);
            if (!analysis.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(analysis);
            byte[] buffer = new byte[12 + value.Length];
            buffer[0] = 0x02;
            buffer[1] = cpu;
            buffer[2] = 0x00;
            buffer[3] = (byte)(0x08 + value.Length);
            analysis.Content.GetAddressBinaryContent().CopyTo(buffer, 4);
            buffer[10] = BitConverter.GetBytes(value.Length)[1];
            buffer[11] = BitConverter.GetBytes(value.Length)[0];
            for (int i = 0; i < value.Length; i++)
            {
                buffer[12 + i] = value[i] ? (byte)0x01 : (byte)0x00;
            }

            return OperationResult.CreateSuccessResult(buffer);
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "cpu">cpu에 사용할 입력값입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteRandomBoolCommand(byte cpu, string[] address, bool[] value)
        {
            if (address.Length != value.Length)
                return new OperationResult<byte[]>(ProtocolMessages.TwoParametersLengthIsNotSame);
            byte[] buffer = new byte[6 + address.Length * 8 - 1];
            buffer[0] = 0x05;
            buffer[1] = cpu;
            buffer[2] = BitConverter.GetBytes(buffer.Length - 4)[1];
            buffer[3] = BitConverter.GetBytes(buffer.Length - 4)[0];
            buffer[4] = BitConverter.GetBytes(address.Length)[1];
            buffer[5] = BitConverter.GetBytes(address.Length)[0];
            for (int i = 0; i < address.Length; i++)
            {
                buffer[1] = (byte)AddressParameters.ExtractParameter(ref address[i], "cpu", cpu);
                OperationResult<YokogawaLinkAddress> analysis = YokogawaLinkAddress.ParseFrom(address[i], 0);
                if (!analysis.IsSuccess)
                    return OperationResult.CreateFailedResult<byte[]>(analysis);
                analysis.Content.GetAddressBinaryContent().CopyTo(buffer, 6 + 8 * i);
                buffer[12 + 8 * i] = value[i] ? (byte)0x01 : (byte)0x00;
            }

            return OperationResult.CreateSuccessResult(buffer);
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "cpu">cpu에 사용할 입력값입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteWordCommand(byte cpu, string address, byte[] value)
        {
            cpu = (byte)AddressParameters.ExtractParameter(ref address, "cpu", cpu);
            OperationResult<YokogawaLinkAddress> analysis = YokogawaLinkAddress.ParseFrom(address, 0);
            if (!analysis.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(analysis);
            byte[] buffer = new byte[12 + value.Length];
            buffer[0] = 0x12;
            buffer[1] = cpu;
            buffer[2] = 0x00;
            buffer[3] = (byte)(0x08 + value.Length);
            analysis.Content.GetAddressBinaryContent().CopyTo(buffer, 4);
            buffer[10] = BitConverter.GetBytes(value.Length / 2)[1];
            buffer[11] = BitConverter.GetBytes(value.Length / 2)[0];
            value.CopyTo(buffer, 12);
            return OperationResult.CreateSuccessResult(buffer);
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "cpu">cpu에 사용할 입력값입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteRandomWordCommand(byte cpu, string[] address, byte[] value)
        {
            if (address.Length * 2 != value.Length)
                return new OperationResult<byte[]>(ProtocolMessages.TwoParametersLengthIsNotSame);
            byte[] buffer = new byte[6 + address.Length * 8];
            buffer[0] = 0x15;
            buffer[1] = cpu;
            buffer[2] = BitConverter.GetBytes(buffer.Length - 4)[1];
            buffer[3] = BitConverter.GetBytes(buffer.Length - 4)[0];
            buffer[4] = BitConverter.GetBytes(address.Length)[1];
            buffer[5] = BitConverter.GetBytes(address.Length)[0];
            for (int i = 0; i < address.Length; i++)
            {
                buffer[1] = (byte)AddressParameters.ExtractParameter(ref address[i], "cpu", cpu);
                OperationResult<YokogawaLinkAddress> analysis = YokogawaLinkAddress.ParseFrom(address[i], 0);
                if (!analysis.IsSuccess)
                    return OperationResult.CreateFailedResult<byte[]>(analysis);
                analysis.Content.GetAddressBinaryContent().CopyTo(buffer, 6 + 8 * i);
                buffer[12 + 8 * i] = value[i * 2 + 0];
                buffer[13 + 8 * i] = value[i * 2 + 1];
            }

            return OperationResult.CreateSuccessResult(buffer);
        }

        /// <summary>운전 시작 명령 프레임을 생성합니다.</summary>
        /// <param name = "cpu">cpu에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildStartCommand(byte cpu)
        {
            return OperationResult.CreateSuccessResult(new byte[] { 0x45, cpu, 0x00, 0x00 });
        }

        /// <summary>운전 정지 명령 프레임을 생성합니다.</summary>
        /// <param name = "cpu">cpu에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildStopCommand(byte cpu)
        {
            return OperationResult.CreateSuccessResult(new byte[] { 0x46, cpu, 0x00, 0x00 });
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "cpu">cpu에 사용할 입력값입니다.</param>
        /// <param name = "moduleUnit">moduleUnit에 사용할 입력값입니다.</param>
        /// <param name = "moduleSlot">moduleSlot에 사용할 입력값입니다.</param>
        /// <param name = "dataPosition">dataPosition에 사용할 입력값입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static List<byte[]> BuildReadSpecialModule(byte cpu, byte moduleUnit, byte moduleSlot, ushort dataPosition, ushort length)
        {
            List<byte[]> lists = new List<byte[]>();
            OperationResult<int[], int[]> splits = AddressParameters.SplitReadLength(dataPosition, length, 64);
            for (int i = 0; i < splits.Content1.Length; i++)
            {
                // length의 기본 단위는 64입니다.
                byte[] buffer = new byte[10];
                buffer[0] = 0x31;
                buffer[1] = cpu;
                buffer[2] = BitConverter.GetBytes(buffer.Length - 4)[1];
                buffer[3] = BitConverter.GetBytes(buffer.Length - 4)[0];
                buffer[4] = moduleUnit;
                buffer[5] = moduleSlot;
                buffer[6] = BitConverter.GetBytes(splits.Content1[i])[1];
                buffer[7] = BitConverter.GetBytes(splits.Content1[i])[0];
                buffer[8] = BitConverter.GetBytes(splits.Content2[i])[1];
                buffer[9] = BitConverter.GetBytes(splits.Content2[i])[0];
                lists.Add(buffer);
            }

            return lists;
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "cpu">cpu에 사용할 입력값입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<List<byte[]>> BuildReadSpecialModule(byte cpu, string address, ushort length)
        {
            if (address.StartsWith("Special:") || address.StartsWith("special:"))
            {
                address = address.Substring(8);
                cpu = (byte)AddressParameters.ExtractParameter(ref address, "cpu", cpu);
                OperationResult<int> unit = AddressParameters.ExtractParameter(ref address, "unit");
                if (!unit.IsSuccess)
                    return OperationResult.CreateFailedResult<List<byte[]>>(unit);
                OperationResult<int> slot = AddressParameters.ExtractParameter(ref address, "slot");
                if (!slot.IsSuccess)
                    return OperationResult.CreateFailedResult<List<byte[]>>(slot);
                try
                {
                    return OperationResult.CreateSuccessResult(BuildReadSpecialModule(cpu, (byte)unit.Content, (byte)slot.Content, ushort.Parse(address), length));
                }
                catch (Exception ex)
                {
                    return new OperationResult<List<byte[]>>("Address format wrong: " + ex.Message);
                }
            }
            else
            {
                return new OperationResult<List<byte[]>>("Special module address must start with Special:");
            }
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "cpu">cpu에 사용할 입력값입니다.</param>
        /// <param name = "moduleUnit">moduleUnit에 사용할 입력값입니다.</param>
        /// <param name = "moduleSlot">moduleSlot에 사용할 입력값입니다.</param>
        /// <param name = "dataPosition">dataPosition에 사용할 입력값입니다.</param>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildWriteSpecialModule(byte cpu, byte moduleUnit, byte moduleSlot, ushort dataPosition, byte[] data)
        {
            // length의 기본 단위는 64입니다.
            byte[] buffer = new byte[10 + data.Length];
            buffer[0] = 0x32;
            buffer[1] = cpu;
            buffer[2] = BitConverter.GetBytes(buffer.Length - 4)[1];
            buffer[3] = BitConverter.GetBytes(buffer.Length - 4)[0];
            buffer[4] = moduleUnit;
            buffer[5] = moduleSlot;
            buffer[6] = BitConverter.GetBytes(dataPosition)[1];
            buffer[7] = BitConverter.GetBytes(dataPosition)[0];
            buffer[8] = BitConverter.GetBytes(data.Length / 2)[1];
            buffer[9] = BitConverter.GetBytes(data.Length / 2)[0];
            data.CopyTo(buffer, 10);
            return buffer;
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "cpu">cpu에 사용할 입력값입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteSpecialModule(byte cpu, string address, byte[] data)
        {
            OperationResult<List<byte[]>> analysis = BuildReadSpecialModule(cpu, address, 0);
            if (!analysis.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(analysis);
            byte[] buffer = new byte[10 + data.Length];
            buffer[0] = 0x32;
            buffer[1] = analysis.Content[0][1];
            buffer[2] = BitConverter.GetBytes(buffer.Length - 4)[1];
            buffer[3] = BitConverter.GetBytes(buffer.Length - 4)[0];
            buffer[4] = analysis.Content[0][4];
            buffer[5] = analysis.Content[0][5];
            buffer[6] = analysis.Content[0][6];
            buffer[7] = analysis.Content[0][7];
            buffer[8] = BitConverter.GetBytes(data.Length / 2)[1];
            buffer[9] = BitConverter.GetBytes(data.Length / 2)[0];
            data.CopyTo(buffer, 10);
            return OperationResult.CreateSuccessResult(buffer);
        }
    }
}
