using System;

namespace FieldLink.Plc.Protocols.Fatek;

/// <summary>FATEK 통신 프로토콜 통신 옵션입니다.</summary>
public sealed class FatekOptions
{
    private const int DefaultMaxWords = 64;
    private const int DefaultMaxBits = 255;

    private int _maxReadWords = DefaultMaxWords;
    private int _maxWriteWords = DefaultMaxWords;
    private int _maxReadBits = DefaultMaxBits;
    private int _maxWriteBits = DefaultMaxBits;

    /// <summary>PLC 슬레이브 국번(Station 번호)입니다. 기본값은 1입니다. (1~254)</summary>
    public byte Station { get; init; } = 1;

    /// <summary>한 번의 요청으로 읽거나 쓸 수 있는 최대 워드 수입니다. 기본값은 64입니다. (최대 64)</summary>
    public int MaxWordsPerRequest
    {
        get => _maxReadWords;
        init
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "최대 워드 수는 1 이상이어야 합니다.");
            }

            _maxReadWords = value;
            _maxWriteWords = value;
        }
    }

    /// <summary>한 번의 요청으로 읽을 수 있는 최대 워드 수입니다. 기본값은 64입니다. (최대 64)</summary>
    public int MaxReadWords
    {
        get => _maxReadWords;
        init
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "최대 읽기 워드 수는 1 이상이어야 합니다.");
            }

            _maxReadWords = value;
        }
    }

    /// <summary>한 번의 요청으로 읽을 수 있는 최대 비트 수입니다. 기본값은 255입니다. (최대 255)</summary>
    public int MaxReadBits
    {
        get => _maxReadBits;
        init
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "최대 읽기 비트 수는 1 이상이어야 합니다.");
            }

            _maxReadBits = value;
        }
    }

    /// <summary>한 번의 요청으로 쓸 수 있는 최대 워드 수입니다. 기본값은 64입니다. (최대 64)</summary>
    public int MaxWriteWords
    {
        get => _maxWriteWords;
        init
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "최대 쓰기 워드 수는 1 이상이어야 합니다.");
            }

            _maxWriteWords = value;
        }
    }

    /// <summary>한 번의 요청으로 쓸 수 있는 최대 비트 수입니다. 기본값은 255입니다. (최대 255)</summary>
    public int MaxWriteBits
    {
        get => _maxWriteBits;
        init
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "최대 쓰기 비트 수는 1 이상이어야 합니다.");
            }

            _maxWriteBits = value;
        }
    }
}
