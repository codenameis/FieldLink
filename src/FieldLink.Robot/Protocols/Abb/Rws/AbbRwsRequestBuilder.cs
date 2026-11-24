using System;
using System.Collections.Generic;
using FieldLink.Transport.Http;

namespace FieldLink.Robot.Protocols.Abb.Rws;

/// <summary>ABB RWS HTTP 요청 객체를 생성하는 순수 정적 빌더입니다.</summary>
public static class AbbRwsRequestBuilder
{
    private const string DefaultMechunit = "ROB_1";
    private const string DefaultEventLogLanguage = "en";
    private const string UrlPrefix = "url=";
    private const string CtrlStatePath = "/rw/panel/ctrlstate";
    private const string ErrorStatePath = "/rw/motionsystem/errorstate";
    private const string SpeedRatioPath = "/rw/panel/speedratio";
    private const string OperationModePath = "/rw/panel/opmode";
    private const string DefaultIoDevicePath = "/rw/iosystem/devices/D652_10";
    private const string SecondaryIoDevicePath = "/rw/iosystem/devices/BK5250";
    private const string SystemPath = "/rw/system";
    private const string ServoEnableSignalPath = "/rw/iosystem/signals/Local/DRV_1/DRV1K1";
    private const string RapidExecutionPath = "/rw/rapid/execution";
    private const string RapidTasksPath = "/rw/rapid/tasks";

    private static readonly char[] DisallowedSegmentChars = { '/', '\\', '?', '#' };

    /// <summary>원시 URL에 대한 HTTP GET 요청을 생성합니다.</summary>
    /// <param name="rawUrl">상대 경로 또는 절대 HTTP/HTTPS URL입니다.</param>
    /// <returns>생성된 HTTP 요청 객체입니다.</returns>
    public static HttpTransportRequest BuildRawGetRequest(string rawUrl)
    {
        if (rawUrl is null)
        {
            throw new ArgumentNullException(nameof(rawUrl));
        }

        return new HttpTransportRequest(HttpMethodKind.Get, rawUrl);
    }

    /// <summary>원시 URL에 대한 HTTP POST 요청을 생성합니다.</summary>
    /// <param name="rawUrl">상대 경로 또는 절대 HTTP/HTTPS URL입니다.</param>
    /// <param name="bodyBytes">전송할 요청 본문 바이트입니다.</param>
    /// <param name="contentType">선택적 Content-Type 헤더입니다.</param>
    /// <param name="headers">선택적 추가 HTTP 요청 헤더입니다.</param>
    /// <returns>생성된 HTTP POST 요청 객체입니다.</returns>
    public static HttpTransportRequest BuildRawPostRequest(
        string rawUrl,
        byte[] bodyBytes,
        string? contentType = null,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        if (rawUrl is null)
        {
            throw new ArgumentNullException(nameof(rawUrl));
        }

        if (bodyBytes is null)
        {
            throw new ArgumentNullException(nameof(bodyBytes));
        }

        return headers is null
            ? new HttpTransportRequest(HttpMethodKind.Post, rawUrl, bodyBytes, contentType)
            : new HttpTransportRequest(HttpMethodKind.Post, rawUrl, bodyBytes, contentType) { Headers = headers };
    }

    /// <summary>해석된 ABB RWS 단축 주소에 대응하는 HTTP GET 요청을 생성합니다.</summary>
    /// <param name="address">해석된 주소 객체입니다.</param>
    /// <param name="eventLogLanguage">로그 단축 주소일 때 사용할 언어 코드입니다.</param>
    /// <returns>생성된 HTTP GET 요청 객체입니다.</returns>
    public static HttpTransportRequest BuildRequest(AbbRwsAddress address, string eventLogLanguage = DefaultEventLogLanguage)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (address.RawUrl is string rawUrl)
        {
            return BuildRawGetRequest(rawUrl);
        }

        if (address.Shortcut is not AbbRwsShortcutKind shortcut)
        {
            throw new ArgumentOutOfRangeException(nameof(address));
        }

        string path = shortcut switch
        {
            AbbRwsShortcutKind.CtrlState => CtrlStatePath,
            AbbRwsShortcutKind.ErrorState => ErrorStatePath,
            AbbRwsShortcutKind.JointTarget => $"/rw/motionsystem/mechunits/{DefaultMechunit}/jointtarget",
            AbbRwsShortcutKind.PhysicalJoints => $"/rw/motionsystem/mechunits/{DefaultMechunit}/jointtarget",
            AbbRwsShortcutKind.SpeedRatio => SpeedRatioPath,
            AbbRwsShortcutKind.OperationMode => OperationModePath,
            AbbRwsShortcutKind.IoIn => DefaultIoDevicePath,
            AbbRwsShortcutKind.IoOut => DefaultIoDevicePath,
            AbbRwsShortcutKind.Io2In => SecondaryIoDevicePath,
            AbbRwsShortcutKind.Io2Out => SecondaryIoDevicePath,
            AbbRwsShortcutKind.Log => BuildEventLogPath(eventLogLanguage),
            AbbRwsShortcutKind.System => SystemPath,
            AbbRwsShortcutKind.RobTarget => $"/rw/motionsystem/mechunits/{DefaultMechunit}/robtarget",
            AbbRwsShortcutKind.ServoEnable => ServoEnableSignalPath,
            AbbRwsShortcutKind.RapidExecution => RapidExecutionPath,
            AbbRwsShortcutKind.RapidTasks => RapidTasksPath,
            _ => throw new ArgumentOutOfRangeException(nameof(address))
        };

