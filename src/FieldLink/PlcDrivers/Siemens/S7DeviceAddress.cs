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
    /// <summary>Siemens S7 주소입니다. C/T의 AddressStart와 Length는 항목 인덱스와 개수이며,
    /// 그 외 영역의 AddressStart는 비트 오프셋, Length는 바이트 수입니다. 쓰기 길이는 데이터에서 계산합니다.</summary>
    public class S7DeviceAddress : DeviceAddress
    {
        // S7 ANY 주소 항목의 시작 위치는 24비트다. CPU별 실제 메모리 용량은 별도 조건이다.
        internal const int MaximumWireAddress = 0xFFFFFF;
        /// <summary>데이터를 가져오기 위한 코드, 또는 읽기를 대기하도록 설정</summary>
        public byte DataCode { get; set; }
        /// <summary>PLC의 DB 블록 데이터 정보를 가져오거나 설정합니다.</summary>
        public ushort DbBlock { get; set; }

        /// <summary>정해진 주소 정보에서 실제 장치 주소 정보로 분해</summary>
        /// <param name = "address">주소 정보</param>
        /// <param name = "length">데이터 길이</param>
        public override void Parse(string address, ushort length)
        {
            OperationResult<S7DeviceAddress> addressData = ParseFrom(address, length);
            if (!addressData.IsSuccess)
                throw new FormatException(addressData.Message);
            AddressStart = addressData.Content.AddressStart;
            Length = addressData.Content.Length;
            DataCode = addressData.Content.DataCode;
            DbBlock = addressData.Content.DbBlock;
        }

        /// <summary>현재 프로토콜 값의 문자열 표현을 반환합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public override string ToString()
        {
            // 레거시 1E/1F도 C/T로 표시하지만 ParseFrom의 기본 해석은 일반 S7 1C/1D다.
            if (DataCode == 0x1D || DataCode == 0x1F)
                return "T" + AddressStart.ToString();
            if (DataCode == 0x1C || DataCode == 0x1E)
                return "C" + AddressStart.ToString();
            if (DataCode == 0x05)
                return "SM" + GetActualStringAddress(AddressStart);
            if (DataCode == 0x06)
                return "AI" + GetActualStringAddress(AddressStart);
            if (DataCode == 0x07)
                return "AQ" + GetActualStringAddress(AddressStart);
            if (DataCode == 0x80)
                return "P" + GetActualStringAddress(AddressStart);
            if (DataCode == 0x81)
                return "I" + GetActualStringAddress(AddressStart);
            if (DataCode == 0x82)
                return "Q" + GetActualStringAddress(AddressStart);
            if (DataCode == 0x83)
                return "M" + GetActualStringAddress(AddressStart);
            if (DataCode == 0x84)
                return "DB" + DbBlock + "." + GetActualStringAddress(AddressStart);
            return AddressStart.ToString();
        }

        /// <summary>GetActualStringAddress 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "addressStart">addressStart에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        private static string GetActualStringAddress(int addressStart)
        {
            if (addressStart % 8 == 0)
                return (addressStart / 8).ToString();
            else
                return $"{addressStart / 8}.{addressStart % 8}";
        }

        /// <summary>일반 주소의 바이트·비트 위치 또는 C/T의 항목 인덱스를 24비트 주소로 해석합니다.</summary>
        /// <param name = "address">문자열 주소 -> String address</param>
        /// <param name = "isCT">타이머와 카운터의 주소인지</param>
        /// <returns>실제 값 -> Actual value</returns>
        public static int CalculateAddressStarted(string address, bool isCT = false)
        {
            if (address == null)
                throw new ArgumentNullException(nameof(address));
            string[] parts = address.Split('.');
            if (isCT)
            {
                if (parts.Length != 1)
                    throw new FormatException("S7 카운터·타이머 주소는 정수 인덱스여야 합니다.");
                return ParseAddressNumber(parts[0], MaximumWireAddress);
            }
            if (parts.Length > 2)
                throw new FormatException("S7 주소는 바이트 번호 또는 바이트.비트 형식이어야 합니다.");
            // 곱셈 전에 범위를 검사해 오버플로 및 직렬화 시 상위 바이트 손실을 막는다.
            int byteOffset = ParseAddressNumber(parts[0], MaximumWireAddress / 8);
            int bitNumber = parts.Length == 2 ? ParseAddressNumber(parts[1], 7) : 0;
            return byteOffset * 8 + bitNumber;
        }

        private static int ParseAddressNumber(string text, int maximum)
        {
            int value;
            if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) || value > maximum)
                throw new FormatException("S7 주소 숫자는 0부터 " + maximum + " 사이의 부호 없는 정수여야 합니다.");
            return value;
        }

        internal static OperationResult ValidateWireAddress(S7DeviceAddress address)
        {
            if (address == null)
                return new OperationResult("S7 주소가 없습니다.");
            // 공개 주소 객체는 파싱 이후에도 수정할 수 있으므로 패킷 생성 직전에 재검증한다.
            if (address.AddressStart < 0 || address.AddressStart > MaximumWireAddress)
                return new OperationResult("S7 시작 주소가 24비트 전송 필드 범위를 벗어났습니다.");
            return OperationResult.CreateSuccessResult();
        }

        /// <summary>실제 Siemens의 주소에서 주소 객체를 분석합니다.</summary>
        /// <param name = "address">Siemens 주소 데이터 정보</param>
        /// <returns>성공 여부를 나타내는 결과 객체</returns>
        public static OperationResult<S7DeviceAddress> ParseFrom(string address)
        {
            return ParseFrom(address, 0);
        }

        /// <summary>실제 Siemens의 주소에서 주소 객체를 분석합니다.</summary>
        /// <param name = "address">Siemens 주소 데이터 정보</param>
        /// <param name = "length">읽을 데이터 길이</param>
        /// <returns>성공 여부를 나타내는 결과 객체</returns>
        public static OperationResult<S7DeviceAddress> ParseFrom(string address, ushort length)
        {
            S7DeviceAddress addressData = new S7DeviceAddress();
            try
            {
                if (string.IsNullOrEmpty(address))
                    return new OperationResult<S7DeviceAddress>("S7 주소가 없습니다.");
                address = address.ToUpperInvariant();
                addressData.Length = length;
                addressData.DbBlock = 0;
                if (address.StartsWith("SM"))
                {
                    // 200 계열의 시스템 플래그
                    addressData.DataCode = 0x05;
                    if (address.StartsWith("SMX") || address.StartsWith("SMB") || address.StartsWith("SMW") || address.StartsWith("SMD"))
                        addressData.AddressStart = CalculateAddressStarted(address.Substring(3));
                    else
                        addressData.AddressStart = CalculateAddressStarted(address.Substring(2));
                }
                else if (address.StartsWith("AI"))
                {
                    addressData.DataCode = 0x06;
                    if (address.StartsWith("AIX") || address.StartsWith("AIB") || address.StartsWith("AIW") || address.StartsWith("AID"))
                        addressData.AddressStart = CalculateAddressStarted(address.Substring(3));
                    else
                        addressData.AddressStart = CalculateAddressStarted(address.Substring(2));
                }
                else if (address.StartsWith("AQ"))
                {
                    addressData.DataCode = 0x07;
                    if (address.StartsWith("AQX") || address.StartsWith("AQB") || address.StartsWith("AQW") || address.StartsWith("AQD"))
                        addressData.AddressStart = CalculateAddressStarted(address.Substring(3));
                    else
                        addressData.AddressStart = CalculateAddressStarted(address.Substring(2));
                }
                else if (address[0] == 'P')
                {
                    // 직접적인 주변 접속
                    addressData.DataCode = 0x80;
                    if (address.StartsWith("PIX") || address.StartsWith("PIB") || address.StartsWith("PIW") || address.StartsWith("PID") || address.StartsWith("PQX") || address.StartsWith("PQB") || address.StartsWith("PQW") || address.StartsWith("PQD"))
                        addressData.AddressStart = CalculateAddressStarted(address.Substring(3));
                    else if (address.StartsWith("PI") || address.StartsWith("PQ"))
                        addressData.AddressStart = CalculateAddressStarted(address.Substring(2));
                    else
                        addressData.AddressStart = CalculateAddressStarted(address.Substring(1));
                }
                else if (address[0] == 'I')
                {
                    addressData.DataCode = 0x81;
                    if (address.StartsWith("IX") || address.StartsWith("IB") || address.StartsWith("IW") || address.StartsWith("ID"))
                        addressData.AddressStart = CalculateAddressStarted(address.Substring(2));
                    else
                        addressData.AddressStart = CalculateAddressStarted(address.Substring(1));
                }
                else if (address[0] == 'Q')
                {
                    addressData.DataCode = 0x82;
                    if (address.StartsWith("QX") || address.StartsWith("QB") || address.StartsWith("QW") || address.StartsWith("QD"))
                        addressData.AddressStart = CalculateAddressStarted(address.Substring(2));
                    else
                        addressData.AddressStart = CalculateAddressStarted(address.Substring(1));
                }
                else if (address[0] == 'M')
                {
                    addressData.DataCode = 0x83;
                    if (address.StartsWith("MX") || address.StartsWith("MB") || address.StartsWith("MW") || address.StartsWith("MD"))
                        addressData.AddressStart = CalculateAddressStarted(address.Substring(2));
                    else
                        addressData.AddressStart = CalculateAddressStarted(address.Substring(1));
                }
                else if (address[0] == 'D')
                {
                    addressData.DataCode = 0x84;
                    int prefixLength = address.StartsWith("DB") ? 2 : 1;
                    int separator = address.IndexOf('.');
                    if (separator <= prefixLength)
                        throw new FormatException("S7 DB 주소에는 DB 번호와 오프셋 구분자가 필요합니다.");
                    addressData.DbBlock = (ushort)ParseAddressNumber(
                        address.Substring(prefixLength, separator - prefixLength), ushort.MaxValue);
                    string addTemp = address.Substring(separator + 1);
                    if (addTemp.StartsWith("DBX") || addTemp.StartsWith("DBB") || addTemp.StartsWith("DBW") || addTemp.StartsWith("DBD"))
                        addTemp = addTemp.Substring(3);
                    addressData.AddressStart = CalculateAddressStarted(addTemp);
                }
                else if (address[0] == 'T')
                {
                    // 일반 S7 TIMER=1D. S7-200 IEC TIMER=1F와 구분한다.
                    addressData.DataCode = 0x1D;
                    addressData.AddressStart = CalculateAddressStarted(address.Substring(1), true);
                }
                else if (address[0] == 'C')
                {
                    // 일반 S7 COUNTER=1C. S7-200 IEC COUNTER=1E와 구분한다.
                    addressData.DataCode = 0x1C;
                    addressData.AddressStart = CalculateAddressStarted(address.Substring(1), true);
                }
                else if (address[0] == 'V')
                {
                    addressData.DataCode = 0x84;
                    addressData.DbBlock = 1;
                    if (address.StartsWith("VB") || address.StartsWith("VW") || address.StartsWith("VD") || address.StartsWith("VX"))
                        addressData.AddressStart = CalculateAddressStarted(address.Substring(2));
                    else
                        addressData.AddressStart = CalculateAddressStarted(address.Substring(1));
                }
                else
                {
                    return new OperationResult<S7DeviceAddress>(ProtocolMessages.NotSupportedDataType);
                }
            }
            catch (Exception ex)
            {
                return new OperationResult<S7DeviceAddress>(ex.Message);
            }

            return OperationResult.CreateSuccessResult(addressData);
        }
    }
}
