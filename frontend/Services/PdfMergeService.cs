using iText.Kernel.Pdf;
using iText.Kernel.Utils;

namespace UploaderDoc.Services;

public class PdfMergeService : IPdfMergeService
{
    public Task<byte[]> MergePdfsAsync(IEnumerable<Stream> pdfStreams)
    {
        return Task.Run(() =>
        {
            using var outputStream = new MemoryStream();
            using var writer = new PdfWriter(outputStream);
            using var mergedPdf = new PdfDocument(writer);
            
            var merger = new PdfMerger(mergedPdf);
            
            foreach (var stream in pdfStreams)
            {
                try
                {
                    stream.Position = 0; // Reset stream position
                    using var reader = new PdfReader(stream);
                    using var sourcePdf = new PdfDocument(reader);
                    
                    // Merge all pages from the source PDF
                    merger.Merge(sourcePdf, 1, sourcePdf.GetNumberOfPages());
                }
                catch (Exception ex)
                {
                    // Log the error but continue with other PDFs
                    Console.WriteLine($"Error processing PDF: {ex.Message}");
                }
            }
            
            mergedPdf.Close();
            return outputStream.ToArray();
        });
    }
}