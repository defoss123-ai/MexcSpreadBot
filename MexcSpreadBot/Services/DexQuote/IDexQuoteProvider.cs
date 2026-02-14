using MexcSpreadBot.Data;

namespace MexcSpreadBot.Services.DexQuoteProviders
{
    public interface IDexQuoteProvider
    {
        string Name { get; }
        bool CanHandle(QuotePair pair);
        Task<DexQuote?> GetQuoteAsync(QuotePair pair, CancellationToken ct);
    }
}
