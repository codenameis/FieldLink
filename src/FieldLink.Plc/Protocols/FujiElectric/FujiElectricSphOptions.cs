using System;

namespace FieldLink.Plc.Protocols.FujiElectric;

/// <summary>MICREX-SX SPH Ethernet 로더 통신 옵션입니다. (사양서 Part C)</summary>
public sealed class FujiElectricSphOptions
{
    private const byte DefaultConnectionId = 1;
    private const byte MinConnectionId = 0x01;
    private const byte MaxConnectionId = 0xFE;
    private const int MinWords = 1;
    private const int MaxSphWords = 230;

    private byte _connectionId = DefaultConnectionId;
    private int _maxReadWords = MaxSphWords;
    private int _maxWriteWords = MaxSphWords;

    /// <summary>SX-Bus 연결 스테이션 ID(0x01~0xFE)입니다. 기본값은 1입니다. (오프셋 6)</summary>
    public byte ConnectionId
    {
        get => _connectionId;
        init
        {
            if (value < MinConnectionId || value > MaxConnectionId)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, $"연결 ID는 0x{MinConnectionId:X2}~0x{MaxConnectionId:X2} 사이여야 합니다.");
            }

            _connectionId = value;
        }
    }

    /// <summary>1회 요청당 최대 읽기 워드 수입니다. 기본값은 230입니다. (사양서 §C.1.3)</summary>
    public int MaxReadWords
    {
        get => _maxReadWords;
        init => _maxReadWords = ValidateWordLimit(value, nameof(value), "최대 읽기 워드 수");
    }

    /// <summary>1회 요청당 최대 쓰기 워드 수입니다. 기본값은 230입니다.</summary>
    public int MaxWriteWords
    {
        get => _maxWriteWords;
        init => _maxWriteWords = ValidateWordLimit(value, nameof(value), "최대 쓰기 워드 수");
    }

    private static int ValidateWordLimit(int value, string paramName, string label)
    {
        if (value is < MinWords or > MaxSphWords)
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"{label}는 {MinWords}~{MaxSphWords} 사이여야 합니다.");
        }

        return value;
    }
}
