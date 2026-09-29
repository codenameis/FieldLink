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

namespace FieldLink.PlcDrivers.AllenBradley
{
    /// <summary>AllenBradleyNet 요청 프레임을 생성합니다.</summary>
    public static class AllenBradleyNetCommandBuilder
    {
        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "command">포장할 명령 바이트입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackCommandWithHeader(CipCommandOptions context, byte[] command)
        {
            return AllenBradleyCommandBuilder.PackRequestHeader(context.CipCommand, context.SessionHandle, command);
        }

        /// <summary>라벨을 읽는 메시지를 지정하여, 라벨 주소를 수동으로 슬롯 번호를 지정할 수 있습니다. 예를 들어 slot=2;AAA</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "address">태그 이름의 주소</param>
        /// <param name = "length">배열 정보가, 배열이 아니라면 1</param>
        /// <returns>결과 객체를 포함하는 메시지 정보</returns>
        public static OperationResult<byte[]> BuildReadCommand(CipCommandOptions context, string[] address, ushort[] length)
        {
            if (address == null || length == null)
                return new OperationResult<byte[]>("address or length is null");
            if (address.Length != length.Length)
                return new OperationResult<byte[]>("address and length is not same array");
            try
            {
                byte slotTmp = context.Slot;
                List<byte[]> cips = new List<byte[]>();
                for (int i = 0; i < address.Length; i++)
                {
                    slotTmp = (byte)AddressParameters.ExtractParameter(ref address[i], "slot", context.Slot);
                    cips.Add(AllenBradleyCommandBuilder.PackRequsetRead(address[i], length[i]));
                }

                byte[] commandSpecificData = AllenBradleyCommandBuilder.PackCommandSpecificData(new byte[4], PackCommandService(context, context.PortSlot ?? new byte[] { 0x01, slotTmp }, cips.ToArray()));
                return OperationResult.CreateSuccessResult(commandSpecificData);
            }
            catch (Exception ex)
            {
                return new OperationResult<byte[]>("Address Wrong:" + ex.Message);
            }
        }

        /// <summary>다채로운 태그를 읽을 수 있는 메시지를 생성합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "address">태그 이름의 주소</param>
        /// <returns>결과 객체를 포함하는 메시지 정보</returns>
        public static OperationResult<byte[]> BuildReadCommand(CipCommandOptions context, string[] address)
        {
            if (address == null)
                return new OperationResult<byte[]>("address or length is null");
            ushort[] length = new ushort[address.Length];
            for (int i = 0; i < address.Length; i++)
            {
                length[i] = 1;
            }

            return BuildReadCommand(context, address, length);
        }

        /// <summary>문자 메시지 명령어 생성</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "address">태그 이름의 주소</param>
        /// <param name = "typeCode">데이터 형식</param>
        /// <param name = "data">원본 데이터</param>
        /// <param name = "length">배열의 경우, 배열의 길이는</param>
        /// <returns>결과 객체를 포함하는 메시지 정보</returns>
        public static OperationResult<List<byte[]>> BuildWriteCommand(CipCommandOptions context, string address, ushort typeCode, byte[] data, int length = 1)
        {
            try
            {
                byte slotTmp = (byte)AddressParameters.ExtractParameter(ref address, "slot", context.Slot);
                int writeSegment = AddressParameters.ExtractParameter(ref address, "x", -1);
                if (writeSegment == AllenBradleyDefinitions.CIP_WRITE_FRAGMENT || writeSegment == AllenBradleyDefinitions.CIP_READ_FRAGMENT)
                {
                    int startOffset = 0;
                    List<byte[]> list = ProtocolBytes.ArraySplitByLength(data, 474);
                    for (int i = 0; i < list.Count; i++)
                    {
                        byte[] cip = AllenBradleyCommandBuilder.PackRequestWriteSegment(address, typeCode, list[i], startOffset, length);
                        startOffset += list[i].Length;
                        byte[] commandSpecificData = AllenBradleyCommandBuilder.PackCommandSpecificData(new byte[4], PackCommandService(context, context.PortSlot ?? new byte[] { 0x01, slotTmp }, cip));
                        list[i] = commandSpecificData;
                    }

                    return OperationResult.CreateSuccessResult(list);
                }
                else
                {
                    byte[] cip = AllenBradleyCommandBuilder.PackRequestWrite(address, typeCode, data, length);
                    byte[] commandSpecificData = AllenBradleyCommandBuilder.PackCommandSpecificData(new byte[4], PackCommandService(context, context.PortSlot ?? new byte[] { 0x01, slotTmp }, cip));
                    return OperationResult.CreateSuccessResult(new List<byte[]>() { commandSpecificData });
                }
            }
            catch (Exception ex)
            {
                return new OperationResult<List<byte[]>>("Address Wrong:" + ex.Message);
            }
        }

        /// <summary>문자 메시지 명령어 생성</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "address">태그 이름의 주소</param>
        /// <param name = "data">부울 데이터</param>
        /// <returns>결과 객체를 포함하는 메시지 정보</returns>
        public static OperationResult<byte[]> BuildWriteCommand(CipCommandOptions context, string address, bool data)
        {
            try
            {
                byte slotTmp = (byte)AddressParameters.ExtractParameter(ref address, "slot", context.Slot);
                byte[] cip = AllenBradleyCommandBuilder.PackRequestWrite(address, data);
                byte[] commandSpecificData = AllenBradleyCommandBuilder.PackCommandSpecificData(new byte[4], PackCommandService(context, context.PortSlot ?? new byte[] { 0x01, slotTmp }, cip));
                return OperationResult.CreateSuccessResult(commandSpecificData);
            }
            catch (Exception ex)
            {
                return new OperationResult<byte[]>("Address Wrong:" + ex.Message);
            }
        }

        /// <summary>명령에 프로토콜 헤더와 필요한 검증 정보를 결합합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "portSlot">portSlot에 사용할 입력값입니다.</param>
        /// <param name = "cips">cips에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] PackCommandService(CipCommandOptions context, byte[] portSlot, params byte[][] cips)
        {
            if (context.MessageRouter != null)
                portSlot = context.MessageRouter.GetRouter();
            return AllenBradleyCommandBuilder.PackCommandService(portSlot, cips);
        }
    }
}
