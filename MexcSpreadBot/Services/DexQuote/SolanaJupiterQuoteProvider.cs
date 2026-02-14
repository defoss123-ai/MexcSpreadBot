using MexcSpreadBot.Data;

namespace MexcSpreadBot.Services.DexQuote
{
    public sealed class SolanaJupiterQuoteProvider : IDexQuoteProvider
    {
        public string Name => "Jupiter";

        public bool CanHandle(QuotePair pair) => pair.Chain == ChainType.Solana;

        public Task<(double Bid, double Ask)> GetQuoteAsync(QuotePair pair, CancellationToken ct)
        {
            // BTCUSDT-first rollout currently uses EVM only. Placeholder for Solana expansion.
            return Task.FromResult((0d, 0d));
        }
    }
}
