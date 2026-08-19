using CondoLink.Api.Features.Auth;

namespace CondoLink.Tests;

public sealed class FirstAccessWhatsAppInvitationServiceTests
{
    [Fact]
    public void Dynamic_button_parameter_completes_the_configured_template_url_once()
    {
        const string prefix = "https://www.comvy.com.br/primeiro-acesso";
        const string link = prefix + "?userId=abc&token=A%2BB%2FC%3D";

        var parameter = FirstAccessWhatsAppInvitationService
            .DynamicButtonParameter(link);

        Assert.Equal("?userId=abc&token=A%2BB%2FC%3D", parameter);
        Assert.Equal(link, prefix + parameter);
    }
}
