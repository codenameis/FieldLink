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

namespace FieldLink.PlcDrivers.YASKAWA
{
    /// <summary>확장 memobus 프로토콜의 주소 정보</summary>
    public class MemobusAddress : DeviceAddress
    {
        /// <summary>현재 주소와 관련된 기능 코드 정보를 얻거나 설정합니다.</summary>
        public byte SFC { get; set; }
        /// <summary>현재 주소 대응 메인 기능 코드 정보를 얻거나 설정합니다.</summary>
        public byte MFC { get; set; }

        /// <summary>현재 프로토콜 값의 문자열 표현을 반환합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public override string ToString() => AddressStart.ToString();
        /// <summary>메모버스 주소를 검색하고 분석하는 정보 및 기능 코드</summary>
        /// <param name = "address">주소 정보</param>
        /// <param name = "isBit">비트 여부</param>
        /// <returns>memobus의 주소 정보</returns>
        public static OperationResult<MemobusAddress> ParseFrom(string address, bool isBit)
        {
            try
            {
                MemobusAddress memobusAddress = new MemobusAddress();
                memobusAddress.MFC = (byte)AddressParameters.ExtractParameter(ref address, "mfc", 0x20);
                memobusAddress.SFC = (byte)AddressParameters.ExtractParameter(ref address, "x", isBit ? 0x01 : 0x03);
                memobusAddress.AddressStart = ushort.Parse(address);
                return OperationResult.CreateSuccessResult(memobusAddress);
            }
            catch (Exception ex)
            {
                return new OperationResult<MemobusAddress>(ex.Message);
            }
        }
    }
}
