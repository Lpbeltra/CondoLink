using System.IO.Compression;
using System.Text;

namespace CondoLink.Api.Features.CondominiumSetup;

internal static class ResidentImportTemplate
{
    internal static readonly string[] Headers =
        ["Bloco", "Unidade", "Nome", "E-mail", "Telefone", "Relacionamento", "Morador", "Residência principal", "Enviar primeiro acesso"];

    public static byte[] Create()
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            Add(archive, "[Content_Types].xml", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/worksheets/sheet2.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/></Types>""");
            Add(archive, "_rels/.rels", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""");
            Add(archive, "xl/workbook.xml", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Moradores" sheetId="1" r:id="rId1"/><sheet name="Instruções" sheetId="2" r:id="rId2"/></sheets></workbook>""");
            Add(archive, "xl/_rels/workbook.xml.rels", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet2.xml"/><Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>""");
            Add(archive, "xl/styles.xml", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><fonts count="2"><font/><font><b/></font></fonts><fills count="1"><fill><patternFill patternType="none"/></fill></fills><borders count="1"><border/></borders><cellStyleXfs count="1"><xf/></cellStyleXfs><cellXfs count="3"><xf xfId="0"/><xf xfId="0" numFmtId="49" applyNumberFormat="1"/><xf xfId="0" fontId="1" applyFont="1"/></cellXfs></styleSheet>""");
            var headerCells = string.Concat(Headers.Select((value, index) => Cell(index, 1, value, 2)));
            Add(archive, "xl/worksheets/sheet1.xml", $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><cols><col min="1" max="9" width="22" customWidth="1" style="1"/></cols><sheetData><row r="1">{headerCells}</row></sheetData><dataValidations count="4"><dataValidation type="list" allowBlank="0" sqref="F2:F5001"><formula1>"Proprietário,Inquilino,Morador autorizado"</formula1></dataValidation><dataValidation type="list" allowBlank="0" sqref="G2:G5001"><formula1>"Sim,Não"</formula1></dataValidation><dataValidation type="list" allowBlank="0" sqref="H2:H5001"><formula1>"Sim,Não"</formula1></dataValidation><dataValidation type="list" allowBlank="0" sqref="I2:I5001"><formula1>"WhatsApp,E-mail,WhatsApp + E-mail,Não"</formula1></dataValidation></dataValidations></worksheet>""");
            var instructions = new[]
            {
                "Preencha somente a aba Moradores. Não altere os cabeçalhos.",
                "Obrigatórios: Nome e E-mail. Ao vincular uma unidade, informe Unidade, Relacionamento, Morador e Residência principal. Bloco é obrigatório apenas quando o condomínio utiliza blocos. Telefone é opcional.",
                "Unidade é texto: exemplos 01, 101A e Térreo. Se informada, a unidade deve existir; a importação de moradores não cria unidades.",
                "Telefone Brasil: 44999999999, (44) 99999-9999 ou +5544999999999. Internacional: informe +código do país, exemplo +12125551234.",
                "Relacionamento: Proprietário, Inquilino ou Morador autorizado.",
                "Morador e Residência principal: use Sim ou Não. Residência principal exige Morador = Sim.",
                "Enviar primeiro acesso: use WhatsApp, E-mail, WhatsApp + E-mail ou Não. A opção combinada exige telefone válido e e-mail entregável. Arquivos antigos sem a coluna assumem Não.",
                "Bloco: use o identificador cadastrado, como Bloco 1 ou Torre A. Abreviações só são aceitas quando inequívocas.",
                "CSV também é aceito em UTF-8, com vírgula ou ponto e vírgula e os mesmos cabeçalhos."
            };
            var instructionRows = string.Concat(instructions.Select((value, index) => $"<row r=\"{index + 1}\">{Cell(0, index + 1, value, index == 0 ? 2 : 0)}</row>"));
            Add(archive, "xl/worksheets/sheet2.xml", $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><cols><col min="1" max="1" width="120" customWidth="1"/></cols><sheetData>{instructionRows}</sheetData></worksheet>""");
        }
        return output.ToArray();
    }

    private static string Cell(int column, int row, string value, int style) =>
        $"<c r=\"{(char)('A' + column)}{row}\" t=\"inlineStr\" s=\"{style}\"><is><t>{System.Security.SecurityElement.Escape(value)}</t></is></c>";

    private static void Add(ZipArchive archive, string path, string content)
    {
        using var writer = new StreamWriter(archive.CreateEntry(path).Open(), new UTF8Encoding(false));
        writer.Write(content);
    }
}
