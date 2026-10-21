using System;
using System.Collections.Generic;

namespace FieldLink.PacketBench.Operations;

/// <summary>쓰기 한 번의 결과입니다.</summary>
internal sealed class WriteOutcome
{
    public bool IsSuccess { get; init; }

    public string Message { get; init; } = string.Empty;

    public int CompletedCount { get; init; }

    public int TotalCount { get; init; }

    /// <summary>검증을 하지 않았으면 null, 했으면 일치 여부입니다.</summary>
    public bool? IsVerified { get; init; }

    public string VerificationMessage { get; init; } = string.Empty;

    public IReadOnlyList<WriteRow> Rows { get; init; } = Array.Empty<WriteRow>();
}
