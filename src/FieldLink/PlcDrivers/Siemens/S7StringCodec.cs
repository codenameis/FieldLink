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

namespace FieldLink.PlcDrivers.Siemens
{
    /// <summary>PLC 문자열의 길이 헤더, 바이트 순서 및 패딩을 처리합니다.</summary>
    public static class S7StringCodec
    {
        /// <summary>먼저 읽은 문자열 헤더에서 다음 읽기에 필요한 전체 바이트 수를 계산합니다.</summary>
        /// <param name = "model">문자열 메모리 형식을 결정하는 Siemens PLC 기종입니다.</param>
        /// <param name = "header">앞서 읽은 프로토콜 헤더입니다.</param>
        /// <param name = "wide">WSTRING의 길이를 계산하면 true입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <remarks>S200Smart의 wide 경로는 기존 사용자 정의 UTF-16BE 형식이며, 길이 헤더는 문자 수가 아닌 데이터 바이트 수입니다.</remarks>
        public static OperationResult<ushort> GetReadLength(SiemensPLCS model, byte[] header, bool wide)
        {
            OperationResult<int> length = GetDeclaredByteLength(model, header, wide);
            if (!length.IsSuccess)
                return length.ConvertFailed<ushort>();
            if (length.Content > ushort.MaxValue)
                return new OperationResult<ushort>("전체 문자열 바이트 수가 이 API의 UInt16 반환 범위를 초과합니다. 분할 읽기가 필요합니다.");
            return OperationResult.CreateSuccessResult((ushort)length.Content);
        }

