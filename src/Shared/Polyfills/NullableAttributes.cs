// netstandard2.0에는 Nullable 흐름 분석 특성이 없어 직접 공급한다. (.NET Core 3.0+에는 기본 포함)
// 이 파일은 Directory.Build.props가 모든 제품 프로젝트에 링크로 포함한다.
namespace System.Diagnostics.CodeAnalysis;

/// <summary>반환값이 지정한 값일 때 out 인수가 null이 아님을 컴파일러에 알립니다.</summary>
[AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
internal sealed class NotNullWhenAttribute : Attribute
{
    public NotNullWhenAttribute(bool returnValue)
    {
        ReturnValue = returnValue;
    }

    public bool ReturnValue { get; }
}

/// <summary>반환값이 지정한 값일 때 out 인수가 null일 수 있음을 컴파일러에 알립니다.</summary>
[AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
internal sealed class MaybeNullWhenAttribute : Attribute
{
    public MaybeNullWhenAttribute(bool returnValue)
    {
        ReturnValue = returnValue;
    }

    public bool ReturnValue { get; }
}

/// <summary>메서드가 정상 반환하지 않음(항상 예외)을 컴파일러에 알립니다.</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
internal sealed class DoesNotReturnAttribute : Attribute
{
}

/// <summary>멤버가 true를 반환하면 지정한 멤버가 null이 아님을 컴파일러에 알립니다.</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
internal sealed class MemberNotNullWhenAttribute : Attribute
{
    public MemberNotNullWhenAttribute(bool returnValue, params string[] members)
    {
        ReturnValue = returnValue;
        Members = members;
    }

    public bool ReturnValue { get; }

    public string[] Members { get; }
}
