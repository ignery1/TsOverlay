using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using TS6_SpeakerOverlay.Services;
using TS6_SpeakerOverlay.ViewModels;
using TS6_SpeakerOverlay.Views;

namespace TS6_SpeakerOverlay
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            var config = Services.ConfigService.Load();

            // First run: requests elevation (admin) once.
            // Necessary for the overlay to stay on top / allow click-through
            // even when the target game is running as administrator.
            if (!config.HasRequestedAdminOnce && !IsRunningAsAdmin())
            {
                if (TryRelaunchAsAdmin())
                {
                    config.HasRequestedAdminOnce = true;
                    Services.ConfigService.Save(config);
                    Current.Shutdown();
                    return;
                }
            }

            Helpers.LanguageHelper.SetLanguage(config.Language);
            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

            CrashHandler.Initialize();

            base.OnStartup(e);

            // Release notes for a recently applied update (if any).
            if (!string.IsNullOrWhiteSpace(config.PendingReleaseNotes))
            {
                string title = string.IsNullOrWhiteSpace(config.PendingUpdateVersion)
                    ? "Atualizado!"
                    : $"Atualizado para a versão {config.PendingUpdateVersion}";

                System.Windows.MessageBox.Show(
                    config.PendingReleaseNotes, title, MessageBoxButton.OK, MessageBoxImage.Information);

                config.PendingReleaseNotes = "";
                config.PendingUpdateVersion = "";
                Services.ConfigService.Save(config);
            }

            //  The loading screen is now visible throughout the entire startup process:
            // update checking and waiting for the TS6 connection - it only closes after this (or switches to the "downloading" screen if an update is available).
            var loading = new LoadingWindow();
            loading.Show();
            loading.SetStatus("Iniciando...");

            MainWindow mainWindow;
            try
            {
                mainWindow = new MainWindow();
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                loading.Close();
                CrashHandler.Handle(ex, "Startup", isFatal: true);
                return;
            }

            _ = RunStartupSequenceAsync(mainWindow, loading);
        }

        // Orchestrates the rest of the startup process: checks for updates, waits for TS6 to connect,
        // and only then launches the setup wizard (if it's the first run)—all while the
        // "loading" screen is visible and the text status is updated at each step.
        private async Task RunStartupSequenceAsync(MainWindow mainWindow, LoadingWindow loading)
        {
            loading.SetStatus("Verificando atualizações...");
            bool isShuttingDown = await CheckForUpdatesAsync(manual: false, loading: loading);
            if (isShuttingDown) return; // já vai fechar sozinho pra aplicar a atualização

            if (mainWindow.DataContext is MainViewModel vm)
            {
                loading.SetStatus("Conectando ao TeamSpeak 6...");

                const int timeoutMs = 6000;
                const int pollMs = 250;
                int waited = 0;
                while (!vm.IsConnected && waited < timeoutMs)
                {
                    await Task.Delay(pollMs);
                    waited += pollMs;
                }

                loading.Close();

                if (!vm.IsConnected)
                {
                    System.Windows.MessageBox.Show(
                        "Não foi possível conectar ao TeamSpeak 6.\n\n" +
                        "Verifique se:\n" +
                        "1) O TeamSpeak 6 está aberto;\n" +
                        "2) Em Configurações > Apps Remotos, a opção está HABILITADA;\n" +
                        "3) Ao reabrir o TsOverlay, autorize o pedido de conexão que aparecer no TS6.",
                        "TsOverlay - Conexão com o TeamSpeak 6",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }

                if (!vm.Config.HasCompletedFirstRun)
                {
                    var wizard = new Views.SetupWizardWindow(vm, mainWindow) { Owner = mainWindow };
                    wizard.ShowDialog();
                }
            }
            else
            {
                loading.Close();
            }
        }

        // Reused both in the startup (silent if no new updates are found)
        // and the "Check for Updates" item in the tray menu (manual=true,
        // notifies the user even if they are already up to date). "loading", if provided, is
        // closed as soon as an update is found - the download screen takes over
        // from there. Returns true if an update was downloaded and the app is already
        // shutting down (in this case the caller should stop what it was doing).
        public static async Task<bool> CheckForUpdatesAsync(bool manual, LoadingWindow? loading = null)
        {
            var manifest = await UpdateService.CheckForUpdateAsync();
            if (manifest == null)
            {
                if (manual)
                {
                    System.Windows.MessageBox.Show(
                        "Você já está na versão mais recente.",
                        "TsOverlay", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                return false;
            }

            var result = System.Windows.MessageBox.Show(
                $"Nova versão {manifest.Version} disponível. Deseja baixar e instalar agora?",
                "Atualização disponível",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return false;

            loading?.Close();

            var downloading = new Views.DownloadingWindow(manifest.Version);
            downloading.Show();

            var progress = new Progress<double>(pct => downloading.SetProgress(pct));
            bool applied = await UpdateService.DownloadAndApplyUpdateAsync(manifest, progress);

            downloading.Close();

            if (!applied)
            {
                System.Windows.MessageBox.Show(
                    "Não foi possível baixar a atualização. Tente novamente mais tarde.",
                    "TsOverlay", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            var cfg = Services.ConfigService.Load();
            cfg.PendingUpdateVersion = manifest.Version;
            cfg.PendingReleaseNotes = manifest.ReleaseNotes ?? "";
            cfg.HasCompletedFirstRun = false;
            Services.ConfigService.Save(cfg);

            System.Windows.Application.Current.Shutdown();
            return true;
        }

        private static bool IsRunningAsAdmin()
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static bool TryRelaunchAsAdmin()
        {
            try
            {
                string exePath = Process.GetCurrentProcess().MainModule!.FileName!;
                var psi = new ProcessStartInfo(exePath)
                {
                    UseShellExecute = true,
                    Verb = "runas"
                };
                Process.Start(psi);
                return true;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}