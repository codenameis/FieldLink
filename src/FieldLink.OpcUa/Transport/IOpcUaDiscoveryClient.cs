using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Opc.Ua;

namespace FieldLink.OpcUa.Transport;

internal interface IOpcUaDiscoveryClient : IDisposable
{
    Task<IReadOnlyList<EndpointDescription>> GetEndpointsAsync(Uri endpointUrl, CancellationToken cancellationToken);
}
