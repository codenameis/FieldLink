using System;
using System.Globalization;
using System.Text;
using FieldLink.Robot.Conversion;

namespace FieldLink.Robot.Protocols.Yaskawa.YrcEthernet;

/// <summary>YASKAWA YRC Ethernet Server 핸드셰이크 및 2단계 TCP ASCII 프레임을 생성하는 순수 정적 빌더입니다.</summary>
public static class YrcEthernetRequestBuilder
{
    private const string ConnectCommand = "CONNECT Robot_access KeepAlive:-1\r\n";
    private const int DisplayMessageMaxBytes = 30;
    private const int BitsPerIoGroup = 8;

    /// <summary>초기 연결 핸드셰이크(<c>CONNECT</c>) 프레임을 생성합니다.</summary>
    public static byte[] BuildConnectRequest(Encoding? encoding = null) =>
        (encoding ?? Encoding.ASCII).GetBytes(ConnectCommand);

    /// <summary>Phase 1 <c>HOSTCTRL_REQUEST {CMD} {LEN}\r\n</c> 요청 헤더를 생성합니다.</summary>
    public static byte[] BuildPhase1Header(string command, byte[]? commandDataBytes, Encoding? encoding = null)
    {
        if (command is null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        if (string.IsNullOrWhiteSpace(command))
        {
            throw new ArgumentException("명령 이름은 비어 있을 수 없습니다.", nameof(command));
        }

        // 사양서 08 §3 & §8.1: Phase 2 본문 끝에 \r(1B)만 추가되므로 DATA_LEN은 len + 1
        int dataLength = commandDataBytes is { Length: > 0 } ? commandDataBytes.Length + 1 : 0;
        string header = $"HOSTCTRL_REQUEST {command} {dataLength.ToString(CultureInfo.InvariantCulture)}\r\n";
        return (encoding ?? Encoding.ASCII).GetBytes(header);
    }

    /// <summary>Phase 2 본문 페이로드 끝에 <c>\r</c> 1바이트를 붙여 전송 바이트를 생성합니다.</summary>
    public static byte[] BuildPhase2Payload(byte[] commandDataBytes)
    {
        if (commandDataBytes is null)
        {
            throw new ArgumentNullException(nameof(commandDataBytes));
        }

        byte[] payload = new byte[commandDataBytes.Length + 1];
        Array.Copy(commandDataBytes, 0, payload, 0, commandDataBytes.Length);
        payload[commandDataBytes.Length] = 0x0D;
        return payload;
    }

    internal static string BuildIoReadData(int startAddress, int pointCount)
    {
        if (pointCount <= 0 || pointCount % BitsPerIoGroup != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pointCount), pointCount, $"IOREAD 점수는 {BitsPerIoGroup}의 배수여야 합니다.");
        }

        return $"{startAddress.ToString(CultureInfo.InvariantCulture)},{pointCount.ToString(CultureInfo.InvariantCulture)}";
    }

    internal static string BuildIoWriteData(int startAddress, bool[] values)
    {
        if (values is null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        if (values.Length == 0 || values.Length % BitsPerIoGroup != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(values), values.Length, $"IOWRITE 비트 배열 길이는 {BitsPerIoGroup}의 배수여야 합니다.");
        }

        byte[] packed = RobotBitPacking.Pack(values);
        var builder = new StringBuilder();
        builder.Append(startAddress.ToString(CultureInfo.InvariantCulture));
        builder.Append(',');
        builder.Append(values.Length.ToString(CultureInfo.InvariantCulture));
        for (int i = 0; i < packed.Length; i++)
        {
            builder.Append(',');
            builder.Append(packed[i].ToString(CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    internal static string ValidateDisplayMessage(string message, Encoding encoding)
    {
        if (message is null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        int byteCount = encoding.GetByteCount(message);
        if (byteCount > DisplayMessageMaxBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(message),
                byteCount,
                $"펜던트 표시 메시지(MDSP)는 최대 {DisplayMessageMaxBytes}바이트여야 합니다.");
        }

        return message;
    }
}
