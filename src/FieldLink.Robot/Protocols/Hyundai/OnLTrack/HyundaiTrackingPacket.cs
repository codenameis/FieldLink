using System;
using System.Collections.Generic;

namespace FieldLink.Robot.Protocols.Hyundai.OnLTrack;

/// <summary>Hyundai OnLTrack 64바이트 UDP 패킷 도메인 모델입니다. 생성 후 바뀌지 않습니다.</summary>
/// <remarks>직교 위치 <c>Data[0..2]</c>는 밀리미터(mm), 회전 <c>Data[3..5]</c>는 도(deg) 단위를 사용합니다.</remarks>
public sealed class HyundaiTrackingPacket
{
    private const int AxisCount = 6;
    private static readonly double[] EmptyData = new double[AxisCount];

    private readonly IReadOnlyList<double> _data = Array.AsReadOnly(EmptyData);

    /// <summary>명령 구분 문자(<c>'S'</c>, <c>'P'</c>, <c>'F'</c>)입니다.</summary>
    public HyundaiTrackingCommand Command { get; init; } = HyundaiTrackingCommand.Process;

    /// <summary>최대 3바이트 ASCII 더미 문자열입니다.</summary>
    public string CharDummy { get; init; } = string.Empty;

    /// <summary>패킷 상태 코드입니다. (PC 시작 ACK=<c>1</c>, 증분 명령=<c>2</c>)</summary>
    public int State { get; init; }

    /// <summary>패킷 시퀀스 카운터입니다.</summary>
    public int Count { get; init; }

    /// <summary>4바이트 정수 예약 영역입니다.</summary>
    public int IntDummy { get; init; }

    /// <summary>6축 좌표 또는 증분 값 <c>[X(mm), Y(mm), Z(mm), Rx(deg), Ry(deg), Rz(deg)]</c>입니다.</summary>
    public IReadOnlyList<double> Data
    {
        get => _data;
        init
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (value.Count != AxisCount)
            {
                throw new ArgumentException("Hyundai 트래킹 데이터는 정확히 6축(X, Y, Z, Rx, Ry, Rz)이어야 합니다.", nameof(value));
            }

            double[] copy = new double[AxisCount];
            for (int i = 0; i < AxisCount; i++)
            {
                copy[i] = value[i];
            }

            _data = Array.AsReadOnly(copy);
        }
    }
}
