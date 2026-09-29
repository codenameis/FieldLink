using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Secs;
using FieldLink.Secs.Types;

namespace FieldLink.Communication.Samples
{
    public static class SecsGemExample
    {
        public static async Task<OnlineData> ReadIdentityAsync(IPEndPoint endpoint, ushort deviceID, CancellationToken cancellationToken)
        {
            using (var client = new HsmsClient(endpoint, deviceID, selectOnOpen: true, stringEncoding: Encoding.ASCII))
            {
                await client.OpenAsync(cancellationToken);
                OnlineData identity = await client.Gem.AreYouThereAsync(cancellationToken);
                await client.CloseAsync(cancellationToken);
                return identity;
            }
        }

        // 반환된 서버의 StartAsync, StopAsync, Dispose는 호출 애플리케이션이 소유합니다.
        public static HsmsServer CreateIdentityServer(IPEndPoint endpoint, string model, string version)
        {
            SecsValue identity = new OnlineData(model, version);
            var server = new HsmsServer(endpoint, stringEncoding: Encoding.ASCII);
            server.MessageReceived += (session, request) =>
            {
                if (request.W && request.StreamNo == 1 && request.FunctionNo == 1)
                    // 수신 펌프와 별도 콜백 큐이므로 응답 전송 완료를 동기 대기할 수 있습니다.
                    session.ReplyAsync(request, identity).GetAwaiter().GetResult();
            };
            return server;
        }
    }
}
