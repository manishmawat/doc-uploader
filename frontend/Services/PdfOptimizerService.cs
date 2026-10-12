using iText.Kernel.Pdf;
using iText.Kernel.Geom;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;

namespace UploaderDoc.Services;

public class PdfOptimizerService : IPdfOptimizerService
{
    public async Task<byte[]> RotatePdfAsync(Stream pdfStream, int rotationAngle)
    {
        return await Task.Run(() =>
        {
            Console.WriteLine($"RotatePdfAsync: Stream length = {pdfStream.Length}, Position = {pdfStream.Position}");
            pdfStream.Position = 0;
            
            Console.WriteLine("Creating MemoryStream for output");
            var outputStream = new MemoryStream();
            try
            {
                Console.WriteLine("Creating PdfReader");
                var reader = new PdfReader(pdfStream);
                Console.WriteLine("Creating PdfWriter");
                var writer = new PdfWriter(outputStream);
                Console.WriteLine("Creating PdfDocument");
                var pdfDoc = new PdfDocument(reader, writer);
                
                // Normalize rotation angle to 0, 90, 180, or 270
                rotationAngle = ((rotationAngle % 360) + 360) % 360;
                if (rotationAngle % 90 != 0)
                {
                    throw new ArgumentException("Rotation angle must be a multiple of 90 degrees");
                }
                
                // Rotate all pages
                int numberOfPages = pdfDoc.GetNumberOfPages();
                for (int i = 1; i <= numberOfPages; i++)
                {
                    var page = pdfDoc.GetPage(i);
                    int currentRotation = page.GetRotation();
                    page.SetRotation((currentRotation + rotationAngle) % 360);
                }
                
                pdfDoc.Close();
                writer.Close();
                
                var result = outputStream.ToArray();
                return result;
            }
            finally
            {
                outputStream?.Dispose();
            }
        });
    }

    public async Task<byte[]> OptimizePdfSizeAsync(Stream pdfStream, CompressionLevel compressionLevel)
    {
        return await Task.Run(() =>
        {
            Console.WriteLine($"OptimizePdfSizeAsync: Stream length = {pdfStream.Length}, Position = {pdfStream.Position}");
            pdfStream.Position = 0;
            
            var outputStream = new MemoryStream();
            try
            {
                Console.WriteLine("Creating PdfReader for optimization");
                var reader = new PdfReader(pdfStream);
                
                // Configure writer properties based on compression level
                var writerProperties = new WriterProperties();
                
                switch (compressionLevel)
                {
                    case CompressionLevel.High:
                        writerProperties.SetCompressionLevel(9);
                        writerProperties.SetFullCompressionMode(true);
                        break;
                    case CompressionLevel.Medium:
                        writerProperties.SetCompressionLevel(6);
                        writerProperties.SetFullCompressionMode(true);
                        break;
                    case CompressionLevel.Low:
                        writerProperties.SetCompressionLevel(3);
                        break;
                    case CompressionLevel.None:
                    default:
                        writerProperties.SetCompressionLevel(0);
                        break;
                }
                
                var writer = new PdfWriter(outputStream, writerProperties);
                var pdfDoc = new PdfDocument(reader, writer);
                
                pdfDoc.Close();
                writer.Close();
                
                var result = outputStream.ToArray();
                return result;
            }
            finally
            {
                outputStream?.Dispose();
            }
        });
    }

    public async Task<byte[]> RotateAndOptimizePdfAsync(Stream pdfStream, int rotationAngle, CompressionLevel compressionLevel)
    {
        return await Task.Run(() =>
        {
            Console.WriteLine($"RotateAndOptimizePdfAsync: Stream length = {pdfStream.Length}, Position = {pdfStream.Position}");
            pdfStream.Position = 0;
            
            var outputStream = new MemoryStream();
            try
            {
                Console.WriteLine("Creating PdfReader for rotation and optimization");
                var reader = new PdfReader(pdfStream);
                
                // Configure writer properties based on compression level
                var writerProperties = new WriterProperties();
                
                switch (compressionLevel)
                {
                    case CompressionLevel.High:
                        writerProperties.SetCompressionLevel(9);
                        writerProperties.SetFullCompressionMode(true);
                        break;
                    case CompressionLevel.Medium:
                        writerProperties.SetCompressionLevel(6);
                        writerProperties.SetFullCompressionMode(true);
                        break;
                    case CompressionLevel.Low:
                        writerProperties.SetCompressionLevel(3);
                        break;
                    case CompressionLevel.None:
                    default:
                        writerProperties.SetCompressionLevel(0);
                        break;
                }
                
                var writer = new PdfWriter(outputStream, writerProperties);
                var pdfDoc = new PdfDocument(reader, writer);
                
                // Normalize rotation angle
                rotationAngle = ((rotationAngle % 360) + 360) % 360;
                if (rotationAngle % 90 != 0)
                {
                    throw new ArgumentException("Rotation angle must be a multiple of 90 degrees");
                }
                
                // Rotate all pages if rotation is specified
                if (rotationAngle != 0)
                {
                    int numberOfPages = pdfDoc.GetNumberOfPages();
                    for (int i = 1; i <= numberOfPages; i++)
                    {
                        var page = pdfDoc.GetPage(i);
                        int currentRotation = page.GetRotation();
                        page.SetRotation((currentRotation + rotationAngle) % 360);
                    }
                }
                
                pdfDoc.Close();
                writer.Close();
                
                var result = outputStream.ToArray();
                return result;
            }
            finally
            {
                outputStream?.Dispose();
            }
        });
    }
}
