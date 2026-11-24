using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace FieldLink.Robot.Protocols.Abb.Rws;

/// <summary>ABB RWS의 원시 URL(<c>url=...</c>) 또는 단축 주소를 나타내는 불변 주소 객체입니다.</summary>
public sealed class AbbRwsAddress
{
    private const string UrlPrefix = "url=";
    private const string LogPrefix = "log";
    private const int DefaultLogCount = 10;

    private static readonly IReadOnlyDictionary<string, AbbRwsShortcutKind> ShortcutMap =
        new Dictionary<string, AbbRwsShortcutKind>(StringComparer.OrdinalIgnoreCase)
        {
            ["CtrlState"] = AbbRwsShortcutKind.CtrlState,
            ["ErrorState"] = AbbRwsShortcutKind.ErrorState,
            ["jointtarget"] = AbbRwsShortcutKind.JointTarget,
            ["PhysicalJoints"] = AbbRwsShortcutKind.PhysicalJoints,
            ["SpeedRatio"] = AbbRwsShortcutKind.SpeedRatio,
            ["OperationMode"] = AbbRwsShortcutKind.OperationMode,
            ["ioin"] = AbbRwsShortcutKind.IoIn,
            ["ioout"] = AbbRwsShortcutKind.IoOut,
            ["io2in"] = AbbRwsShortcutKind.Io2In,
            ["io2out"] = AbbRwsShortcutKind.Io2Out,
            ["system"] = AbbRwsShortcutKind.System,
            ["robtarget"] = AbbRwsShortcutKind.RobTarget,
            ["ServoEnable"] = AbbRwsShortcutKind.ServoEnable,
            ["RapidExecution"] = AbbRwsShortcutKind.RapidExecution,
            ["RapidTasks"] = AbbRwsShortcutKind.RapidTasks,
        };

    private AbbRwsAddress(bool isRawUrl, string? rawUrl, AbbRwsShortcutKind? shortcut, int logCount)
    {
        IsRawUrl = isRawUrl;
        RawUrl = rawUrl;
        Shortcut = shortcut;
        LogCount = logCount;
    }

    /// <summary>접두사 <c>url=</c>을 사용한 원시 HTTP 경로인지 여부입니다.</summary>
    public bool IsRawUrl { get; }

    /// <summary><c>url=</c> 접두사를 제거한 원시 HTTP 경로입니다. 단축 주소면 null입니다.</summary>
    public string? RawUrl { get; }

    /// <summary>해석된 단축 주소 종류입니다. 원시 URL이면 null입니다.</summary>
    public AbbRwsShortcutKind? Shortcut { get; }

    /// <summary>이벤트 로그 조회 시 최대 수집 개수입니다. 기본값은 10입니다.</summary>
    public int LogCount { get; }

    /// <summary>주소 문자열을 해석해 <see cref="AbbRwsAddress"/>를 생성합니다.</summary>
    /// <param name="address">원시 URL(<c>url=...</c>) 또는 단축 주소 문자열입니다.</param>
    /// <returns>해석된 불변 주소 객체입니다.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="address"/>가 null일 때.</exception>
    /// <exception cref="FormatException">지원하지 않는 단축 주소이거나 형식이 올바르지 않을 때.</exception>
    public static AbbRwsAddress Parse(string address)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (!TryParse(address, out AbbRwsAddress? parsed))
        {
            throw new FormatException($"유효하지 않은 ABB RWS 주소입니다: '{address}'.");
        }

        return parsed;
    }

    /// <summary>주소 문자열을 예외 없이 <see cref="AbbRwsAddress"/>로 해석합니다.</summary>
    /// <param name="address">원시 URL 또는 단축 주소 문자열입니다.</param>
    /// <param name="result">해석에 성공하면 주소 객체, 실패하면 null입니다.</param>
    /// <returns>해석 성공 시 true, 실패 시 false입니다.</returns>
    public static bool TryParse(string? address, [NotNullWhen(true)] out AbbRwsAddress? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(address))
        {
            return false;
        }

        string trimmed = address!.Trim();
        if (trimmed.StartsWith(UrlPrefix, StringComparison.OrdinalIgnoreCase))
        {
            string rawUrl = trimmed.Substring(UrlPrefix.Length);
            if (string.IsNullOrWhiteSpace(rawUrl))
            {
                return false;
            }

            result = new AbbRwsAddress(isRawUrl: true, rawUrl, shortcut: null, DefaultLogCount);
            return true;
        }

        if (trimmed.StartsWith(LogPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return TryParseLogAddress(trimmed, out result);
        }

        if (ShortcutMap.TryGetValue(trimmed, out AbbRwsShortcutKind shortcut))
        {
            result = new AbbRwsAddress(isRawUrl: false, rawUrl: null, shortcut, DefaultLogCount);
            return true;
        }

        return false;
    }

    private static bool TryParseLogAddress(string trimmed, [NotNullWhen(true)] out AbbRwsAddress? result)
    {
        result = null;
        if (trimmed.Length == LogPrefix.Length)
        {
            result = new AbbRwsAddress(isRawUrl: false, rawUrl: null, AbbRwsShortcutKind.Log, DefaultLogCount);
            return true;
        }

        string countPart = trimmed.Substring(LogPrefix.Length);
        if (!int.TryParse(countPart, NumberStyles.None, CultureInfo.InvariantCulture, out int logCount) || logCount <= 0)
        {
            return false;
        }

        result = new AbbRwsAddress(isRawUrl: false, rawUrl: null, AbbRwsShortcutKind.Log, logCount);
        return true;
    }
}
