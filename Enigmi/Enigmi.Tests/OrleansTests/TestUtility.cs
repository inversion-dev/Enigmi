using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Enigmi.Common;
using Microsoft.Extensions.Configuration;

namespace Enigmi.Tests.OrleansTests;

public class TestUtility
{
    public BlobContainerClient? ContainerClient { get; set; }

    public BlobServiceClient? BlobServiceClient { get; set; }
    
    public TestUtility(string containerName)
    {
        Setting = HostSetup.ConfigurationExtensions.GetConfiguration().GetSection("Settings").Get<Settings>()!;
        BlobServiceClient = new BlobServiceClient(this.Setting.BlobstorageConfig.ConnectionString);
        ContainerClient = BlobServiceClient.GetBlobContainerClient(containerName);
    }

    public async Task<byte[]> DownloadBlobBytes(string blobName)
    {
        blobName.ThrowIfNullOrWhitespace();
        ContainerClient.ThrowIfNull();
        
        var blobClient = ContainerClient.GetBlobClient(blobName);
        var memoryStream = new MemoryStream();
        await blobClient.DownloadToAsync(memoryStream);
        return memoryStream.ToArray();
    }

    private Settings Setting { get; set; }
    
    public async Task<List<string>> GetAllBlobNames()
    {
        var blobNames = new List<string>();
        var resultSegment = ContainerClient!.GetBlobsAsync()
            .AsPages(default, 50);

        await foreach (Azure.Page<BlobItem> blobPage in resultSegment)
        {
            foreach (BlobItem blobItem in blobPage.Values)
            {
                blobNames.Add(blobItem.Name);
            }
        }

        return blobNames;
    }
}