using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Plc;

namespace FieldLink.PacketBench.Operations;

/// <summary>값 목록을 골라 블록 쓰기 또는 주소 증가 쓰기로 보내고, 필요하면 되읽어 검증합니다.</summary>
internal static class WriteExecutor
{
    private delegate bool ValueParser<T>(string text, out T value);

    public static async Task<WriteOutcome> ExecuteAsync(
        PlcClient client,
        string address,
        WriteInterpretation interpretation,
        WriteMode mode,
        IReadOnlyList<string> texts,
        bool verify,
        CancellationToken cancellationToken)
    {
        if (client is null)
        {
            throw new ArgumentNullException(nameof(client));
        }

        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("주소를 입력하세요.", nameof(address));
        }

        switch (interpretation)
        {
            case WriteInterpretation.Bit:
                {
                    if (!TryParseAll(texts, ValueTextParser.TryParseBit, out bool[] values, out string error))
                    {
                        return CreateParseFailure(error);
                    }

                    return await RunAsync(
                        client,
                        address,
                        values,
                        mode,
                        verify,
                        (target, itemAddress, items, token) => target.WriteBooleanAsync(itemAddress, items, token),
                        (target, itemAddress, item, token) => target.WriteBooleanAsync(itemAddress, item, token),
                        (target, itemAddress, count, token) => target.ReadBooleanAsync(itemAddress, count, token),
                        value => value ? "1" : "0",
                        cancellationToken).ConfigureAwait(true);
                }

            case WriteInterpretation.Word:
                {
                    if (!TryParseAll(texts, ValueTextParser.TryParseUInt16, out ushort[] values, out string error))
                    {
                        return CreateParseFailure(error);
                    }

                    return await RunAsync(
                        client,
                        address,
                        values,
                        mode,
                        verify,
                        (target, itemAddress, items, token) => target.WriteUInt16Async(itemAddress, items, token),
                        (target, itemAddress, item, token) => target.WriteUInt16Async(itemAddress, item, token),
                        (target, itemAddress, count, token) => target.ReadUInt16Async(itemAddress, count, token),
                        value => value.ToString(CultureInfo.InvariantCulture),
                        cancellationToken).ConfigureAwait(true);
                }

            case WriteInterpretation.Int16:
                {
                    if (!TryParseAll(texts, ValueTextParser.TryParseInt16, out short[] values, out string error))
                    {
                        return CreateParseFailure(error);
                    }

                    return await RunAsync(
                        client,
                        address,
                        values,
                        mode,
                        verify,
                        (target, itemAddress, items, token) => target.WriteInt16Async(itemAddress, items, token),
                        (target, itemAddress, item, token) => target.WriteInt16Async(itemAddress, item, token),
                        (target, itemAddress, count, token) => target.ReadInt16Async(itemAddress, count, token),
                        value => value.ToString(CultureInfo.InvariantCulture),
                        cancellationToken).ConfigureAwait(true);
                }

            case WriteInterpretation.DWord:
                {
                    if (!TryParseAll(texts, ValueTextParser.TryParseUInt32, out uint[] values, out string error))
                    {
                        return CreateParseFailure(error);
                    }

                    return await RunAsync(
                        client,
                        address,
                        values,
                        mode,
                        verify,
                        (target, itemAddress, items, token) => target.WriteUInt32Async(itemAddress, items, token),
                        (target, itemAddress, item, token) => target.WriteUInt32Async(itemAddress, item, token),
                        (target, itemAddress, count, token) => target.ReadUInt32Async(itemAddress, count, token),
                        value => value.ToString(CultureInfo.InvariantCulture),
                        cancellationToken).ConfigureAwait(true);
                }

            case WriteInterpretation.Int32:
                {
                    if (!TryParseAll(texts, ValueTextParser.TryParseInt32, out int[] values, out string error))
                    {
                        return CreateParseFailure(error);
                    }

                    return await RunAsync(
                        client,
                        address,
                        values,
                        mode,
                        verify,
                        (target, itemAddress, items, token) => target.WriteInt32Async(itemAddress, items, token),
                        (target, itemAddress, item, token) => target.WriteInt32Async(itemAddress, item, token),
                        (target, itemAddress, count, token) => target.ReadInt32Async(itemAddress, count, token),
                        value => value.ToString(CultureInfo.InvariantCulture),
                        cancellationToken).ConfigureAwait(true);
                }

            case WriteInterpretation.Float32:
                {
                    if (!TryParseAll(texts, ValueTextParser.TryParseSingle, out float[] values, out string error))
                    {
                        return CreateParseFailure(error);
                    }

                    return await RunAsync(
                        client,
                        address,
                        values,
                        mode,
                        verify,
                        (target, itemAddress, items, token) => target.WriteSingleAsync(itemAddress, items, token),
                        (target, itemAddress, item, token) => target.WriteSingleAsync(itemAddress, item, token),
                        (target, itemAddress, count, token) => target.ReadSingleAsync(itemAddress, count, token),
                        value => value.ToString("R", CultureInfo.InvariantCulture),
                        cancellationToken).ConfigureAwait(true);
                }

            default:
                {
                    if (!TryParseAll(texts, ValueTextParser.TryParseDouble, out double[] values, out string error))
                    {
                        return CreateParseFailure(error);
                    }

                    return await RunAsync(
                        client,
                        address,
                        values,
                        mode,
                        verify,
                        (target, itemAddress, items, token) => target.WriteDoubleAsync(itemAddress, items, token),
                        (target, itemAddress, item, token) => target.WriteDoubleAsync(itemAddress, item, token),
                        (target, itemAddress, count, token) => target.ReadDoubleAsync(itemAddress, count, token),
                        value => value.ToString("R", CultureInfo.InvariantCulture),
                        cancellationToken).ConfigureAwait(true);
                }
        }
    }

    private static async Task<WriteOutcome> RunAsync<T>(
        PlcClient client,
        string address,
        T[] values,
        WriteMode mode,
        bool verify,
        Func<PlcClient, string, T[], CancellationToken, Task<PlcResult>> writeBlock,
        Func<PlcClient, string, T, CancellationToken, Task<PlcResult>> writeSingle,
        Func<PlcClient, string, int, CancellationToken, Task<PlcResult<T[]>>> readMany,
        Func<T, string> format,
        CancellationToken cancellationToken)
    {
        var rows = new List<WriteRow>(values.Length);
        int completed = 0;

        if (mode == WriteMode.Block)
        {
            PlcResult result = await writeBlock(client, address, values, cancellationToken).ConfigureAwait(true);
            if (!result.IsSuccess)
            {
                return CreateFailure(result.Error, completed, values.Length, rows);
            }

            for (int i = 0; i < values.Length; i++)
            {
                rows.Add(new WriteRow(AddressIncrementer.Increment(address, i), format(values[i]), "성공"));
            }

            completed = values.Length;
        }
        else
        {
            for (int i = 0; i < values.Length; i++)
            {
                string itemAddress = AddressIncrementer.Increment(address, i);
                PlcResult result = await writeSingle(client, itemAddress, values[i], cancellationToken).ConfigureAwait(true);
                if (!result.IsSuccess)
                {
                    rows.Add(new WriteRow(itemAddress, format(values[i]), "실패"));
                    return CreateFailure(result.Error, completed, values.Length, rows);
                }

                rows.Add(new WriteRow(itemAddress, format(values[i]), "성공"));
                completed++;
            }
        }

        bool? isVerified = null;
        string verificationMessage = string.Empty;

        if (verify)
        {
            (isVerified, verificationMessage) = await VerifyAsync(
                client, address, values, readMany, format, cancellationToken).ConfigureAwait(true);
        }

        return new WriteOutcome
        {
            IsSuccess = true,
            Message = mode == WriteMode.Block
                ? $"블록 쓰기 {completed}개 완료"
                : $"주소 증가 쓰기 {completed}개 완료",
            CompletedCount = completed,
            TotalCount = values.Length,
            IsVerified = isVerified,
            VerificationMessage = verificationMessage,
            Rows = rows,
        };
    }

    private static async Task<(bool? IsVerified, string Message)> VerifyAsync<T>(
        PlcClient client,
        string address,
        T[] expected,
        Func<PlcClient, string, int, CancellationToken, Task<PlcResult<T[]>>> readMany,
        Func<T, string> format,
        CancellationToken cancellationToken)
    {
        PlcResult<T[]> result = await readMany(client, address, expected.Length, cancellationToken).ConfigureAwait(true);
        if (!result.IsSuccess)
        {
            return (null, $"검증 읽기 실패: {result.Error.Message}");
        }

        T[] actual = result.Value;
        for (int i = 0; i < expected.Length && i < actual.Length; i++)
        {
            if (!EqualityComparer<T>.Default.Equals(expected[i], actual[i]))
            {
                return (false,
                    $"검증 불일치: {AddressIncrementer.Increment(address, i)} — 쓴 값 {format(expected[i])}, 읽은 값 {format(actual[i])}");
            }
        }

        return (true, $"검증 일치 ({expected.Length}개)");
    }

    private static bool TryParseAll<T>(
        IReadOnlyList<string> texts,
        ValueParser<T> parser,
        out T[] values,
        out string error)
    {
        values = Array.Empty<T>();
        error = string.Empty;

        if (texts.Count == 0)
        {
            error = "값을 한 줄 이상 입력하세요.";
            return false;
        }

        values = new T[texts.Count];
        for (int i = 0; i < texts.Count; i++)
        {
            if (!parser(texts[i], out T value))
            {
                error = $"{i + 1}번째 값 '{texts[i]}'을(를) 해석할 수 없습니다.";
                values = Array.Empty<T>();
                return false;
            }

            values[i] = value;
        }

        return true;
    }

    private static WriteOutcome CreateParseFailure(string error) => new()
    {
        IsSuccess = false,
        Message = error,
        CompletedCount = 0,
        TotalCount = 0,
    };

    private static WriteOutcome CreateFailure(PlcError error, int completed, int total, IReadOnlyList<WriteRow> rows)
    {
        string detail = error.DeviceErrorCode is null
            ? error.Message
            : $"{error.Message} (장치 코드 0x{error.DeviceErrorCode:X4})";

        return new WriteOutcome
        {
            IsSuccess = false,
            Message = $"쓰기 실패: {detail} (완료 {completed}/{total})",
            CompletedCount = completed,
            TotalCount = total,
            Rows = rows,
        };
    }
}
