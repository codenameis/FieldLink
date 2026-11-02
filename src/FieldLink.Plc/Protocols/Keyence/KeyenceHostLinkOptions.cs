using System;

namespace FieldLink.Plc.Protocols.Keyence;

/// <summary>Keyence KV 상위 링크(Host Link) 통신 옵션입니다.</summary>
public sealed class KeyenceHostLinkOptions
{
    private const int DefaultMaxElements = 256;

    private int _maxReadElements = DefaultMaxElements;
    private int _maxWriteElements = DefaultMaxElements;

    /// <summary>멀티드롭 국번(0~99)입니다. null이면 1:1 직결(국번 생략) 모드입니다.</summary>
    public byte? Station { get; init; }

    /// <summary>1회 요청당 최대 워드/비트 수입니다. 기본값은 256입니다. (사양서 §A.1.3)</summary>
    public int MaxWordsPerRequest
    {
        get => _maxReadElements;
        init
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "최대 요청 요소 수는 1 이상이어야 합니다.");
            }

            _maxReadElements = value;
            _maxWriteElements = value;
        }
    }

    /// <summary>1회 요청당 최대 읽기 요소 수입니다. 기본값은 256입니다. (사양서 §A.1.3)</summary>
    public int MaxReadElements
    {
        get => _maxReadElements;
        init
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "최대 읽기 요소 수는 1 이상이어야 합니다.");
            }

            _maxReadElements = value;
        }
    }

    /// <summary>1회 요청당 최대 쓰기 요소 수입니다. 기본값은 256입니다.</summary>
    public int MaxWriteElements
    {
        get => _maxWriteElements;
        init
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "최대 쓰기 요소 수는 1 이상이어야 합니다.");
            }

            _maxWriteElements = value;
        }
    }
}
