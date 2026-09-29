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
using static FieldLink.PlcDrivers.Turck.ReaderNetResponseParser;
using static FieldLink.PlcDrivers.Turck.ReaderNetValueConverter;
using static FieldLink.PlcDrivers.Turck.ReaderNetCommandBuilder;

namespace FieldLink.PlcDrivers.Turck
{
    /// <summary>ReaderNet 주소를 프로토콜의 영역 코드와 위치로 변환합니다.</summary>
    public static class ReaderNetAddressParser
    {
        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "isBit">비트 단위 요청이면 true입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<ushort> ParseAddress(string address, bool isBit)
        {
            try
            {
                if (!isBit)
                    return OperationResult.CreateSuccessResult(ushort.Parse(address));
                else
                {
                    if (address.IndexOf('.') < 0)
                    {
                        return OperationResult.CreateSuccessResult(ushort.Parse(address));
                    }
                    else
                    {
                        string[] splits = address.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
                        return OperationResult.CreateSuccessResult((ushort)(int.Parse(splits[0]) * 8 + int.Parse(splits[1])));
                    }
                }
            }
            catch (Exception ex)
            {
                return new OperationResult<ushort>("Address input wrong, reason: " + ex.Message);
            }
        }

        /// <summary>CalculateBlockAddress 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <param name = "bytesOfBlock">bytesOfBlock에 사용할 입력값입니다.</param>
        /// <param name = "startBlock">startBlock에 사용할 입력값입니다.</param>
        /// <param name = "lengthOfBlock">lengthOfBlock에 사용할 입력값입니다.</param>
        internal static void CalculateBlockAddress(ushort address, ushort length, byte bytesOfBlock, out byte startBlock, out byte lengthOfBlock)
        {
            startBlock = (byte)(address / bytesOfBlock);
            int endBlock = (byte)((address + length - 1) / bytesOfBlock);
            lengthOfBlock = (byte)(endBlock - startBlock + 1);
        }
    }
}
