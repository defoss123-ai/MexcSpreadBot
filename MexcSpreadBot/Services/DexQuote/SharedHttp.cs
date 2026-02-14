namespace MexcSpreadBot.Services.DexQuoteProviders
{
    public static class SharedHttp
    {
        public static readonly HttpClient Client = new()
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
    }
}