        /// <summary>길이 헤더를 포함한 STRING 메모리 데이터에서 문자열을 꺼냅니다.</summary>
        /// <param name = "model">문자열 메모리 형식을 결정하는 Siemens PLC 기종입니다.</param>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "encoding">문자열 변환에 사용할 인코딩입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string ParseString(SiemensPLCS model, byte[] data, Encoding encoding)
        {
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));
            int offset = model == SiemensPLCS.S200Smart ? 1 : 2;
            int length = ValidateStringData(model, data, false);
            return encoding.GetString(data, offset, length - offset);
        }

        /// <summary>WSTRING 메모리 데이터의 길이 헤더와 기종별 바이트 순서를 처리합니다.</summary>
        /// <param name = "model">문자열 메모리 형식을 결정하는 Siemens PLC 기종입니다.</param>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <remarks>S200Smart에서는 사용자 정의 UTF-16BE 데이터 중 길이 헤더에 선언된 바이트만 읽습니다.</remarks>
        /// <exception cref="ArgumentException">길이 헤더 또는 문자열 데이터가 올바르지 않습니다.</exception>
        public static string ParseWideString(SiemensPLCS model, byte[] data)
        {
            int length = ValidateStringData(model, data, true);
            int offset = model == SiemensPLCS.S200Smart ? 1 : 4;
            // Sharp7 GetWStringAt: 최대/현재 길이 WORD 뒤에서 현재 길이만 UTF-16BE로 읽는다.
            return Encoding.BigEndianUnicode.GetString(data, offset, length - offset);
        }
        /// <summary>기존 STRING 헤더의 최대 길이를 유지하여 기록할 메모리 데이터를 만듭니다.</summary>
        /// <param name = "model">문자열 메모리 형식을 결정하는 Siemens PLC 기종입니다.</param>
        /// <param name = "header">앞서 읽은 프로토콜 헤더입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <param name = "encoding">문자열 변환에 사용할 인코딩입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <remarks>header는 앞서 읽은 2바이트입니다. S200Smart에서는 사용하지 않습니다.</remarks>
        public static OperationResult<byte[]> BuildString(SiemensPLCS model, byte[] header, string value, Encoding encoding)
        {
            if (value == null)
                value = string.Empty;
            if (encoding == null)
                return new OperationResult<byte[]>("문자열 인코딩이 없습니다.");
            bool smart = model == SiemensPLCS.S200Smart;
            if (!smart)
            {
                OperationResult<int> validation = GetDeclaredByteLength(model, header, false);
                if (!validation.IsSuccess)
                    return validation.ConvertFailed<byte[]>();
            }
            int maximum = smart ? 254 : header[0];
            if (encoding.GetByteCount(value) > maximum)
                return new OperationResult<byte[]>("문자열 길이가 PLC에 정의된 최대 길이를 초과합니다.");
            byte[] buffer = encoding.GetBytes(value);
            if (encoding == Encoding.Unicode)
                buffer = ProtocolBytes.BytesReverseByWord(buffer);
            byte[] prefix = smart ? new byte[] { (byte)buffer.Length } : new byte[] { (byte)maximum, (byte)buffer.Length };
            return OperationResult.CreateSuccessResult(ProtocolBytes.SpliceArray(prefix, buffer));
        }

        /// <summary>기존 WSTRING 헤더의 최대 길이를 유지하여 기록할 메모리 데이터를 만듭니다.</summary>
        /// <param name = "model">문자열 메모리 형식을 결정하는 Siemens PLC 기종입니다.</param>
        /// <param name = "header">앞서 읽은 프로토콜 헤더입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <remarks>header는 앞서 읽은 4바이트입니다. S200Smart에서는 사용하지 않으며 기존 사용자 정의 형식(바이트 수 1바이트 + UTF-16BE)을 기록합니다. 공식 S200Smart WSTRING 지원을 의미하지 않습니다.</remarks>
        public static OperationResult<byte[]> BuildWideString(SiemensPLCS model, byte[] header, string value)
        {
            if (model == SiemensPLCS.S200Smart)
                return BuildString(model, header, value, Encoding.Unicode);
            OperationResult<int> validation = GetDeclaredByteLength(model, header, true);
            if (!validation.IsSuccess)
                return validation.ConvertFailed<byte[]>();
            if (value == null)
                value = string.Empty;
            int maximum = header[0] * 256 + header[1];
            if (value.Length > maximum)
                return new OperationResult<byte[]>("문자열 길이가 PLC에 정의된 최대 길이를 초과합니다.");
            byte[] buffer = Encoding.BigEndianUnicode.GetBytes(value);
            byte[] write = new byte[buffer.Length + 4];
            write[0] = header[0];
            write[1] = header[1];
            write[2] = (byte)(value.Length >> 8);
            write[3] = (byte)value.Length;
            buffer.CopyTo(write, 4);
            return OperationResult.CreateSuccessResult(write);
        }

        // Siemens STRING 구조 및 Sharp7 Get/SetWStringAt. 자료·기종별 상한 차이는
        // docs/siemens-s7-normalization.md에 기록한다. 선언 용량을 추측해 늘리지 않는다.
        private static OperationResult<int> GetDeclaredByteLength(SiemensPLCS model, byte[] header, bool wide)
        {
            bool smart = model == SiemensPLCS.S200Smart;
            int headerSize = smart ? 1 : wide ? 4 : 2;
            if (header == null || header.Length < headerSize)
                return new OperationResult<int>("문자열 길이 헤더가 없거나 짧습니다.");
            int maximum = smart ? 254 : wide ? header[0] * 256 + header[1] : header[0];
            int count = smart ? header[0] : wide ? header[2] * 256 + header[3] : header[1];
            int formatMaximum = wide && !smart ? 65534 : 254;
            if (maximum > formatMaximum || count > maximum)
                return new OperationResult<int>("문자열의 최대 길이 또는 현재 길이가 올바르지 않습니다.");
            if (smart && wide && (count & 1) != 0)
                return new OperationResult<int>("사용자 정의 UTF-16 문자열의 데이터 바이트 수는 짝수여야 합니다.");
            return OperationResult.CreateSuccessResult(headerSize + count * (wide && !smart ? 2 : 1));
        }

        private static int ValidateStringData(SiemensPLCS model, byte[] data, bool wide)
        {
            OperationResult<int> length = GetDeclaredByteLength(model, data, wide);
            if (!length.IsSuccess)
                throw new ArgumentException(length.Message, nameof(data));
            if (data.Length < length.Content)
                throw new ArgumentException("문자열 데이터가 선언 길이보다 짧습니다.", nameof(data));
            return length.Content;
        }
    }
}
