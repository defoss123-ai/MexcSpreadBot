namespace MexcSpreadBot.Data
{
    public sealed class MexcBookTop
    {
        public string Symbol { get; set; } = string.Empty;
        public double Bid { get; set; }
        public double Ask { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }
}
