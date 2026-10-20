using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace FieldLink.Plc.Protocols.Mitsubishi.MC;

/// <summary>MC 프로토콜 주소 문자열(예: "D100", "X1F", "ZR1000")을 해석한 불변 값 객체입니다. (사양서 §2)</summary>
public sealed class MCAddress
{
    private MCAddress(string originalAddress, MCDevice device, uint deviceNumber)
    {
        OriginalAddress = originalAddress;
        Device = device;
        DeviceNumber = deviceNumber;
    }

    /// <summary>사용자가 입력한 원본 주소입니다.</summary>
    public string OriginalAddress { get; }

    /// <summary>주소의 디바이스 종류입니다.</summary>
    public MCDevice Device { get; }

    /// <summary>디바이스 번호입니다. 진법은 <see cref="MCDevice.NumberBase"/>를 따르며 여기에는 값 자체가 들어 있습니다.</summary>
    public uint DeviceNumber { get; }

    /// <summary>비트 단위 디바이스인지 여부입니다.</summary>
    public bool IsBit => Device.IsBit;

    /// <summary>주소 문자열을 해석합니다.</summary>
    /// <param name="address">해석할 주소 문자열입니다.</param>
    /// <exception cref="ArgumentNullException"><paramref name="address"/>가 null일 때.</exception>
    /// <exception cref="FormatException">주소 형식이 올바르지 않을 때.</exception>
    public static MCAddress Parse(string address)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (!TryParse(address, out MCAddress? result))
        {
            throw new FormatException($"유효하지 않은 MC 프로토콜 주소입니다: '{address}'");
        }

        return result;
    }

    /// <summary>주소 문자열 해석을 시도합니다.</summary>
    public static bool TryParse(string? address, [NotNullWhen(true)] out MCAddress? result)
    {
        result = null;
        if (address is null || string.IsNullOrWhiteSpace(address))
        {
            return false;
        }

        string normalized = address.Trim().ToUpperInvariant();
        if (!MCDevice.TryFindPrefix(normalized, out MCDevice? device) || device is null)
        {
            return false;
        }

        string numberText = normalized.Substring(device.Symbol.Length);
        if (!TryParseNumber(numberText, device.NumberBase, out uint number))
        {
            return false;
        }

        result = new MCAddress(address.Trim(), device, number);
        return true;
    }

    /// <summary>디바이스 번호를 원소 수만큼 뒤로 옮긴 새 주소를 만듭니다. 분할 전송에서 사용합니다.</summary>
    /// <param name="elementCount">옮길 원소 수입니다. 0 이상입니다.</param>
    /// <exception cref="ArgumentOutOfRangeException">음수이거나 결과가 32비트 범위를 넘을 때.</exception>
    public MCAddress WithOffset(int elementCount)
    {
        if (elementCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(elementCount), elementCount, "오프셋은 0 이상이어야 합니다.");
        }

        long shifted = (long)DeviceNumber + elementCount;
        if (shifted > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(elementCount), elementCount, "디바이스 번호가 32비트 범위를 넘습니다.");
        }

        return new MCAddress(OriginalAddress, Device, (uint)shifted);
    }

    /// <inheritdoc />
    public override string ToString() => OriginalAddress;

    private static bool TryParseNumber(string numberText, int numberBase, out uint number)
    {
        number = 0;
        if (numberText.Length == 0)
        {
            return false;
        }

        NumberStyles style = numberBase == 16 ? NumberStyles.AllowHexSpecifier : NumberStyles.None;
        return uint.TryParse(numberText, style, CultureInfo.InvariantCulture, out number);
    }
}
