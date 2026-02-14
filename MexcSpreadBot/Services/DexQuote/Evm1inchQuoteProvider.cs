using MexcSpreadBot.Data;

namespace MexcSpreadBot.Services.DexQuoteProviders
{
    public sealed class Evm1inchQuoteProvider : IDexQuoteProvider
    {
        public string Name => "1inch";

        public bool CanHandle(QuotePair pair) => pair.Chain == ChainType.Evm;

        public Task<DexQuote?> GetQuoteAsync(QuotePair pair, CancellationToken ct)
        {
            // 1inch production quote APIs generally require API key; keep as fallback provider contract.
            return Task.FromResult<DexQuote?>(null);
        }
    }
}
