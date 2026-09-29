using FieldLink.PlcDrivers.AllenBradley;
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
using static FieldLink.PlcDrivers.Omron.OmronConnectedCipNetValueConverter;
using static FieldLink.PlcDrivers.Omron.OmronConnectedCipNetResponseParser;

namespace FieldLink.PlcDrivers.Omron
{
    /// <summary>OmronConnectedCipNet 요청 프레임을 생성합니다.</summary>
    public static class OmronConnectedCipNetCommandBuilder
    {
        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildReadCommand(OmronConnectedCipOptions context, string[] address, ushort[] length)
        {
            try
            {
                List<byte[]> cips = new List<byte[]>();
                for (int i = 0; i < address.Length; i++)
                {
                    cips.Add(AllenBradleyCommandBuilder.PackRequsetRead(address[i], length[i], true));
                }

                return OperationResult.CreateSuccessResult(NetworkConnectedCipCommandBuilder.PackCommandService(context.Connection, cips.ToArray()));
            }
            catch (Exception ex)
            {
                return new OperationResult<byte[]>("Address Wrong:" + ex.Message);
            }
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "typeCode">typeCode에 사용할 입력값입니다.</param>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> BuildWriteCommand(OmronConnectedCipOptions context, string address, ushort typeCode, byte[] data, int length = 1)
        {
            try
            {
                return OperationResult.CreateSuccessResult(NetworkConnectedCipCommandBuilder.PackCommandService(context.Connection, AllenBradleyCommandBuilder.PackRequestWrite(address, typeCode, data, length, true)));
            }
            catch (Exception ex)
            {
                return new OperationResult<byte[]>("Address Wrong:" + ex.Message);
            }
        }
    }
}
