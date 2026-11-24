using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using FieldLink.Robot.Conversion;

namespace FieldLink.Robot.Protocols.Abb.Rws;

/// <summary>ABB RWS XHTML 응답 본문을 도메인 값으로 해석하는 순수 정적 파서입니다.</summary>
public static class AbbRwsResponseParser
{
    private const string JointTargetClassPattern = "((rax_[0-9]+)|(eax_[a-z]))";

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
        Match match = Regex.Match(xhtml, pattern, RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            throw new RobotException(
                RobotErrorKind.ParseError,
                $"ABB RWS 응답에서 span class='{className}' 요소를 찾지 못했습니다.");
        }

        return match.Groups[1].Value;
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

    /// <summary>지정된 클래스명의 <c>&lt;li&gt;</c> 블록 내부 자식 <c>&lt;span&gt;</c> 요소들을 사전으로 파싱합니다.</summary>
    /// <param name="xhtml">ABB RWS XHTML 응답 문자열입니다.</param>
    /// <param name="listItemClassName">대상 <c>li</c>의 <c>class</c> 속성 이름입니다.</param>
    /// <returns>키-값 사전입니다.</returns>
    /// <exception cref="RobotException">해당 항목이 없거나 XML 파싱에 실패했을 때.</exception>
    public static IReadOnlyDictionary<string, string> ParseObjectByClass(string xhtml, string listItemClassName)
    {
        if (xhtml is null)
        {
            throw new ArgumentNullException(nameof(xhtml));
        }

        if (string.IsNullOrWhiteSpace(listItemClassName))
        {
            throw new ArgumentException("리스트 항목 클래스 이름은 필수입니다.", nameof(listItemClassName));
        }

        string pattern = $"<li class=\"{Regex.Escape(listItemClassName)}\"[\\S\\s]+?</li>";
        Match match = Regex.Match(xhtml, pattern, RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            throw new RobotException(
                RobotErrorKind.ParseError,
                $"ABB RWS 응답에서 li class='{listItemClassName}' 요소를 찾지 못했습니다.");
        }

        return ParseListItemElement(match.Value);
    }

