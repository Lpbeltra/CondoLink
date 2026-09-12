using CondoLink.Api.Features.EmployeeDocuments;
using CondoLink.Domain.Enums;

namespace CondoLink.Tests;

public sealed class EmployeeDocumentSplitterTests
{
    private static readonly EmployeeDocumentSplitter.Candidate JoaoSilva = new(Guid.NewGuid(), "João da Silva", "MAT-001", "Porteiro");
    private static readonly EmployeeDocumentSplitter.Candidate MariaSouza = new(Guid.NewGuid(), "Maria Aparecida Souza", "MAT-002", "Zeladora");
    private static readonly EmployeeDocumentSplitter.Candidate CarlosAlberto = new(Guid.NewGuid(), "Carlos Alberto Souza", null, "Síndico Profissional");

    [Fact]
    public void Matches_by_registration_number_with_high_confidence()
    {
        var identity = EmployeeDocumentSplitter.DetectPageIdentity(
            "RECIBO DE PAGAMENTO\nMatrícula: MAT-001\nFuncionário: J. SILVA", [JoaoSilva, MariaSouza]);
        Assert.Equal(JoaoSilva.EmployeeId, identity.EmployeeId);
        Assert.Equal(EmployeeDocumentIdentificationConfidence.High, identity.Confidence);
        Assert.Equal(EmployeeDocumentIdentificationMethod.RegistrationNumber, identity.Method);
    }

    [Fact]
    public void Matches_by_exact_full_name_with_high_confidence()
    {
        var identity = EmployeeDocumentSplitter.DetectPageIdentity(
            "Nome do funcionário: MARIA APARECIDA SOUZA\nCargo: Zeladora", [JoaoSilva, MariaSouza]);
        Assert.Equal(MariaSouza.EmployeeId, identity.EmployeeId);
        Assert.Equal(EmployeeDocumentIdentificationConfidence.High, identity.Confidence);
        Assert.Equal(EmployeeDocumentIdentificationMethod.ExactName, identity.Method);
    }

    [Fact]
    public void Registration_number_is_preferred_over_a_coincidental_name_match()
    {
        // Both employees' names could plausibly appear; the registration number is decisive.
        var identity = EmployeeDocumentSplitter.DetectPageIdentity(
            "Matrícula: MAT-002\nJoão da Silva assinou como testemunha.\nMaria Aparecida Souza", [JoaoSilva, MariaSouza]);
        Assert.Equal(MariaSouza.EmployeeId, identity.EmployeeId);
        Assert.Equal(EmployeeDocumentIdentificationMethod.RegistrationNumber, identity.Method);
    }

    [Fact]
    public void Partial_name_overlap_is_a_low_confidence_fuzzy_suggestion_not_an_exact_match()
    {
        // "Carlos Souza" isn't the exact registered name "Carlos Alberto Souza",
        // but two of its three tokens do appear — a fuzzy, human-reviewable suggestion.
        var identity = EmployeeDocumentSplitter.DetectPageIdentity("Sr. Carlos Souza, holerite anexo.", [CarlosAlberto]);
        Assert.Equal(CarlosAlberto.EmployeeId, identity.EmployeeId);
        Assert.Equal(EmployeeDocumentIdentificationConfidence.Low, identity.Confidence);
        Assert.Equal(EmployeeDocumentIdentificationMethod.FuzzyName, identity.Method);
    }

    [Fact]
    public void No_signal_returns_unidentified()
    {
        var identity = EmployeeDocumentSplitter.DetectPageIdentity("Documento genérico sem nenhum identificador.", [JoaoSilva, MariaSouza]);
        Assert.Null(identity.EmployeeId);
        Assert.Equal(EmployeeDocumentIdentificationConfidence.None, identity.Confidence);
    }

    [Fact]
    public void Phone_number_text_is_never_used_as_a_matching_signal()
    {
        // Regression guard: a phone number appearing in page text must never
        // resolve identity — only registration number / name signals may.
        var candidate = new EmployeeDocumentSplitter.Candidate(Guid.NewGuid(), "Ana Paula Ferreira", "MAT-777", null);
        var identity = EmployeeDocumentSplitter.DetectPageIdentity("Telefone: +5511999998888\nDocumento sem outro identificador.", [candidate]);
        Assert.Null(identity.EmployeeId);
    }

    [Fact]
    public void One_page_per_employee_produces_one_segment_each()
    {
        var pages = new (int, string)[]
        {
            (1, "Matrícula: MAT-001 João da Silva"),
            (2, "Matrícula: MAT-002 Maria Aparecida Souza"),
        };
        var segments = EmployeeDocumentSplitter.Segment(pages, [JoaoSilva, MariaSouza]);
        Assert.Equal(2, segments.Count);
        Assert.Equal((1, 1, JoaoSilva.EmployeeId), (segments[0].PageStart, segments[0].PageEnd, segments[0].EmployeeId));
        Assert.Equal((2, 2, MariaSouza.EmployeeId), (segments[1].PageStart, segments[1].PageEnd, segments[1].EmployeeId));
    }

    [Fact]
    public void Continuation_pages_without_their_own_signal_extend_the_current_document()
    {
        var pages = new (int, string)[]
        {
            (1, "Matrícula: MAT-001 João da Silva - Página 1 de 2"),
            (2, "continuação do holerite, sem identificação própria nesta página"),
            (3, "Matrícula: MAT-002 Maria Aparecida Souza"),
        };
        var segments = EmployeeDocumentSplitter.Segment(pages, [JoaoSilva, MariaSouza]);
        Assert.Equal(2, segments.Count);
        Assert.Equal((1, 2, JoaoSilva.EmployeeId), (segments[0].PageStart, segments[0].PageEnd, segments[0].EmployeeId));
        Assert.Equal((3, 3, MariaSouza.EmployeeId), (segments[1].PageStart, segments[1].PageEnd, segments[1].EmployeeId));
    }

    [Fact]
    public void Unidentified_leading_page_becomes_its_own_segment_instead_of_being_dropped()
    {
        var pages = new (int, string)[] { (1, "página sem nenhum identificador") };
        var segments = EmployeeDocumentSplitter.Segment(pages, [JoaoSilva]);
        Assert.Single(segments);
        Assert.Null(segments[0].EmployeeId);
        Assert.Equal(EmployeeDocumentIdentificationConfidence.None, segments[0].Confidence);
    }
}
