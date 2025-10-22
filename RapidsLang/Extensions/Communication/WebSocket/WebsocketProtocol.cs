using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using JetBrains.Annotations;
using RapidsLang.Extensions.Communication.WebSocket.C2S;
using RapidsLang.Extensions.Communication.WebSocket.S2C;
using RapidsLang.Extensions.Pipes;
using RapidsLang.Interpreter;
using RapidsLang.Interpreter.Variables;

namespace RapidsLang.Extensions.Communication.WebSocket;


public class WebsocketProtocol : CommunicationProtocol
{
    [JsonPropertyName("port")] [UsedImplicitly] public int? Port { get; set; } = DefaultPort;

    private const int DefaultPort = 62712;
    private const long AbsoluteMaxMessageSize = 1_073_741_824; // 1gb

    private static readonly Dictionary<int, HttpListener> Servers;
    private readonly HttpListener _server;

    private readonly List<System.Net.WebSockets.WebSocket> _webSockets = [];
    private readonly Lock _requestsLock = new();
    private readonly List<C2SWebsocketRequest> _requests = new();

    private readonly Dictionary<Identifier, Dictionary<Guid, PipeSubscriber>> _eventListeners = [];
    

    public WebsocketProtocol()
    {
        var port = Port ?? DefaultPort;

        if (Servers.TryGetValue(port, out var server))
        {
            _server = server;
        }
        else
        {
            _server = new HttpListener();
            // Rider says http is insecure
            // however, most connections happen on localhost
            _server.Prefixes.Add($"http://*:{port}/"); 
            
            _server.Start();
            Servers[port] = _server;
        }
    }

    static WebsocketProtocol()
    {
        Servers = [];
    }

    public override PipeWriteResult WriteToInput(Identifier identifier, RapidsVariable? value)
    {
        Task.Run(() => BroadcastMessage(new S2CWriteToTarget(identifier, value ?? new RapidsNullVariable())));
        
        return new GoodPipeWriteResult();
    }

    public override void SubscribeToOutput(Identifier identifier, PipeSubscriber subscriber)
    {
        if (!_eventListeners.TryGetValue(identifier, out Dictionary<Guid, PipeSubscriber>? value))
        {
            value = [];
            _eventListeners[identifier] = value;
        }

        value[subscriber.Guid] = subscriber;
    }

    public override void UnsubscribeToOutput(Identifier identifier, Guid guid)
    {
        _eventListeners[identifier].Remove(guid);

        if (_eventListeners[identifier].Count == 0)
        {
            BroadcastMessage(new S2CSourceEndListening(identifier)).Wait();
        }
    }

    public override void Init()
    {
        base.Init();

        Task.Run(BeginAcceptingClients);
    }

    public override void Tick(InterpreterContext ctx)
    {
        lock (_requestsLock)
        {
            foreach (var req in _requests)
            {
                if (req is C2SSourceData sourceData)
                {
                    if (_eventListeners.TryGetValue(sourceData.Identifier, out var eventListeners))
                    {
                        foreach (var listener in eventListeners)
                        {
                            // in the future, it should FromJSON once, then deep clone the value, but this works.
                            listener.Value.Event.Invoke(RapidsVariable.FromJSON(sourceData.Data));
                        }
                    }
                }
            }
            
            _requests.Clear();
        }
    }

    private async Task BeginAcceptingClients()
    {
        while (true)
        {
            HttpListenerContext ctx = await _server.GetContextAsync();

            if (ctx.Request.IsWebSocketRequest)
            {
                HttpListenerWebSocketContext wsCtx = await ctx.AcceptWebSocketAsync(null);
                
                _webSockets.Add(wsCtx.WebSocket);

                _ = HandleWebsocket(wsCtx.WebSocket);
            }
        }
    }

    private async Task HandleWebsocket(System.Net.WebSockets.WebSocket ws)
    {
        try
        {
            while (ws.State == WebSocketState.Open)
            {
                var timeout = new CancellationTokenSource();
                timeout.CancelAfter(TimeSpan.FromSeconds(15));
                var res = await ReceiveFullMessageAsync(ws, timeout.Token);

                var req = JsonSerializer.Deserialize<C2SWebsocketRequest>(res.Data);
                if (req is null)
                {
                    // client seems to have sent invalid json... So that's not supposed to happen.
                    // lets just sort of ignore this request and hope this doesn't happen again.
                    // in the future, this should probably end up as something a user can look for.
                    Debug.WriteLine("WS request was not json deserializable. This is almost certainly an issue with the client.");
                    continue;
                }

                lock (_requestsLock)
                {
                    _requests.Add(req);
                }
            }
        }
        catch (WebSocketException ex)
        {
            Console.WriteLine(ex);
        }
        finally
        {
            _webSockets.Remove(ws);
            ws.Dispose();
        }
    }

    private async Task BroadcastMessage(S2CWebsocketRequest req)
    {
        foreach (var webSocket in _webSockets)
        {
            var options = new JsonSerializerOptions()
            {
                Converters = { new RapidsVariableJsonConverter() }
            };
            
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(req, options));
            
            await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }

    private record WebSocketMessage(byte[] Data, WebSocketMessageType MessageType);

    private static async Task<WebSocketMessage> ReceiveFullMessageAsync(
        System.Net.WebSockets.WebSocket ws,
        CancellationToken cancellationToken,
        long? maxMessageSize = AbsoluteMaxMessageSize,
        int initialBufferSize = 4096 // 4kb
    )
    {
        using var ms = new MemoryStream();

        var buffer = new ArraySegment<byte>(new byte[initialBufferSize]);

        WebSocketReceiveResult res;
        do
        {
            res = await ws.ReceiveAsync(buffer, cancellationToken);

            if (res.MessageType is WebSocketMessageType.Close)
            {
                break;
            }

            ms.Write(buffer.Array!, buffer.Offset, res.Count);

            if (maxMessageSize.HasValue && ms.Length > maxMessageSize.Value)
            {
                throw new Exception(
                    $"WebSocket message size {ms.Length} bytes exceeds the allowed limit of {maxMessageSize.Value} bytes.");
            }
        } while (!res.EndOfMessage);

        return new WebSocketMessage(ms.ToArray(), res.MessageType);
    }
}