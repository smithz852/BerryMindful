namespace BerryMindful.Services.NotificationServices;

public class NotificationOptions
{
    /// <summary>Local hour of day (0–23) the daily digest runs at.</summary>
    public int DailyHourLocal { get; set; } = 8;

    /// <summary>Run a digest pass immediately on startup — useful for dev testing.</summary>
    public bool RunOnStartup { get; set; }

    /// <summary>Client base URL used for links in digest emails.</summary>
    public string AppBaseUrl { get; set; } = "http://localhost:5173";
}
