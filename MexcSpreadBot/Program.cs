using MexcSpreadBot.Helpers;

namespace MexcSpreadBot
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Directory.CreateDirectory("data");

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, ex) => FileLog.Error("UI thread exception", ex.Exception);
            AppDomain.CurrentDomain.UnhandledException += (_, ex) => FileLog.Error("Unhandled exception", ex.ExceptionObject as Exception);
            TaskScheduler.UnobservedTaskException += (_, ex) =>
            {
                FileLog.Error("Unobserved task exception", ex.Exception);
                ex.SetObserved();
            };

            ApplicationConfiguration.Initialize();
            Application.Run(new FormMain());
        }

        public static void WriteToLog(string userMsg, Exception ex)
        {
            FileLog.Error(userMsg, ex);
        }
    }
}
