namespace FieldLink.Plc.Protocols.Mitsubishi.MC;

/// <summary>시리즈마다 달라지는 디바이스 지정 형식을 한 곳에 모은 확장 메서드입니다. (사양서 §3-2, §3-3)</summary>
public static class MCSeriesExtensions
{
    private const uint MaxQLDeviceNumber = 0xFFFFFF;

    /// <summary>접근 단위에 맞는 서브커맨드를 돌려줍니다.</summary>
    public static MCSubcommand GetSubcommand(this MCSeries series, bool isBitUnit)
    {
        if (series == MCSeries.Iqr)
        {
            return isBitUnit ? MCSubcommand.IqrBit : MCSubcommand.IqrWord;
        }

        return isBitUnit ? MCSubcommand.QLBit : MCSubcommand.QLWord;
    }

    /// <summary>바이너리 프레임에서 디바이스 번호가 차지하는 바이트 수입니다. (QL: 3, iQ-R: 4)</summary>
    public static int GetDeviceNumberByteCount(this MCSeries series) => series == MCSeries.Iqr ? 4 : 3;

    /// <summary>바이너리 프레임에서 디바이스 코드가 차지하는 바이트 수입니다. (QL: 1, iQ-R: 2)</summary>
    public static int GetDeviceCodeByteCount(this MCSeries series) => series == MCSeries.Iqr ? 2 : 1;

    /// <summary>ASCII 프레임에서 디바이스 코드가 차지하는 문자 수입니다. (QL: 2, iQ-R: 4)</summary>
    public static int GetDeviceCodeCharacterCount(this MCSeries series) => series == MCSeries.Iqr ? 4 : 2;

    /// <summary>ASCII 프레임에서 디바이스 번호가 차지하는 문자 수입니다. (QL: 6, iQ-R: 8)</summary>
    public static int GetDeviceNumberCharacterCount(this MCSeries series) => series == MCSeries.Iqr ? 8 : 6;

    /// <summary>해당 시리즈가 표현할 수 있는 가장 큰 디바이스 번호입니다.</summary>
    public static uint GetMaxDeviceNumber(this MCSeries series) => series == MCSeries.Iqr ? uint.MaxValue : MaxQLDeviceNumber;
}
