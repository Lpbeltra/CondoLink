using System.IO.Compression;
using CondoLink.Api.Features.CondominiumSetup;

namespace CondoLink.Tests;

public sealed class ResidentImportTemplateTests
{
    [Fact]
    public void Template_offers_combined_first_access_channel()
    {
        using var stream = new MemoryStream(ResidentImportTemplate.Create());
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        using var reader = new StreamReader(archive.GetEntry("xl/worksheets/sheet1.xml")!.Open());

        var worksheet = reader.ReadToEnd();

        Assert.Contains("WhatsApp,E-mail,WhatsApp + E-mail,Não", worksheet);
    }
}
