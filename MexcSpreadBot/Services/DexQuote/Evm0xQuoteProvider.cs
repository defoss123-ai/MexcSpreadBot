using MexcSpreadBot.Data;
using System.Globalization;
using System.Text.Json;

namespace MexcSpreadBot.Services.DexQuoteProviders
{
    public sealed class Evm0xQuoteProvider : IDexQuoteProvider
    {
        public string Name => "0x";

        public bool CanHandle(QuotePair pair) => pair.Chain == ChainType.Evm;

        public async Task<DexQuote?> GetQuoteAsync(QuotePair pair, CancellationToken ct)
        {
            var sellBaseUrl = $"https://api.0x.org/swap/v1/price?sellToken={pair.BaseTokenAddress}&buyToken={pair.QuoteTokenAddress}&sellAmount=100000000";
            var sellQuoteUrl = $"https://api.0x.org/swap/v1/price?sellToken={pair.QuoteTokenAddress}&buyToken={pair.BaseTokenAddress}&sellAmount=100000000";

            var responses = await Task.WhenAll(
                SharedHttp.Client.GetStringAsync(sellBaseUrl, ct),
                SharedHttp.Client.GetStringAsync(sellQuoteUrl, ct));

            var bid = ReadPrice(responses[0]);
            var inverse = ReadPrice(responses[1]);
            var ask = inverse > 0 ? 1d / inverse : 0;
            return bid > 0 && ask > 0 ? new DexQuote { Bid = bid, Ask = ask } : null;
        }

        private static double ReadPrice(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var raw = doc.RootElement.GetProperty("price").GetString();
            return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var val) ? val : 0;
        }
    }
}
