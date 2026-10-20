using System;
using System.Collections.Generic;

namespace FieldLink.Plc.Protocols.Mitsubishi.MC;

/// <summary>MC 프로토콜이 다루는 디바이스 한 종류의 기호·코드·단위·진법입니다. (사양서 §2)</summary>
public sealed class MCDevice
{
    private const int DecimalBase = 10;
    private const int HexadecimalBase = 16;

    // 기호가 두 글자인 디바이스를 먼저 찾아야 한다. (예: "SD"를 "S"로 잘못 읽지 않는다)
    private static readonly MCDevice[] AllDevices =
    {
        new("SM", 0x91, isBit: true, DecimalBase),
        new("SD", 0xA9, isBit: false, DecimalBase),
        new("SB", 0xA1, isBit: true, HexadecimalBase),
        new("SW", 0xB5, isBit: false, HexadecimalBase),
        new("TS", 0xC1, isBit: true, DecimalBase),
        new("TC", 0xC0, isBit: true, DecimalBase),
        new("TN", 0xC2, isBit: false, DecimalBase),
        new("CS", 0xC4, isBit: true, DecimalBase),
        new("CC", 0xC3, isBit: true, DecimalBase),
        new("CN", 0xC5, isBit: false, DecimalBase),
        new("ZR", 0xB0, isBit: false, DecimalBase),
        new("X", 0x9C, isBit: true, HexadecimalBase),
        new("Y", 0x9D, isBit: true, HexadecimalBase),
        new("M", 0x90, isBit: true, DecimalBase),
        new("L", 0x92, isBit: true, DecimalBase),
        new("F", 0x93, isBit: true, DecimalBase),
        new("V", 0x94, isBit: true, DecimalBase),
        new("B", 0xA0, isBit: true, HexadecimalBase),
        new("S", 0x98, isBit: true, DecimalBase),
        new("D", 0xA8, isBit: false, DecimalBase),
        new("W", 0xB4, isBit: false, HexadecimalBase),
        new("Z", 0xCC, isBit: false, DecimalBase),
        new("R", 0xAF, isBit: false, DecimalBase),
    };

    private MCDevice(string symbol, byte code, bool isBit, int numberBase)
    {
        Symbol = symbol;
        Code = code;
        IsBit = isBit;
        NumberBase = numberBase;
    }

    /// <summary>디바이스 기호입니다. (예: "D", "ZR")</summary>
    public string Symbol { get; }

    /// <summary>바이너리 프레임의 디바이스 코드입니다. iQ-R은 이 값을 2바이트로 넓혀 보냅니다.</summary>
    public byte Code { get; }

    /// <summary>비트 단위 디바이스이면 true, 워드 단위이면 false입니다.</summary>
    public bool IsBit { get; }

    /// <summary>디바이스 번호의 진법입니다. 10 또는 16입니다.</summary>
    public int NumberBase { get; }

    /// <summary>주소 문자열 맨 앞의 디바이스 기호를 찾습니다.</summary>
    /// <param name="addressText">대문자로 바꾼 주소 문자열입니다.</param>
    /// <param name="device">찾은 디바이스입니다.</param>
    /// <returns>알려진 기호로 시작하면 true입니다.</returns>
    public static bool TryFindPrefix(string addressText, out MCDevice? device)
    {
        if (addressText is null)
        {
            throw new ArgumentNullException(nameof(addressText));
        }

        foreach (MCDevice candidate in AllDevices)
        {
            if (addressText.StartsWith(candidate.Symbol, StringComparison.Ordinal))
            {
                device = candidate;
                return true;
            }
        }

        device = null;
        return false;
    }

    /// <summary>지원하는 모든 디바이스 목록입니다. 테스트와 문서 확인용입니다.</summary>
    public static IReadOnlyList<MCDevice> GetAll() => AllDevices;

    /// <inheritdoc />
    public override string ToString() => Symbol;
}
