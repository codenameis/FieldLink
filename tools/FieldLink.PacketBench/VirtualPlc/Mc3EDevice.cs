namespace FieldLink.PacketBench.VirtualPlc;

/// <summary>가상 MC PLC 가 제공하는 디바이스입니다.</summary>
internal enum Mc3EDevice
{
    /// <summary>데이터 레지스터 (워드)</summary>
    D,

    /// <summary>링크 레지스터 (워드)</summary>
    W,

    /// <summary>파일 레지스터 (워드)</summary>
    R,

    /// <summary>내부 릴레이 (비트)</summary>
    M,

    /// <summary>입력 릴레이 (비트, 읽기 전용)</summary>
    X,

    /// <summary>출력 릴레이 (비트)</summary>
    Y,
}

/// <summary>디바이스별 코드·단위·크기입니다. (사양서 §2)</summary>
internal static class Mc3EDeviceExtensions
{
    private const byte DeviceCodeD = 0xA8;
    private const byte DeviceCodeW = 0xB4;
    private const byte DeviceCodeR = 0xAF;
    private const byte DeviceCodeM = 0x90;
    private const byte DeviceCodeX = 0x9C;
    private const byte DeviceCodeY = 0x9D;

    public static byte GetCode(this Mc3EDevice device) => device switch
    {
        Mc3EDevice.D => DeviceCodeD,
        Mc3EDevice.W => DeviceCodeW,
        Mc3EDevice.R => DeviceCodeR,
        Mc3EDevice.M => DeviceCodeM,
        Mc3EDevice.X => DeviceCodeX,
        Mc3EDevice.Y => DeviceCodeY,
        _ => 0,
    };

    public static bool IsBit(this Mc3EDevice device) => device is Mc3EDevice.M or Mc3EDevice.X or Mc3EDevice.Y;

    public static bool IsWritable(this Mc3EDevice device) => device != Mc3EDevice.X;

    public static int GetCapacity(this Mc3EDevice device) => device switch
    {
        Mc3EDevice.D => 4096,
        Mc3EDevice.W => 1024,
        Mc3EDevice.R => 1024,
        Mc3EDevice.M => 8192,
        Mc3EDevice.X => 1024,
        Mc3EDevice.Y => 1024,
        _ => 0,
    };

    public static string GetDisplayName(this Mc3EDevice device) => device switch
    {
        Mc3EDevice.D => "D (데이터)",
        Mc3EDevice.W => "W (링크)",
        Mc3EDevice.R => "R (파일)",
        Mc3EDevice.M => "M (릴레이)",
        Mc3EDevice.X => "X (입력)",
        Mc3EDevice.Y => "Y (출력)",
        _ => device.ToString(),
    };

    public static bool TryFromCode(byte code, out Mc3EDevice device)
    {
        switch (code)
        {
            case DeviceCodeD:
                device = Mc3EDevice.D;
                return true;
            case DeviceCodeW:
                device = Mc3EDevice.W;
                return true;
            case DeviceCodeR:
                device = Mc3EDevice.R;
                return true;
            case DeviceCodeM:
                device = Mc3EDevice.M;
                return true;
            case DeviceCodeX:
                device = Mc3EDevice.X;
                return true;
            case DeviceCodeY:
                device = Mc3EDevice.Y;
                return true;
            default:
                device = Mc3EDevice.D;
                return false;
        }
    }
}
