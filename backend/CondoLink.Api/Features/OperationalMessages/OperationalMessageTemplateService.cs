using System.Text.RegularExpressions;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.OperationalMessages;

public sealed record OperationalMessageDefinition(
    string Key, string Title, string Description, string Prefix, string Suffix,
    string StructuralSuffix, WhatsAppNotificationType NotificationType);

public sealed partial class OperationalMessageTemplateService(AppDbContext db)
{
    public const int PartMaximumLength = 1200;
    public const int AdministrativeContentMaximumLength = 1000;
    public const int OutboundMaximumLength = 4000;

    private const string NewInteraction =
        "Se precisar de mais informações ou quiser iniciar outro atendimento, é só enviar \"Oi\".";

    public static IReadOnlyList<OperationalMessageDefinition> Definitions { get; } =
    [
        new("WaitingForThirdParty", "Aguardando terceiro",
            "Mensagem enviada quando o atendimento depende de uma pessoa ou serviço externo.",
            "Olá, {PrimeiroNome}! Há uma atualização sobre sua solicitação.\n\nA administração informou que o atendimento está aguardando um terceiro:",
            "Você será avisado quando houver uma nova atualização.\n\n" + NewInteraction, "",
            WhatsAppNotificationType.StatusChanged),
        new("WaitingForResident", "Aguardando morador",
            "Mensagem enviada quando a administração precisa de uma resposta do morador.",
            "Olá, {PrimeiroNome}! Precisamos de uma informação sua para continuar o atendimento.",
            "", "Responda por aqui para continuar.", WhatsAppNotificationType.InformationRequested),
        new("WaitingForResidentClosure", "Conclusão aguardando confirmação",
            "Mensagem enviada quando a administração considera o atendimento concluído e aguarda confirmação.",
            "Olá, {PrimeiroNome}! A administração informou que sua solicitação foi concluída:",
            "", "Está tudo certo?\n\n1 - Sim, finalizar atendimento\n2 - Ainda tenho uma dúvida",
            WhatsAppNotificationType.StatusChanged),
        new("Resolved", "Resolvida", "Mensagem enviada quando o atendimento é finalizado.",
            "Olá, {PrimeiroNome}! Sua solicitação foi finalizada pela administração.",
            NewInteraction, "", WhatsAppNotificationType.RequestResolved),
        new("Cancelled", "Cancelada", "Mensagem enviada quando o atendimento é cancelado.",
            "Olá, {PrimeiroNome}! Sua solicitação foi cancelada pela administração.",
            NewInteraction, "", WhatsAppNotificationType.RequestCancelled),
        new("Reopened", "Reaberta", "Mensagem enviada quando um atendimento encerrado é reaberto.",
            "Olá, {PrimeiroNome}! Sua solicitação foi reaberta e voltou a ser acompanhada pela administração.",
            NewInteraction, "", WhatsAppNotificationType.RequestReopened),
    ];

    public static OperationalMessageDefinition? Definition(string key) =>
        Definitions.FirstOrDefault(x => x.Key.Equals(key, StringComparison.Ordinal));

    public async Task<(string Prefix, string Suffix, bool IsOverride)> EffectiveAsync(
        string key, CancellationToken ct)
    {
        var definition = Definition(key) ?? throw new ArgumentException("Gatilho inválido.", nameof(key));
        var configured = await db.OperationalMessageTemplates.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Key == key, ct);
        return configured is null
            ? (definition.Prefix, definition.Suffix, false)
            : (configured.Prefix, configured.Suffix, true);
    }

    public async Task<string> ComposeAsync(string key, string firstName,
        string condominiumName, string administrativeContent, CancellationToken ct)
    {
        var definition = Definition(key) ?? throw new ArgumentException("Gatilho inválido.", nameof(key));
        var effective = await EffectiveAsync(key, ct);
        var prefix = Replace(effective.Prefix, firstName, condominiumName);
        var suffix = Replace(effective.Suffix, firstName, condominiumName);
        var parts = new[] { prefix, administrativeContent, suffix, definition.StructuralSuffix }
            .Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim());
        var result = string.Join("\n\n", parts);
        if (result.Length > OutboundMaximumLength)
            throw new InvalidOperationException($"A mensagem final excede {OutboundMaximumLength} caracteres.");
        return result;
    }

    public static string? Validate(string prefix, string suffix)
    {
        if (prefix.Length > PartMaximumLength || suffix.Length > PartMaximumLength)
            return $"Cada parte deve ter no máximo {PartMaximumLength} caracteres.";
        if (prefix.Contains("{MensagemDoSindico}", StringComparison.Ordinal)
            || suffix.Contains("{MensagemDoSindico}", StringComparison.Ordinal))
            return "{MensagemDoSindico} é estrutural e não pode ser inserido nos campos editáveis.";
        foreach (Match token in PlaceholderRegex().Matches(prefix + suffix))
            if (token.Value is not "{PrimeiroNome}" and not "{NomeCondominio}")
                return $"Placeholder não permitido: {token.Value}.";
        var maximum = prefix.Length + suffix.Length + AdministrativeContentMaximumLength + 300;
        return maximum > OutboundMaximumLength
            ? $"A composição pode exceder {OutboundMaximumLength} caracteres." : null;
    }

    private static string Replace(string value, string firstName, string condominiumName) =>
        value.Replace("{PrimeiroNome}", firstName, StringComparison.Ordinal)
            .Replace("{NomeCondominio}", condominiumName, StringComparison.Ordinal);

    [GeneratedRegex(@"\{[^{}]+\}")]
    private static partial Regex PlaceholderRegex();
}
