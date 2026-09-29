namespace FieldLink.Communication
{
    /// <summary>한 장치와 데이터그램을 교환하는 UDP 클라이언트 계약입니다. 원격 연결 상태를 가정하지 않습니다.</summary>
    /// <remarks>공통 작업은 ICommunicationClient에서 상속합니다. 드라이버가 UDP 전송을 명시적으로 선택할 때 사용합니다.</remarks>
    public interface IUdpClient : ICommunicationClient
    {
    }
}
