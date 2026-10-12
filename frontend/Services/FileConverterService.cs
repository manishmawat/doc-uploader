using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;

namespace UploaderDoc.Services;

public class FileConverterService : IFileConverterService
{
    private readonly HashSet<string> _supportedFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".tif", ".webp"
    };

    public async Task<byte[]> ConvertToPdfAsync(Stream fileStream, string fileName)
    {
        try
        {
            Console.WriteLine($"ConvertToPdfAsync called for file: {fileName}");
            var extension = Path.GetExtension(fileName);
            
            if (!IsSupportedFormat(fileName))
            {
                throw new NotSupportedException($"File format {extension} is not supported for conversion.");
            }

            return await ConvertImageToPdfAsync(fileStream);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in ConvertToPdfAsync for {fileName}: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            throw new InvalidOperationException($"Failed to convert {fileName} to PDF: {ex.Message}", ex);
        }
    }

    public bool IsSupportedFormat(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return _supportedFormats.Contains(extension);
    }

    public IEnumerable<string> GetSupportedFormats()
    {
        return _supportedFormats.ToList();
    }

    private async Task<byte[]> ConvertImageToPdfAsync(Stream imageStream)
    {
        Console.WriteLine("ConvertImageToPdfAsync started");
        imageStream.Position = 0;
        
        // Read image data
        Console.WriteLine($"Image stream length: {imageStream.Length}");
        var imageBytes = new byte[imageStream.Length];
        var totalBytesRead = 0;
        int bytesRead;
        while (totalBytesRead < imageBytes.Length && 
               (bytesRead = await imageStream.ReadAsync(imageBytes.AsMemory(totalBytesRead))) > 0)
        {
            totalBytesRead += bytesRead;
        }
        Console.WriteLine($"Read {totalBytesRead} bytes from image stream");
        
        Console.WriteLine("Creating PDF document with A4 size");
        
        // Use a different approach - capture the stream content during writing
        var outputStream = new MemoryStream();
        try
        {
            // Try to create the PDF writer with minimal configuration
            Console.WriteLine("Creating PDF writer");
            var writer = new PdfWriter(outputStream);
            
            Console.WriteLine("Creating PDF document");
            var pdfDoc = new PdfDocument(writer);
            
            Console.WriteLine("Creating document layout");
            var document = new Document(pdfDoc);
            
            // Use A4 page size by default
            var pageSize = iText.Kernel.Geom.PageSize.A4;
            pdfDoc.SetDefaultPageSize(pageSize);
            
            Console.WriteLine("Creating iText image from byte array");
            // Create iText image and add to PDF
            var pdfImage = new iText.Layout.Element.Image(iText.IO.Image.ImageDataFactory.Create(imageBytes));
            
            Console.WriteLine("Scaling image to fit A4 page");
            // Scale image to fit the A4 page (with some margin)
            pdfImage.ScaleToFit(pageSize.GetWidth() - 40, pageSize.GetHeight() - 40);
            pdfImage.SetFixedPosition(20, 20); // Add margin by positioning

            Console.WriteLine("Adding image to PDF");
            document.Add(pdfImage);
            
            Console.WriteLine("Closing document in sequence");
            document.Close();
            pdfDoc.Close();
            writer.Close();
            
            Console.WriteLine("Getting PDF bytes from stream");
            var pdfBytes = outputStream.ToArray();
            Console.WriteLine($"Successfully extracted {pdfBytes.Length} bytes from stream");
            
            return pdfBytes;
        }
        finally
        {
            outputStream?.Dispose();
        }
    }
}