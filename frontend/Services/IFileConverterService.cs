namespace UploaderDoc.Services;

public interface IFileConverterService
{
    Task<byte[]> ConvertToPdfAsync(Stream fileStream, string fileName);
    bool IsSupportedFormat(string fileName);
    IEnumerable<string> GetSupportedFormats();
}

public class ConvertedFile
{
    public string OriginalFileName { get; set; } = string.Empty;
    public string ConvertedFileName { get; set; } = string.Empty;
    public byte[] PdfData { get; set; } = Array.Empty<byte>();
    public long OriginalSize { get; set; }
    public long ConvertedSize { get; set; }
}