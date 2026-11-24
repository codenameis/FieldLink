using System;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Robot.Abstractions;

/// <summary>로봇 컨트롤러와의 기본 연결 및 원시·문자열 읽기/쓰기 계약을 정의합니다.</summary>
public interface IRobotClient : IDisposable
{
    /// <summary>현재 통신 세션을 수행할 수 있는 열림 상태인지 여부입니다.</summary>
    bool IsOpen { get; }

    /// <summary>전송 연결을 수립하고 필요한 프로토콜 초기 핸드셰이크를 수행합니다.</summary>
    /// <param name="cancellationToken">취소 토큰입니다.</param>
    /// <returns>연결 결과입니다.</returns>
    Task<RobotResult> OpenAsync(CancellationToken cancellationToken = default);

    /// <summary>프로토콜 세션을 정리하고 전송 연결을 닫습니다.</summary>
    /// <param name="cancellationToken">취소 토큰입니다.</param>
    /// <returns>종료 결과입니다.</returns>
    Task<RobotResult> CloseAsync(CancellationToken cancellationToken = default);

    /// <summary>지정된 주소 또는 엔드포인트에서 원시 바이트 데이터를 읽습니다.</summary>
    /// <param name="address">프로토콜별 주소 또는 명령 문자열입니다.</param>
    /// <param name="cancellationToken">취소 토큰입니다.</param>
    /// <returns>읽은 원시 바이트 배열을 담은 결과입니다.</returns>
    Task<RobotResult<byte[]>> ReadRawAsync(string address, CancellationToken cancellationToken = default);

    /// <summary>지정된 주소 또는 엔드포인트에서 문자열 또는 JSON 표현을 읽습니다.</summary>
    /// <param name="address">프로토콜별 주소 또는 명령 문자열입니다.</param>
    /// <param name="cancellationToken">취소 토큰입니다.</param>
    /// <returns>디코딩된 문자열 결과를 담은 결과입니다.</returns>
    Task<RobotResult<string>> ReadStringAsync(string address, CancellationToken cancellationToken = default);

    /// <summary>지정된 주소 또는 엔드포인트에 원시 바이트 데이터를 씁니다.</summary>
    /// <param name="address">대상 주소 또는 명령 문자열입니다.</param>
    /// <param name="data">기록할 원시 바이트 배열입니다.</param>
    /// <param name="cancellationToken">취소 토큰입니다.</param>
    /// <returns>쓰기 작업 결과입니다. 미지원 프로토콜은 <see cref="RobotErrorKind.Unsupported"/>를 반환합니다.</returns>
    Task<RobotResult> WriteRawAsync(string address, byte[] data, CancellationToken cancellationToken = default);

    /// <summary>지정된 주소 또는 엔드포인트에 문자열 데이터를 씁니다.</summary>
    /// <param name="address">대상 주소 또는 명령 문자열입니다.</param>
    /// <param name="value">기록할 문자열 값입니다.</param>
    /// <param name="cancellationToken">취소 토큰입니다.</param>
    /// <returns>쓰기 작업 결과입니다. 미지원 프로토콜은 <see cref="RobotErrorKind.Unsupported"/>를 반환합니다.</returns>
    Task<RobotResult> WriteStringAsync(string address, string value, CancellationToken cancellationToken = default);
}
