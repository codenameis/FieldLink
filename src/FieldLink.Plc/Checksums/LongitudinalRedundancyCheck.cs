using System;

namespace FieldLink.Plc.Checksums;

/// <summary>Modbus ASCII 규격에 따른 종단 중복 검사(LRC, Longitudinal Redundancy Check)를 계산합니다.</summary>
/// <remarks>
/// <para>Modbus over Serial Line Specification V1.02 §5.2 사양을 따릅니다.</para>
/// <para>데이터 바이트들의 합계(modulo 256)에 대한 2의 보수를 1바이트로 산출합니다.</para>
/// </remarks>
public static class LongitudinalRedundancyCheck
{
    /// <summary>지정된 바이트 배열의 LRC 1바이트 값을 계산합니다.</summary>
    /// <param name="buffer">체크섬을 계산할 데이터 배열입니다.</param>
    /// <param name="offset">계산을 시작할 배열 오프셋입니다.</param>
    /// <param name="count">계산에 포함할 바이트 수입니다.</param>
    /// <returns>계산된 1바이트 LRC 값입니다.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/>가 null일 때.</exception>
    /// <exception cref="ArgumentOutOfRangeException">오프셋 또는 바이트 수가 유효하지 않을 때.</exception>
    public static byte Calculate(byte[] buffer, int offset, int count)
    {
        if (buffer is null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        if (offset < 0 || offset > buffer.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "오프셋은 0 이상이고 배열 길이 이하여야 합니다.");
        }

        if (count < 0 || offset + count > buffer.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "바이트 수는 0 이상이고 배열 범위를 벗어날 수 없습니다.");
        }

        byte sum = 0;
        for (int i = 0; i < count; i++)
        {
            sum = (byte)(sum + buffer[offset + i]);
        }

        return (byte)((-sum) & 0xFF);
    }

    /// <summary>전체 바이트 배열의 LRC 1바이트 값을 계산합니다.</summary>
    /// <param name="buffer">체크섬을 계산할 데이터 배열입니다.</param>
    /// <returns>계산된 1바이트 LRC 값입니다.</returns>
    public static byte Calculate(byte[] buffer)
    {
        if (buffer is null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        return Calculate(buffer, 0, buffer.Length);
    }

    /// <summary>Modbus ASCII 프레임에 삽입할 대문자 16진 문자열(2자리)을 반환합니다.</summary>
    /// <param name="buffer">체크섬을 계산할 데이터 배열입니다.</param>
    /// <param name="offset">계산을 시작할 배열 오프셋입니다.</param>
    /// <param name="count">계산에 포함할 바이트 수입니다.</param>
    /// <returns>2자리의 대문자 16진 문자열입니다. (예: "7E")</returns>
    public static string CalculateAscii(byte[] buffer, int offset, int count)
    {
        byte lrc = Calculate(buffer, offset, count);
        return lrc.ToString("X2");
    }
}
