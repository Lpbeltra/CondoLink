using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace CondoLink.Api.Features.EmployeeDocuments;

/// <summary>
/// Produces a standalone PDF containing only the given 1-based page range of
/// an uploaded source PDF — the individualized document handed to a single
/// employee, both for preview and for the WhatsApp document header.
/// </summary>
public static class EmployeeDocumentPdfSlicer
{
    public static byte[] Slice(Stream sourceStream, int pageStart, int pageEnd)
    {
        sourceStream.Position = 0;
        using var source = PdfReader.Open(sourceStream, PdfDocumentOpenMode.Import);
        using var output = new PdfDocument();
        for (var pageNumber = pageStart; pageNumber <= pageEnd; pageNumber++)
            output.AddPage(source.Pages[pageNumber - 1]);
        using var buffer = new MemoryStream();
        output.Save(buffer, false);
        return buffer.ToArray();
    }
}
