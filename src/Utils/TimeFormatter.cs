namespace UptimeKumaTrayAgent.Utils;

public static class TimeFormatter
{
    public static string FormatAge(DateTimeOffset? value)
    {
        if (value is null)
        {
            return "nie";
        }

        var age = DateTimeOffset.Now - value.Value;
        if (age.TotalSeconds < 1)
        {
            return "gerade eben";
        }

        if (age.TotalMinutes < 1)
        {
            return $"{Math.Max(1, (int)age.TotalSeconds)} Sekunden";
        }

        if (age.TotalHours < 1)
        {
            return $"{(int)age.TotalMinutes} Minuten";
        }

        if (age.TotalDays < 1)
        {
            return $"{(int)age.TotalHours} Stunden";
        }

        return $"{(int)age.TotalDays} Tage";
    }

    public static string FormatDate(DateTimeOffset? value)
    {
        return value?.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss") ?? "";
    }

    public static string FormatDuration(TimeSpan duration)
    {
        return duration.TotalDays >= 1
            ? $"{(int)duration.TotalDays}d {duration.Hours:00}:{duration.Minutes:00}:{duration.Seconds:00}"
            : $"{duration.Hours:00}:{duration.Minutes:00}:{duration.Seconds:00}";
    }
}
