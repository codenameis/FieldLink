using System.Threading;
using System.Threading.Tasks;
using Opc.Ua;

namespace FieldLink.OpcUa.Transport;

internal interface IOpcUaSessionFactory
{
    Task<IOpcUaSessionAdapter> CreateSessionAsync(
        ApplicationConfiguration configuration,
        ConfiguredEndpoint endpoint,
        OpcUaClientOptions options,
        CancellationToken cancellationToken);
}
