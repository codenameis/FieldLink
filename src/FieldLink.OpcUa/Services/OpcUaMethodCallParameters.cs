using System.Collections.Generic;
using Opc.Ua;

namespace FieldLink.OpcUa.Services;

internal readonly struct OpcUaMethodCallParameters
{
    public OpcUaMethodCallParameters(
        ExpandedNodeId objectId,
        ExpandedNodeId methodId,
        IReadOnlyList<Variant> inputArguments)
    {
        ObjectId = objectId;
        MethodId = methodId;
        InputArguments = inputArguments;
    }

    public ExpandedNodeId ObjectId { get; }

    public ExpandedNodeId MethodId { get; }

    public IReadOnlyList<Variant> InputArguments { get; }
}
