namespace MexcSpreadBot.Data
{
    public class PairData
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
        public DateTime MexUpdatedAtUtc { get; set; }
        public DateTime DexUpdatedAtUtc { get; set; }
        public long MexAgeMs => MexUpdatedAtUtc == default ? long.MaxValue : (long)(DateTime.UtcNow - MexUpdatedAtUtc).TotalMilliseconds;
        public long DexAgeMs => DexUpdatedAtUtc == default ? long.MaxValue : (long)(DateTime.UtcNow - DexUpdatedAtUtc).TotalMilliseconds;
        public long MexLatencyMs { get; set; }
        public long DexLatencyMs { get; set; }
        public DateTime LastUpdateTime { get; set; }
    }
}
