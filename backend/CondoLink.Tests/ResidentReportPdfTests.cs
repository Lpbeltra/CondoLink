using CondoLink.Api.Features.CondominiumMembers;

namespace CondoLink.Tests;

public sealed class ResidentReportPdfTests
{
    [Fact]
    public void Natural_order_places_2_before_10()
    {
        var values = new[] { "Bloco 10", "Bloco 2", "Bloco 1" };
        Array.Sort(values, NaturalStringComparer.Instance);
        Assert.Equal(["Bloco 1", "Bloco 2", "Bloco 10"], values);
    }

    [Theory]
    [InlineData("Pending", "Acesso pendente")]
    [InlineData("InviteSent", "Convite enviado")]
    [InlineData("Completed", "Acesso concluído")]
    [InlineData("DeliveryFailed", "Falha no envio")]
    public void First_access_status_uses_management_labels(string value, string label) =>
        Assert.Equal(label, ResidentReportPdf.FirstAccessLabel(value));

    [Fact]
    public void Filename_is_safe_and_stable() =>
        Assert.Equal("residencial-monticello",
            ExportCondominiumMembersPdf.SafeFileName("Residencial Monticello"));
}
