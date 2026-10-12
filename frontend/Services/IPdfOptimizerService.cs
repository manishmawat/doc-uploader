namespace UploaderDoc.Services;

public interface IPdfOptimizerService
{
    Task<byte[]> RotatePdfAsync(Stream pdfStream, int rotationAngle);
    Task<byte[]> OptimizePdfSizeAsync(Stream pdfStream, CompressionLevel compressionLevel);
    Task<byte[]> RotateAndOptimizePdfAsync(Stream pdfStream, int rotationAngle, CompressionLevel compressionLevel);
}

public enum CompressionLevel
{
    None,
    Low,
    Medium,
    High
}
