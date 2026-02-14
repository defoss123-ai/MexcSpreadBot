using MexcSpreadBot.Data;

namespace MexcSpreadBot.Services.DexQuote
{
    public sealed class Evm1inchQuoteProvider : IDexQuoteProvider
    {
        public string Name => "1inch";

        public bool CanHandle(QuotePair pair) => pair.Chain == ChainType.Evm;

        public Task<DexQuote?> GetQuoteAsync(QuotePair pair, CancellationToken ct)
        {
            // 1inch production APIs often require API keys.
            return Task.FromResult<DexQuote?>(null);
        }
    }
}
