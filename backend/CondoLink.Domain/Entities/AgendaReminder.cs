using CondoLink.Domain.Enums;

namespace CondoLink.Domain.Entities;

public sealed class AgendaReminder
{
    private AgendaReminder() { }

    public AgendaReminder(Guid condominiumId, Guid createdByUserId, string title,
        string? description, Guid? unitId, string? relatedThirdParty,
        DateTime startsAtUtc, string timeZoneId, AgendaRecurrenceType recurrence,
        bool notifyByWhatsApp, bool notifyByEmail, DateTime now)
    {
        Id = Guid.NewGuid(); CondominiumId = condominiumId;
        CreatedByUserId = createdByUserId;
        Update(title, description, unitId, relatedThirdParty, startsAtUtc,
            timeZoneId, recurrence, notifyByWhatsApp, notifyByEmail, now);
        CreatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid CondominiumId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public string Title { get; private set; } = null!;
    public string? Description { get; private set; }
    public Guid? UnitId { get; private set; }
    public string? RelatedThirdParty { get; private set; }
    public DateTime StartsAtUtc { get; private set; }
    public DateTime? NextOccurrenceAtUtc { get; private set; }
    public string TimeZoneId { get; private set; } = null!;
    public AgendaRecurrenceType RecurrenceType { get; private set; }
    public int RecurrenceDayOfMonth { get; private set; }
    public bool NotifyByWhatsApp { get; private set; }
    public bool NotifyByEmail { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public void Update(string title, string? description, Guid? unitId,
        string? relatedThirdParty, DateTime startsAtUtc, string timeZoneId,
        AgendaRecurrenceType recurrence, bool notifyByWhatsApp,
        bool notifyByEmail, DateTime now)
    {
        title = title.Trim();
        if (title.Length is < 1 or > 160) throw new ArgumentException("Invalid title.");
        description = Clean(description, 1000, nameof(description));
        relatedThirdParty = Clean(relatedThirdParty, 200, nameof(relatedThirdParty));
        if (startsAtUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC required.");
        var preserveCompletion = CreatedAt != default && CompletedAt.HasValue && !IsActive;
        Title = title; Description = description; UnitId = unitId;
        RelatedThirdParty = relatedThirdParty; StartsAtUtc = startsAtUtc;
        NextOccurrenceAtUtc = startsAtUtc; TimeZoneId = timeZoneId;
        RecurrenceType = recurrence;
        RecurrenceDayOfMonth = TimeZoneInfo.ConvertTimeFromUtc(startsAtUtc,
            TimeZoneInfo.FindSystemTimeZoneById(timeZoneId)).Day;
        NotifyByWhatsApp = notifyByWhatsApp; NotifyByEmail = notifyByEmail;
        IsActive = !preserveCompletion;
        CompletedAt = preserveCompletion ? CompletedAt : null;
        if (preserveCompletion) NextOccurrenceAtUtc = null;
        UpdatedAt = now;
    }

    public void Complete(DateTime now)
    {
        if (!IsActive) return;
        IsActive = false; NextOccurrenceAtUtc = null;
        CompletedAt = now; UpdatedAt = now;
    }

    public void Reactivate(DateTime nextOccurrenceUtc, DateTime now)
    {
        if (IsActive) return;
        if (nextOccurrenceUtc <= now) throw new ArgumentException("Future occurrence required.");
        IsActive = true; NextOccurrenceAtUtc = nextOccurrenceUtc;
        CompletedAt = null; UpdatedAt = now;
    }

    public void Advance(DateTime scheduledForUtc, DateTime? nextUtc, DateTime now)
    {
        if (NextOccurrenceAtUtc != scheduledForUtc) return;
        NextOccurrenceAtUtc = nextUtc;
        // Consuming an occurrence never completes the reminder.
        IsActive = true; CompletedAt = null; UpdatedAt = now;
    }

    private static string? Clean(string? value, int max, string name)
    {
        value = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (value?.Length > max) throw new ArgumentException($"{name} is too long.");
        return value;
    }
}
