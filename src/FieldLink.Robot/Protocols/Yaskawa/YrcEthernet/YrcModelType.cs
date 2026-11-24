namespace FieldLink.Robot.Protocols.Yaskawa.YrcEthernet;

/// <summary>YASKAWA YRC 시리즈 컨트롤러 축 구성 모델 구분입니다.</summary>
public enum YrcModelType
{
    /// <summary>6축 기본 모델 (<c>YRC1000</c> 등, 팔꿈치 각 <c>Re</c> 없음).</summary>
    Yrc1000,

    /// <summary>7축 확장 모델 (<c>YRC100</c> 등, 팔꿈치 각 <c>Re</c> 포함).</summary>
    Yrc100,
}
