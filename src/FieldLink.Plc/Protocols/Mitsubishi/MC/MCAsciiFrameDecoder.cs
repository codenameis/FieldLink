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
    private const int MinimumResponseBodyCharacters = 4;
    private const int ReservedOffset4E = 8;
    private const int HexNibbleShift = 4;
    private const int HexLetterBaseValue = 10;
    private const string Response3ESubheader = "D000";
    private const string Response4ESubheader = "D400";
    private const string Reserved4E = "0000";

    private readonly int _headerLength;
    private readonly bool _isFrame4E;

    /// <summary>프레임 종류에 맞는 디코더를 만듭니다.</summary>
    /// <param name="frameType">3E 또는 4E 프레임 종류입니다.</param>
    public MCAsciiFrameDecoder(MCFrameType frameType)
    {
        if (!Enum.IsDefined(typeof(MCFrameType), frameType))
        {
            throw new ArgumentOutOfRangeException(nameof(frameType), frameType, "지원하지 않는 MC 프레임 종류입니다.");
        }

        _isFrame4E = frameType == MCFrameType.Frame4E;
        _headerLength = _isFrame4E ? Header4ELength : Header3ELength;
    }

    /// <inheritdoc />
    /// <exception cref="TransportException">헤더 또는 길이 필드가 올바르지 않을 때.</exception>
    public bool TryGetFrameLength(ArraySegment<byte> received, out int frameLength)
    {
        frameLength = 0;
        if (received.Count < _headerLength)
        {
            return false;
        }

        if (received.Array is not { } array)
        {
            throw new TransportException(TransportFailure.InvalidFrame, "수신 세그먼트의 내부 배열이 비어 있습니다.");
        }

        string expectedSubheader = _isFrame4E ? Response4ESubheader : Response3ESubheader;
        if (!MatchesAscii(array, received.Offset, expectedSubheader))
        {
            throw new TransportException(
                TransportFailure.InvalidFrame,
                $"MC ASCII 응답 서브헤더가 '{expectedSubheader}'가 아닙니다.");
        }

        if (_isFrame4E && !MatchesAscii(array, received.Offset + ReservedOffset4E, Reserved4E))
        {
            throw new TransportException(TransportFailure.InvalidFrame, "4E ASCII 응답의 예약 필드가 '0000'이 아닙니다.");
        }

        int lengthField = ReadLengthField(array, received.Offset);
        if (lengthField < MinimumResponseBodyCharacters)
        {
            throw new TransportException(
                TransportFailure.InvalidFrame,
                $"MC ASCII 응답 길이 필드({lengthField})는 최소 {MinimumResponseBodyCharacters} 이상이어야 합니다.");
        }

        int totalLength = _headerLength + lengthField;
        if (received.Count < totalLength)
        {
            return false;
        }

        frameLength = totalLength;
        return true;
    }

    private static bool MatchesAscii(byte[] array, int offset, string expected)
    {
        for (int index = 0; index < expected.Length; index++)
        {
            if (array[offset + index] != expected[index])
            {
                return false;
            }
        }

        return true;
    }

    private int ReadLengthField(byte[] array, int segmentOffset)
    {
        int start = segmentOffset + _headerLength - LengthFieldCharacterCount;
        int value = 0;

        for (int index = 0; index < LengthFieldCharacterCount; index++)
        {
            value = (value << HexNibbleShift) | GetHexDigitValue(array[start + index]);
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
            return character - 'A' + HexLetterBaseValue;
        }

        if (character is >= (byte)'a' and <= (byte)'f')
        {
            return character - 'a' + HexLetterBaseValue;
        }

        throw new TransportException(TransportFailure.InvalidFrame, $"길이 필드에 16진 문자가 아닌 값이 있습니다. (0x{character:X2})");
    }
}
