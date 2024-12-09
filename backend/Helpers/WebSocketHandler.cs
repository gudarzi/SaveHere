using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;

namespace SaveHere.WebAPI;

public static class WebSocketHandler
{
  private static ConcurrentDictionary<string, WebSocket> _sockets = new ConcurrentDictionary<string, WebSocket>();

  public static async Task HandleWebSocketAsync(WebSocket webSocket)
  {
    var socketId = Guid.NewGuid().ToString();
    _sockets.TryAdd(socketId, webSocket);

    var buffer = new byte[1024 * 4];
    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

    while (!result.CloseStatus.HasValue)
    {
      result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
    }

    _sockets.TryRemove(socketId, out _);
    await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
  }

  public static async Task SendMessageAsync(string message)
  {
    var messageBuffer = Encoding.UTF8.GetBytes(message);

    foreach (var socket in _sockets.Values)
    {
      if (socket.State == WebSocketState.Open)
      {
        await socket.SendAsync(new ArraySegment<byte>(messageBuffer), WebSocketMessageType.Text, true, CancellationToken.None);
      }
    }
  }
}
