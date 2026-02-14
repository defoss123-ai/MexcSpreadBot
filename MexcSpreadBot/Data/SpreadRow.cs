namespace MexcSpreadBot.Data
{
    public sealed class SpreadRow
    {
        public string Symbol { get; set; } = string.Empty;
        public double MexBid { get; set; }
        public double MexAsk { get; set; }
        public double DexBid { get; set; }
        public double DexAsk { get; set; }
        public double SpreadA { get; set; }
        public double SpreadB { get; set; }
        public double SpreadNetA { get; set; }
        public double SpreadNetB { get; set; }
        public long MexAgeMs { get; set; }
        public long DexAgeMs { get; set; }
        public DateTime LastUpdateTime { get; set; }
    }
}
