using System;

namespace FieldLink.Plc.Protocols.FujiElectric;

/// <summary>Fuji SPB 통신 프로토콜 옵션입니다.</summary>
public sealed class FujiElectricSpbOptions
{
    private const byte DefaultStation = 1;
    private const byte MinStation = 1;
    private const byte MaxStation = 255;
    private const int MinWords = 1;
    private const int MaxSpbWords = 64;

    private byte _station = DefaultStation;
    private int _maxReadWords = MaxSpbWords;
    private int _maxWriteWords = MaxSpbWords;

    /// <summary>PLC 슬레이브 국번(1~255)입니다. 기본값은 1입니다.</summary>
    public byte Station
    {
        get => _station;
        set
        {
            if (value < MinStation || value > MaxStation)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, $"국번은 {MinStation}~{MaxStation} 사이여야 합니다.");
            }

            _station = value;
        }
    }

    /// <summary>1회 요청당 최대 읽기 워드 수입니다. 기본값은 64입니다. (사양서 §A.1.4)</summary>
    public int MaxReadWords
    {
        get => _maxReadWords;
        set => _maxReadWords = ValidateWordLimit(value, nameof(value), "최대 읽기 워드 수");
    }

    /// <summary>1회 요청당 최대 쓰기 워드 수입니다. 기본값은 64입니다.</summary>
    public int MaxWriteWords
    {
        get => _maxWriteWords;
        set => _maxWriteWords = ValidateWordLimit(value, nameof(value), "최대 쓰기 워드 수");
    }

    /// <summary>요청 프레임에 2바이트 ASCII LRC 체크섬을 포함할지 여부입니다. 기본값은 false입니다.</summary>
    public bool IncludeLrc { get; set; }

    private static int ValidateWordLimit(int value, string paramName, string label)
    {
        if (value is < MinWords or > MaxSpbWords)
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"{label}는 {MinWords}~{MaxSpbWords} 사이여야 합니다.");
        }

        return value;
    }
}
