using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FieldLink.Robot.Protocols.Yaskawa.YrcEthernet;

/// <summary>YASKAWA YRC <c>RPOSC</c> 조회 및 <c>MOVJ</c> 모션 명령에 사용하는 직교 자세 모델입니다.</summary>
public sealed class YrcRobotPose
{
    private const int ExternalAxisCount = 6;
    private const int Yrc100MinimumTokenCount = 9;
    private const int Yrc1000MinimumTokenCount = 8;
    private const int Yrc100StatusByteIndex = 7;
    private const int Yrc1000StatusByteIndex = 6;
    private const int ReCoordinateTokenIndex = 6;
    private const int MinimumToolNumber = 0;
    private const int MaximumToolNumber = 63;
    private const int MinimumFrame = 0;
    private const int MaximumFrame = 65;
    private const double DefaultSpeedPercent = 100.0;
    private const double MinimumSpeedPercent = 0.01;
    private const double MaximumSpeedPercent = 100.0;

    private static readonly double[] EmptyExternalAxes = new double[ExternalAxisCount];

    private readonly double _x;
    private readonly double _y;
    private readonly double _z;
    private readonly double _rx;
    private readonly double _ry;
    private readonly double _rz;
    private readonly double _re;
    private readonly int _toolNumber;
    private readonly int _frame;
    private readonly double _speedPercent = DefaultSpeedPercent;
    private readonly IReadOnlyList<double> _externalAxes = Array.AsReadOnly(EmptyExternalAxes);

    /// <summary>X 좌표(mm)입니다.</summary>
    public double X
    {
        get => _x;
        init => _x = ValidateFinite(value, nameof(X));
    }

    /// <summary>Y 좌표(mm)입니다.</summary>
    public double Y
    {
        get => _y;
        init => _y = ValidateFinite(value, nameof(Y));
    }

    /// <summary>Z 좌표(mm)입니다.</summary>
    public double Z
    {
        get => _z;
        init => _z = ValidateFinite(value, nameof(Z));
    }

    /// <summary>Rx 회전 각도(deg)입니다.</summary>
    public double Rx
    {
        get => _rx;
        init => _rx = ValidateFinite(value, nameof(Rx));
    }

    /// <summary>Ry 회전 각도(deg)입니다.</summary>
    public double Ry
    {
        get => _ry;
        init => _ry = ValidateFinite(value, nameof(Ry));
    }

    /// <summary>Rz 회전 각도(deg)입니다.</summary>
    public double Rz
    {
        get => _rz;
        init => _rz = ValidateFinite(value, nameof(Rz));
    }

    /// <summary>7축 팔꿈치 각도 <c>Re</c>(deg)입니다. (<see cref="YrcModelType.Yrc100"/> 전용)</summary>
    public double Re
    {
        get => _re;
        init => _re = ValidateFinite(value, nameof(Re));
    }

    /// <summary>6비트 형태 플래그 바이트입니다.</summary>
    public byte StatusByte { get; init; }

    /// <summary>툴 번호(<c>0..63</c>)입니다.</summary>
    public int ToolNumber
    {
        get => _toolNumber;
        init
        {
            if (value is < MinimumToolNumber or > MaximumToolNumber)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, $"툴 번호는 {MinimumToolNumber}에서 {MaximumToolNumber} 사이여야 합니다.");
            }

            _toolNumber = value;
        }
    }

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
                throw new ArgumentException($"외부축 배열 길이는 {ExternalAxisCount}이어야 합니다.", nameof(value));
            }

            double[] copy = new double[ExternalAxisCount];
            for (int i = 0; i < ExternalAxisCount; i++)
            {
                copy[i] = ValidateFinite(value[i], nameof(ExternalAxes));
            }

            _externalAxes = Array.AsReadOnly(copy);
        }
    }

    /// <summary><c>MOVJ</c> 실행 속도 비율(<c>0.01..100.0</c>)입니다. 기본값은 100.0입니다.</summary>
    public double SpeedPercent
    {
        get => _speedPercent;
        init
        {
            ValidateFinite(value, nameof(SpeedPercent));
            if (value is < MinimumSpeedPercent or > MaximumSpeedPercent)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    $"속도 비율은 {MinimumSpeedPercent}에서 {MaximumSpeedPercent} 사이여야 합니다.");
            }

            _speedPercent = value;
        }
    }

    /// <summary>기준 좌표계(<c>0</c>=기지, <c>1</c>=로봇, <c>2..65</c>=사용자1..64)입니다.</summary>
    public int Frame
    {
        get => _frame;
        init
        {
            if (value is < MinimumFrame or > MaximumFrame)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, $"좌표계 번호는 {MinimumFrame}에서 {MaximumFrame} 사이여야 합니다.");
            }

            _frame = value;
        }
    }

    /// <summary><c>RPOSC</c> 응답 CSV 문자열을 <see cref="YrcRobotPose"/>로 해석합니다.</summary>
    public static YrcRobotPose Parse(string csv, YrcModelType modelType)
    {
        if (csv is null)
        {
            throw new ArgumentNullException(nameof(csv));
        }

        string[] tokens = csv.Split(',');
        int minimumTokens = modelType == YrcModelType.Yrc100 ? Yrc100MinimumTokenCount : Yrc1000MinimumTokenCount;
        if (tokens.Length < minimumTokens)
        {
            throw new RobotException(
                RobotErrorKind.ParseError,
                $"YRC RPOSC 응답 필드 수({tokens.Length})가 모델({modelType}) 최소 요구 수({minimumTokens})보다 적습니다.");
        }

        double[] externalAxes = new double[ExternalAxisCount];
        int externalStartIndex = minimumTokens;
        for (int i = 0; i < ExternalAxisCount && externalStartIndex + i < tokens.Length; i++)
        {
            externalAxes[i] = ParseDouble(tokens[externalStartIndex + i]);
        }

        int statusIndex = modelType == YrcModelType.Yrc100 ? Yrc100StatusByteIndex : Yrc1000StatusByteIndex;
        int toolNumber = ParseInt(tokens[statusIndex + 1]);
        if (toolNumber is < MinimumToolNumber or > MaximumToolNumber)
        {
            throw new RobotException(
                RobotErrorKind.ParseError,
                $"YRC RPOSC 응답의 툴 번호({toolNumber})가 유효 범위({MinimumToolNumber}..{MaximumToolNumber})를 벗어났습니다.");
        }

        return new YrcRobotPose
        {
            X = ParseDouble(tokens[0]),
            Y = ParseDouble(tokens[1]),
            Z = ParseDouble(tokens[2]),
            Rx = ParseDouble(tokens[3]),
            Ry = ParseDouble(tokens[4]),
            Rz = ParseDouble(tokens[5]),
            Re = modelType == YrcModelType.Yrc100 ? ParseDouble(tokens[ReCoordinateTokenIndex]) : 0.0,
            StatusByte = ParseByte(tokens[statusIndex]),
            ToolNumber = toolNumber,
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

    private static double ValidateFinite(double value, string parameterName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "좌표 및 속도 값은 유한한 숫자여야 합니다.");
        }

        return value;
    }

    private static double ParseDouble(string token)
    {
        if (!double.TryParse(token.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            || double.IsNaN(value)
            || double.IsInfinity(value))
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
