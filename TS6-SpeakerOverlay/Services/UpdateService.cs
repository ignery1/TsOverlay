using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace TS6_SpeakerOverlay.Services
{
    // [新增] Auto-update
    public static class UpdateService
    {
        private const string MANIFEST_URL = "https://yourdomain.com/ts-overlay/version.json";
                
        public class UpdateManifest
        {
            public string Version { get; set; } = "";
            public string Url { get; set; } = "";
            public string ReleaseNotes { get; set; } = "";
        }

        /// <summary>
        /// Checks only if a newer version exists in the manifest, without downloading anything.
        /// Returns the manifest (with the new version) if an update is available,
        /// or null if already up to date, if there is no internet connection, or in the event of an error.
        /// Use this to prompt the user before calling DownloadAndApplyUpdateAsync.
        /// </summary>
        public static async Task<UpdateManifest?> CheckForUpdateAsync()
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };

                string json = await http.GetStringAsync(MANIFEST_URL);
                var manifest = JsonSerializer.Deserialize<UpdateManifest>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (manifest == null || string.IsNullOrWhiteSpace(manifest.Version) || string.IsNullOrWhiteSpace(manifest.Url))
                    return null;

                if (!Version.TryParse(manifest.Version, out var remoteVersion))
                    return null;

                var localVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 0);

                if (remoteVersion <= localVersion)
                    return null;

                return manifest;
            }
            catch
            {
                return null;
            }
        }

        public static async Task<bool> DownloadAndApplyUpdateAsync(UpdateManifest manifest, IProgress<double>? progress = null)
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };

                string currentExePath = Process.GetCurrentProcess().MainModule!.FileName!;
                string tempDir = Path.Combine(Path.GetTempPath(), "TsOverlay-Update");
                Directory.CreateDirectory(tempDir);
                string newExePath = Path.Combine(tempDir, "update.exe");

                using (var response = await http.GetAsync(manifest.Url, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    long? totalBytes = response.Content.Headers.ContentLength;

                    await using var httpStream = await response.Content.ReadAsStreamAsync();
                    await using var fileStream = File.Create(newExePath);

                    var buffer = new byte[81920];
                    long totalRead = 0;
                    int bytesRead;
                    while ((bytesRead = await httpStream.ReadAsync(buffer)) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                        totalRead += bytesRead;
                        if (totalBytes is > 0)
                            progress?.Report(totalRead * 100.0 / totalBytes.Value);
                    }
                }

                // The current .exe is running and cannot replace itself.
                // A .bat file waits for the process to close, copies the new .exe over the old one,
                // reopens the app, and deletes itself.
                string batPath = Path.Combine(tempDir, "apply_update.bat");
                string batContent =
                    "@echo off\r\n" +
                    "timeout /t 2 /nobreak > NUL\r\n" +
                    ":retry\r\n" +
                    $"copy /y \"{newExePath}\" \"{currentExePath}\" > NUL 2> NUL\r\n" +
                    "if errorlevel 1 (\r\n" +
                    "  timeout /t 1 /nobreak > NUL\r\n" +
                    "  goto retry\r\n" +
                    ")\r\n" +
                    $"start \"\" \"{currentExePath}\"\r\n" +
                    "del \"%~f0\"\r\n";

                File.WriteAllText(batPath, batContent);

                var psi = new ProcessStartInfo("cmd.exe", $"/c \"{batPath}\"")
                {
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                };
                Process.Start(psi);

                return true;
            }
            catch
            {
                // No internet, manifest offline, 404 bucket, etc.: silent failure,
                // the app continues to function normally in the current version.
                return false;
            }
        }
    }
}