using MexcSpreadBot.Data;

namespace MexcSpreadBot.Services.DexQuote
{
    public interface IDexQuoteProvider
    {
        string Name { get; }
        bool CanHandle(QuotePair pair);
        Task<(double Bid, double Ask)> GetQuoteAsync(QuotePair pair, CancellationToken ct);
    }
}
