using MexcSpreadBot.Data;
using MexcSpreadBot.Helpers;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace MexcSpreadBot.Services.DexQuote
{
    public sealed class DexQuoteService : IDisposable
    {
        private readonly IReadOnlyList<IDexQuoteProvider> _providers;
        private readonly ConcurrentDictionary<string, PairData> _state;
        private readonly SemaphoreSlim _semaphore;
        private CancellationTokenSource? _cts;
        private Task? _worker;

        public event Action<PairData>? QuoteUpdated;
        public event Action<string>? Log;

        public DexQuoteService(ConcurrentDictionary<string, PairData> state, int maxParallelism = 20)
        {
            _state = state;
            _semaphore = new SemaphoreSlim(maxParallelism, maxParallelism);
            _providers = new IDexQuoteProvider[]
            {
                new Evm0xQuoteProvider(),
                new Evm1inchQuoteProvider(),
                new SolanaJupiterQuoteProvider()
            };
        }

        public void Start(IEnumerable<QuotePair> pairs, int intervalSeconds)
        {
            Stop();
            _cts = new CancellationTokenSource();
            var pairList = pairs.ToList();
            _worker = Task.Run(() => RunPollingAsync(pairList, intervalSeconds, _cts.Token));
        }

        public void Stop()
        {
            if (_cts == null)
            {
                return;
            }

            _cts.Cancel();
            try { _worker?.Wait(TimeSpan.FromSeconds(2)); } catch { }
            _cts.Dispose();
            _cts = null;
            _worker = null;
        }

        private async Task RunPollingAsync(List<QuotePair> pairs, int intervalSeconds, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                foreach (var chunk in pairs.Chunk(20))
                {
                    var tasks = chunk.Select(pair => PollOnePairAsync(pair, ct)).ToArray();
                    await Task.WhenAll(tasks);
                }

                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), ct);
            }
        }

        private async Task PollOnePairAsync(QuotePair pair, CancellationToken ct)
        {
            await _semaphore.WaitAsync(ct);
            try
            {
                var candidates = _providers.Where(p => p.CanHandle(pair)).ToList();
                var attempts = candidates
                    .Select(async provider =>
                    {
                        try
                        {
                            var sw = Stopwatch.StartNew();
                            var quote = await provider.GetQuoteAsync(pair, ct);
                            sw.Stop();
                            return (provider.Name, quote.Bid, quote.Ask, sw.ElapsedMilliseconds);
                        }
                        catch (Exception ex)
                        {
                            FileLog.Error($"DEX provider failed: {provider.Name} {pair.Symbol}", ex);
                            return (provider.Name, 0d, 0d, 0L);
                        }
                    }).ToArray();

                var results = await Task.WhenAll(attempts);
                var selected = results.FirstOrDefault(x => x.Bid > 0 && x.Ask > 0);
                if (selected.Bid <= 0 || selected.Ask <= 0)
                {
                    return;
                }

                var now = DateTime.UtcNow;
                var updated = _state.AddOrUpdate(pair.Symbol,
                    _ => new PairData { Symbol = pair.Symbol },
                    (_, current) =>
                    {
                        current.Symbol = pair.Symbol;
                        current.DexBid = selected.Bid;
                        current.DexAsk = selected.Ask;
                        current.DexLatencyMs = selected.ElapsedMilliseconds;
                        current.DexUpdatedAtUtc = now;
                        return current;
                    });

                QuoteUpdated?.Invoke(updated);
                Log?.Invoke($"[{DateTime.Now:HH:mm:ss}] DEX {pair.Symbol} via {selected.Name}: {selected.ElapsedMilliseconds}ms");
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public void Dispose()
        {
            Stop();
            _semaphore.Dispose();
        }
    }
}
