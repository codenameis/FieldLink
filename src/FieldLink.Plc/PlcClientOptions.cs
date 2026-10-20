using System;
using System.Text;
using FieldLink.Plc.Abstractions;

namespace FieldLink.Plc;

/// <summary>PLC 클라이언트의 시간 제한, 기본 바이트 순서, 문자열 형식 설정입니다.</summary>
public sealed class PlcClientOptions
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);

    /// <summary>기본값으로 설정을 초기화합니다.</summary>
    public PlcClientOptions()
    {
        Timeout = DefaultTimeout;
        StringEncoding = Encoding.ASCII;
    }

    /// <summary>요청 전체에 적용되는 기본 제한 시간입니다. 기본값은 3초입니다.</summary>
    public TimeSpan Timeout { get; init; }

    /// <summary>
    /// 숫자의 바이트 순서입니다.
    /// 지정하지 않으면 프로토콜의 기본 순서(<see cref="PlcProtocolTraits.DefaultByteOrder"/>)를 사용합니다.
    /// </summary>
    public ByteOrder? ByteOrder { get; init; }

    /// <summary>문자열 인코딩입니다. 기본값은 ASCII입니다.</summary>
    public Encoding StringEncoding { get; init; }

    /// <summary>
    /// 문자열 메모리 저장 형식입니다.
    /// 지정하지 않으면 프로토콜의 기본 형식(<see cref="PlcProtocolTraits.StringFormat"/>)을 사용합니다.
    /// </summary>
    public IPlcStringFormat? StringFormat { get; init; }
}
