using FieldLink.PlcDrivers.Common;
using System;

namespace FieldLink.PlcDrivers.Modbus
{
    /// <summary>전송 외피가 제거된 Modbus 응답을 요청과 대조합니다.</summary>
    public static class ModbusResponseParser
    {
        /// <summary>국번·기능·읽기 길이 또는 쓰기 에코를 검사합니다. 정상 장치 예외 응답은 오류 코드를 보존합니다.</summary>
        public static OperationResult<byte[]> Parse(byte[] request, byte[] response)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            return Parse(request, response, request.Length >= 2 ? request[1] : (byte)0);
        }

        internal static OperationResult<byte[]> Parse(byte[] request, byte[] response, byte layout)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (response == null)
                throw new ArgumentNullException(nameof(response));
            if (request.Length < 2 || response.Length < 2)
                return new OperationResult<byte[]>("Modbus 코어 길이가 부족합니다.");
            if (request[0] != response[0])
                return new OperationResult<byte[]>("응답 국번이 요청과 다릅니다.");
            if (response[1] == (request[1] | 0x80))
                return response.Length == 3 ? new OperationResult<byte[]>(response[2], ModbusFrameRules.GetDescriptionByErrorCode(response[2])) : new OperationResult<byte[]>("예외 응답 길이가 잘못되었습니다.");
            if (request[1] != response[1])
                return new OperationResult<byte[]>("응답 기능이 요청과 다릅니다.");
            if (layout == 1 || layout == 2 || layout == 3 || layout == 4 || layout == 23)
            {
                if (request.Length < 6 || response.Length < 3)
                    return new OperationResult<byte[]>("읽기 응답 길이가 부족합니다.");
                int count = request[4] * 256 + request[5];
                int bytes = layout <= 2 ? (count + 7) / 8 : count * 2;
                if (response[2] != bytes || response.Length != bytes + 3)
                    return new OperationResult<byte[]>("요청 길이와 읽기 응답 길이가 다릅니다.");
                if (layout <= 2 && count % 8 != 0 && (response[response.Length - 1] >> (count % 8)) != 0)
                    return new OperationResult<byte[]>("Modbus 비트 응답의 사용하지 않는 상위 비트는 0이어야 합니다.");
                return OperationResult.CreateSuccessResult(response.RemoveBegin(3));
            }
            int echo = layout == 22 ? 8 : 6;
            if (request.Length < echo || response.Length != echo)
                return new OperationResult<byte[]>("쓰기 승인 응답 길이가 잘못되었습니다.");
            for (int i = 0; i < echo; i++)
                if (request[i] != response[i])
                    return new OperationResult<byte[]>("쓰기 승인 값이 요청과 다릅니다.");
            return OperationResult.CreateSuccessResult(new byte[0]);
        }
    }
}
