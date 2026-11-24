using System;

namespace FieldLink.Plc.Protocols.Keyence;

/// <summary>Keyence KV 상위 링크(Host Link) 통신 옵션입니다.</summary>
public sealed class KeyenceHostLinkOptions
{
    private const byte MaxStationNumber = 99;
    private const int MinElementsPerRequest = 1;
    private const int MaxElementsLimit = 1000;
    private const int DefaultMaxElements = 256;

    private readonly byte? _station;
    private readonly int? _maxWordsPerRequest;
    private readonly int? _maxReadElements;
    private readonly int? _maxWriteElements;

    /// <summary>멀티드롭 국번(0~99)입니다. null이면 1:1 직결(국번 생략) 모드입니다.</summary>
    public byte? Station
    {
        get => _station;
        init
        {
            if (value > MaxStationNumber)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, $"국번은 0~{MaxStationNumber} 범위여야 합니다.");
            }

            _station = value;
        }
    }

    /// <summary>1회 요청당 최대 워드/비트 수입니다. 기본값은 256입니다. (1~1000, 사양서 §A.1.3)</summary>
    public int MaxWordsPerRequest
    {
        get => _maxReadElements ?? _maxWordsPerRequest ?? DefaultMaxElements;
        init
        {
            ValidateElementLimit(value, nameof(value), "최대 요청 요소 수");
            _maxWordsPerRequest = value;
        }
    }

    /// <summary>1회 읽기 요청의 최대 워드/비트 수입니다. 기본값은 256입니다. (1~1000)</summary>
    public int MaxReadElements
    {
        get => _maxReadElements ?? _maxWordsPerRequest ?? DefaultMaxElements;
        init
        {
            ValidateElementLimit(value, nameof(value), "최대 읽기 요소 수");
            _maxReadElements = value;
        }
    }

    /// <summary>1회 쓰기 요청의 최대 워드/비트 수입니다. 기본값은 256입니다. (1~1000)</summary>
    public int MaxWriteElements
    {
        get => _maxWriteElements ?? _maxWordsPerRequest ?? DefaultMaxElements;
        init
        {
            ValidateElementLimit(value, nameof(value), "최대 쓰기 요소 수");
            _maxWriteElements = value;
        }
    }

    private static void ValidateElementLimit(int value, string paramName, string label)
    {
        if (value < MinElementsPerRequest || value > MaxElementsLimit)
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                value,
                $"{label}는 {MinElementsPerRequest}~{MaxElementsLimit} 범위여야 합니다.");
        }
    }
}
