using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using FieldLink.Robot.Conversion;

namespace FieldLink.Robot.Protocols.Efort.ER7BC10;

/// <summary>EFORT ER7B-C10 컨트롤러의 전체 상태 스냅샷 모델입니다. 생성 후 바뀌지 않습니다.</summary>
public sealed class EfortRobotSnapshot
{
    private static readonly byte[] EmptyDigitalIo = new byte[32];
    private static readonly int[] EmptyIntegerIo = new int[32];
    private static readonly float[] EmptyAxisFloats = new float[7];
    private static readonly float[] EmptyCartesianFloats = new float[6];
    private static readonly int[] EmptyAxisInts = new int[7];

    /// <summary>패킷 시작 문자열입니다.</summary>
    public string PacketStart { get; init; } = string.Empty;

    /// <summary>응답 명령 번호입니다.</summary>
    public ushort PacketOrders { get; init; }

    /// <summary>하트비트 시퀀스 번호입니다.</summary>
    public ushort PacketHeartbeat { get; init; }

    /// <summary>알람 상태입니다. (1: 알람 발생, 0: 정상)</summary>
    public byte ErrorStatus { get; init; }

    /// <summary>비상정지 상태입니다. (1: 정상 해제, 0: 비상정지 활성)</summary>
    public byte HstopStatus { get; init; }

    /// <summary>제어 권한 상태입니다. (1: 권한 있음, 0: 없음)</summary>
    public byte AuthorityStatus { get; init; }

    /// <summary>서보 활성화 상태입니다. (1: Enable, 0: Disable)</summary>
    public byte ServoStatus { get; init; }

    /// <summary>축 이동 상태입니다. (1: 이동 중, 0: 정지)</summary>
    public byte AxisMoveStatus { get; init; }

    /// <summary>프로그램 실행 상태입니다. (1: 실행 중, 0: 미실행)</summary>
    public byte ProgMoveStatus { get; init; }

    /// <summary>프로그램 로드 상태입니다. (1: 로드됨, 0: 미로드)</summary>
    public byte ProgLoadStatus { get; init; }

    /// <summary>프로그램 일시정지 상태입니다. (1: Hold, 0: 미정지)</summary>
    public byte ProgHoldStatus { get; init; }

    /// <summary>운전 모드 상태입니다. (1: 수동, 2: 자동, 3: 원격)</summary>
    public ushort ModeStatus { get; init; }

    /// <summary>속도 비율(%)입니다.</summary>
    public ushort SpeedStatus { get; init; }

    /// <summary>디지털 출력 32채널 값입니다.</summary>
    public IReadOnlyList<byte> DigitalOutputs { get; init; } = Array.AsReadOnly(EmptyDigitalIo);

    /// <summary>디지털 입력 32채널 값입니다.</summary>
    public IReadOnlyList<byte> DigitalInputs { get; init; } = Array.AsReadOnly(EmptyDigitalIo);

    /// <summary>정수형 출력 32채널 값입니다.</summary>
    public IReadOnlyList<int> IntegerOutputs { get; init; } = Array.AsReadOnly(EmptyIntegerIo);

    /// <summary>정수형 입력 32채널 값입니다.</summary>
    public IReadOnlyList<int> IntegerInputs { get; init; } = Array.AsReadOnly(EmptyIntegerIo);

    /// <summary>로드된 프로젝트 이름입니다.</summary>
    public string ProjectName { get; init; } = string.Empty;

    /// <summary>로드된 프로그램 이름입니다.</summary>
    public string ProgramName { get; init; } = string.Empty;

    /// <summary>오류 메시지 문자열입니다.</summary>
    public string ErrorText { get; init; } = string.Empty;

    /// <summary>1~7축 관절 위치(각도) 배열입니다.</summary>
    public IReadOnlyList<float> AxisPositions { get; init; } = Array.AsReadOnly(EmptyAxisFloats);

    /// <summary>직교 좌표계 6축 값 <c>[X, Y, Z, A, B, C]</c>입니다.</summary>
    public IReadOnlyList<float> CartesianPositions { get; init; } = Array.AsReadOnly(EmptyCartesianFloats);

    /// <summary>1~7축 속도 배열입니다.</summary>
    public IReadOnlyList<float> AxisSpeeds { get; init; } = Array.AsReadOnly(EmptyAxisFloats);

    /// <summary>1~7축 가속도 배열입니다.</summary>
    public IReadOnlyList<float> AxisAccelerations { get; init; } = Array.AsReadOnly(EmptyAxisFloats);

    /// <summary>1~7축 가가속도(Jerk) 배열입니다.</summary>
    public IReadOnlyList<float> AxisJerks { get; init; } = Array.AsReadOnly(EmptyAxisFloats);

    /// <summary>1~7축 토크 배열입니다.</summary>
    public IReadOnlyList<float> AxisTorques { get; init; } = Array.AsReadOnly(EmptyAxisFloats);

    /// <summary>1~7축 방향 전환 카운터 배열입니다.</summary>
    public IReadOnlyList<int> AxisDirectionCounts { get; init; } = Array.AsReadOnly(EmptyAxisInts);

    /// <summary>1~7축 누적 구동 시간 배열입니다.</summary>
    public IReadOnlyList<int> AxisTimes { get; init; } = Array.AsReadOnly(EmptyAxisInts);

    /// <summary>컨트롤러 연속 가동 시간입니다.</summary>
    public int DeviceTime { get; init; }

    /// <summary>패킷 종료 문자열입니다.</summary>
    public string PacketEnd { get; init; } = string.Empty;

    /// <summary>스냅샷의 주요 상태와 좌표를 JSON 문자열로 직렬화합니다.</summary>
    public string ToJson()
    {
        var builder = new StringBuilder();
        builder.Append("{\"PacketOrders\":");
        builder.Append(PacketOrders.ToString(CultureInfo.InvariantCulture));
        builder.Append(",\"PacketHeartbeat\":");
        builder.Append(PacketHeartbeat.ToString(CultureInfo.InvariantCulture));
        builder.Append(",\"ErrorStatus\":");
        builder.Append(ErrorStatus.ToString(CultureInfo.InvariantCulture));
        builder.Append(",\"HstopStatus\":");
        builder.Append(HstopStatus.ToString(CultureInfo.InvariantCulture));
        builder.Append(",\"ModeStatus\":");
        builder.Append(ModeStatus.ToString(CultureInfo.InvariantCulture));
        builder.Append(",\"SpeedStatus\":");
        builder.Append(SpeedStatus.ToString(CultureInfo.InvariantCulture));
        builder.Append(",\"ProjectName\":");
        RobotJsonWriter.AppendQuotedString(builder, ProjectName);
        builder.Append(",\"ProgramName\":");
        RobotJsonWriter.AppendQuotedString(builder, ProgramName);
        builder.Append(",\"ErrorText\":");
        RobotJsonWriter.AppendQuotedString(builder, ErrorText);
        builder.Append(",\"AxisPositions\":");
        RobotJsonWriter.AppendSingleArray(builder, AxisPositions);
        builder.Append(",\"CartesianPositions\":");
        RobotJsonWriter.AppendSingleArray(builder, CartesianPositions);
        builder.Append(",\"DeviceTime\":");
        builder.Append(DeviceTime.ToString(CultureInfo.InvariantCulture));
        builder.Append('}');
        return builder.ToString();
    }
}
