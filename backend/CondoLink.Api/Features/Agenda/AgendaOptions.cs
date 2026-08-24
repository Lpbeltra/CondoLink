namespace CondoLink.Api.Features.Agenda;

public sealed class AgendaOptions
{
    public const string SectionName = "Agenda";
    public string OperationalTimeZone { get; set; } = "America/Sao_Paulo";
    public int WorkerIntervalSeconds { get; set; } = 60;
    public int WorkerBatchSize { get; set; } = 20;
}
