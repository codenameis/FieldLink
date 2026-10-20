namespace FieldLink.Plc.Protocols.Inovance;

/// <summary>Inovance PLC 시리즈 구분입니다.</summary>
public enum InovanceSeries
{
    /// <summary>AM400 / AM600 / AM800 / AC800 계열 (InoProShop / CODESYS IEC 61131-3 매핑).</summary>
    AM,

    /// <summary>H3U / H3U-XP 계열 (AutoShop 고정 Modbus 매핑).</summary>
    H3u,

    /// <summary>H5U / Easy 계열.</summary>
    H5u,
}
