using System;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using FieldLink.Robot.Conversion;

namespace FieldLink.Robot.Protocols.Abb.Rws;

/// <summary>ABB RWS XHTML 응답 본문을 도메인 값으로 해석하는 순수 정적 파서입니다.</summary>
public static partial class AbbRwsResponseParser
{
    private const string JointTargetClassPattern = "((rax_[0-9]+)|(eax_[a-z]))";
    private const string CtrlStateClassName = "ctrlstate";
    private const string ErrorStateClassName = "err-state";
    private const string SpeedRatioClassName = "speedratio";
    private const string OperationModeClassName = "opmode";
    private const string InputDataClassName = "indata";
    private const string OutputDataClassName = "outdata";
    private const string EventLogItemClassName = "elog-message-li";
    private const string SystemItemClassName = "sys-system-li";
    private const string RobTargetItemClassName = "ms-robtargets";
    private const string IoSignalItemClassName = "ios-signal";
    private const string RapidExecutionItemClassName = "rap-execution";
    private const string RapidTaskItemClassName = "rap-task-li";
    private const int RegexTimeoutSeconds = 1;

    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(RegexTimeoutSeconds);

    /// <summary>옵션에 따라 HTTP 응답 바이트를 UTF-8 또는 ISO-8859-1 문자열로 디코딩합니다.</summary>
    /// <param name="responseBytes">수신한 원시 응답 바이트입니다.</param>
    /// <param name="useIso88591">ISO-8859-1 디코딩을 강제할지 여부입니다.</param>
    /// <returns>디코딩된 문자열입니다.</returns>
    public static string DecodeBody(byte[] responseBytes, bool useIso88591)
    {
        if (responseBytes is null)
        {
            throw new ArgumentNullException(nameof(responseBytes));
        }

        Encoding encoding = useIso88591 ? Encoding.GetEncoding("ISO-8859-1") : Encoding.UTF8;
        return encoding.GetString(responseBytes);
    }

