using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MigraDoc;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Fonts;

namespace CondoLink.Api.Features.CondominiumMembers;

internal sealed record ResidentReportRow(
    string? Block, string Unit, string Name, string Email, string? Phone,
    string Relationship, bool IsResident, bool IsPrimaryResidence,
    string FirstAccessStatus);

internal sealed class ResidentReportPdf
{
    private const string FontFamily = "ComvySans";
    private static readonly object FontLock = new();

    public byte[] Create(string condominiumName, DateTime generatedAt,
        IReadOnlyList<ResidentReportRow> residents)
    {
        EnsureFontResolver();
        var document = new Document();
        document.Info.Title = $"Relação de moradores - {condominiumName}";
        document.Styles[StyleNames.Normal]!.Font.Name = FontFamily;
        document.Styles[StyleNames.Normal]!.Font.Size = 8.5;
        var section = document.AddSection();
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.TopMargin = Unit.FromCentimeter(1.3);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(1.3);
        section.PageSetup.LeftMargin = Unit.FromCentimeter(1.25);
        section.PageSetup.RightMargin = Unit.FromCentimeter(1.25);

        AddHeader(section, condominiumName, generatedAt, residents);
        foreach (var blockGroup in residents
                     .GroupBy(x => x.Block ?? string.Empty)
                     .OrderBy(x => x.Key, NaturalStringComparer.Instance))
        {
            if (blockGroup.Key.Length > 0)
                AddHeading(section, blockGroup.Key, 13, Colors.DarkBlue);
            foreach (var unitGroup in blockGroup.GroupBy(x => x.Unit)
                         .OrderBy(x => x.Key, NaturalStringComparer.Instance))
            {
                AddHeading(section, $"Unidade {unitGroup.Key}", 10.5, Colors.Black);
                AddResidentsTable(section, unitGroup.OrderBy(x => x.Name,
                    StringComparer.Create(new CultureInfo("pt-BR"), true)));
            }
        }
        if (residents.Count == 0)
        {
            var empty = section.AddParagraph("Nenhum morador ativo cadastrado.");
            empty.Format.SpaceBefore = Unit.FromCentimeter(1);
            empty.Format.Font.Color = Colors.Gray;
        }

        var footer = section.Footers.Primary.AddParagraph();
        footer.Format.Alignment = ParagraphAlignment.Center;
        footer.Format.Font.Size = 7;
        footer.Format.Font.Color = Colors.Gray;
        footer.AddText("Comvy · página ");
        footer.AddPageField();

        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();
        using var output = new MemoryStream();
        renderer.PdfDocument.Save(output, false);
        return output.ToArray();
    }

    private static void AddHeader(Section section, string condominiumName,
        DateTime generatedAt, IReadOnlyList<ResidentReportRow> residents)
    {
        var brand = section.AddParagraph("Comvy");
        brand.Format.Font.Size = 10;
        brand.Format.Font.Bold = true;
        brand.Format.Font.Color = Color.FromRgb(102, 130, 244);
        var title = section.AddParagraph("Relação de moradores");
        title.Format.Font.Size = 18;
        title.Format.Font.Bold = true;
        title.Format.SpaceAfter = Unit.FromPoint(2);
        var condominium = section.AddParagraph(condominiumName);
        condominium.Format.Font.Size = 12;
        condominium.Format.Font.Bold = true;
        var unitCount = residents.Where(x => x.Unit != "Sem unidade")
            .Select(x => $"{x.Block}\0{x.Unit}")
            .Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var summary = section.AddParagraph(
            $"{residents.Select(x => x.Email).Distinct(StringComparer.OrdinalIgnoreCase).Count()} moradores ativos · {unitCount} unidades");
        summary.Format.SpaceBefore = Unit.FromPoint(3);
        var generated = section.AddParagraph(
            $"Gerado em {generatedAt:dd/MM/yyyy 'às' HH:mm}");
        generated.Format.Font.Color = Colors.Gray;
        generated.Format.SpaceAfter = Unit.FromCentimeter(.6);
    }

