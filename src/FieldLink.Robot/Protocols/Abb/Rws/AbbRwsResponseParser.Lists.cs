using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace FieldLink.Robot.Protocols.Abb.Rws;

public static partial class AbbRwsResponseParser
{
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
        Match match = MatchWithTimeout(xhtml, pattern);
        if (!match.Success)
        {
            throw new RobotException(
                RobotErrorKind.ParseError,
                $"ABB RWS 응답에서 li class='{listItemClassName}' 요소를 찾지 못했습니다.",
                Encoding.UTF8.GetBytes(xhtml));
        }

        return ParseListItemElement(match.Value, xhtml);
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
        MatchCollection matches = MatchesWithTimeout(xhtml, pattern);
        int limit = Math.Min(matches.Count, maxCount);
        var items = new List<IReadOnlyDictionary<string, string>>(limit);

        for (int i = 0; i < limit; i++)
        {
            items.Add(ParseListItemElement(matches[i].Value, xhtml));
        }

        return items.AsReadOnly();
    }

    private static IReadOnlyDictionary<string, string> ParseListItemElement(string listItemXml, string fullXhtml)
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
                Encoding.UTF8.GetBytes(fullXhtml),
                exception);
        }
    }
}
