namespace TS6_SpeakerOverlay.Models
{
    // Represents a detected running process for the "target app" filter.
    // DisplayName is what appears to the user (window title, easier to recognize,
    // e.g., "AikaClient"); ExeName is the actual value used for comparison (e.g., "aika.exe"),
    // which is the same format saved in Config.TargetProcessName.
    public class RunningProcessOption
    {
        public string DisplayName { get; set; } = string.Empty;
        public string ExeName { get; set; } = string.Empty;

        // [New] Window title (e.g.), used as an additional detection signal
        // for anti-cheat-protected processes, where the .exe name might vary
        // but the window title usually remains stable.
        public string WindowTitle { get; set; } = string.Empty;

        public override string ToString() => DisplayName;
    }
}