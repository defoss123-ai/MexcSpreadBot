using MexcSpreadBot.Data;
using MexcSpreadBot.Services.DexQuote;
using MexcSpreadBot.Services.Mexc;
using MexcSpreadBot.Services.Spread;
using System.Collections.Concurrent;

namespace MexcSpreadBot.Helpers
{
    public sealed class RealtimeSpreadScanner : IDisposable
    {
        private readonly ConcurrentDictionary<string, MexcBookTop> _mexcTops = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, PairData> _dexState = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, SpreadRow> _spreads = new(StringComparer.OrdinalIgnoreCase);

        private readonly MexcRealtimeFeed _mexcFeed;
        private readonly DexQuoteService _dexQuoteService;
        private readonly SpreadEngine _spreadEngine;
        private readonly List<QuotePair> _pairs;

        public event Action<SpreadRow>? SpreadUpdated;
        public event Action<string>? Log;

        public RealtimeSpreadScanner(IEnumerable<QuotePair> pairs, double mexcFeePercent, double dexFeePercent, double slippagePercent)
        {
            _pairs = pairs.ToList();
            _mexcFeed = new MexcRealtimeFeed(_mexcTops, _pairs.Select(x => x.Symbol));
            _dexQuoteService = new DexQuoteService(_dexState, 20);
            _spreadEngine = new SpreadEngine(_mexcTops, _dexState, _spreads, mexcFeePercent, dexFeePercent, slippagePercent);

            _mexcFeed.BookTopUpdated += top => RecalculateAndPublish(top.Symbol);
            _dexQuoteService.QuoteUpdated += quote => RecalculateAndPublish(quote.Symbol);

            _mexcFeed.Log += m => Log?.Invoke(m);
            _dexQuoteService.Log += m => Log?.Invoke(m);
        }

        public void Start(int dexIntervalSeconds)
        {
            _mexcFeed.Start();
            _dexQuoteService.Start(_pairs, dexIntervalSeconds);
            Log?.Invoke($"[{DateTime.Now:HH:mm:ss}] Scanner started");
        }

        public void Stop()
        {
            _dexQuoteService.Stop();
            _mexcFeed.Stop();
            Log?.Invoke($"[{DateTime.Now:HH:mm:ss}] Scanner stopped");
        }

        private void RecalculateAndPublish(string symbol)
        {
            if (!_spreadEngine.TryRecalculate(symbol, out var row))
            {
                return;
            }

            SpreadUpdated?.Invoke(new SpreadRow
            {
                Symbol = row.Symbol,
                MexBid = row.MexBid,
                MexAsk = row.MexAsk,
                DexBid = row.DexBid,
                DexAsk = row.DexAsk,
                SpreadA = row.SpreadA,
                SpreadB = row.SpreadB,
                SpreadNetA = row.SpreadNetA,
                SpreadNetB = row.SpreadNetB,
                MexAgeMs = row.MexAgeMs,
                DexAgeMs = row.DexAgeMs,
                LastUpdateTime = row.LastUpdateTime
            });
        }

        public void Dispose()
        {
            Stop();
            _dexQuoteService.Dispose();
            _mexcFeed.Dispose();
        }
    }
}
