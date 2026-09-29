using FieldLink.PlcDrivers.Common;
using System;
using System.Text;

namespace FieldLink.PlcDrivers.Siemens
{
    /// <summary>PPI SYS0 기종 조회 응답을 해석합니다.</summary>
    public static class PpiModelResponseParser
    {
        /// <summary>원본과 같이 데이터 형식이 FF/04일 때 20바이트 ASCII 기종 문자열을 추출합니다.</summary>
        public static OperationResult<string> Parse(byte[] response)
        {
            try
            {
                byte[] data = new byte[20];
                if (response[21] == 0xFF && response[22] == 4)
                    Array.Copy(response, 25, data, 0, 20);
                return OperationResult.CreateSuccessResult(Encoding.ASCII.GetString(data));
            }
            catch (Exception error) { return new OperationResult<string>("PLC 기종 응답 해석에 실패했습니다: " + error.Message); }
        }
    }
}
