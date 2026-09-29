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
using static FieldLink.PlcDrivers.LSIS.LSFastEnetAddressParser;
using static FieldLink.PlcDrivers.LSIS.LSFastEnetValueConverter;
using static FieldLink.PlcDrivers.LSIS.LSFastEnetCommandBuilder;
using static FieldLink.PlcDrivers.LSIS.LSFastEnetDefinitions;

namespace FieldLink.PlcDrivers.LSIS
{
    /// <summary>LSFastEnet 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class LSFastEnetResponseParser
    {
        /// <summary>장치 오류 또는 상태 코드에 대응하는 설명을 반환합니다.</summary>
        /// <param name = "code">code에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string GetErrorDesciption(byte code)
        {
            switch (code)
            {
                case 0:
                    return "Normal";
                case 1:
                    return "Physical layer error (TX, RX unavailable)";
                case 3:
                    return "There is no identifier of Function Block to receive in communication channel";
                case 4:
                    return "Mismatch of data type";
                case 5:
                    return "Reset is received from partner station";
                case 6:
                    return "Communication instruction of partner station is not ready status";
                case 7:
                    return "Device status of remote station is not desirable status";
                case 8:
                    return "Access to some target is not available";
                case 9:
                    return "Can’ t deal with communication instruction of partner station by too many reception";
                case 10:
                    return "Time Out error";
                case 11:
                    return "Structure error";
                case 12:
                    return "Abort";
                case 13:
                    return "Reject(local/remote)";
                case 14:
                    return "Communication channel establishment error (Connect/Disconnect)";
                case 15:
                    return "High speed communication and connection service error";
                case 33:
                    return "Can’t find variable identifier";
                case 34:
                    return "Address error";
                case 50:
                    return "Response error";
                case 113:
                    return "Object Access Unsupported";
                case 187:
                    return "Unknown error code (communication code of other company) is received";
                default:
                    return "Unknown error";
            }
        }

        /// <summary>실제 데이터 컨텐츠를 반환하고, 읽기 및 쓰기 반환을 지원합니다.</summary>
        /// <param name = "context">호출자가 제공하는 프로토콜 설정과 식별자입니다.</param>
        /// <param name = "response">응답 데이터</param>
        /// <returns>실제 데이터</returns>
        public static OperationResult<byte[]> ExtractActualData(FastEnetFrameOptions context, byte[] response)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            // XGL-EFMTB V3.5 §7.1: 20-byte header, little-endian application fields.
            if (response == null || response.Length < 28)
                return new OperationResult<byte[]>("Incomplete LS FastEnet response.");
            byte[] company = Encoding.ASCII.GetBytes(context.CompanyID ?? string.Empty);
            if (company.Length == 0 || company.Length > 10)
                return new OperationResult<byte[]>("Invalid LS company identifier.");
            for (int i = 0; i < 10; i++)
            {
                if (response[i] != (i < company.Length ? company[i] : 0))
                    return new OperationResult<byte[]>("LS company identifier does not match.");
            }
            if (response[13] != 0x11 || BitConverter.ToUInt16(response, 16) != response.Length - 20)
                return new OperationResult<byte[]>("Invalid LS response source or application length.");
            if (response[19] != 0)
            {
                int checksum = 0;
                for (int i = 0; i < 19; i++)
                    checksum += response[i];
                if (response[19] != (byte)checksum)
                    return new OperationResult<byte[]>("LS header checksum does not match.");
            }
            ushort command = BitConverter.ToUInt16(response, 20);
            ushort dataType = BitConverter.ToUInt16(response, 22);
            if (command != 0x55 && command != 0x59)
                return new OperationResult<byte[]>(ProtocolMessages.NotSupportedFunction);
            if (dataType > 4 && dataType != 0x14)
                return new OperationResult<byte[]>("Invalid LS response data type.");

            if (BitConverter.ToUInt16(response, 26) != 0)
            {
                // V2.30 illustrates one-byte NAKs; V3.5 defines a two-byte error code.
                if (response.Length != 29 && response.Length != 30)
                    return new OperationResult<byte[]>("Invalid LS error response length.");
                int error = response.Length == 30 ? BitConverter.ToUInt16(response, 28) : response[28];
                UpdateCpuStatus(context, response);
                string description = error <= byte.MaxValue ? GetErrorDesciption((byte)error) : "Unknown error";
                return new OperationResult<byte[]>(error, "Error 0x" + error.ToString("X4") + ": " + description);
            }
            if (response.Length < 30)
                return new OperationResult<byte[]>("Missing LS response block count.");
            ushort blockCount = BitConverter.ToUInt16(response, 28);
            if (blockCount == 0 || blockCount > 16 || (dataType == 0x14 && blockCount != 1))
                return new OperationResult<byte[]>("Invalid LS response block count.");
            if (command == 0x59)
            {
                if (response.Length != 30)
                    return new OperationResult<byte[]>("Unexpected data in LS write acknowledgement.");
                UpdateCpuStatus(context, response);
                return OperationResult.CreateSuccessResult(new byte[0]);
            }

            int offset = 30;
            var content = new List<byte>();
            for (int block = 0; block < blockCount; block++)
            {
                if (response.Length - offset < 2)
                    return new OperationResult<byte[]>("Missing LS read block length.");
                int length = BitConverter.ToUInt16(response, offset);
                offset += 2;
                if (length > response.Length - offset)
                    return new OperationResult<byte[]>("Truncated LS read block.");
                for (int i = 0; i < length; i++)
                    content.Add(response[offset + i]);
                offset += length;
            }
            if (offset != response.Length)
                return new OperationResult<byte[]>("Unexpected trailing data in LS response.");
            UpdateCpuStatus(context, response);
            return OperationResult.CreateSuccessResult(content.ToArray());
        }

        private static void UpdateCpuStatus(FastEnetFrameOptions context, byte[] response)
        {
            ushort plcInfo = BitConverter.ToUInt16(response, 10);
            switch (plcInfo & 0x3F)
            {
                case 1:
                    context.CpuType = "XGK/R-CPUH";
                    break;
                case 2:
                    context.CpuType = "XGK-CPUS";
                    break;
                case 4:
                    context.CpuType = "XGK-CPUE";
                    break;
                case 5:
                    context.CpuType = "XGK/I-CPUU";
                    break;
                case 6:
                    context.CpuType = "XGB/XBCU";
                    break;
            }
            context.CpuError = (plcInfo & 0x80) != 0;
            if ((plcInfo & 0x100) != 0)
                context.LSCpuStatus = LSCpuStatus.RUN;
            if ((plcInfo & 0x200) != 0)
                context.LSCpuStatus = LSCpuStatus.STOP;
            if ((plcInfo & 0x400) != 0)
                context.LSCpuStatus = LSCpuStatus.ERROR;
            if ((plcInfo & 0x800) != 0)
                context.LSCpuStatus = LSCpuStatus.DEBUG;
        }
    }
}
