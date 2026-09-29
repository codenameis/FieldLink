using FieldLink.PlcDrivers.Common;
using System;

namespace FieldLink.PlcDrivers.FATEK
{
    /// <summary>읽기 응답의 장치 상태와 요청 길이를 확인한 뒤 값을 추출합니다.</summary>
    public static class FatekProgramReadResponseParser
    {
        /// <summary>ASCII 워드를 낮은 자리 바이트 우선 데이터로 변환합니다.</summary>
        public static OperationResult<byte[]> ParseWords(byte[] response, ushort length)
        {
            var check = FatekProgramResponseParser.CheckResponse(response);
            if (!check.IsSuccess)
                return check.ConvertFailed<byte[]>();
            if (response.Length < 9 + length * 4)
                return new OperationResult<byte[]>("FATEK 워드 응답이 요청 길이보다 짧습니다.");
            try { return OperationResult.CreateSuccessResult(FatekProgramResponseParser.ExtraResponse(response, length)); }
            catch (FormatException) { return new OperationResult<byte[]>("FATEK 워드 응답에 잘못된 16진수가 있습니다."); }
        }
        /// <summary>ASCII 비트 응답을 요청한 길이만큼 추출합니다.</summary>
        public static OperationResult<bool[]> ParseBits(byte[] response, ushort length)
        {
            var check = FatekProgramResponseParser.CheckResponse(response);
            if (!check.IsSuccess)
                return check.ConvertFailed<bool[]>();
            if (response.Length < 9 + length)
                return new OperationResult<bool[]>("FATEK 비트 응답이 요청 길이보다 짧습니다.");
            var bits = new bool[length];
            for (int i = 0; i < length; i++) bits[i] = response[6 + i] == '1';
            return OperationResult.CreateSuccessResult(bits);
        }
    }
}
