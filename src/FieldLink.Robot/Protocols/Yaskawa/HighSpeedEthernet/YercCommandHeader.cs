namespace FieldLink.Robot.Protocols.Yaskawa.HighSpeedEthernet;

/// <summary>YASKAWA <c>YERC</c> 명령의 Command, Instance, Attribute, Service 4요소를 묶는 불변 헤더 매개변수입니다.</summary>
public sealed class YercCommandHeader
{
    /// <summary>명령 헤더 4요소를 초기화합니다.</summary>
    public YercCommandHeader(ushort command, ushort instance, byte attribute, byte service)
    {
        Command = command;
        Instance = instance;
        Attribute = attribute;
        Service = service;
    }

    /// <summary>CIP Class에 대응하는 명령 번호입니다.</summary>
    public ushort Command { get; }

    /// <summary>인스턴스 또는 데이터 주소입니다.</summary>
    public ushort Instance { get; }

    /// <summary>속성 번호입니다.</summary>
    public byte Attribute { get; }

    /// <summary>서비스 코드입니다.</summary>
    public byte Service { get; }
}
