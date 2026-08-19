using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace CondoLink.Api.Features.CondominiumSetup;

internal static class SetupSpreadsheetReader
{
    internal const long MaximumFileSize = 5 * 1024 * 1024;
    internal const int MaximumRows = 5000;

    public static async Task<SpreadsheetResult> ReadAsync(
        IFormFile file,
        IReadOnlyList<string> expectedHeaders,
        CancellationToken cancellationToken,
        IReadOnlyList<string>? optionalHeaders = null)
    {
        if (file.Length == 0)
            return SpreadsheetResult.Failure("Arquivo vazio.");
        if (file.Length > MaximumFileSize)
            return SpreadsheetResult.Failure("O arquivo deve possuir no máximo 5 MB.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        await using var stream = file.OpenReadStream();
        List<IReadOnlyList<string>> rows;
        try
        {
            rows = extension switch
            {
                ".csv" => await ReadCsvAsync(stream, cancellationToken),
                ".xlsx" => ReadXlsx(stream),
                _ => throw new InvalidDataException(
                    "Formato não suportado. Envie um arquivo CSV ou XLSX.")
            };
        }
        catch (Exception exception) when (
            exception is InvalidDataException or IOException
                or System.Xml.XmlException)
        {
            return SpreadsheetResult.Failure(
                $"Não foi possível ler a planilha: {exception.Message}");
        }

        if (rows.Count == 0)
            return SpreadsheetResult.Failure("A planilha não possui cabeçalho.");
        if (rows.Count - 1 > MaximumRows)
            return SpreadsheetResult.Failure(
                $"A planilha deve possuir no máximo {MaximumRows} linhas.");

        var headers = rows[0].Select(NormalizeHeader).ToArray();
        var missing = expectedHeaders
            .Where(expected => !headers.Contains(
                NormalizeHeader(expected), StringComparer.OrdinalIgnoreCase))
            .ToArray();
        if (missing.Length > 0)
        {
            return SpreadsheetResult.Failure(
                "Colunas obrigatórias ausentes: " + string.Join(", ", missing)
                + ". Não renomeie as colunas do modelo.");
        }

        var requestedHeaders = expectedHeaders.Concat(optionalHeaders ?? []).ToArray();
        var indexes = requestedHeaders.ToDictionary(
            header => header,
            header => Array.FindIndex(
                headers,
                current => string.Equals(
                    current,
                    NormalizeHeader(header),
                    StringComparison.OrdinalIgnoreCase)),
            StringComparer.OrdinalIgnoreCase);

        var data = rows.Skip(1)
            .Select((row, index) => new SpreadsheetRow(
                index + 2,
                requestedHeaders.ToDictionary(
                    header => header,
                    header =>
                    {
                        var column = indexes[header];
                        return column >= 0 && column < row.Count ? row[column] : string.Empty;
                    },
                    StringComparer.OrdinalIgnoreCase)))
            .Where(row => row.Values.Values.Any(
                value => !string.IsNullOrWhiteSpace(value)))
            .ToArray();

        return SpreadsheetResult.Success(data);
    }

    private static async Task<List<IReadOnlyList<string>>> ReadCsvAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: true);
        var content = await reader.ReadToEndAsync(cancellationToken);
        if (content.Length == 0) return [];
        var firstLine = content.Split(['\r', '\n'], 2)[0];
        var separator = firstLine.Count(character => character == ';')
            > firstLine.Count(character => character == ',')
            ? ';'
            : ',';
        return ParseCsv(content, separator);
    }

    private static List<IReadOnlyList<string>> ParseCsv(
        string content,
        char separator)
    {
        var rows = new List<IReadOnlyList<string>>();
        var row = new List<string>();
        var value = new StringBuilder();
        var quoted = false;

        for (var index = 0; index < content.Length; index++)
        {
            var character = content[index];
            if (character == '"')
            {
                if (quoted && index + 1 < content.Length
                    && content[index + 1] == '"')
                {
                    value.Append('"');
                    index++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (character == separator && !quoted)
            {
                row.Add(value.ToString());
                value.Clear();
            }
            else if ((character == '\r' || character == '\n') && !quoted)
            {
                if (character == '\r' && index + 1 < content.Length
                    && content[index + 1] == '\n')
                    index++;
                row.Add(value.ToString());
                value.Clear();
                rows.Add(row);
                row = [];
            }
            else
            {
                value.Append(character);
            }
        }

        if (value.Length > 0 || row.Count > 0)
        {
            row.Add(value.ToString());
            rows.Add(row);
        }

        return rows;
    }

    private static List<IReadOnlyList<string>> ReadXlsx(Stream stream)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, true);
        var sharedStrings = ReadSharedStrings(archive);
        var sheetPath = ResolveFirstSheetPath(archive);
        var entry = archive.GetEntry(sheetPath)
            ?? throw new InvalidDataException(
                "A primeira planilha do arquivo não foi encontrada.");
        using var sheetStream = entry.Open();
        var document = XDocument.Load(sheetStream);
        XNamespace spreadsheet =
            "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var rows = new List<IReadOnlyList<string>>();

        foreach (var rowElement in document.Descendants(spreadsheet + "row"))
        {
            var values = new SortedDictionary<int, string>();
            foreach (var cell in rowElement.Elements(spreadsheet + "c"))
            {
                var reference = (string?)cell.Attribute("r") ?? "A1";
                var column = ColumnIndex(reference);
                var type = (string?)cell.Attribute("t");
                string value;
                if (type == "inlineStr")
                {
                    value = string.Concat(
                        cell.Descendants(spreadsheet + "t")
                            .Select(text => text.Value));
                }
                else
                {
                    value = cell.Element(spreadsheet + "v")?.Value ?? string.Empty;
                    if (type == "s" && int.TryParse(value, out var sharedIndex)
                        && sharedIndex >= 0
                        && sharedIndex < sharedStrings.Count)
                    {
                        value = sharedStrings[sharedIndex];
                    }
                    else if (type == "b")
                    {
                        value = value == "1" ? "TRUE" : "FALSE";
                    }
                }
                values[column] = value;
            }

            var length = values.Count == 0 ? 0 : values.Keys.Max() + 1;
            rows.Add(Enumerable.Range(0, length)
                .Select(index => values.GetValueOrDefault(index, string.Empty))
                .ToArray());
        }

        return rows;
    }

    private static List<string> ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null) return [];
        using var stream = entry.Open();
        var document = XDocument.Load(stream);
        XNamespace spreadsheet =
            "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        return document.Descendants(spreadsheet + "si")
            .Select(item => string.Concat(
                item.Descendants(spreadsheet + "t")
                    .Select(text => text.Value)))
            .ToList();
    }

    private static string ResolveFirstSheetPath(ZipArchive archive)
    {
        var workbookEntry = archive.GetEntry("xl/workbook.xml");
        var relationsEntry =
            archive.GetEntry("xl/_rels/workbook.xml.rels");
        if (workbookEntry is null || relationsEntry is null)
            return "xl/worksheets/sheet1.xml";

        using var workbookStream = workbookEntry.Open();
        using var relationsStream = relationsEntry.Open();
        var workbook = XDocument.Load(workbookStream);
        var relations = XDocument.Load(relationsStream);
        XNamespace spreadsheet =
            "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace relationship =
            "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        XNamespace packageRelationship =
            "http://schemas.openxmlformats.org/package/2006/relationships";
        var relationId = (string?)workbook
            .Descendants(spreadsheet + "sheet")
            .FirstOrDefault()?.Attribute(relationship + "id");
        var target = relations
            .Descendants(packageRelationship + "Relationship")
            .FirstOrDefault(item =>
                (string?)item.Attribute("Id") == relationId)
            ?.Attribute("Target")?.Value;
        if (string.IsNullOrWhiteSpace(target))
            return "xl/worksheets/sheet1.xml";
        return target.StartsWith('/') ? target.TrimStart('/') : "xl/" + target;
    }

    private static int ColumnIndex(string reference)
    {
        var index = 0;
        foreach (var character in reference.TakeWhile(char.IsLetter))
            index = index * 26 + char.ToUpperInvariant(character) - 'A' + 1;
        return Math.Max(0, index - 1);
    }

    private static string NormalizeHeader(string value) =>
        value.Trim().TrimStart('\uFEFF').ToLowerInvariant() switch
        {
            "bloco" => "Block",
            "unidade" => "Unit",
            "nome" => "Name",
            "e-mail" or "email" => "Email",
            "telefone" => "Phone",
            "relacionamento" => "Relationship",
            "morador" => "Resident",
            "residência principal" or "residencia principal" => "PrimaryResidence",
            "enviar acesso por e-mail" or "enviar acesso por email" => "SendAccessEmail",
            "enviar primeiro acesso" => "FirstAccessChannel",
            var header => header
        };
}

internal sealed record SpreadsheetRow(
    int Line,
    IReadOnlyDictionary<string, string> Values);

internal sealed record SpreadsheetResult(
    IReadOnlyList<SpreadsheetRow> Rows,
    string? Error)
{
    public static SpreadsheetResult Success(
        IReadOnlyList<SpreadsheetRow> rows) => new(rows, null);
    public static SpreadsheetResult Failure(string error) => new([], error);
}
