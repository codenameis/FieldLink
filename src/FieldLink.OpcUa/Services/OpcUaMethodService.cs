using System.Threading;
using System.Threading.Tasks;
using FieldLink.OpcUa.Internal;
using FieldLink.OpcUa.Transport;
using Opc.Ua;

namespace FieldLink.OpcUa.Services;

internal static class OpcUaMethodService
{
    private const int ExpectedSingleResultCount = 1;

    public static async Task<CallMethodResult> CallAsync(
        IOpcUaSessionAdapter session,
        OpcUaDeadline deadline,
        OpcUaMethodCallParameters parameters,
        CancellationToken cancellationToken)
    {
        NodeId resolvedObjectId = OpcUaNodeResolver.ResolveNodeId(
            parameters.ObjectId,
            session.NamespaceUris,
            nameof(parameters.ObjectId));
        NodeId resolvedMethodId = OpcUaNodeResolver.ResolveNodeId(
            parameters.MethodId,
            session.NamespaceUris,
            nameof(parameters.MethodId));

        VariantCollection arguments = new(parameters.InputArguments.Count);
        for (int i = 0; i < parameters.InputArguments.Count; i++)
        {
            arguments.Add(parameters.InputArguments[i]);
        }

        CallMethodRequestCollection requests = new()
        {
            new CallMethodRequest
            {
                ObjectId = resolvedObjectId,
                MethodId = resolvedMethodId,
                InputArguments = arguments
            }
        };

        uint timeoutHint = deadline.GetOperationTimeoutHintMilliseconds(cancellationToken, nameof(CallAsync));

        // 메서드 호출은 장비 동작 등 부작용을 일으키므로 타임아웃이나 응답 유실 시 절대 자동 재전송하지 않는다.
        CallResponse response = await session
            .CallAsync(timeoutHint, requests, cancellationToken)
            .ConfigureAwait(false);

        if (response.Results is null || response.Results.Count != ExpectedSingleResultCount || response.Results[0] is null)
        {
            int actualCount = response.Results?.Count ?? 0;
            throw new ServiceResultException(
                StatusCodes.BadUnexpectedError,
                $"서버 Call 응답 결과 수({actualCount})가 1이 아닙니다.");
        }

        return response.Results[0];
    }
}
