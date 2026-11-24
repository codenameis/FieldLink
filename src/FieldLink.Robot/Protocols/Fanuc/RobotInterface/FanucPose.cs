using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FieldLink.Robot.Protocols.Fanuc.RobotInterface;

/// <summary>FANUC 로봇 자세(직교 좌표, 형태, 조인트, 프레임 번호) 모델입니다. 생성 후 바뀌지 않습니다.</summary>
public sealed class FanucPose
{
    private const int XyzwprAxisCount = 9;
    private const int ConfigElementCount = 7;

    private static readonly float[] EmptyNineAxes = new float[XyzwprAxisCount];
    private static readonly short[] EmptyConfig = new short[ConfigElementCount];

    private readonly IReadOnlyList<float> _xyzwpr = Array.AsReadOnly(EmptyNineAxes);
    private readonly IReadOnlyList<short> _config = Array.AsReadOnly(EmptyConfig);
    private readonly IReadOnlyList<float> _joint = Array.AsReadOnly(EmptyNineAxes);

    /// <summary>직교 좌표 9요소 <c>[X, Y, Z, W, P, R, E1, E2, E3]</c>입니다.</summary>
    public IReadOnlyList<float> Xyzwpr
    {
        get => _xyzwpr;
        init
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            float[] copy = new float[value.Count];
            for (int i = 0; i < copy.Length; i++)
            {
                copy[i] = value[i];
            }

            _xyzwpr = Array.AsReadOnly(copy);
        }
    }

    /// <summary>형태(Config) 7요소 정수 배열입니다.</summary>
    public IReadOnlyList<short> Config
    {
        get => _config;
        init
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            short[] copy = new short[value.Count];
            for (int i = 0; i < copy.Length; i++)
            {
                copy[i] = value[i];
            }

            _config = Array.AsReadOnly(copy);
        }
    }

    /// <summary>형태 배열을 문자열(<c>N U T 0 0 0</c> 형식)로 변환한 표현입니다.</summary>
    public string ConfigText => FormatConfig(_config);

    /// <summary>직교 좌표 유효 플래그입니다.</summary>
    public bool IsCartesianValid { get; init; }

    /// <summary>조인트 좌표 9요소 <c>[J1..J9]</c>입니다.</summary>
    public IReadOnlyList<float> Joint
    {
        get => _joint;
        init
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            float[] copy = new float[value.Count];
            for (int i = 0; i < copy.Length; i++)
            {
                copy[i] = value[i];
            }

            _joint = Array.AsReadOnly(copy);
        }
    }

    /// <summary>조인트 좌표 유효 플래그입니다.</summary>
    public bool IsJointValid { get; init; }

    /// <summary>사용자 좌표계 번호(UF)입니다.</summary>
    public short UserFrame { get; init; }

    /// <summary>툴 좌표계 번호(UT)입니다.</summary>
    public short UserTool { get; init; }

    private static string FormatConfig(IReadOnlyList<short> config)
    {
        if (config.Count < ConfigElementCount)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.Append(config[0] != 0 ? "F" : "N");
        builder.Append(config[1] != 0 ? "L" : "R");
        builder.Append(config[2] != 0 ? "U" : "D");
        builder.Append(config[3] != 0 ? "T" : "B");
        builder.Append(',');
        builder.Append(config[4].ToString(CultureInfo.InvariantCulture));
        builder.Append(',');
        builder.Append(config[5].ToString(CultureInfo.InvariantCulture));
        builder.Append(',');
        builder.Append(config[6].ToString(CultureInfo.InvariantCulture));
        return builder.ToString();
    }
}
