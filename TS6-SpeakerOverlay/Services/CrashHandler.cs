using System;
using System.IO;
using System.Text;

namespace TS6_SpeakerOverlay.Services
{
    // [新增] Captura exceções não tratadas em qualquer lugar do app (thread de UI,
    // threads de fundo, tasks) e mostra uma tela amigável em vez do processo
    // simplesmente sumir sem explicação — que é o que acontecia ao publicar,
    // já que sendo WinExe não existe console pra mostrar o erro.
    public static class CrashHandler
    {
        private static readonly string AppDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TS6-SpeakerOverlay");
        private static readonly string LogFolder = Path.Combine(AppDataFolder, "crash-logs");

        private static bool _initialized = false;

        /// <summary>
        /// Chame isso o MAIS CEDO possível — idealmente na primeira linha do
        /// construtor da App, antes de qualquer outra coisa — pra capturar até
        /// erros que acontecem durante a inicialização.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            // Qualquer thread, exceção não observada -> normalmente fatal.
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                Handle(e.ExceptionObject as Exception, "AppDomain (fatal)", isFatal: true);

            // Exceção na thread de UI (o caso mais comum) -> conseguimos evitar
            // que o processo morra, então NÃO é fatal por padrão.
            if (System.Windows.Application.Current != null)
            {
                System.Windows.Application.Current.DispatcherUnhandledException += (s, e) =>
                {
                    Handle(e.Exception, "UI (Dispatcher)", isFatal: false);
                    e.Handled = true;
                };
            }

            // Task em segundo plano cuja exceção ninguém "observou" (await/try-catch).
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
                // Garante que a janela de erro sempre abra na thread de UI.
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
                // Se nem a janela de erro conseguir abrir, não há mais nada a fazer
                // além de garantir que o processo encerre (se for fatal).
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