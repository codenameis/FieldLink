using System.Text;
using FieldLink.PlcDrivers.Common;
using FieldLink.PlcDrivers.Beckhoff;
using FieldLink.PlcDrivers.FATEK;
using FieldLink.PlcDrivers.GE;

namespace FieldLink.Communication.Tests;

// 응답은 제품의 응답 빌더를 호출하지 않고 고정된 필드와 데이터로 구성한다.
// 장치 오류는 실패 여부와 오류 코드까지, 데이터는 바이트 순서까지 비교한다.
internal static partial class ProtocolResponseTests
{
    internal static Task BeckhoffAdsReadAndErrorsAsync()
    {
        byte[] response = new byte[50];
        response[2] = 44; // AMS/TCP 본문 길이
        response[22] = 2; // Read
        response[24] = 5; // 응답 플래그
        response[26] = 12; // ADS 결과 + 길이 + 4바이트 데이터
        response[42] = 4;
        H("3412CDAB").CopyTo(response, 46);
        var options = new AdsFrameOptions();
        Payload(BeckhoffAdsNetResponseParser.UnpackResponseContent(options, [], response), "3412CDAB");
        response[38] = 0x02; response[39] = 0x07; // ADS: 잘못된 인덱스 그룹
        Failed(BeckhoffAdsNetResponseParser.UnpackResponseContent(options, [], response), 1794);
        response[30] = 6; // AMS 오류가 ADS 오류보다 우선한다.
        Failed(BeckhoffAdsNetResponseParser.UnpackResponseContent(options, [], response), 6);
        Failed(AdsResponseParser.CheckResponse([]));
        return Task.CompletedTask;
    }

    internal static Task GeSrtpShortAndLongRepliesAsync()
    {
        byte[] shortReply = new byte[56];
        shortReply[0] = 3;
        shortReply[31] = 0xD4;
        H("3412CDABFFFF").CopyTo(shortReply, 44);
        Payload(GeResponseParser.ExtraResponseContent(shortReply), "3412CDABFFFF");
        shortReply[42] = 5;
        Failed(GeResponseParser.ExtraResponseContent(shortReply), 5);
        byte[] longReply = new byte[60];
        longReply[0] = 3;
        longReply[31] = 0x94;
        H("3412CDAB").CopyTo(longReply, 56);
        Payload(GeResponseParser.ExtraResponseContent(longReply), "3412CDAB");
        Failed(GeResponseParser.ExtraResponseContent([3]));
        TestAssert.Equal(new DateTime(2026, 9, 23, 14, 35, 59),
            GeResponseParser.ExtraDateTime(H("593514230926")).Content);
        Failed(GeResponseParser.ExtraDateTime(H("000000001326")));
        return Task.CompletedTask;
    }

    internal static Task FatekWordsStatusAndFramingAsync()
    {
        byte[] response = A("\u0002014601234ABCDD1\u0003");
        TestAssert.True(FatekProgramResponseParser.CheckResponse(response).IsSuccess);
        TestAssert.Bytes(H("3412CDAB"), FatekProgramResponseParser.ExtraResponse(response, 2));
        using var complete = new MemoryStream(response);
        using var partial = new MemoryStream(TestBytes.Slice(response, 0, trimEnd: 1));
        TestAssert.True(FatekProgramResponseParser.CheckReceiveDataComplete(complete));
        TestAssert.True(!FatekProgramResponseParser.CheckReceiveDataComplete(partial));
        response[5] = (byte)'2';
        Failed(FatekProgramResponseParser.CheckResponse(response), '2');
        Failed(FatekProgramResponseParser.CheckResponse([]));
        return Task.CompletedTask;
    }

    private static byte[] A(string value) => Encoding.ASCII.GetBytes(value);
    private static byte[] H(string value) => TestBytes.FromHexString(value);

    private static void Payload(OperationResult<byte[]> result, string expectedHex)
    {
        TestAssert.True(result.IsSuccess, result.Message);
        TestAssert.Bytes(H(expectedHex), result.Content);
    }

    private static void Failed(OperationResult result, int? code = null)
    {
        TestAssert.True(!result.IsSuccess, "실패 응답이 성공으로 처리되었습니다.");
        TestAssert.True(!string.IsNullOrWhiteSpace(result.Message), "실패 원인이 없습니다.");
        if (code.HasValue)
            TestAssert.Equal(code.Value, result.ErrorCode);
    }
}
