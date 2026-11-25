using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.OpcUa.Internal;
using FieldLink.OpcUa.Transport;
using Opc.Ua;

namespace FieldLink.OpcUa.Services;

internal static class OpcUaBrowseService
{
    private const int ExpectedSingleResultCount = 1;
    private const int MaxConsecutiveEmptyPages = 64;
    private const int ContinuationReleaseTimeoutSeconds = 2;

    private static readonly TimeSpan ContinuationReleaseTimeout = TimeSpan.FromSeconds(ContinuationReleaseTimeoutSeconds);

    public static async Task<IReadOnlyList<ReferenceDescription>> BrowseAsync(
        IOpcUaSessionAdapter session,
        OpcUaDeadline deadline,
        ExpandedNodeId nodeId,
        OpcUaBrowseOptions options,
        CancellationToken cancellationToken)
    {
        NodeId resolvedId = OpcUaNodeResolver.ResolveNodeId(nodeId, session.NamespaceUris, nameof(nodeId));
        BrowseDescriptionCollection nodesToBrowse = BuildBrowseDescription(resolvedId, options);
        BrowseProgress progress = new();

        try
        {
            await ExecuteInitialBrowseAsync(session, deadline, options, nodesToBrowse, progress, cancellationToken)
                .ConfigureAwait(false);

            await CollectRemainingPagesAsync(session, deadline, options, progress, cancellationToken)
                .ConfigureAwait(false);

            return new ReadOnlyCollection<ReferenceDescription>(progress.Collected);
        }
        catch
        {
            await ReleaseContinuationPointSafelyAsync(session, progress.ContinuationPoint).ConfigureAwait(false);
            throw;
        }
    }

    private static BrowseDescriptionCollection BuildBrowseDescription(NodeId resolvedId, OpcUaBrowseOptions options)
    {
        return new BrowseDescriptionCollection
        {
            new BrowseDescription
            {
                NodeId = resolvedId,
                BrowseDirection = options.BrowseDirection,
                ReferenceTypeId = options.ReferenceTypeId,
                IncludeSubtypes = options.IncludeSubtypes,
                NodeClassMask = options.NodeClassMask,
                ResultMask = (uint)BrowseResultMask.All
            }
        };
    }

    private static async Task ExecuteInitialBrowseAsync(
        IOpcUaSessionAdapter session,
        OpcUaDeadline deadline,
        OpcUaBrowseOptions options,
        BrowseDescriptionCollection nodesToBrowse,
        BrowseProgress progress,
        CancellationToken cancellationToken)
    {
        uint timeoutHint = deadline.GetOperationTimeoutHintMilliseconds(cancellationToken, nameof(BrowseAsync));
        BrowseResponse response = await session
            .BrowseAsync(timeoutHint, options.MaxReferencesPerPage, nodesToBrowse, cancellationToken)
            .ConfigureAwait(false);

        if (response.Results is null || response.Results.Count != ExpectedSingleResultCount || response.Results[0] is null)
        {
            throw new ServiceResultException(StatusCodes.BadUnexpectedError, "Browse 응답 결과 개수가 올바르지 않습니다.");
        }

        int previousCount = progress.Collected.Count;
        CollectPageReferences(response.Results[0], options, progress);
        TrackEmptyPages(progress.Collected.Count - previousCount, progress);
    }

    private static async Task CollectRemainingPagesAsync(
        IOpcUaSessionAdapter session,
        OpcUaDeadline deadline,
        OpcUaBrowseOptions options,
        BrowseProgress progress,
        CancellationToken cancellationToken)
    {
        while (progress.ContinuationPoint is { Length: > 0 })
        {
            uint timeoutHint = deadline.GetOperationTimeoutHintMilliseconds(cancellationToken, nameof(BrowseAsync));
            ByteStringCollection continuationPoints = new() { progress.ContinuationPoint };

            BrowseNextResponse nextResponse = await session
                .BrowseNextAsync(timeoutHint, releaseContinuationPoints: false, continuationPoints, cancellationToken)
                .ConfigureAwait(false);

            if (nextResponse.Results is null || nextResponse.Results.Count != ExpectedSingleResultCount || nextResponse.Results[0] is null)
            {
                throw new ServiceResultException(StatusCodes.BadUnexpectedError, "BrowseNext 응답 결과 개수가 올바르지 않습니다.");
            }

            int previousCount = progress.Collected.Count;
            CollectPageReferences(nextResponse.Results[0], options, progress);
            TrackEmptyPages(progress.Collected.Count - previousCount, progress);
        }
    }

    private static void TrackEmptyPages(int addedCount, BrowseProgress progress)
    {
        if (addedCount > 0 || progress.ContinuationPoint is not { Length: > 0 })
        {
            progress.ConsecutiveEmptyPages = 0;
            return;
        }

        progress.ConsecutiveEmptyPages++;
        if (progress.ConsecutiveEmptyPages >= MaxConsecutiveEmptyPages)
        {
            throw new InvalidOperationException(
                $"서버가 참조 없이 연속 {MaxConsecutiveEmptyPages}회 ContinuationPoint를 반환하여 탐색을 중단했습니다.");
        }
    }

    private static void CollectPageReferences(
        BrowseResult result,
        OpcUaBrowseOptions options,
        BrowseProgress progress)
    {
        // 이후 상태 코드 또는 MaxTotalReferences 검사에서 예외가 나도 최신 ContinuationPoint를 반드시 해제한다.
        progress.ContinuationPoint = result.ContinuationPoint is { Length: > 0 }
            ? (byte[])result.ContinuationPoint.Clone()
            : null;

        if (StatusCode.IsBad(result.StatusCode))
        {
            throw new ServiceResultException(result.StatusCode, "서버가 Browse 요청을 거부했습니다.");
        }

        if (result.References is null)
        {
            return;
        }

        for (int i = 0; i < result.References.Count; i++)
        {
            ReferenceDescription? reference = result.References[i];
            if (reference is null)
            {
                continue;
            }

            progress.Collected.Add(reference);
            if (progress.Collected.Count > options.MaxTotalReferences)
            {
                throw new InvalidOperationException(
                    $"수집된 참조 수가 MaxTotalReferences 한도({options.MaxTotalReferences})를 초과했습니다.");
            }
        }
    }

    private static async Task ReleaseContinuationPointSafelyAsync(
        IOpcUaSessionAdapter session,
        byte[]? continuationPoint)
    {
        if (continuationPoint is not { Length: > 0 })
        {
            return;
        }

        try
        {
            using CancellationTokenSource releaseTimeout = new(ContinuationReleaseTimeout);
            ByteStringCollection pointsToRelease = new() { continuationPoint };
            await session
                .BrowseNextAsync((uint)ContinuationReleaseTimeout.TotalMilliseconds, releaseContinuationPoints: true, pointsToRelease, releaseTimeout.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 종료/취소 시 ContinuationPoint 해제는 최선 노력으로 처리
        }
        catch (ServiceResultException)
        {
            // 세션이 이미 닫혔거나 ContinuationPoint가 만료된 경우 무시
        }
        catch (ObjectDisposedException)
        {
            // 세션이 이미 폐기된 경우 무시
        }
    }

    private sealed class BrowseProgress
    {
        public List<ReferenceDescription> Collected { get; } = new();

        public byte[]? ContinuationPoint { get; set; }

        public int ConsecutiveEmptyPages { get; set; }
    }
}
