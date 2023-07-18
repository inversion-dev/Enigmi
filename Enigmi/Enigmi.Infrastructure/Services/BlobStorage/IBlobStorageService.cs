namespace Enigmi.Infrastructure.Services.BlobStorage;

public interface IBlobStorageService
{
    Task UploadFileAsync(string blobPath, byte[] fileBytes, bool overwrite = false);

    Task DownloadBlobToStream(string blobPath, Stream stream);

    Task<T> DownloadBlob<T>(string blobPath);

    Task CopyBlobAsync(string fromBlobPath, string toBlobPath, bool overwrite = false);
}