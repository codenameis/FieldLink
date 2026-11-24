using System;
using System.Text;

namespace FieldLink.Robot.Protocols.Kuka.SocketAscii;

/// <summary>KUKA Socket ASCII 응답 청크를 해석하고 제어 응답 오류를 검사하는 순수 정적 파서입니다.</summary>
public static class KukaSocketAsciiResponseParser
{
    private const string ErrorToken = "err";

    /// <summary>수신된 읽기 응답 바이트를 문자열로 디코딩합니다.</summary>
    public static string ParseReadResponse(byte[] response, Encoding? encoding = null)
    {
        if (response is null)
        {
            throw new ArgumentNullException(nameof(response));
        }

        return (encoding ?? Encoding.UTF8).GetString(response);
    }

    /// <summary>쓰기 및 프로그램 제어 응답 문자열에 대소문자 구분 <c>"err"</c> 포함 여부를 검증합니다.</summary>
    public static void ValidateControlResponse(byte[] response, Encoding? encoding = null)
    {
        if (response is null)
        {
            throw new ArgumentNullException(nameof(response));
        }

        string text = (encoding ?? Encoding.UTF8).GetString(response);
        if (text.IndexOf(ErrorToken, StringComparison.Ordinal) >= 0)
        {
            throw new RobotDeviceException(0, $"Result contains err: {text}", response);
        }
    }
}
