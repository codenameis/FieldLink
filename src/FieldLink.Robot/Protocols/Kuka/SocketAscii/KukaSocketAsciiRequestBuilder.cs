using System;
using System.Collections.Generic;
using System.Text;

namespace FieldLink.Robot.Protocols.Kuka.SocketAscii;

/// <summary>KUKA Socket ASCII 원시 TCP 명령 문자열 바이트를 생성하는 순수 정적 빌더입니다.</summary>
public static class KukaSocketAsciiRequestBuilder
{
    private const string ReadPrefix = "00";
    private const string WritePrefix = "01";
    private const string StartProgramPrefix = "03";
    private const string ResetProgramCommand = "0601";
    private const string StopProgramCommand = "0621";
    private const string AddressSeparator = ",";
    private const string AssignmentSeparator = "=";

    private static readonly char[] DisallowedAddressCharacters = { ',', '=', '\r', '\n', '\0' };
    private static readonly char[] DisallowedControlCharacters = { '\r', '\n', '\0' };

    /// <summary>단일 또는 다중 변수 읽기(<c>00</c>) 요청 바이트를 생성합니다.</summary>
    public static byte[] BuildReadRequest(IReadOnlyList<string> addresses, Encoding? encoding = null)
    {
        if (addresses is null)
        {
            throw new ArgumentNullException(nameof(addresses));
        }

        if (addresses.Count == 0)
        {
            throw new ArgumentException("읽을 변수 주소가 최소 1개 이상 필요합니다.", nameof(addresses));
        }

        for (int i = 0; i < addresses.Count; i++)
        {
            ValidateVariableAddress(addresses[i], nameof(addresses));
        }

        string payload = ReadPrefix + string.Join(AddressSeparator, addresses);
        return (encoding ?? Encoding.UTF8).GetBytes(payload);
    }

    /// <summary>단일 또는 다중 변수 쓰기(<c>01</c>) 요청 바이트를 생성합니다.</summary>
    public static byte[] BuildWriteRequest(
        IReadOnlyList<string> addresses,
        IReadOnlyList<string> values,
        Encoding? encoding = null)
    {
        if (addresses is null)
        {
            throw new ArgumentNullException(nameof(addresses));
        }

        if (values is null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        if (addresses.Count == 0 || addresses.Count != values.Count)
        {
            throw new ArgumentException("쓰기 주소 배열과 값 배열의 길이가 같고 1개 이상이어야 합니다.", nameof(values));
        }

        string[] pairs = new string[addresses.Count];
        for (int i = 0; i < addresses.Count; i++)
        {
            ValidateVariableAddress(addresses[i], nameof(addresses));

            string value = values[i] ?? throw new ArgumentNullException(nameof(values));
            if (value.IndexOfAny(DisallowedControlCharacters) >= 0)
            {
                throw new ArgumentException("쓰기 값에는 CR(\\r), LF(\\n) 또는 널 문자를 포함할 수 없습니다.", nameof(values));
            }

            pairs[i] = $"{addresses[i]}{AssignmentSeparator}{value}";
        }

        string payload = WritePrefix + string.Join(AddressSeparator, pairs);
        return (encoding ?? Encoding.UTF8).GetBytes(payload);
    }

    /// <summary>지정 프로그램 시작(<c>03</c>) 요청 바이트를 생성합니다.</summary>
    public static byte[] BuildStartProgramRequest(string programName, Encoding? encoding = null)
    {
        if (programName is null)
        {
            throw new ArgumentNullException(nameof(programName));
        }

        if (string.IsNullOrWhiteSpace(programName))
        {
            throw new ArgumentException("프로그램 이름은 비어 있을 수 없습니다.", nameof(programName));
        }

        if (programName.IndexOfAny(DisallowedControlCharacters) >= 0)
        {
            throw new ArgumentException("프로그램 이름에 개행 또는 제어 문자를 포함할 수 없습니다.", nameof(programName));
        }

        return (encoding ?? Encoding.UTF8).GetBytes(StartProgramPrefix + programName);
    }

    /// <summary>현재 프로그램 리셋(<c>0601</c>) 요청 바이트를 생성합니다.</summary>
    public static byte[] BuildResetProgramRequest(Encoding? encoding = null) =>
        (encoding ?? Encoding.UTF8).GetBytes(ResetProgramCommand);

    /// <summary>현재 프로그램 정지(<c>0621</c>) 요청 바이트를 생성합니다.</summary>
    public static byte[] BuildStopProgramRequest(Encoding? encoding = null) =>
        (encoding ?? Encoding.UTF8).GetBytes(StopProgramCommand);

    private static void ValidateVariableAddress(string address, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("변수 주소 목록에 비어 있는 항목이 있습니다.", parameterName);
        }

        if (address.IndexOfAny(DisallowedAddressCharacters) >= 0)
        {
            throw new ArgumentException($"변수 주소('{address}')에는 ',', '=', CR(\\r), LF(\\n) 문자를 포함할 수 없습니다.", parameterName);
        }
    }
}
