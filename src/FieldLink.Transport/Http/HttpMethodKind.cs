namespace FieldLink.Transport.Http;

/// <summary>HTTP 전송에서 지원하는 요청 메서드 종류입니다.</summary>
public enum HttpMethodKind
{
    /// <summary>리소스 조회 (HTTP GET).</summary>
    Get,

    /// <summary>명령 또는 데이터 전송 (HTTP POST).</summary>
    Post,

    /// <summary>리소스 갱신 (HTTP PUT).</summary>
    Put,

    /// <summary>리소스 삭제 (HTTP DELETE).</summary>
    Delete,
}
