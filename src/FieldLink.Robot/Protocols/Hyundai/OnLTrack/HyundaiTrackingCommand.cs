namespace FieldLink.Robot.Protocols.Hyundai.OnLTrack;

/// <summary>Hyundai OnLTrack UDP 패킷의 오프셋 <c>0</c> 명령 구분 문자입니다.</summary>
public enum HyundaiTrackingCommand : byte
{
    /// <summary>온라인 트래킹 시작 및 시작 ACK (<c>'S'</c>).</summary>
    Start = (byte)'S',

    /// <summary>실시간 트래킹 주기 및 6축 증분 명령 (<c>'P'</c>).</summary>
    Process = (byte)'P',

    /// <summary>온라인 트래킹 종료 통지 (<c>'F'</c>).</summary>
    Finish = (byte)'F',
}
