using System;

namespace FieldLink.Plc.Protocols.Siemens.S7Comm;

/// <summary>S7 통신의 연결 대상(랙·슬롯)·TSAP·PDU 길이·포트 설정입니다. (사양서 §1, §3-2, §3-6)</summary>
public sealed class S7CommOptions
{
    private const int MaxRack = 7;
    private const int MaxSlot = 31;
    private const int MinRequestedPduLength = 64;
    private const int MaxRequestedPduLength = 65535;
    private const int DefaultRack = 0;
    private const int DefaultSlot = 1;
    private const int DefaultRequestedPduLength = 480;
    private const ushort DefaultLocalTsap = 0x0100;
    private const int DefaultPort = 102;
    private const int RemoteTsapBase = 0x0100;
    private const int SlotsPerRack = 32;

    /// <summary>랙·슬롯·요청 PDU 길이로 옵션을 만듭니다.</summary>
    /// <param name="rack">대상 CPU의 랙 번호입니다. 0~7.</param>
    /// <param name="slot">대상 CPU의 슬롯 번호입니다. 0~31. S7-300은 보통 2, S7-1200/1500은 보통 0 또는 1입니다.</param>
    /// <param name="requestedPduLength">Setup Communication에서 요청할 PDU 길이입니다. 64~65535, 기본 480.</param>
    /// <exception cref="ArgumentOutOfRangeException">범위를 벗어났을 때.</exception>
    public S7CommOptions(int rack = DefaultRack, int slot = DefaultSlot, int requestedPduLength = DefaultRequestedPduLength)
    {
        if (rack is < 0 or > MaxRack)
        {
            throw new ArgumentOutOfRangeException(nameof(rack), rack, "랙 번호는 0~7이어야 합니다.");
        }

        if (slot is < 0 or > MaxSlot)
        {
            throw new ArgumentOutOfRangeException(nameof(slot), slot, "슬롯 번호는 0~31이어야 합니다.");
        }

        if (requestedPduLength is < MinRequestedPduLength or > MaxRequestedPduLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requestedPduLength),
                requestedPduLength,
                $"요청 PDU 길이는 {MinRequestedPduLength}~{MaxRequestedPduLength}이어야 합니다.");
        }

        Rack = rack;
        Slot = slot;
        RequestedPduLength = requestedPduLength;
        LocalTsap = DefaultLocalTsap;
        Port = DefaultPort;
    }

    /// <summary>대상 CPU의 랙 번호입니다.</summary>
    public int Rack { get; }

    /// <summary>대상 CPU의 슬롯 번호입니다.</summary>
    public int Slot { get; }

    /// <summary>Setup Communication에서 요청할 PDU 길이입니다.</summary>
    public int RequestedPduLength { get; }

    /// <summary>COTP 연결 요청의 호출(로컬) TSAP입니다. 기본값 0x0100.</summary>
    public ushort LocalTsap { get; init; }

    /// <summary>대상 장비의 TCP 포트입니다. 기본값 102.</summary>
    public int Port { get; init; }

    /// <summary>COTP 연결 요청의 피호출(원격) TSAP입니다. 0x0100 + 랙 × 32 + 슬롯입니다.</summary>
    public ushort RemoteTsap => (ushort)(RemoteTsapBase + (Rack * SlotsPerRack) + Slot);
}
