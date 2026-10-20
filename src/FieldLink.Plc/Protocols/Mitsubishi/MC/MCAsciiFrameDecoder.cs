using System;
using FieldLink.Transport;
using FieldLink.Transport.Framing;

namespace FieldLink.Plc.Protocols.Mitsubishi.MC;

/// <summary>MC 프로토콜 ASCII 헤더의 길이 필드(4자리 16진 문자)로 프레임 경계를 판정하는 디코더입니다. (사양서 §1)</summary>
/// <remarks>길이 필드가 이진 정수가 아니라 16진 문자여서 <see cref="HeaderLengthFrameDecoder"/>를 쓸 수 없습니다.</remarks>
public sealed class MCAsciiFrameDecoder : IFrameDecoder
{
    private const int Header3ELength = 18;
    private const int Header4ELength = 26;
    private const int LengthFieldCharacterCount = 4;

    private readonly int _headerLength;

    /// <summary>프레임 종류에 맞는 디코더를 만듭니다.</summary>
    /// <param name="frameType">3E 또는 4E 프레임 종류입니다.</param>
    public MCAsciiFrameDecoder(MCFrameType frameType)
    {
        _headerLength = frameType == MCFrameType.Frame4E ? Header4ELength : Header3ELength;
    }

    /// <inheritdoc />
    /// <exception cref="TransportException">길이 필드에 16진 문자가 아닌 값이 있을 때.</exception>
    public bool TryGetFrameLength(ArraySegment<byte> received, out int frameLength)
    {
        frameLength = 0;
        if (received.Count < _headerLength)
        {
            return false;
        }

        int lengthField = ReadLengthField(received);
        int totalLength = _headerLength + lengthField;
        if (received.Count < totalLength)
        {
            return false;
        }

        frameLength = totalLength;
        return true;
    }

    private int ReadLengthField(ArraySegment<byte> received)
    {
        byte[] array = received.Array!;
        int start = received.Offset + _headerLength - LengthFieldCharacterCount;
        int value = 0;

        for (int index = 0; index < LengthFieldCharacterCount; index++)
        {
            value = (value << 4) | GetHexDigitValue(array[start + index]);
        }

        return value;
    }

    private static int GetHexDigitValue(byte character)
    {
        if (character is >= (byte)'0' and <= (byte)'9')
        {
            return character - '0';
        }

        if (character is >= (byte)'A' and <= (byte)'F')
        {
            return character - 'A' + 10;
        }

        if (character is >= (byte)'a' and <= (byte)'f')
        {
            return character - 'a' + 10;
        }

        throw new TransportException(TransportFailure.InvalidFrame, $"길이 필드에 16진 문자가 아닌 값이 있습니다. (0x{character:X2})");
    }
}
