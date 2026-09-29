using FieldLink.Communication;

namespace FieldLink.PlcDrivers.Common
{
    /// <summary>요청별 상태 없이 프레임 길이·헤더·응답 식별을 검사합니다.</summary>
    /// <remarks>입력 배열을 수정하거나 보관하지 않습니다. 호출 중 입력을 변경하지 마십시오.
    /// 전송 경계는 IFrameBoundary, 명령별 내용 검증은 각 응답 파서의 책임입니다.</remarks>
    public interface IProtocolFrameRules
    {
        /// <summary>고정 헤더 크기입니다. 종료 문자 또는 데이터그램 방식은 0입니다.</summary>
        int HeaderLength { get; }
        /// <summary>헤더에 선언된 본문 길이를 반환합니다. 불완전한 헤더·필수 요청 누락은 인수 예외입니다.</summary>
        int GetBodyLength(byte[] header, byte[] request = null);
        /// <summary>헤더 크기와 지원하는 시그니처를 검사합니다. 본문 내용 검사는 포함하지 않습니다.</summary>
        bool IsHeaderValid(byte[] header, byte[] request = null);
        /// <summary>헤더의 순서 식별자를 반환합니다. 식별자가 없는 규칙은 null을 반환합니다.</summary>
        int? GetSequenceId(byte[] header);
        /// <summary>헤더 후보의 시작 위치입니다. 후보를 찾지 못했거나 재동기화를 지원하지 않으면 0입니다.</summary>
        int FindHeaderOffset(byte[] bufferedData);
        /// <summary>현재 버퍼가 프로토콜의 완료 조건을 만족하는지 검사합니다. 잘못된 선언 길이는 예외입니다.</summary>
        bool IsComplete(byte[] request, byte[] received);
        /// <summary>이미 경계가 확정된 응답의 요청 대응을 분류합니다. 명령 성공 판정은 포함하지 않습니다.</summary>
        ResponseDisposition ClassifyResponse(byte[] request, byte[] response);
    }
}