        return new HttpTransportRequest(HttpMethodKind.Get, path);
    }

    /// <summary>지정 기구 단위(<c>mechunit</c>)의 조인트 목표 각도 조회 요청을 생성합니다.</summary>
    /// <param name="mechunit">기구 단위 이름입니다. 기본값은 <c>ROB_1</c>입니다.</param>
    public static HttpTransportRequest BuildJointTargetRequest(string mechunit = DefaultMechunit)
    {
        ValidateSegment(mechunit, nameof(mechunit));
        return new HttpTransportRequest(HttpMethodKind.Get, $"/rw/motionsystem/mechunits/{Uri.EscapeDataString(mechunit)}/jointtarget");
    }

    /// <summary>지정 기구 단위(<c>mechunit</c>)의 직교 목표 좌표 조회 요청을 생성합니다.</summary>
    /// <param name="mechunit">기구 단위 이름입니다. 기본값은 <c>ROB_1</c>입니다.</param>
    public static HttpTransportRequest BuildRobotTargetRequest(string mechunit = DefaultMechunit)
    {
        ValidateSegment(mechunit, nameof(mechunit));
        return new HttpTransportRequest(HttpMethodKind.Get, $"/rw/motionsystem/mechunits/{Uri.EscapeDataString(mechunit)}/robtarget");
    }

    /// <summary>IO 장치 입출력 조회 요청을 생성합니다.</summary>
    /// <param name="deviceName">IO 장치 이름입니다. (예: <c>D652_10</c>, <c>BK5250</c>)</param>
    public static HttpTransportRequest BuildDeviceIoRequest(string deviceName)
    {
        ValidateSegment(deviceName, nameof(deviceName));
        return new HttpTransportRequest(HttpMethodKind.Get, $"/rw/iosystem/devices/{Uri.EscapeDataString(deviceName)}");
    }

    /// <summary>개별 IO 신호 조회 요청을 생성합니다.</summary>
    /// <param name="network">네트워크 이름입니다. (예: <c>Local</c>)</param>
    /// <param name="unit">장치 이름입니다. (예: <c>DRV_1</c>)</param>
    /// <param name="signal">신호 이름입니다. (예: <c>DRV1K1</c>)</param>
    public static HttpTransportRequest BuildIoSignalRequest(string network, string unit, string signal)
    {
        ValidateSegment(network, nameof(network));
        ValidateSegment(unit, nameof(unit));
        ValidateSegment(signal, nameof(signal));
        return new HttpTransportRequest(
            HttpMethodKind.Get,
            $"/rw/iosystem/signals/{Uri.EscapeDataString(network)}/{Uri.EscapeDataString(unit)}/{Uri.EscapeDataString(signal)}");
    }

    /// <summary>이벤트 로그 조회 요청을 생성합니다. (사양서 §9.1 쿼리 구분자 수정 반영)</summary>
    /// <param name="language">로그 언어 코드입니다. 기본값은 <c>en</c>입니다.</param>
    public static HttpTransportRequest BuildEventLogsRequest(string language = DefaultEventLogLanguage) =>
        new(HttpMethodKind.Get, BuildEventLogPath(language));

    /// <summary>RAPID 사용자 심볼 변수 배열 조회 요청을 생성합니다.</summary>
    /// <param name="symbolNameOrUrl">심볼 이름 또는 <c>url=...</c> 전체 경로입니다.</param>
    public static HttpTransportRequest BuildRapidUserValuesRequest(string symbolNameOrUrl)
    {
        if (symbolNameOrUrl is null)
        {
            throw new ArgumentNullException(nameof(symbolNameOrUrl));
        }

        if (string.IsNullOrWhiteSpace(symbolNameOrUrl))
        {
            throw new ArgumentException("심볼 이름 또는 URL은 비어 있을 수 없습니다.", nameof(symbolNameOrUrl));
        }

        if (symbolNameOrUrl.StartsWith(UrlPrefix, StringComparison.OrdinalIgnoreCase))
        {
            string rawUrl = symbolNameOrUrl.Substring(UrlPrefix.Length);
            if (string.IsNullOrWhiteSpace(rawUrl))
            {
                throw new ArgumentException("원시 URL은 비어 있을 수 없습니다.", nameof(symbolNameOrUrl));
            }

            return new HttpTransportRequest(HttpMethodKind.Get, rawUrl);
        }

        ValidateSegment(symbolNameOrUrl, nameof(symbolNameOrUrl));
        return new HttpTransportRequest(HttpMethodKind.Get, $"/rw/rapid/symbol/data/RAPID/T_ROB1/user/{Uri.EscapeDataString(symbolNameOrUrl)}");
    }

    private static string BuildEventLogPath(string language)
    {
        ValidateSegment(language, nameof(language));
        return $"/rw/elog/0?lang={Uri.EscapeDataString(language)}&resource=title";
    }

    private static void ValidateSegment(string value, string parameterName)
    {
        if (value is null)
        {
            throw new ArgumentNullException(parameterName);
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("경로 매개변수는 비어 있을 수 없습니다.", parameterName);
        }

        if (value.IndexOfAny(DisallowedSegmentChars) >= 0)
        {
            throw new ArgumentException($"경로 세그먼트('{value}')에 경로 구분자 또는 쿼리 문자를 포함할 수 없습니다.", parameterName);
        }
    }
}
