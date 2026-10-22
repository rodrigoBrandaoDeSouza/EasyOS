namespace EasyOS.Infrastructure;

/// <summary>
/// Relógio da aplicação. <see cref="Now"/> devolve a hora no fuso em que o legado gravava
/// created_date/last_update (configurável; o App Service do legado roda em UTC por padrão).
/// </summary>
public interface IClock
{
    DateTime UtcNow { get; }
    DateTime Now { get; }
}

public sealed class SystemClock(TimeZoneInfo storageTimeZone) : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;

    public DateTime Now => DateTime.SpecifyKind(
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, storageTimeZone), DateTimeKind.Unspecified);
}
