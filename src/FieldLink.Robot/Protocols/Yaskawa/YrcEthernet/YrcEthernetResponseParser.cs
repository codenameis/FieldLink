using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FieldLink.Robot.Conversion;

namespace FieldLink.Robot.Protocols.Yaskawa.YrcEthernet;

/// <summary>YASKAWA YRC Ethernet Server 핸드셰이크 및 Phase 1/3 응답을 해석하는 순수 정적 파서입니다.</summary>
public static class YrcEthernetResponseParser
{
    private const string PersistentConnectResponse = "OK:YR Information Server(Ver) Keep-Alive:-1.";
    private const string OkPrefix = "OK:";
    private const string ErrorPrefix = "ERROR:";
    private const string SuccessZeroResult = "0000";
    private const int MinimumStatsTokenCount = 2;
    private const int StatusBitCount = 16;
    private const int BitsPerByte = 8;
    private const byte CarriageReturn = 0x0D;
    private const byte LineFeed = 0x0A;
    private const int CrLfLength = 2;
    private static readonly TimeSpan ErrorRegexTimeout = TimeSpan.FromSeconds(1);

    /// <summary><c>CONNECT</c> 핸드셰이크 응답을 해석해 영구 연결 유지 여부를 반환합니다.</summary>
    public static bool ParseConnectResponse(string responseLine)
    {
        if (responseLine is null)
        {
            throw new ArgumentNullException(nameof(responseLine));
        }

        string trimmed = TrimLineEnd(responseLine);
        if (string.Equals(trimmed, PersistentConnectResponse, StringComparison.Ordinal))
        {
            return true;
        }

        if (trimmed.StartsWith(OkPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        throw new RobotException(
            RobotErrorKind.ConnectionFailed,
            $"YRC CONNECT 핸드셰이크에 실패했습니다: '{trimmed}'");
    }

    /// <summary>Phase 1 <c>HOSTCTRL_REQUEST</c> 응답이 <c>OK:</c>로 시작하는지 검증합니다.</summary>
    public static void ValidatePhase1Ack(string responseLine)
    {
        if (responseLine is null)
        {
            throw new ArgumentNullException(nameof(responseLine));
        }

        string trimmed = TrimLineEnd(responseLine);
        if (!trimmed.StartsWith(OkPrefix, StringComparison.Ordinal))
        {
            throw new RobotDeviceException(0, trimmed);
        }
    }

    /// <summary>Phase 3 <c>\r</c> 종료 응답을 해석하고 후속 <c>\n</c> 소비 필요 여부를 판정합니다.</summary>
    public static string ParsePhase3Result(string rawLineWithoutCr, out bool shouldDrainNewline)
    {
        if (rawLineWithoutCr is null)
        {
            throw new ArgumentNullException(nameof(rawLineWithoutCr));
        }

        if (rawLineWithoutCr.Length == 0)
        {
            shouldDrainNewline = false;
            throw new RobotException(RobotErrorKind.InvalidResponse, "Return is Null");
        }

        if (rawLineWithoutCr.StartsWith(ErrorPrefix, StringComparison.Ordinal))
        {
            shouldDrainNewline = true;
            return rawLineWithoutCr;
        }

        if (string.Equals(rawLineWithoutCr, SuccessZeroResult, StringComparison.Ordinal))
        {
            shouldDrainNewline = true;
            return SuccessZeroResult;
        }

        shouldDrainNewline = false;
        return rawLineWithoutCr;
    }

    /// <summary><c>ERROR: ... (code).</c> 문자열에서 숫자 에러 코드를 추출해 <see cref="RobotDeviceException"/>을 생성합니다.</summary>
    public static RobotDeviceException ParseErrorResultException(string errorLine)
    {
        if (errorLine is null)
        {
            throw new ArgumentNullException(nameof(errorLine));
        }

        Match match = Regex.Match(errorLine, @"\(([0-9]+)\)\.?$", RegexOptions.CultureInvariant, ErrorRegexTimeout);
        if (match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int code))
        {
            return new RobotDeviceException(code, $"{YrcErrorCode.GetDescription(code)} ({errorLine})");
        }

        return new RobotDeviceException(0, errorLine);
    }

    /// <summary><c>RSTATS</c> CSV 2바이트 응답을 16비트 상태 배열로 파싱합니다.</summary>
    public static bool[] ParseStatsBits(string csv)
    {
        if (csv is null)
        {
            throw new ArgumentNullException(nameof(csv));
        }

        string[] tokens = csv.Split(',');
        if (tokens.Length < MinimumStatsTokenCount
            || !byte.TryParse(tokens[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out byte lowByte)
            || !byte.TryParse(tokens[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out byte highByte))
        {
            throw new RobotException(RobotErrorKind.ParseError, $"YRC RSTATS 응답('{csv}')을 2바이트로 파싱하지 못했습니다.");
        }

        return RobotBitPacking.Unpack(new[] { lowByte, highByte }, 0, StatusBitCount);
    }

    /// <summary><c>IOREAD</c> CSV 바이트 목록을 불리언 배열로 파싱합니다.</summary>
    public static bool[] ParseIoReadBits(string csv, int expectedBitCount)
    {
        if (csv is null)
        {
            throw new ArgumentNullException(nameof(csv));
        }

        if (expectedBitCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedBitCount), expectedBitCount, "기대 비트 수는 0 이상이어야 합니다.");
        }

        string[] tokens = csv.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        byte[] bytes = new byte[tokens.Length];
        for (int i = 0; i < tokens.Length; i++)
        {
            if (!byte.TryParse(tokens[i].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out bytes[i]))
            {
                throw new RobotException(RobotErrorKind.ParseError, $"YRC IOREAD 토큰('{tokens[i]}')을 바이트로 파싱하지 못했습니다.");
            }
        }

        int requiredBytes = (expectedBitCount + BitsPerByte - 1) / BitsPerByte;
        if (bytes.Length < requiredBytes)
        {
            throw new RobotException(
                RobotErrorKind.InvalidResponse,
                $"YRC IOREAD 응답 바이트 수({bytes.Length})가 요청 비트 수({expectedBitCount})에 필요한 {requiredBytes}바이트보다 적습니다.");
        }

        return RobotBitPacking.Unpack(bytes, 0, expectedBitCount);
    }

    internal static string DecodeLine(byte[] frameBytes, Encoding encoding, bool stripCrLf)
    {
        int length = frameBytes.Length;
        if (stripCrLf && length >= CrLfLength && frameBytes[length - 2] == CarriageReturn && frameBytes[length - 1] == LineFeed)
        {
            length -= CrLfLength;
        }
        else if (!stripCrLf && length >= 1 && frameBytes[length - 1] == CarriageReturn)
        {
            length -= 1;
        }

        return encoding.GetString(frameBytes, 0, length);
    }

    private static string TrimLineEnd(string value) => value.TrimEnd('\r', '\n');
}
