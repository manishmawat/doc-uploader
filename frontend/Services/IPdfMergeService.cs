namespace UploaderDoc.Services;

public interface IPdfMergeService
{
    Task<byte[]> MergePdfsAsync(IEnumerable<Stream> pdfStreams);
}