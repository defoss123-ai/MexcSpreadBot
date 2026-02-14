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
                var attempts = candidates.Select(provider => TryGetQuoteAsync(provider, pair, ct)).ToArray();
                var results = await Task.WhenAll(attempts);

                var selected = results.FirstOrDefault(x => x.Quote != null && x.Quote.Bid > 0 && x.Quote.Ask > 0);
                if (selected.Quote == null)
                {
                    return;
                }

                var quote = selected.Quote;
                var updated = _state.AddOrUpdate(pair.Symbol,
                    _ => new PairData { Symbol = pair.Symbol },
                    (_, current) =>
                    {
                        current.Symbol = pair.Symbol;
                        current.DexBid = quote.Bid;
                        current.DexAsk = quote.Ask;
                        current.DexLatencyMs = selected.ElapsedMilliseconds;
                        current.DexUpdatedAtUtc = quote.UpdatedAtUtc;
                        return current;
                    });

                QuoteUpdated?.Invoke(updated);
                Log?.Invoke($"[{DateTime.Now:HH:mm:ss}] DEX {pair.Symbol} via {selected.Provider}: {selected.ElapsedMilliseconds}ms");
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private static async Task<ProviderAttempt> TryGetQuoteAsync(IDexQuoteProvider provider, QuotePair pair, CancellationToken ct)
        {
            try
            {
                var sw = Stopwatch.StartNew();
                var quote = await provider.GetQuoteAsync(pair, ct);
                sw.Stop();
                return new ProviderAttempt(provider.Name, quote, sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                FileLog.Error($"DEX provider failed: {provider.Name} {pair.Symbol}", ex);
                return new ProviderAttempt(provider.Name, null, 0);
            }
        }

        private sealed record ProviderAttempt(string Provider, DexQuote? Quote, long ElapsedMilliseconds);

        public void Dispose()
        {
            Stop();
            _semaphore.Dispose();
        }
    }
}
