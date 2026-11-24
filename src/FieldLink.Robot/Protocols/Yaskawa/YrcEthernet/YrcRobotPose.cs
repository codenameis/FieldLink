using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FieldLink.Robot.Protocols.Yaskawa.YrcEthernet;

/// <summary>YASKAWA YRC <c>RPOSC</c> 조회 및 <c>MOVJ</c> 모션 명령에 사용하는 직교 자세 모델입니다.</summary>
public sealed class YrcRobotPose
{
    private const int ExternalAxisCount = 6;
    private static readonly double[] EmptyExternalAxes = new double[ExternalAxisCount];

    private readonly IReadOnlyList<double> _externalAxes = Array.AsReadOnly(EmptyExternalAxes);

    /// <summary>X 좌표(mm)입니다.</summary>
    public double X { get; init; }

    /// <summary>Y 좌표(mm)입니다.</summary>
    public double Y { get; init; }

    /// <summary>Z 좌표(mm)입니다.</summary>
    public double Z { get; init; }

    /// <summary>Rx 회전 각도(deg)입니다.</summary>
    public double Rx { get; init; }

    /// <summary>Ry 회전 각도(deg)입니다.</summary>
    public double Ry { get; init; }

    /// <summary>Rz 회전 각도(deg)입니다.</summary>
    public double Rz { get; init; }

    /// <summary>7축 팔꿈치 각도 <c>Re</c>(deg)입니다. (<see cref="YrcModelType.Yrc100"/> 전용)</summary>
    public double Re { get; init; }

    /// <summary>6비트 형태 플래그 바이트입니다.</summary>
    public byte StatusByte { get; init; }

    /// <summary>툴 번호(<c>0..63</c>)입니다.</summary>
    public int ToolNumber { get; init; }

    /// <summary>외부축 6개(<c>Ax7..Ax12</c>) 좌표 배열입니다.</summary>
    public IReadOnlyList<double> ExternalAxes
    {
        get => _externalAxes;
        init
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (value.Count != ExternalAxisCount)
            {
                throw new ArgumentException("외부축 배열 길이는 6이어야 합니다.", nameof(value));
            }

            double[] copy = new double[ExternalAxisCount];
            for (int i = 0; i < ExternalAxisCount; i++)
            {
                copy[i] = value[i];
            }

            _externalAxes = Array.AsReadOnly(copy);
        }
    }

    /// <summary><c>MOVJ</c> 실행 속도 비율(<c>0.01..100.0</c>)입니다. 기본값은 100.0입니다.</summary>
    public double SpeedPercent { get; init; } = 100.0;

    /// <summary>기준 좌표계(<c>0</c>=기지, <c>1</c>=로봇, <c>2..65</c>=사용자1..64)입니다.</summary>
    public int Frame { get; init; }

    /// <summary><c>RPOSC</c> 응답 CSV 문자열을 <see cref="YrcRobotPose"/>로 해석합니다.</summary>
    public static YrcRobotPose Parse(string csv, YrcModelType modelType)
    {
        if (csv is null)
        {
            throw new ArgumentNullException(nameof(csv));
        }

        string[] tokens = csv.Split(',');
        int minimumTokens = modelType == YrcModelType.Yrc100 ? 9 : 8;
        if (tokens.Length < minimumTokens)
        {
            throw new RobotException(
                RobotErrorKind.ParseError,
                $"YRC RPOSC 응답 필드 수({tokens.Length})가 모델({modelType}) 최소 요구 수({minimumTokens})보다 적습니다.");
        }

        double[] externalAxes = new double[ExternalAxisCount];
        int extStartIndex = minimumTokens;
        for (int i = 0; i < ExternalAxisCount && extStartIndex + i < tokens.Length; i++)
        {
            externalAxes[i] = ParseDouble(tokens[extStartIndex + i]);
        }

        int statusIndex = modelType == YrcModelType.Yrc100 ? 7 : 6;
        return new YrcRobotPose
        {
            X = ParseDouble(tokens[0]),
            Y = ParseDouble(tokens[1]),
            Z = ParseDouble(tokens[2]),
            Rx = ParseDouble(tokens[3]),
            Ry = ParseDouble(tokens[4]),
            Rz = ParseDouble(tokens[5]),
            Re = modelType == YrcModelType.Yrc100 ? ParseDouble(tokens[6]) : 0.0,
            StatusByte = ParseByte(tokens[statusIndex]),
            ToolNumber = ParseInt(tokens[statusIndex + 1]),
            ExternalAxes = externalAxes
        };
    }

    /// <summary><c>MOVJ</c> 명령 본문에 사용할 CSV 문자열로 변환합니다.</summary>
    public string ToMoveCommandString(YrcModelType modelType)
    {
        var builder = new StringBuilder();
        builder.Append(SpeedPercent.ToString("0.0#", CultureInfo.InvariantCulture));
        builder.Append("%,");
        builder.Append(Frame.ToString(CultureInfo.InvariantCulture));
        builder.Append(',');
        builder.Append(X.ToString("F3", CultureInfo.InvariantCulture));
        builder.Append(',');
        builder.Append(Y.ToString("F3", CultureInfo.InvariantCulture));
        builder.Append(',');
        builder.Append(Z.ToString("F3", CultureInfo.InvariantCulture));
        builder.Append(',');
        builder.Append(Rx.ToString("F4", CultureInfo.InvariantCulture));
        builder.Append(',');
        builder.Append(Ry.ToString("F4", CultureInfo.InvariantCulture));
        builder.Append(',');
        builder.Append(Rz.ToString("F4", CultureInfo.InvariantCulture));

        if (modelType == YrcModelType.Yrc100)
        {
            builder.Append(',');
            builder.Append(Re.ToString("F4", CultureInfo.InvariantCulture));
        }

        builder.Append(',');
        builder.Append(StatusByte.ToString(CultureInfo.InvariantCulture));
        builder.Append(',');
        builder.Append(ToolNumber.ToString(CultureInfo.InvariantCulture));

        for (int i = 0; i < ExternalAxisCount; i++)
        {
            builder.Append(',');
            builder.Append(ExternalAxes[i].ToString(CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    private static double ParseDouble(string token)
    {
        if (!double.TryParse(token.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
        {
            throw new RobotException(RobotErrorKind.ParseError, $"실수 값('{token}') 변환에 실패했습니다.");
        }

        return value;
    }

    private static int ParseInt(string token)
    {
        if (!int.TryParse(token.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
        {
            throw new RobotException(RobotErrorKind.ParseError, $"정수 값('{token}') 변환에 실패했습니다.");
        }

        return value;
    }

    private static byte ParseByte(string token)
    {
        if (!byte.TryParse(token.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out byte value))
        {
            throw new RobotException(RobotErrorKind.ParseError, $"바이트 값('{token}') 변환에 실패했습니다.");
        }

        return value;
    }
}
