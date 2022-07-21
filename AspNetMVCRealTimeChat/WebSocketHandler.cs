using System.Net.WebSockets;
using System.Text;

namespace AspNetMVCRealTimeChat
{
    public class SocketConnection
    {
        public Guid Id { get; set; }
        public WebSocket WebSocket { get; set; }
    }
    public interface IWebSocketHandler
    {
        Task Handle(Guid id, WebSocket webSocket);
    }

    public class WebSocketHandler : IWebSocketHandler
    {
        public List<SocketConnection> websocketConnections = new List<SocketConnection>();
        public WebSocketHandler()
        {
            SetupCleanUpTask();
        }
        private void SetupCleanUpTask()
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    IEnumerable<SocketConnection> openSockets;
                    IEnumerable<SocketConnection> closedSockets;

                    lock (websocketConnections)
                    {
                        openSockets = websocketConnections.Where(x => x.WebSocket.State == WebSocketState.Open || x.WebSocket.State == WebSocketState.Connecting);
                        closedSockets = websocketConnections.Where(x => x.WebSocket.State != WebSocketState.Open && x.WebSocket.State != WebSocketState.Connecting);

                        websocketConnections = openSockets.ToList();
                    }

                    //foreach (var closedWebsocketConnection in closedSockets)
                    //{
                    //    await SendMessageToSockets($"User with id <span style='font-weight:bold;'>{closedWebsocketConnection.Id}</span> has left the chat");
                    //}

                    await Task.Delay(5000);
                }
            });
        }
        public async Task Handle(Guid id, WebSocket webSocket)
        {
            lock (websocketConnections)
            {
                websocketConnections.Add(new SocketConnection
                {
                    Id = id,
                    WebSocket = webSocket
                });
            }

            //await SendMessageToSockets($"User with id <span style='font-weight:bold;'>{id}</span> has joined the chat");

            while (webSocket.State == WebSocketState.Open)
            {
                var message = await ReceiveMessage(id, webSocket);
                if (message != null)
                    await SendMessageToSockets(message, id);
            }
        }

        private async Task<string> ReceiveMessage(Guid id, WebSocket webSocket)
        {
            var arraySegment = new ArraySegment<byte>(new byte[4096]);
            var receivedMessage = await webSocket.ReceiveAsync(arraySegment, CancellationToken.None);
            if (receivedMessage.MessageType == WebSocketMessageType.Text)
            {
                var message = Encoding.Default.GetString(arraySegment).TrimEnd('\0');
                if (!string.IsNullOrWhiteSpace(message))
                    return message;
            }
            return null;
        }

        private async Task SendMessageToSockets(string message, Guid idToIgnore)
        {
            IEnumerable<SocketConnection> toSentTo;

            lock (websocketConnections)
            {
                toSentTo = websocketConnections.Where(x => x.Id != idToIgnore).ToList();
            }

            var tasks = toSentTo.Select(async websocketConnection =>
            {
                var bytes = Encoding.Default.GetBytes(message);
                var arraySegment = new ArraySegment<byte>(bytes);
                await websocketConnection.WebSocket.SendAsync(arraySegment, WebSocketMessageType.Text, true, CancellationToken.None);
            });
            await Task.WhenAll(tasks);
        }
    }
}
