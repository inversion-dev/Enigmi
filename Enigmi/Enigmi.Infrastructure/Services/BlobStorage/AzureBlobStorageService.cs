using Enigmi.Common;
using Azure.Identity;
using Azure.Storage.Blobs;
using Enigmi.Domain.Utils;
using static System.FormattableString;
using Newtonsoft.Json;

namespace Enigmi.Infrastructure.Services.BlobStorage;

public class AzureBlobStorageService : IBlobStorageService
{
    public Settings Settings { get; }

    public AzureBlobStorageService(Settings settings)
    {
        settings.ThrowIfNull();
        Settings = settings.ThrowIfNull();
    }

    private ChainedTokenCredential GetCredentials() => new ChainedTokenCredential(
                        #if DEBUG
            new AzureCliCredential(),
                        #endif
                        new ManagedIdentityCredential());

    private Uri GetUri(string blobPath) => new Uri(Invariant($"{Settings.BlobstorageConfig.RootUrl}{BlobPathHelper.PrependBlobPathIfRequired(Settings, blobPath)}"));

    public async Task UploadFileAsync(string blobPath, byte[] fileBytes, bool overwrite = false)
    {
        blobPath.ThrowIfNull();
        fileBytes.ThrowIfNull();

        var blobClient = new BlobClient(GetUri(blobPath), GetCredentials());
        await blobClient.UploadAsync(new BinaryData(fileBytes), overwrite).ContinueOnAnyContext();
    }

    public async Task CopyBlobAsync(string fromBlobPath, string toBlobPath, bool overwrite = false)
    {
        fromBlobPath.ThrowIfNull();
        toBlobPath.ThrowIfNull();

        var toBlobClient = new BlobClient(GetUri(toBlobPath), GetCredentials());
        var operation = await toBlobClient.StartCopyFromUriAsync(GetUri(fromBlobPath));
        await operation.WaitForCompletionAsync();
        if (!operation.HasCompleted)
        {
            throw new Common.Exceptions.ApplicationException(Invariant($"Copy of blob from '{fromBlobPath}' to '{toBlobPath}' failed"));
        }
    }

    public async Task DownloadBlobToStream(string blobPath, Stream stream)
    {   
        var blobClient = new BlobClient(GetUri(blobPath), GetCredentials());

        var exists = await blobClient.ExistsAsync().ContinueOnAnyContext();

        if (!exists)
        {
           throw new ApplicationException($"Blob does not exists at {blobPath}");
        }

        await blobClient.DownloadToAsync(stream).ContinueOnAnyContext();
    }

    public async Task<T> DownloadBlob<T>(string blobPath)
    {
        blobPath.ThrowIfNullOrWhitespace();

        using var memoryStream = new MemoryStream();
        await this.DownloadBlobToStream(blobPath, memoryStream);
        memoryStream.Position = 0;

        using StreamReader sr = new StreamReader(memoryStream);
        using JsonReader reader = new JsonTextReader(sr);
        JsonSerializer serializer = new JsonSerializer();

        var obj = serializer.Deserialize<T>(reader);

        obj.ThrowIfNull();
        return obj;
    }
}