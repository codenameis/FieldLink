using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using FieldLink.Robot.Conversion;

namespace FieldLink.Robot.Protocols.Estun.Modbus;

/// <summary>Estun 컨트롤러 레지스터 <c>0..99</c>(200바이트)의 상태 스냅샷 모델입니다. 생성 후 바뀌지 않습니다.</summary>
public sealed class EstunRobotSnapshot
{
    private static readonly bool[] EmptyIoBits = new bool[64];
    private static readonly float[] EmptyAnalogChannels = new float[16];

    /// <summary>현재 글로벌 속도 비율 값입니다.</summary>
    public short GlobalSpeedValue { get; init; }

    /// <summary>수동 운전 모드 여부입니다. (<c>Bit 0</c>)</summary>
    public bool IsManualMode { get; init; }

    /// <summary>자동 운전 모드 여부입니다. (<c>Bit 1</c>)</summary>
    public bool IsAutoMode { get; init; }

    /// <summary>원격 운전 모드 여부입니다. (<c>Bit 2</c>)</summary>
    public bool IsRemoteMode { get; init; }

    /// <summary>서보 Enable 상태 여부입니다. (<c>Bit 3</c>)</summary>
    public bool IsServoEnabled { get; init; }

    /// <summary>컨트롤러 실행 상태 여부입니다. (<c>Bit 4</c>)</summary>
    public bool IsRunning { get; init; }

    /// <summary>오류 발생 상태 여부입니다. (<c>Bit 5</c>)</summary>
    public bool HasError { get; init; }

    /// <summary>프로그램 실행 중 여부입니다. (<c>Bit 6</c>)</summary>
    public bool IsProgramRunning { get; init; }

    /// <summary>로봇 축 이동 중 여부입니다. (<c>Bit 7</c>)</summary>
    public bool IsMoving { get; init; }

    /// <summary>현재 로드된 프로젝트 이름입니다.</summary>
    public string ProjectName { get; init; } = string.Empty;

    /// <summary>64채널 디지털 출력 상태입니다.</summary>
    public IReadOnlyList<bool> DigitalOutputs { get; init; } = Array.AsReadOnly(EmptyIoBits);

    /// <summary>64채널 디지털 입력 상태입니다.</summary>
    public IReadOnlyList<bool> DigitalInputs { get; init; } = Array.AsReadOnly(EmptyIoBits);

    /// <summary>명령 실행 상태(<c>Reg 18</c>)입니다. 권한 준비 시 <c>0x0801</c>입니다.</summary>
    public ushort RobotCommandStatus { get; init; }

    /// <summary>16채널 아날로그 출력 값입니다.</summary>
    public IReadOnlyList<float> AnalogOutputs { get; init; } = Array.AsReadOnly(EmptyAnalogChannels);

    /// <summary>16채널 아날로그 입력 값입니다.</summary>
    public IReadOnlyList<float> AnalogInputs { get; init; } = Array.AsReadOnly(EmptyAnalogChannels);

    /// <summary>읽기/쓰기 권한 플래그(<c>Reg 99</c>)입니다.</summary>
    public short ReadWriteFlag { get; init; }

    /// <summary>스냅샷 상태를 JSON 문자열로 직렬화합니다.</summary>
    public string ToJson()
    {
        var builder = new StringBuilder();
        builder.Append("{\"GlobalSpeedValue\":");
        builder.Append(GlobalSpeedValue.ToString(CultureInfo.InvariantCulture));
        builder.Append(",\"ProjectName\":");
        RobotJsonWriter.AppendQuotedString(builder, ProjectName);
        builder.Append(",\"IsServoEnabled\":");
        builder.Append(IsServoEnabled ? "true" : "false");
        builder.Append(",\"HasError\":");
        builder.Append(HasError ? "true" : "false");
        builder.Append(",\"IsProgramRunning\":");
        builder.Append(IsProgramRunning ? "true" : "false");
        builder.Append(",\"RobotCommandStatus\":");
        builder.Append(RobotCommandStatus.ToString(CultureInfo.InvariantCulture));
        builder.Append(",\"AnalogOutputs\":");
        RobotJsonWriter.AppendSingleArray(builder, AnalogOutputs);
        builder.Append(",\"AnalogInputs\":");
        RobotJsonWriter.AppendSingleArray(builder, AnalogInputs);
        builder.Append('}');
        return builder.ToString();
    }
}
