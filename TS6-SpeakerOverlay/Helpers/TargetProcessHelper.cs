using System;
using System.Linq;
using TS6_SpeakerOverlay.Models;

namespace TS6_SpeakerOverlay.Helpers
{
    // [Added] Support for multiple process names for the same "target app," separated by
    // commas in Config.TargetProcessName (e.g.)
    public static class TargetProcessHelper
    {
        private static string[] Split(string? raw) =>
            (raw ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                       .Select(s => s.Trim())
                       .Where(s => s.Length > 0)
                       .ToArray();

        /// <summary>
        /// Adds a process name to the configured list (without duplicating),
        /// preserving existing names.
        /// </summary>
        public static void Add(AppConfig config, string exeName)
        {
            var names = Split(config.TargetProcessName).ToList();
            if (!names.Any(n => n.Equals(exeName, StringComparison.OrdinalIgnoreCase)))
            {
                names.Add(exeName);
            }
            config.TargetProcessName = string.Join(", ", names);
        }

        /// <summary>
        /// Adds a window title to the configured list (without duplicates).
        /// Additional detection signal: some processes (e.g., those protected by anti-cheat)
        /// block most Process properties, so relying solely on the .exe name
        /// is unreliable—the window title is usually much more stable.
        /// </summary>
        public static void AddTitle(AppConfig config, string windowTitle)
        {
            if (string.IsNullOrWhiteSpace(windowTitle)) return;

            var titles = Split(config.TargetWindowTitle).ToList();
            if (!titles.Any(t => t.Equals(windowTitle, StringComparison.OrdinalIgnoreCase)))
            {
                titles.Add(windowTitle);
            }
            config.TargetWindowTitle = string.Join(", ", titles);
        }

        /// <summary>
        /// Checks if the foreground process matches ANY of the configured
        /// process names OR window titles (name comparison ignores
        /// the ".exe" extension). Matching just one of the two criteria is sufficient.
        /// </summary>
        public static bool Matches(AppConfig config, string? foregroundProcessName, string? foregroundWindowTitle = null)
        {
            if (!string.IsNullOrWhiteSpace(foregroundProcessName))
            {
                foreach (var raw in Split(config.TargetProcessName))
                {
                    string name = raw.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                        ? raw[..^4]
                        : raw;

                    if (foregroundProcessName.Equals(name, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            // Second criterion: window title (pure Win32, more resistant to anti-cheat)
            if (!string.IsNullOrWhiteSpace(foregroundWindowTitle))
            {
                foreach (var title in Split(config.TargetWindowTitle))
                {
                    if (foregroundWindowTitle.Equals(title, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }
    }
}