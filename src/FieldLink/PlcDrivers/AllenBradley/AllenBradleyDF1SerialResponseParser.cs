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
using static FieldLink.PlcDrivers.AllenBradley.AllenBradleyDF1SerialCommandBuilder;

namespace FieldLink.PlcDrivers.AllenBradley
{
    /// <summary>AllenBradleyDF1Serial 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class AllenBradleyDF1SerialResponseParser
    {
        /// <summary>프로토콜 응답에서 데이터와 상태 정보를 추출합니다.</summary>
        /// <param name = "content">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> ExtractActualData(byte[] content)
        {
            try
            {
                int startIndex = -1;
                for (int i = 0; i < content.Length; i++)
                {
                    if (content[i] == 0x10 && content[i + 1] == 0x02)
                    {
                        startIndex = i + 2;
                        break;
                    }
                }

                if (startIndex < 0 || startIndex >= content.Length - 6)
                    return new OperationResult<byte[]>("Message must start with '10 02', source: " + content.ToHexString(' '));
                // 10 03의 끝을 찾아내기 위해 실제 데이터의 내용을 정제합니다
                MemoryStream ms = new MemoryStream();
                for (int i = startIndex; i < content.Length - 1; i++)
                {
                    if (content[i] == 0x10 && content[i + 1] == 0x10)
                    {
                        ms.WriteByte(content[i]);
                        i++;
                        continue;
                    }

                    if (content[i] == 0x10 && content[i + 1] == 0x03)
                    {
                        break;
                    }

                    ms.WriteByte(content[i]);
                }

                content = ms.ToArray();
                if (content[3] == 0xF0)
                    return new OperationResult<byte[]>(GetExtStatusDescription(content[6]));
                if (content[3] != 0x00)
                    return new OperationResult<byte[]>(GetStatusDescription(content[3]));
                if (content.Length > 6)
                    return OperationResult.CreateSuccessResult(content.RemoveBegin(6));
                else
                    return OperationResult.CreateSuccessResult(new byte[0]);
            }
            catch (Exception ex)
            {
                return new OperationResult<byte[]>(ex.Message + " Source:" + content.ToHexString(' '));
            }
        }

        /// <summary>장치 오류 또는 상태 코드에 대응하는 설명을 반환합니다.</summary>
        /// <param name = "code">code에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string GetStatusDescription(byte code)
        {
            byte low = (byte)(code & 0x0f);
            byte hig = (byte)(code & 0xf0);
            switch (low)
            {
                case 0x01:
                    return "DST node is out of buffer space";
                case 0x02:
                    return "Cannot guarantee delivery: link layer(The remote node specified does not ACK command.)";
                case 0x03:
                    return "Duplicate token holder detected";
                case 0x04:
                    return "Local port is disconnected";
                case 0x05:
                    return "Application layer timed out waiting for a response";
                case 0x06:
                    return "Duplicate node detected";
                case 0x07:
                    return "Station is offline";
                case 0x08:
                    return "Hardware fault";
            }

            switch (hig)
            {
                case 0x10:
                    return "Illegal command or format";
                case 0x20:
                    return "Host has a problem and will not communicate";
                case 0x30:
                    return "Remote node host is missing, disconnected, or shut down";
                case 0x40:
                    return "Host could not complete function due to hardware fault";
                case 0x50:
                    return "Addressing problem or memory protect rungs";
                case 0x60:
                    return "Function not allowed due to command protection selection";
                case 0x70:
                    return "Processor is in Program mode";
                case 0x80:
                    return "Compatibility mode file missing or communication zone problem";
                case 0x90:
                    return "Remote node cannot buffer command";
                case 0xA0:
                    return "Wait ACK (1775KA buffer full)";
                case 0xB0:
                    return "Remote node problem due to download";
                case 0xC0:
                    return "Wait ACK (1775KA buffer full)";
                case 0xF0:
                    return "Error code in the EXT STS byte";
            }

            return ProtocolMessages.UnknownError;
        }

        /// <summary>장치 오류 또는 상태 코드에 대응하는 설명을 반환합니다.</summary>
        /// <param name = "code">code에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string GetExtStatusDescription(byte code)
        {
            switch (code)
            {
                case 0x01:
                    return "A field has an illegal value";
                case 0x02:
                    return "Less levels specified in address than minimum for any address";
                case 0x03:
                    return "More levels specified in address than system supports";
                case 0x04:
                    return "Symbol not found";
                case 0x05:
                    return "Symbol is of improper format";
                case 0x06:
                    return "Address doesn’t point to something usable";
                case 0x07:
                    return "File is wrong size";
                case 0x08:
                    return "Cannot complete request, situation has changed since the start of the command";
                case 0x09:
                    return "Data or file is too large";
                case 0x0A:
                    return "Transaction size plus word address is too large";
                case 0x0B:
                    return "Access denied, improper privilege";
                case 0x0C:
                    return "Condition cannot be generated  resource is not available";
                case 0x0D:
                    return "Condition already exists  resource is already available";
                case 0x0E:
                    return "Command cannot be executed";
                case 0x0F:
                    return "Histogram overflow";
                case 0x10:
                    return "No access";
                case 0x11:
                    return "Illegal data type";
                case 0x12:
                    return "Invalid parameter or invalid data";
                case 0x13:
                    return "Address reference exists to deleted area";
                case 0x14:
                    return "Command execution failure for unknown reason; possible PLC3 histogram overflow";
                case 0x15:
                    return "Data conversion error";
                case 0x16:
                    return "Scanner not able to communicate with 1771 rack adapter";
                case 0x17:
                    return "Type mismatch";
                case 0x18:
                    return "1771 module response was not valid";
                case 0x19:
                    return "Duplicated label";
                case 0x1A:
                    return "File is open; another node owns it";
                case 0x1B:
                    return "Another node is the program owner";
                case 0x1C:
                    return "Reserved";
                case 0x1D:
                    return "Reserved";
                case 0x1E:
                    return "Data table element protection violation";
                case 0x1F:
                    return "Temporary internal problem";
                case 0x22:
                    return "Remote rack fault";
                case 0x23:
                    return "Timeout";
                case 0x24:
                    return "Unknown error";
                default:
                    return ProtocolMessages.UnknownError;
            }
        }
    }
}
