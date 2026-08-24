using CondoLink.Domain.Enums;

namespace CondoLink.Api.Features.Agenda;

public static class AgendaRecurrence
{
    public static DateTime? Next(DateTime scheduledUtc, AgendaRecurrenceType type,
        int originalDay, string timeZoneId)
    {
        if (type == AgendaRecurrenceType.None) return null;
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var local = TimeZoneInfo.ConvertTimeFromUtc(scheduledUtc, zone);
        DateTime nextLocal;
        if (type == AgendaRecurrenceType.Weekly) nextLocal = local.AddDays(7);
        else
        {
            var month = local.Month == 12 ? 1 : local.Month + 1;
            var year = local.Month == 12 ? local.Year + 1 : local.Year;
            var day = Math.Min(originalDay, DateTime.DaysInMonth(year, month));
            nextLocal = new DateTime(year, month, day, local.Hour, local.Minute,
                local.Second, DateTimeKind.Unspecified);
        }
        return TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(nextLocal, DateTimeKind.Unspecified), zone);
    }
}
