using System;
using System.Collections.Generic;

namespace FieldLink.Robot.Protocols.Yaskawa.HighSpeedEthernet;

/// <summary>YASKAWA High-Speed Ethernet (<c>0x75</c>) 현재 로봇 자세 모델입니다. 생성 후 바뀌지 않습니다.</summary>
public sealed class YercPose
{
    /// <summary>좌표 데이터 형식 코드입니다.</summary>
    public int DataType { get; init; }

    /// <summary>로봇 형태(Form) 비트 필드입니다.</summary>
    public int Form { get; init; }

    /// <summary>툴 번호입니다.</summary>
    public int ToolNumber { get; init; }

    /// <summary>사용자 좌표계 번호입니다.</summary>
    public int UserCoordinateNumber { get; init; }

    /// <summary>확장 형태 비트 필드입니다.</summary>
    public int ExtendedForm { get; init; }

    /// <summary>각 축 좌표 정수 배열(<c>[X, Y, Z, Rx, Ry, Rz, ...]</c>)입니다.</summary>
    public IReadOnlyList<int> Coordinates { get; init; } = Array.Empty<int>();
}
