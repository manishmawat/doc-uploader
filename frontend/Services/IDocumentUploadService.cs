namespace UploaderDoc.Services
{
    public interface IDocumentUploadService
    {
        Task UploadDocumentAsync(Stream fileStream, string fileName);
    }
}