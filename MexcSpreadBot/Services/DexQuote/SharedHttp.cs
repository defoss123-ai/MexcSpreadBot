namespace MexcSpreadBot.Services.DexQuote
{
    public static class SharedHttp
    {
        public static readonly HttpClient Client = new()
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
    }
}
