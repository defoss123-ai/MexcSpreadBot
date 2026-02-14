using System.Text;

namespace MexcSpreadBot.Helpers
{
    public static class FileLog
    {
        private static readonly object Sync = new();
        private static readonly string LogPath = Path.Combine(AppContext.BaseDirectory, "logs.txt");

        public static void Info(string message) => Write("INFO", message, null);
        public static void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);

        private static void Write(string level, string message, Exception? ex)
        {
            var sb = new StringBuilder()
                .Append(DateTime.UtcNow.ToString("O"))
                .Append(" [")
                .Append(level)
                .Append("] ")
                .Append(message);

            if (ex != null)
            {
                sb.Append(" | ").Append(ex.GetType().Name).Append(": ").Append(ex.Message).AppendLine();
                sb.Append(ex.StackTrace);
            }

            lock (Sync)
            {
                File.AppendAllText(LogPath, sb.ToString() + Environment.NewLine);
            }
        }
    }
}
