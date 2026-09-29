using System;
using System.Net.WebSockets;
using System.Text;

namespace FieldLink.Communication.WebSockets
{
    /// <summary>완성된 WebSocket 텍스트 또는 바이너리 메시지입니다.</summary>
    public sealed class WebSocketMessage
    {
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private readonly string text;

        internal WebSocketMessage(WebSocketMessageType type, byte[] payload)
        {
            Type = type;
            Payload = payload;
            if (type == WebSocketMessageType.Text)
                text = StrictUtf8.GetString(payload);
        }

        /// <summary>메시지가 텍스트인지 바이너리인지 나타냅니다.</summary>
        public WebSocketMessageType Type { get; }
        /// <summary>수신 버퍼와 독립된 바이트 배열입니다.</summary>
        public byte[] Payload { get; }
        /// <summary>텍스트 메시지의 UTF-8 내용입니다. 바이너리 메시지에서는 사용할 수 없습니다.</summary>
        public string Text => Type == WebSocketMessageType.Text
            ? text
            : throw new InvalidOperationException("텍스트 메시지가 아닙니다.");
    }
}
