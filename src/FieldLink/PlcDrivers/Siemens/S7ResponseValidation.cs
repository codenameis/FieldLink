using FieldLink.PlcDrivers.Common;

namespace FieldLink.PlcDrivers.Siemens
{
    // RFC 1006 §6의 TPKT와 Sharp7 ReadMultiVars/WriteMultiVars의 ACK_DATA 필드.
    // https://github.com/davenardella/Sharp7/blob/dc254a636ad3e4051babf7b3e991e640ea7a93c3/src/Sharp7.cs
    internal static class S7ResponseValidation
    {
        internal const int ItemDataOffset = 21;

        internal static OperationResult Validate(byte[] response, byte function)
        {
            OperationResult envelope = ValidateEnvelope(response);
            if (!envelope.IsSuccess)
                return envelope;
            int parameterLength = response[13] * 256 + response[14];
            if (parameterLength != 2 || response.Length < ItemDataOffset ||
                response[19] != function || response[20] == 0)
                return new OperationResult("S7 응답의 기능 코드 또는 항목 수가 올바르지 않습니다.");
            return OperationResult.CreateSuccessResult();
        }

        internal static OperationResult ValidateEnvelope(byte[] response)
        {
            if (response == null || response.Length < 19)
                return new OperationResult("S7 ACK_DATA 헤더 길이가 19바이트보다 짧습니다.");
            int parameterLength = response[13] * 256 + response[14];
            int dataLength = response[15] * 256 + response[16];
            if (response[0] != 3 || response[1] != 0 || response[2] * 256 + response[3] != response.Length ||
                response[4] != 2 || response[5] != 0xF0 || response[6] != 0x80 ||
                response[7] != 0x32 || response[8] != 3 || 19 + parameterLength + dataLength != response.Length)
                return new OperationResult("S7 응답의 외피 또는 선언 길이가 올바르지 않습니다.");
            // PLC가 매개변수와 항목 없이 헤더 오류만 보낸 경우에도 장치 오류 코드를 보존한다.
            int error = response[17] * 256 + response[18];
            if (error != 0)
                return new OperationResult(error, "S7 응답 헤더에 장치 오류가 있습니다.");
            return OperationResult.CreateSuccessResult();
        }
    }
}
