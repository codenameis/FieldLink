using System;
using System.IO.Ports;

namespace FieldLink.Transport.Serial;

/// <summary>시리얼 포트 전송의 통신 파라미터 및 시간 제한 설정입니다.</summary>
public sealed class SerialTransportOptions
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan DefaultSilenceInterval = TimeSpan.FromMilliseconds(2);

    /// <summary>포트 이름과 통신 속도로 시리얼 전송 설정을 초기화합니다.</summary>
    /// <param name="portName">시리얼 포트 이름입니다. (예: "COM1", "/devicePrefix/ttyUSB0")</param>
    /// <param name="baudRate">통신 속도(보율)입니다. 기본값은 9600입니다.</param>
    /// <param name="parity">패리티 비트 설정입니다. Modbus RTU 표준 권장은 Even입니다.</param>
    /// <param name="dataBits">데이터 비트 수입니다. 7 또는 8.</param>
    /// <param name="stopBits">정지 비트 설정입니다. 기본값은 One입니다.</param>
    /// <exception cref="ArgumentNullException"><paramref name="portName"/>이 null일 때.</exception>
    /// <exception cref="ArgumentException"><paramref name="portName"/>이 비어 있거나 공백일 때.</exception>
    /// <exception cref="ArgumentOutOfRangeException">통신 속도 또는 데이터 비트가 유효하지 않을 때.</exception>
    public SerialTransportOptions(
        string portName,
        int baudRate = 9600,
        Parity parity = Parity.Even,
        int dataBits = 8,
        StopBits stopBits = StopBits.One)
    {
        if (portName is null)
        {
            throw new ArgumentNullException(nameof(portName));
        }

        if (string.IsNullOrWhiteSpace(portName))
        {
            throw new ArgumentException("포트 이름은 비어 있을 수 없습니다.", nameof(portName));
        }

        if (baudRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(baudRate), baudRate, "통신 속도는 1 이상이어야 합니다.");
        }

        if (dataBits is not (7 or 8))
        {
            throw new ArgumentOutOfRangeException(nameof(dataBits), dataBits, "데이터 비트는 7 또는 8이어야 합니다.");
        }

        PortName = portName;
        BaudRate = baudRate;
        Parity = parity;
        DataBits = dataBits;
        StopBits = stopBits;
        ReadTimeout = DefaultTimeout;
        WriteTimeout = DefaultTimeout;
        SilenceInterval = DefaultSilenceInterval;
    }

    /// <summary>시리얼 포트 이름입니다.</summary>
    public string PortName { get; }

    /// <summary>통신 속도(Baud rate)입니다.</summary>
    public int BaudRate { get; }

    /// <summary>패리티 검사 방식입니다.</summary>
    public Parity Parity { get; }

    /// <summary>데이터 비트 수입니다.</summary>
    public int DataBits { get; }

    /// <summary>정지 비트 수입니다.</summary>
    public StopBits StopBits { get; }

    /// <summary>읽기 제한 시간입니다. 기본값은 3초입니다.</summary>
    public TimeSpan ReadTimeout { get; init; }

    /// <summary>쓰기 제한 시간입니다. 기본값은 3초입니다.</summary>
    public TimeSpan WriteTimeout { get; init; }

    /// <summary>프레임 간 무수신 대기 간격입니다. (Modbus RTU 3.5문자 시간 등)</summary>
    public TimeSpan SilenceInterval { get; init; }
}
