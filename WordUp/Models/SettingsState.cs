namespace WordUp.Models;

public sealed class SettingsState
{
    public double AudioVolume { get; set; } = 75;
    public bool DailyReminders { get; set; } = true;
    public bool AutoPlayAudio { get; set; }
    public bool OfflineMode { get; set; }
    public bool IsDarkMode { get; set; }
    public int PracticeSessionCount { get; set; }
}
