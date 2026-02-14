namespace MexcSpreadBot.Data
{
    public sealed class DexQuote
    {
        public string Symbol { get; set; } = string.Empty;
        public double Bid { get; set; }
        public double Ask { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }
}
