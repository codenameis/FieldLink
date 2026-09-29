using FieldLink.PlcDrivers.Common;
using System.Text;

namespace FieldLink.PlcDrivers.Fuji
{
    /// <summary>SPB 읽기 응답의 상태와 데이터 구간을 해석합니다.</summary>
    public static class FujiSpbReadResponseParser
    {
        /// <summary>상태 다음의 4문자 메타데이터를 제거하고 16진수 워드를 추출합니다.</summary>
        public static OperationResult<byte[]> ParseWords(byte[] response)
        {
            var check = FujiSPBResponseParser.CheckResponseData(response);
            if (!check.IsSuccess)
                return check;
            if (check.Content.Length < 4)
                return new OperationResult<byte[]>("SPB 읽기 응답의 메타데이터가 부족합니다.");
            return OperationResult.CreateSuccessResult(Encoding.ASCII.GetString(check.Content.RemoveBegin(4)).ToHexBytes());
        }
    }
}
