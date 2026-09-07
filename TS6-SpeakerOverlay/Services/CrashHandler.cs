using System;
using System.IO;
using System.Text;

namespace TS6_SpeakerOverlay.Services
{
    public static class CrashHandler
    {
        private static readonly string AppDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TS6-SpeakerOverlay");
        private static readonly string LogFolder = Path.Combine(AppDataFolder, "crash-logs");

        private static bool _initialized = false;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                Handle(e.ExceptionObject as Exception, "AppDomain (fatal)", isFatal: true);

            if (System.Windows.Application.Current != null)
            {
                System.Windows.Application.Current.DispatcherUnhandledException += (s, e) =>
                {
                    Handle(e.Exception, "UI (Dispatcher)", isFatal: false);
                    e.Handled = true;
                };
            }

            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                Handle(e.Exception, "Task em segundo plano", isFatal: false);
                e.SetObserved();
            };
        }

        public static void Handle(Exception? ex, string source, bool isFatal)
        {
            if (ex == null) return;

            string report = BuildReport(ex, source);
            string logPath = SaveToDisk(report);

            try
            {
                var dispatcher = System.Windows.Application.Current?.Dispatcher;
                Action showWindow = () =>
                {
                    var window = new Views.CrashWindow(report, logPath, isFatal);
                    window.ShowDialog();
                };

                if (dispatcher != null && !dispatcher.CheckAccess())
                    dispatcher.Invoke(showWindow);
                else
                    showWindow();
            }
            catch
            {
            }

            if (isFatal)
            {
                Environment.Exit(1);
            }
        }

        private static string BuildReport(Exception ex, string source)
        {
            var sb = new StringBuilder();
            sb.AppendLine("TsOverlay - Relatório de erro");
            sb.AppendLine($"Data: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Origem: {source}");
            sb.AppendLine($"Versão: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}");
            sb.AppendLine();
            sb.AppendLine(ex.ToString());
            return sb.ToString();
        }

        private static string SaveToDisk(string report)
        {
            try
            {
                Directory.CreateDirectory(LogFolder);
                string path = Path.Combine(LogFolder, $"crash_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                File.WriteAllText(path, report);
                return path;
            }
            catch
            {
                return "";
            }
        }
    }
}