    /// <summary>지정된 CSS 클래스명을 가진 첫 번째 <c>&lt;span&gt;</c> 요소의 텍스트 값을 추출합니다.</summary>
    /// <param name="xhtml">ABB RWS XHTML 응답 문자열입니다.</param>
    /// <param name="className">대상 <c>span</c>의 <c>class</c> 속성 이름입니다.</param>
    /// <returns>추출된 텍스트 값입니다.</returns>
    /// <exception cref="RobotException">일치하는 요소가 없을 때 <see cref="RobotErrorKind.ParseError"/>로 발생합니다.</exception>
    public static string ParseSpanByClass(string xhtml, string className)
    {
        if (xhtml is null)
        {
            throw new ArgumentNullException(nameof(xhtml));
        }

        if (string.IsNullOrWhiteSpace(className))
        {
            throw new ArgumentException("클래스 이름은 필수입니다.", nameof(className));
        }

        string pattern = $"<span class=\"{Regex.Escape(className)}\">([^<]+)";
        Match match = MatchWithTimeout(xhtml, pattern);
        if (!match.Success)
        {
            throw new RobotException(
                RobotErrorKind.ParseError,
                $"ABB RWS 응답에서 span class='{className}' 요소를 찾지 못했습니다.",
                Encoding.UTF8.GetBytes(xhtml));
        }

        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    /// <summary>지정된 클래스명을 가진 모든 <c>&lt;span&gt;</c> 요소의 실수 값 배열을 추출합니다.</summary>
    /// <param name="xhtml">ABB RWS XHTML 응답 문자열입니다.</param>
    /// <param name="className">대상 <c>span</c>의 <c>class</c> 속성 이름입니다.</param>
    /// <returns>파싱된 실수 배열입니다.</returns>
    public static double[] ParseDoubleListSpanByClass(string xhtml, string className)
    {
        if (xhtml is null)
        {
            throw new ArgumentNullException(nameof(xhtml));
        }

        if (string.IsNullOrWhiteSpace(className))
        {
            throw new ArgumentException("클래스 이름은 필수입니다.", nameof(className));
        }

        return ParseDoubleListByClassPattern(xhtml, Regex.Escape(className));
    }

    /// <summary><c>jointtarget</c> XHTML 응답에서 로봇 관절 및 외부 축 각도 배열을 추출합니다.</summary>
    /// <param name="xhtml">조인트 목표 좌표 XHTML 응답 문자열입니다.</param>
    /// <returns>관절 각도 배열입니다.</returns>
    public static double[] ParseJointTarget(string xhtml)
    {
        if (xhtml is null)
        {
            throw new ArgumentNullException(nameof(xhtml));
        }

        return ParseDoubleListByClassPattern(xhtml, JointTargetClassPattern);
    }

    internal static string ParseShortcutAsString(AbbRwsAddress address, string xhtml)
    {
        if (address.IsRawUrl)
        {
            return xhtml;
        }

        if (address.Shortcut is not AbbRwsShortcutKind shortcut)
        {
            throw new ArgumentOutOfRangeException(nameof(address));
        }

        return shortcut switch
        {
            AbbRwsShortcutKind.CtrlState => ParseSpanByClass(xhtml, CtrlStateClassName),
            AbbRwsShortcutKind.ErrorState => ParseSpanByClass(xhtml, ErrorStateClassName),
            AbbRwsShortcutKind.JointTarget => RobotJsonWriter.SerializeDoubleArray(ParseJointTarget(xhtml)),
            AbbRwsShortcutKind.PhysicalJoints => RobotJsonWriter.SerializeDoubleArray(ParseJointTarget(xhtml)),
            AbbRwsShortcutKind.SpeedRatio => ParseSpanByClass(xhtml, SpeedRatioClassName),
            AbbRwsShortcutKind.OperationMode => ParseSpanByClass(xhtml, OperationModeClassName),
            AbbRwsShortcutKind.IoIn => ParseSpanByClass(xhtml, InputDataClassName),
            AbbRwsShortcutKind.IoOut => ParseSpanByClass(xhtml, OutputDataClassName),
            AbbRwsShortcutKind.Io2In => ParseSpanByClass(xhtml, InputDataClassName),
            AbbRwsShortcutKind.Io2Out => ParseSpanByClass(xhtml, OutputDataClassName),
            AbbRwsShortcutKind.Log => RobotJsonWriter.SerializeStringDictionaryList(
                ParseArrayByClass(xhtml, EventLogItemClassName, address.LogCount)),
            AbbRwsShortcutKind.System => RobotJsonWriter.SerializeStringDictionary(
                ParseObjectByClass(xhtml, SystemItemClassName)),
            AbbRwsShortcutKind.RobTarget => RobotJsonWriter.SerializeStringDictionary(
                ParseObjectByClass(xhtml, RobTargetItemClassName)),
            AbbRwsShortcutKind.ServoEnable => RobotJsonWriter.SerializeStringDictionary(
                ParseObjectByClass(xhtml, IoSignalItemClassName)),
            AbbRwsShortcutKind.RapidExecution => RobotJsonWriter.SerializeStringDictionary(
                ParseObjectByClass(xhtml, RapidExecutionItemClassName)),
            AbbRwsShortcutKind.RapidTasks => RobotJsonWriter.SerializeStringDictionaryList(
                ParseArrayByClass(xhtml, RapidTaskItemClassName, int.MaxValue)),
            _ => throw new ArgumentOutOfRangeException(nameof(address))
        };
    }

    private static double[] ParseDoubleListByClassPattern(string xhtml, string classPattern)
    {
        string pattern = $"<span class=\"{classPattern}\">([^<]+)";
        MatchCollection matches = MatchesWithTimeout(xhtml, pattern);
        double[] values = new double[matches.Count];

        for (int i = 0; i < matches.Count; i++)
        {
            string rawValue = WebUtility.HtmlDecode(matches[i].Groups[matches[i].Groups.Count - 1].Value).Trim();
            if (!double.TryParse(rawValue, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double parsed))
            {
                throw new RobotException(
                    RobotErrorKind.ParseError,
                    $"ABB RWS 숫자 값('{rawValue}')을 부동소수점으로 변환하지 못했습니다.",
                    Encoding.UTF8.GetBytes(xhtml));
            }

            values[i] = parsed;
        }

        return values;
    }

    private static Match MatchWithTimeout(string xhtml, string pattern)
    {
        try
        {
            return Regex.Match(xhtml, pattern, RegexOptions.CultureInvariant, RegexTimeout);
        }
        catch (RegexMatchTimeoutException exception)
        {
            throw new RobotException(
                RobotErrorKind.ParseError,
                "ABB RWS XHTML 응답 정규식 탐색 제한 시간이 초과되었습니다.",
                Encoding.UTF8.GetBytes(xhtml),
                exception);
        }
    }

    private static MatchCollection MatchesWithTimeout(string xhtml, string pattern)
    {
        try
        {
            return Regex.Matches(xhtml, pattern, RegexOptions.CultureInvariant, RegexTimeout);
        }
        catch (RegexMatchTimeoutException exception)
        {
            throw new RobotException(
                RobotErrorKind.ParseError,
                "ABB RWS XHTML 응답 정규식 탐색 제한 시간이 초과되었습니다.",
                Encoding.UTF8.GetBytes(xhtml),
                exception);
        }
    }
}