    /// <summary>지정된 클래스명의 <c>&lt;li&gt;</c> 블록들을 최대 <paramref name="maxCount"/>개까지 사전 목록으로 파싱합니다.</summary>
    /// <param name="xhtml">ABB RWS XHTML 응답 문자열입니다.</param>
    /// <param name="listItemClassName">대상 <c>li</c>의 <c>class</c> 속성 이름입니다.</param>
    /// <param name="maxCount">수집할 최대 항목 수입니다.</param>
    /// <returns>사전 목록입니다.</returns>
    public static IReadOnlyList<IReadOnlyDictionary<string, string>> ParseArrayByClass(
        string xhtml,
        string listItemClassName,
        int maxCount)
    {
        if (xhtml is null)
        {
            throw new ArgumentNullException(nameof(xhtml));
        }

        if (string.IsNullOrWhiteSpace(listItemClassName))
        {
            throw new ArgumentException("리스트 항목 클래스 이름은 필수입니다.", nameof(listItemClassName));
        }

        if (maxCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCount), maxCount, "최대 개수는 0 이상이어야 합니다.");
        }

        string pattern = $"<li class=\"{Regex.Escape(listItemClassName)}\"[\\S\\s]+?</li>";
        MatchCollection matches = Regex.Matches(xhtml, pattern, RegexOptions.CultureInvariant);
        int limit = Math.Min(matches.Count, maxCount);
        var items = new List<IReadOnlyDictionary<string, string>>(limit);

        for (int i = 0; i < limit; i++)
        {
            items.Add(ParseListItemElement(matches[i].Value));
        }

        return items.AsReadOnly();
    }

    internal static string ParseShortcutAsString(AbbRwsAddress address, string xhtml)
    {
        if (address.IsRawUrl)
        {
            return xhtml;
        }

        return address.Shortcut!.Value switch
        {
            AbbRwsShortcutKind.CtrlState => ParseSpanByClass(xhtml, "ctrlstate"),
            AbbRwsShortcutKind.ErrorState => ParseSpanByClass(xhtml, "err-state"),
            AbbRwsShortcutKind.JointTarget => RobotJsonWriter.SerializeDoubleArray(ParseJointTarget(xhtml)),
            AbbRwsShortcutKind.PhysicalJoints => RobotJsonWriter.SerializeDoubleArray(ParseJointTarget(xhtml)),
            AbbRwsShortcutKind.SpeedRatio => ParseSpanByClass(xhtml, "speedratio"),
            AbbRwsShortcutKind.OperationMode => ParseSpanByClass(xhtml, "opmode"),
            AbbRwsShortcutKind.IoIn => ParseSpanByClass(xhtml, "indata"),
            AbbRwsShortcutKind.IoOut => ParseSpanByClass(xhtml, "outdata"),
            AbbRwsShortcutKind.Io2In => ParseSpanByClass(xhtml, "indata"),
            AbbRwsShortcutKind.Io2Out => ParseSpanByClass(xhtml, "outdata"),
            AbbRwsShortcutKind.Log => RobotJsonWriter.SerializeStringDictionaryList(
                ParseArrayByClass(xhtml, "elog-message-li", address.LogCount)),
            AbbRwsShortcutKind.System => RobotJsonWriter.SerializeStringDictionary(
                ParseObjectByClass(xhtml, "sys-system-li")),
            AbbRwsShortcutKind.RobTarget => RobotJsonWriter.SerializeStringDictionary(
                ParseObjectByClass(xhtml, "ms-robtargets")),
            AbbRwsShortcutKind.ServoEnable => RobotJsonWriter.SerializeStringDictionary(
                ParseObjectByClass(xhtml, "ios-signal")),
            AbbRwsShortcutKind.RapidExecution => RobotJsonWriter.SerializeStringDictionary(
                ParseObjectByClass(xhtml, "rap-execution")),
            AbbRwsShortcutKind.RapidTasks => RobotJsonWriter.SerializeStringDictionaryList(
                ParseArrayByClass(xhtml, "rap-task-li", int.MaxValue)),
            _ => throw new ArgumentOutOfRangeException(nameof(address))
        };
    }

    private static double[] ParseDoubleListByClassPattern(string xhtml, string classPattern)
    {
        string pattern = $"<span class=\"{classPattern}\">([^<]+)";
        MatchCollection matches = Regex.Matches(xhtml, pattern, RegexOptions.CultureInvariant);
        double[] values = new double[matches.Count];

        for (int i = 0; i < matches.Count; i++)
        {
            string rawValue = matches[i].Groups[matches[i].Groups.Count - 1].Value.Trim();
            if (!double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
            {
                throw new RobotException(
                    RobotErrorKind.ParseError,
                    $"ABB RWS 숫자 값('{rawValue}')을 부동소수점으로 변환하지 못했습니다.");
            }

            values[i] = parsed;
        }

        return values;
    }

    private static IReadOnlyDictionary<string, string> ParseListItemElement(string listItemXml)
    {
        try
        {
            XElement root = XElement.Parse(listItemXml);
            var properties = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (XElement child in root.Elements())
            {
                if (!string.Equals(child.Name.LocalName, "span", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                XAttribute? classAttribute = child.Attribute("class");
                if (classAttribute is not null)
                {
                    properties[classAttribute.Value] = child.Value;
                }
            }

            return new ReadOnlyDictionary<string, string>(properties);
        }
        catch (XmlException exception)
        {
            throw new RobotException(
                RobotErrorKind.ParseError,
                "ABB RWS XHTML 리스트 항목을 XML로 파싱하는 데 실패했습니다.",
                innerException: exception);
        }
    }
}
