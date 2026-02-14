using MexcSpreadBot.Data;
using MexcSpreadBot.Helpers;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace MexcSpreadBot.Services.Mexc
{
    public sealed class MexcRealtimeFeed : IDisposable
    {
        private readonly ConcurrentDictionary<string, MexcBookTop> _tops;
        private readonly IReadOnlyList<string> _symbols;
        private CancellationTokenSource? _cts;
        private Task? _worker;

        public event Action<MexcBookTop>? BookTopUpdated;
        public event Action<string>? Log;

        public MexcRealtimeFeed(ConcurrentDictionary<string, MexcBookTop> tops, IEnumerable<string> symbols)
        {
            _tops = tops;
            _symbols = symbols.Select(x => x.ToUpperInvariant()).Distinct().ToList();

            foreach (var symbol in _symbols)
            {
                _tops.TryAdd(symbol, new MexcBookTop { Symbol = symbol });
            }
        }

        public void Start()
        {
            Stop();
            _cts = new CancellationTokenSource();
            _worker = Task.Run(() => RunAsync(_cts.Token));
        }

        public void Stop()
        {
            if (_cts == null)
            {
                return;
            }

            _cts.Cancel();
            try
            {
                _worker?.Wait(TimeSpan.FromSeconds(2));
            }
            catch
            {
                // ignore cancellation flow
            }

            _cts.Dispose();
            _cts = null;
            _worker = null;
        }

        private async Task RunAsync(CancellationToken ct)
        {
            var retryDelay = TimeSpan.FromSeconds(1);

            while (!ct.IsCancellationRequested)
            {
                using var ws = new ClientWebSocket();
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

                try
                {
                    LogMessage("Connecting to MEXC websocket...");
                    await ws.ConnectAsync(new Uri("wss://contract.mexc.com/edge"), linkedCts.Token);
                    LogMessage("MEXC websocket connected");

                    var subscribeMessages = _symbols
                        .Select(symbol => JsonSerializer.Serialize(new { method = "sub.ticker", param = new { symbol } }))
                        .ToList();

                    var subTasks = subscribeMessages
                        .Select(msg => ws.SendAsync(Encoding.UTF8.GetBytes(msg), WebSocketMessageType.Text, true, linkedCts.Token));
                    await Task.WhenAll(subTasks);

                    retryDelay = TimeSpan.FromSeconds(1);

                    var heartbeatTask = HeartbeatAsync(ws, linkedCts.Token);
                    var readTask = ReadLoopAsync(ws, linkedCts.Token);
                    await Task.WhenAny(heartbeatTask, readTask);

                    linkedCts.Cancel();
                    await Task.WhenAll(heartbeatTask.ContinueWith(_ => Task.CompletedTask), readTask.ContinueWith(_ => Task.CompletedTask));
                }
                catch (OperationCanceledException)
                {
                    if (ct.IsCancellationRequested)
                    {
                        return;
                    }
                }
                catch (Exception ex)
                {
                    FileLog.Error("MEXC websocket error", ex);
                    LogMessage($"MEXC websocket error: {ex.Message}");
                }

                if (!ct.IsCancellationRequested)
                {
                    LogMessage($"Reconnect in {retryDelay.TotalSeconds:0}s");
                    await Task.Delay(retryDelay, ct);
                    retryDelay = TimeSpan.FromSeconds(Math.Min(retryDelay.TotalSeconds * 2, 30));
                }
            }
        }

        private async Task HeartbeatAsync(ClientWebSocket ws, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
            {
                var ping = JsonSerializer.Serialize(new { method = "ping" });
                await ws.SendAsync(Encoding.UTF8.GetBytes(ping), WebSocketMessageType.Text, true, ct);
                await Task.Delay(TimeSpan.FromSeconds(15), ct);
            }
        }

        private async Task ReadLoopAsync(ClientWebSocket ws, CancellationToken ct)
        {
            var buffer = new byte[64 * 1024];

            while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;

                do
                {
                    result = await ws.ReceiveAsync(buffer, ct);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        return;
                    }

                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                var payload = Encoding.UTF8.GetString(ms.ToArray());
                HandleMessage(payload);
            }
        }

        private void HandleMessage(string payload)
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            if (!root.TryGetProperty("data", out var data))
            {
                return;
            }

            var symbol = data.TryGetProperty("symbol", out var symbolProp) ? symbolProp.GetString() : null;
            if (string.IsNullOrWhiteSpace(symbol))
            {
                return;
            }

            if (!TryGetDouble(data, "bid1", out var bid) || !TryGetDouble(data, "ask1", out var ask))
            {
                return;
            }

            var now = DateTime.UtcNow;
            var top = _tops.AddOrUpdate(symbol,
                _ => new MexcBookTop { Symbol = symbol, Bid = bid, Ask = ask, UpdatedAtUtc = now },
                (_, current) =>
                {
                    current.Bid = bid;
                    current.Ask = ask;
                    current.UpdatedAtUtc = now;
                    return current;
                });

            BookTopUpdated?.Invoke(new MexcBookTop
            {
                Symbol = top.Symbol,
                Bid = top.Bid,
                Ask = top.Ask,
                UpdatedAtUtc = top.UpdatedAtUtc
            });
        }

        private static bool TryGetDouble(JsonElement source, string property, out double value)
        {
            value = 0;
            if (!source.TryGetProperty(property, out var element))
            {
                return false;
            }

            if (element.ValueKind == JsonValueKind.Number)
            {
                return element.TryGetDouble(out value);
            }

            if (element.ValueKind == JsonValueKind.String)
            {
                return double.TryParse(element.GetString(), out value);
            }

            return false;
        }

        private void LogMessage(string message)
        {
            Log?.Invoke($"[{DateTime.Now:HH:mm:ss}] {message}");
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
