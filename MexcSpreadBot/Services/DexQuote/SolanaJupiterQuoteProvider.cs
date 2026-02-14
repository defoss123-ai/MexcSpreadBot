using MexcSpreadBot.Data;

namespace MexcSpreadBot.Services.DexQuote
{
    public sealed class SolanaJupiterQuoteProvider : IDexQuoteProvider
    {
        public string Name => "Jupiter";

        public bool CanHandle(QuotePair pair) => pair.Chain == ChainType.Solana;

        public Task<DexQuote?> GetQuoteAsync(QuotePair pair, CancellationToken ct)
        {
            // Reserved for next rollout step.
            return Task.FromResult<DexQuote?>(null);
        }
    }
}
