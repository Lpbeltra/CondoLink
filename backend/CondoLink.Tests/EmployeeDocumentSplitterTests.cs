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

    // --- CPF / CNPJ hard gates -------------------------------------------------

    [Fact]
    public void Cpf_match_is_blocked_when_the_documents_cnpj_belongs_to_a_different_condominium()
    {
        // Regression: Renato used to work at Mendonza; his record now points at
        // Monticello, but this PDF is an OLD payslip that still carries Mendonza's
        // CNPJ. Same person (CPF matches), wrong condominium — must NOT associate.
        var renato = new EmployeeDocumentSplitter.Candidate(Guid.NewGuid(), "Renato Paranhos de Araujo", "MAT-900", "Zelador",
            "52998224725", "11222333000181"); // Renato's CURRENT condominium (Monticello) CNPJ
        var identity = EmployeeDocumentSplitter.DetectPageIdentity(
            "CONDOMINIO MENDONZA\nCNPJ: 99.888.777/0001-62\nFuncionario: Renato Paranhos de Araujo\nCPF: 529.982.247-25",
            [renato]);
        Assert.Null(identity.EmployeeId);
        Assert.Equal(EmployeeDocumentIdentificationConfidence.None, identity.Confidence);
        Assert.Equal("52998224725", identity.ExtractedCpfDigits);
        Assert.Equal("99888777000162", identity.ExtractedCnpjDigits);
    }

    [Fact]
    public void Cpf_and_matching_cnpj_and_compatible_name_identifies_with_high_confidence()
    {
        var renato = new EmployeeDocumentSplitter.Candidate(Guid.NewGuid(), "Renato Paranhos de Araujo", null, null,
            "52998224725", "11222333000181");
        var identity = EmployeeDocumentSplitter.DetectPageIdentity(
            "CONDOMINIO MONTICELLO\nCNPJ: 11.222.333/0001-81\nFuncionario: Renato Paranhos de Araujo\nCPF: 529.982.247-25",
            [renato]);
        Assert.Equal(renato.EmployeeId, identity.EmployeeId);
        Assert.Equal(EmployeeDocumentIdentificationConfidence.High, identity.Confidence);
        Assert.Equal(EmployeeDocumentIdentificationMethod.Cpf, identity.Method);
    }

    [Fact]
    public void Cpf_and_cnpj_match_with_an_abbreviated_registered_name_still_identifies_with_high_confidence()
    {
        var candidate = new EmployeeDocumentSplitter.Candidate(Guid.NewGuid(), "João A. Teixeira", null, null,
            "52998224725", "11222333000181");
        var identity = EmployeeDocumentSplitter.DetectPageIdentity(
            "CNPJ: 11.222.333/0001-81\nFuncionario: Joao Almeida Teixeira\nCPF: 529.982.247-25", [candidate]);
        Assert.Equal(candidate.EmployeeId, identity.EmployeeId);
        Assert.Equal(EmployeeDocumentIdentificationConfidence.High, identity.Confidence);
    }

    [Fact]
    public void Cpf_and_cnpj_correct_but_completely_different_name_downgrades_to_medium_confidence_for_review()
    {
        var candidate = new EmployeeDocumentSplitter.Candidate(Guid.NewGuid(), "Renato Paranhos de Araujo", null, null,
            "52998224725", "11222333000181");
        var identity = EmployeeDocumentSplitter.DetectPageIdentity(
            "CNPJ: 11.222.333/0001-81\nFuncionario: Carlos Eduardo Mendes\nCPF: 529.982.247-25", [candidate]);
        Assert.Equal(candidate.EmployeeId, identity.EmployeeId);
        Assert.Equal(EmployeeDocumentIdentificationConfidence.Medium, identity.Confidence);
    }

    [Fact]
    public void Registration_number_match_is_overridden_by_a_present_but_non_matching_cpf()
    {
        var candidate = new EmployeeDocumentSplitter.Candidate(Guid.NewGuid(), "Paula Nogueira", "MAT-050", null, "52998224725");
        var identity = EmployeeDocumentSplitter.DetectPageIdentity(
            "Matricula: MAT-050\nFuncionaria: Paula Nogueira\nCPF: 111.444.777-35", [candidate]);
        Assert.Null(identity.EmployeeId);
    }

    [Fact]
    public void Registration_number_match_is_blocked_when_the_documents_cnpj_belongs_to_a_different_condominium()
    {
        var candidate = new EmployeeDocumentSplitter.Candidate(Guid.NewGuid(), "Paula Nogueira", "MAT-050", null, null, "11222333000181");
        var identity = EmployeeDocumentSplitter.DetectPageIdentity(
            "CNPJ: 99.888.777/0001-62\nMatricula: MAT-050\nFuncionaria: Paula Nogueira", [candidate]);
        Assert.Null(identity.EmployeeId);
        Assert.Equal("99888777000162", identity.ExtractedCnpjDigits);
    }

    [Fact]
    public void Registration_number_match_succeeds_when_the_documents_cnpj_matches_the_same_condominium()
    {
        var candidate = new EmployeeDocumentSplitter.Candidate(Guid.NewGuid(), "Paula Nogueira", "MAT-050", null, null, "11222333000181");
        var identity = EmployeeDocumentSplitter.DetectPageIdentity(
            "CNPJ: 11.222.333/0001-81\nMatricula: MAT-050\nFuncionaria: Paula Nogueira", [candidate]);
        Assert.Equal(candidate.EmployeeId, identity.EmployeeId);
        Assert.Equal(EmployeeDocumentIdentificationMethod.RegistrationNumber, identity.Method);
    }

    [Fact]
    public void Same_name_in_two_condominiums_is_disambiguated_by_the_documents_cnpj()
    {
        var atMonticello = new EmployeeDocumentSplitter.Candidate(Guid.NewGuid(), "Carlos Eduardo Lima", null, null, null, "11222333000181");
        var atMendonza = new EmployeeDocumentSplitter.Candidate(Guid.NewGuid(), "Carlos Eduardo Lima", null, null, null, "99888777000162");
        var identity = EmployeeDocumentSplitter.DetectPageIdentity(
            "CNPJ: 99.888.777/0001-62\nFuncionario: Carlos Eduardo Lima", [atMonticello, atMendonza]);
        Assert.Equal(atMendonza.EmployeeId, identity.EmployeeId);
        Assert.Equal(EmployeeDocumentIdentificationMethod.ExactName, identity.Method);
    }

    [Fact]
    public void Cpf_and_cnpj_digit_extraction_do_not_collide_with_each_other()
    {
        // Without a digit-boundary check, a 14-digit CNPJ always contains an
        // 11-digit substring, so it would also be misread as a CPF.
        var identity = EmployeeDocumentSplitter.DetectPageIdentity(
            "CNPJ: 11.222.333/0001-81 apenas, sem CPF nesta pagina.", [MariaSouza]);
        Assert.Null(identity.ExtractedCpfDigits);
        Assert.Equal("11222333000181", identity.ExtractedCnpjDigits);
    }
}
