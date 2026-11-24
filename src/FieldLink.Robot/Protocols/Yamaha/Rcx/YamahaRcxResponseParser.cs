using System;
using System.Globalization;
using System.Text;

namespace FieldLink.Robot.Protocols.Yamaha.Rcx;

/// <summary>YAMAHA RCX ASCII 라인 응답을 검증하고 도메인 값으로 해석하는 순수 정적 파서입니다.</summary>
public static class YamahaRcxResponseParser
{
    private const string OkPrefix = "OK";
    private const string ErrorPrefix = "NG";
    private const int CrLfLength = 2;
    private const byte CarriageReturn = 0x0D;
    private const byte LineFeed = 0x0A;

    /// <summary><c>\r\n</c>으로 끝나는 응답 라인에서 개행 문자를 제거하고 ASCII 문자열로 디코딩합니다.</summary>
    public static string StripLineTerminator(byte[] lineBytes)
    {
        if (lineBytes is null)
        {
            throw new ArgumentNullException(nameof(lineBytes));
        }

        int length = lineBytes.Length;
        if (length >= CrLfLength && lineBytes[length - CrLfLength] == CarriageReturn && lineBytes[length - 1] == LineFeed)
        {
            length -= CrLfLength;
        }

        return Encoding.ASCII.GetString(lineBytes, 0, length);
    }

    /// <summary>응답 라인이 오류(<c>NG</c>)로 시작하는지 판정합니다.</summary>
    public static bool IsErrorLine(string line)
    {
        if (line is null)
        {
            throw new ArgumentNullException(nameof(line));
        }

        return line.StartsWith(ErrorPrefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>완료 확인 라인이 <c>OK</c>로 시작하는지 검증합니다.</summary>
    public static void ValidateOkLine(string line)
    {
        if (line is null)
        {
            throw new ArgumentNullException(nameof(line));
        }

        if (!line.StartsWith(OkPrefix, StringComparison.Ordinal))
        {
            throw new RobotDeviceException(0, line);
        }
    }

    /// <summary>2줄 응답(데이터 라인 + <c>OK</c> 라인)에서 정수 상태 값을 파싱합니다.</summary>
    public static int ParseStatusInt(string valueLine, string statusLine)
    {
        ValidateOkLine(statusLine);
        if (valueLine is null)
        {
            throw new ArgumentNullException(nameof(valueLine));
        }

        if (!int.TryParse(valueLine.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
        {
            throw new RobotException(
                RobotErrorKind.ParseError,
                $"YAMAHA RCX 정수 응답('{valueLine}')을 변환하지 못했습니다.");
        }

        return result;
    }

    /// <summary><c>@?WHERE</c> 응답 라인에서 공백으로 구분된 관절 좌표 <c>float[]</c> 배열을 파싱합니다.</summary>
    public static float[] ParseJoints(string line)
    {
        if (line is null)
        {
            throw new ArgumentNullException(nameof(line));
        }

        if (IsErrorLine(line))
        {
            throw new RobotDeviceException(0, line);
        }

        string[] tokens = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
        {
            throw new RobotException(RobotErrorKind.ParseError, "YAMAHA RCX 관절 좌표 응답이 비어 있습니다.");
        }

        float[] joints = new float[tokens.Length];
        for (int i = 0; i < tokens.Length; i++)
        {
            if (!float.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
            {
                throw new RobotException(
                    RobotErrorKind.ParseError,
                    $"YAMAHA RCX 좌표 토큰('{tokens[i]}')을 실수로 변환하지 못했습니다.");
            }

            joints[i] = parsed;
        }

        return joints;
    }
}
