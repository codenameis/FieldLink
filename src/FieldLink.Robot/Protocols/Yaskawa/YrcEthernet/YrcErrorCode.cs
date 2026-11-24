namespace FieldLink.Robot.Protocols.Yaskawa.YrcEthernet;

/// <summary>YASKAWA YRC Ethernet Server 및 High-Speed Ethernet 공통 오류 코드 설명 조회기입니다.</summary>
public static class YrcErrorCode
{
    /// <summary>에러 코드 번호에 해당하는 설명 문구를 반환합니다.</summary>
    public static string GetDescription(int errorCode) =>
        YaskawaErrorDescriptions.GetDescription(errorCode);
}
