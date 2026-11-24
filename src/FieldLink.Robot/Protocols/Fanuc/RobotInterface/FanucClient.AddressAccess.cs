using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Robot.Protocols.Fanuc.RobotInterface;

public sealed partial class FanucClient
{
    /// <summary>문자열 주소(<c>D</c>, <c>R</c>, <c>SR</c>, <c>AI</c>, <c>AQ</c>, <c>GI</c>, <c>GO</c>)로 워드를 읽습니다.</summary>
    public Task<RobotResult<byte[]>> ReadAddressWordsAsync(string address, ushort wordCount, CancellationToken cancellationToken = default)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        ThrowIfDisposed();
        EnsureOpen();
        if (!TryResolveAddress(address, expectBit: false, out FanucAddress? parsed, out RobotError? resolutionError))
        {
            return Task.FromResult(RobotResult.Failure<byte[]>(resolutionError));
        }

        return ReadWordsAsync(parsed.Selector, parsed.Address, wordCount, cancellationToken);
    }

    /// <summary>문자열 주소에 워드 바이트를 씁니다.</summary>
    public Task<RobotResult> WriteAddressWordsAsync(string address, byte[] data, CancellationToken cancellationToken = default)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        ThrowIfDisposed();
        EnsureOpen();
        if (!TryResolveAddress(address, expectBit: false, out FanucAddress? parsed, out RobotError? resolutionError))
        {
            return Task.FromResult(RobotResult.Failure(resolutionError));
        }

        return WriteWordsAsync(parsed.Selector, parsed.Address, data, cancellationToken);
    }

    /// <summary>문자열 비트 주소(<c>SDO</c>, <c>SDI</c>, <c>RDO</c>, <c>RDI</c>, <c>I</c>, <c>Q</c>, <c>M</c> 등)로 비트를 읽습니다.</summary>
    public Task<RobotResult<bool[]>> ReadAddressBitsAsync(string address, ushort bitCount, CancellationToken cancellationToken = default)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        ThrowIfDisposed();
        EnsureOpen();
        if (!TryResolveAddress(address, expectBit: true, out FanucAddress? parsed, out RobotError? resolutionError))
        {
            return Task.FromResult(RobotResult.Failure<bool[]>(resolutionError));
        }

        return ReadBitsAsync(parsed.Selector, parsed.Address, bitCount, cancellationToken);
    }

    /// <summary>문자열 비트 주소에 비트 배열을 씁니다.</summary>
    public Task<RobotResult> WriteAddressBitsAsync(string address, bool[] values, CancellationToken cancellationToken = default)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        ThrowIfDisposed();
        EnsureOpen();
        if (!TryResolveAddress(address, expectBit: true, out FanucAddress? parsed, out RobotError? resolutionError))
        {
            return Task.FromResult(RobotResult.Failure(resolutionError));
        }

        return WriteBitsAsync(parsed.Selector, parsed.Address, values, cancellationToken);
    }

    private static bool TryResolveAddress(
        string address,
        bool expectBit,
        [NotNullWhen(true)] out FanucAddress? parsed,
        [NotNullWhen(false)] out RobotError? error)
    {
        if (FanucAddress.TryParse(address, expectBit, out parsed))
        {
            error = null;
            return true;
        }

        if (FanucAddress.TryParse(address, !expectBit, out _))
        {
            string expectedKind = expectBit ? "비트" : "워드";
            error = new RobotError(
                RobotErrorKind.Unsupported,
                $"FANUC 주소 '{address}'는 {expectedKind} 연산 영역이 아닙니다.");
            return false;
        }

        string label = expectBit ? "비트" : "워드";
        error = new RobotError(RobotErrorKind.ParseError, $"유효하지 않은 FANUC {label} 주소입니다: '{address}'.");
        return false;
    }
}
