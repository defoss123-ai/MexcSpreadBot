namespace MexcSpreadBot.Data
{
    public sealed class QuotePair
    {
        public string Symbol { get; set; } = string.Empty;
        public ChainType Chain { get; set; } = ChainType.Evm;
        public string BaseTokenAddress { get; set; } = string.Empty;
        public string QuoteTokenAddress { get; set; } = string.Empty;
    }
}
