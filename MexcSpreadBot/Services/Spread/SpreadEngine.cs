using MexcSpreadBot.Data;
using System.Collections.Concurrent;

namespace MexcSpreadBot.Services.Spread
{
    public sealed class SpreadEngine
    {
        private readonly ConcurrentDictionary<string, MexcBookTop> _mexc;
        private readonly ConcurrentDictionary<string, PairData> _dexState;
        private readonly ConcurrentDictionary<string, SpreadRow> _spreads;

        private readonly double _mexcFeePercent;
        private readonly double _dexFeePercent;
        private readonly double _slippagePercent;

        public SpreadEngine(
            ConcurrentDictionary<string, MexcBookTop> mexc,
            ConcurrentDictionary<string, PairData> dexState,
            ConcurrentDictionary<string, SpreadRow> spreads,
            double mexcFeePercent,
            double dexFeePercent,
            double slippagePercent)
        {
            _mexc = mexc;
            _dexState = dexState;
            _spreads = spreads;
            _mexcFeePercent = mexcFeePercent;
            _dexFeePercent = dexFeePercent;
            _slippagePercent = slippagePercent;
        }

        public bool TryRecalculate(string symbol, out SpreadRow row)
        {
            row = new SpreadRow { Symbol = symbol };

            if (!_mexc.TryGetValue(symbol, out var mex) || !_dexState.TryGetValue(symbol, out var dex))
            {
                return false;
            }

            if (mex.Bid <= 0 || mex.Ask <= 0 || dex.DexBid <= 0 || dex.DexAsk <= 0)
            {
                return false;
            }

            var now = DateTime.UtcNow;
            var spreadA = ((dex.DexBid - mex.Ask) / mex.Ask) * 100d;
            var spreadB = ((mex.Bid - dex.DexAsk) / dex.DexAsk) * 100d;

            var totalFees = _mexcFeePercent + _dexFeePercent + _slippagePercent;

            row = _spreads.AddOrUpdate(symbol,
                _ => BuildRow(),
                (_, current) =>
                {
                    CopyTo(current);
                    return current;
                });

            return true;

            SpreadRow BuildRow()
            {
                return new SpreadRow
                {
                    Symbol = symbol,
                    MexBid = mex.Bid,
                    MexAsk = mex.Ask,
                    DexBid = dex.DexBid,
                    DexAsk = dex.DexAsk,
                    SpreadA = spreadA,
                    SpreadB = spreadB,
                    SpreadNetA = spreadA - totalFees,
                    SpreadNetB = spreadB - totalFees,
                    MexAgeMs = (long)(now - mex.UpdatedAtUtc).TotalMilliseconds,
                    DexAgeMs = (long)(now - dex.DexUpdatedAtUtc).TotalMilliseconds,
                    LastUpdateTime = now
                };
            }

            void CopyTo(SpreadRow target)
            {
                target.MexBid = mex.Bid;
                target.MexAsk = mex.Ask;
                target.DexBid = dex.DexBid;
                target.DexAsk = dex.DexAsk;
                target.SpreadA = spreadA;
                target.SpreadB = spreadB;
                target.SpreadNetA = spreadA - totalFees;
                target.SpreadNetB = spreadB - totalFees;
                target.MexAgeMs = (long)(now - mex.UpdatedAtUtc).TotalMilliseconds;
                target.DexAgeMs = (long)(now - dex.DexUpdatedAtUtc).TotalMilliseconds;
                target.LastUpdateTime = now;
            }
        }
    }
}
