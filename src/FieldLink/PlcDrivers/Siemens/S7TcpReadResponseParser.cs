using FieldLink.PlcDrivers.Common;
using System;

namespace FieldLink.PlcDrivers.Siemens
{
    // 요청 주소가 없는 API에서는 OCTET 내용을 그대로 반환한다. C/T의 기존 3/5바이트
    // 레코드를 해석하려면 요청 개수를 아는 SiemensS7NetResponseParser를 사용해야 한다.
    internal static class S7TcpReadResponseParser
    {
        internal static OperationResult<byte[]> Parse(byte[] response, bool singleBit)
        {
            OperationResult validation = S7ResponseValidation.Validate(response, 4);
            if (!validation.IsSuccess)
                return validation.ConvertFailed<byte[]>();
            int count = response[20];
            if (singleBit && count != 1)
                return new OperationResult<byte[]>("단일 비트 읽기에 여러 항목이 반환되었습니다.");
            int offset = S7ResponseValidation.ItemDataOffset;
            byte[] result = new byte[response.Length - offset];
            int written = 0;
            for (int item = 0; item < count; item++)
            {
                if (response.Length - offset < 4)
                    return new OperationResult<byte[]>("읽기 항목 헤더가 누락되었습니다.");
                byte status = response[offset];
                if (status != 0xFF)
                    return new OperationResult<byte[]>(status, "S7 읽기 항목 오류: 0x" + status.ToString("X2"));
                int transportSize = response[offset + 1];
                int length = response[offset + 2] * 256 + response[offset + 3];
                if (singleBit)
                {
                    if (transportSize != 3 || length != 1)
                        return new OperationResult<byte[]>("단일 비트 읽기의 전송 타입 또는 길이가 올바르지 않습니다.");
                }
                else if (transportSize == 4 && length % 8 == 0)
                    length /= 8;
                else if (transportSize != 9)
                    return new OperationResult<byte[]>("지원하지 않는 읽기 전송 타입 또는 길이입니다.");
                offset += 4;
                if (length == 0 || length > response.Length - offset)
                    return new OperationResult<byte[]>("읽기 항목 데이터가 없거나 선언 길이보다 짧습니다.");
                Array.Copy(response, offset, result, written, length);
                written += length;
                offset += length;
                if ((length & 1) != 0 && (item < count - 1 || offset < response.Length))
                {
                    if (offset >= response.Length || response[offset] != 0)
                        return new OperationResult<byte[]>("읽기 항목 정렬 바이트가 올바르지 않습니다.");
                    offset++;
                }
            }
            if (offset != response.Length)
                return new OperationResult<byte[]>("응답에 선언되지 않은 데이터가 남았습니다.");
            Array.Resize(ref result, written);
            return OperationResult.CreateSuccessResult(result);
        }
    }
}