    private static void AddHeading(Section section, string text, double size, Color color)
    {
        var paragraph = section.AddParagraph(text);
        paragraph.Format.Font.Size = size;
        paragraph.Format.Font.Bold = true;
        paragraph.Format.Font.Color = color;
        paragraph.Format.SpaceBefore = Unit.FromPoint(8);
        paragraph.Format.SpaceAfter = Unit.FromPoint(3);
        paragraph.Format.KeepWithNext = true;
    }

    private static void AddResidentsTable(Section section,
        IEnumerable<ResidentReportRow> residents)
    {
        var table = section.AddTable();
        table.Borders.Color = Color.FromRgb(220, 225, 235);
        table.Borders.Width = .4;
        table.Rows.LeftIndent = 0;
        table.AddColumn(Unit.FromCentimeter(4.1));
        table.AddColumn(Unit.FromCentimeter(5.3));
        table.AddColumn(Unit.FromCentimeter(3.4));
        table.AddColumn(Unit.FromCentimeter(3.2));
        table.AddColumn(Unit.FromCentimeter(2.1));
        foreach (var resident in residents)
        {
            var row = table.AddRow();
            row.VerticalAlignment = VerticalAlignment.Center;
            row.Cells[0].AddParagraph(resident.Name);
            row.Cells[0].AddParagraph(resident.Relationship);
            row.Cells[1].AddParagraph(resident.Email);
            row.Cells[1].AddParagraph(resident.Phone ?? "Telefone não informado");
            row.Cells[2].AddParagraph($"Morador: {YesNo(resident.IsResident)}");
            row.Cells[2].AddParagraph($"Principal: {YesNo(resident.IsPrimaryResidence)}");
            row.Cells[3].AddParagraph(FirstAccessLabel(resident.FirstAccessStatus));
            row.Cells[4].AddParagraph(resident.Block ?? "Sem bloco");
            row.Format.SpaceBefore = Unit.FromPoint(2.5);
            row.Format.SpaceAfter = Unit.FromPoint(2.5);
            row.Format.LeftIndent = Unit.FromPoint(2);
            row.Format.RightIndent = Unit.FromPoint(2);
        }
    }

    internal static string FirstAccessLabel(string status) => status switch
    {
        "InviteSent" => "Convite enviado",
        "Completed" => "Acesso concluído",
        "DeliveryFailed" => "Falha no envio",
        _ => "Acesso pendente"
    };

    private static string YesNo(bool value) => value ? "Sim" : "Não";

    private static void EnsureFontResolver()
    {
        lock (FontLock)
        {
            PredefinedFontsAndChars.ErrorFontName = FontFamily;
            GlobalFontSettings.FontResolver ??= new ComvyFontResolver();
        }
    }
}

internal sealed class NaturalStringComparer : IComparer<string>
{
    public static NaturalStringComparer Instance { get; } = new();
    private static readonly Regex Parts = new("(\\d+)", RegexOptions.Compiled);
    public int Compare(string? x, string? y)
    {
        var left = Parts.Split(x ?? string.Empty);
        var right = Parts.Split(y ?? string.Empty);
        for (var i = 0; i < Math.Min(left.Length, right.Length); i++)
        {
            var result = long.TryParse(left[i], out var ln)
                && long.TryParse(right[i], out var rn)
                ? ln.CompareTo(rn)
                : StringComparer.Create(new CultureInfo("pt-BR"), true)
                    .Compare(left[i], right[i]);
            if (result != 0) return result;
        }
        return left.Length.CompareTo(right.Length);
    }
}

internal sealed class ComvyFontResolver : IFontResolver
{
    private static readonly string[] RegularPaths =
    [
        "/usr/share/fonts/truetype/liberation2/LiberationSans-Regular.ttf",
        "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
        "C:/Windows/Fonts/arial.ttf"
    ];
    private static readonly string[] BoldPaths =
    [
        "/usr/share/fonts/truetype/liberation2/LiberationSans-Bold.ttf",
        "/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
        "C:/Windows/Fonts/arialbd.ttf"
    ];
    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic) =>
        new(isBold ? "comvy-bold" : "comvy-regular");
    public byte[] GetFont(string faceName)
    {
        var paths = faceName == "comvy-bold" ? BoldPaths : RegularPaths;
        var path = paths.FirstOrDefault(File.Exists)
            ?? throw new InvalidOperationException("Fonte necessária para gerar PDF não encontrada.");
        return File.ReadAllBytes(path);
    }
}
