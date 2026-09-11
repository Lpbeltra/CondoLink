namespace CondoLink.Domain.Enums;

/// <summary>Stable module identifiers used only for contextual SubManager access.</summary>
public enum SubManagerModule
{
    // Legacy persisted value. Operational request access belongs to Attendance.
    Requests = 1,
    Attendance = 2,
    ManagementCompany = 3,
    Agenda = 4,
    Assistant = 5,
    Documents = 6,
    Management = 7,
    // Denied by default when backfilled: unlike the other modules, this one must be
    // explicitly granted by a Manager rather than inherited automatically.
    EmployeeManagement = 8
}
