namespace FieldLink.Plc.Protocols.Xinje;

/// <summary>XINJE 확장 Modbus 기능 코드(0x1E~0x21)의 디바이스 코드입니다. (사양서 Part A §A.1.4)</summary>
public enum XinjeDeviceCode : byte
{
    /// <summary>입력 접점 X (8진수)</summary>
    X = 0x01,

    /// <summary>출력 접점 Y (8진수)</summary>
    Y = 0x02,

    /// <summary>내부 보조 릴레이 M</summary>
    M = 0x03,

    /// <summary>스텝 릴레이 S</summary>
    S = 0x04,

    /// <summary>타이머 접점 T</summary>
    T = 0x05,

    /// <summary>카운터 접점 C</summary>
    C = 0x06,

    /// <summary>정밀 타이머 접점 ET</summary>
    ET = 0x07,

    /// <summary>정전유지 내부 릴레이 HM</summary>
    HM = 0x08,

    /// <summary>정전유지 스텝 릴레이 HS</summary>
    HS = 0x09,

    /// <summary>정전유지 타이머 접점 HT</summary>
    HT = 0x0A,

    /// <summary>정전유지 카운터 접점 HC</summary>
    HC = 0x0B,

    /// <summary>고속 카운터 접점 HSC</summary>
    HSC = 0x0C,

    /// <summary>특수 보조 릴레이 SM</summary>
    SM = 0x0D,

    /// <summary>데이터 레지스터 D</summary>
    D = 0x80,

    /// <summary>타이머 현재값 TD</summary>
    TD = 0x81,

    /// <summary>카운터 현재값 CD</summary>
    CD = 0x82,

    /// <summary>특수 데이터 레지스터 SD</summary>
    SD = 0x83,

    /// <summary>정밀 타이머 현재값 ETD</summary>
    ETD = 0x85,

    /// <summary>입력 워드 레지스터 ID</summary>
    ID = 0x86,

    /// <summary>출력 워드 레지스터 QD</summary>
    QD = 0x87,

    /// <summary>정전유지 데이터 레지스터 HD</summary>
    HD = 0x88,

    /// <summary>정전유지 타이머 현재값 HTD</summary>
    HTD = 0x89,

    /// <summary>정전유지 카운터 현재값 HCD</summary>
    HCD = 0x8A,

    /// <summary>고속 카운터 현재값 HSCD</summary>
    HSCD = 0x8B,

    /// <summary>고속 특수 데이터 레지스터 HSD</summary>
    HSD = 0x8C,

    /// <summary>플래시 데이터 레지스터 FD</summary>
    FD = 0x8D,

    /// <summary>특수 플래시 레지스터 SFD</summary>
    SFD = 0x8E,
}
