using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Azure.Data.Tables;
using Azure;
using Microsoft.Extensions.Configuration;
using Enigmi.Common;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs;
using Enigmi.Common.Messaging;
using Enigmi.Grains.Shared.PolicyCollection;
using Enigmi.Grains.Shared.PuzzleCollectionSniffer;
using Enigmi.Grains.Shared.PuzzleCollectionSniffer.Messages;
using Enigmi.HostSetup;
using Enigmi.Infrastructure.Services;
using Enigmi.Infrastructure.Services.BlockchainService;
using Enigmi.Infrastructure.Services.SignalR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using Orleans.Serialization;
using static System.FormattableString;
using Response = Azure.Response;

namespace Enigmi.Tests.OrleansTests;

public class ClusterFixture : IDisposable
{
    public ClusterFixture()
    {
        Setting = HostSetup.ConfigurationExtensions.GetConfiguration().GetSection("Settings").Get<Settings>()!;

        BlobServiceClient = new BlobServiceClient(this.Setting.BlobstorageConfig.ConnectionString);
        ContainerClient = BlobServiceClient.GetBlobContainerClient($"{this.Setting.EnvironmentPrefix.ToLowerInvariant()}");

        CleanUp().GetAwaiter().GetResult();
        PrepareBlobStorage().GetAwaiter().GetResult();
        
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddConfiguration(Enigmi.HostSetup.ConfigurationExtensions.GetConfiguration());
        var settings = builder.Configuration.GetSection("Settings").Get<Settings>();
        settings.ThrowIfNull();

        builder.Host.UseOrleans(siloBuilder =>
        {
            siloBuilder.Services.AddSerializer(serializerBuilder =>
            {
                serializerBuilder.ConfigureSerializer();
            });

            siloBuilder
                .UseLocalhostClustering()
                .Configure<ClusterOptions>(options =>
                {
                    options.ClusterId = "OrleansBasics";
                    options.ServiceId = "unittest";
                })
                .SetupSiloStorage();
        });

        builder.Services.ConfigureServices(builder.Configuration);
            
        var blockChainMock = SetupBlockchainServiceMock();
        var policyVaultServiceMock = SetupPolicyVaultServiceMock();
        var signalRHubContextStoreMock = SetupSignalRHubContextStoreMock();
            
        builder.Services.AddTransient<IBlockchainService>(x => blockChainMock.Object);
        builder.Services.AddTransient<IPolicyVaultService>(x => policyVaultServiceMock.Object);
        builder.Services.AddSingleton<ISignalRHubContextStore>(x => signalRHubContextStoreMock.Object);
        
        App = builder.Build();

        App.StartAsync().GetAwaiter().GetResult();
        var cluster = App.Services.GetService<IClusterClient>();
        cluster.ThrowIfNull();
        this.ClusterClient = cluster;

        Task.Run(async () =>
        {
            var policyCollectionGrain = ClusterClient.GetGrain<IPolicyListGrain>(0);
            await policyCollectionGrain.Ping();

           await AddPuzzleCollectionWithStock();
        });
    }

    private Mock<ISignalRHubContextStore> SetupSignalRHubContextStoreMock()
    {
        var signalRHubContextStoreMock = new Mock<ISignalRHubContextStore>();
        var serviceHubContextMock = new Mock<ServiceHubContext>();
        var hubClientsMock = new Mock<IHubClients>();
        var clientProxyMock = new Mock<IClientProxy>();

        clientProxyMock
            .Setup(x => x.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Callback((string method, object?[] args, CancellationToken cancellationToken) =>
            {
                foreach (var message in args)
                {
                    if (message is ISignalRMessage s)
                    {
                        SentSignalrMessages.Add(s);
                    }
                }
            })
            .Returns(Task.CompletedTask);
        hubClientsMock.Setup(x => x.Users(It.IsAny<List<string>>())).Returns(clientProxyMock.Object);
        hubClientsMock.Setup(x => x.Users(It.IsAny<IReadOnlyList<string>>())).Returns(clientProxyMock.Object);
        signalRHubContextStoreMock.Setup(x => x.MessageHubContext).Returns(serviceHubContextMock.Object);
        
        serviceHubContextMock.Setup(x => x.Clients).Returns(hubClientsMock.Object);
        return signalRHubContextStoreMock;
    }

    public WebApplication App { get; set; }

    private Settings Setting { get; set; }

    public IClusterClient ClusterClient { get; set; }

    public List<ISignalRMessage> SentSignalrMessages = new();

    private BlobContainerClient? ContainerClient { get; set; }

    private BlobServiceClient? BlobServiceClient { get; set; }

    public void Dispose()
    {
        App.StopAsync().GetAwaiter().GetResult();
    }

    private async Task CleanUp()
    {
        await ClearBlobStorage();
        await CleanTableStorage();
        
    }

    private async Task PrepareBlobStorage()
    {
        await CreateContainer();
        await PrepareDropsBlobs();
    }

    public async Task<Guid> AddPuzzleCollectionWithStock()
    {
        var sniffer = ClusterClient.GetGrain<IPuzzleCollectionSnifferGrain>(Constants.PuzzleCollectionSnifferGrainKey);
        var seedPuzzleCollectionResponse = await sniffer.SeedPuzzleCollection(new SeedPuzzleCollectionCommand(Invariant($"/drops/Enigmi")));
        var result = seedPuzzleCollectionResponse.Result;
        result.ThrowIfNull();
        return result.PuzzleCollectionId;
    }

    private async Task CreateContainer()
    {
        await ContainerClient!.CreateIfNotExistsAsync();
    }


    private async Task PrepareDropsBlobs()
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
        
        if (!blobNames.Contains(Invariant($"assets/ComingSoon.jpg")))
        {
            await ContainerClient!.UploadBlobAsync(Invariant($"assets/ComingSoon.jpg"),
                new BinaryData(await File.ReadAllBytesAsync(@"TestAssets\ComingSoon.jpg")));
        }

        await CreateDropFolder(blobNames, string.Empty);
        await CreateDropFolder(blobNames, "1");
        await CreateDropFolder(blobNames, "2");
        await CreateDropFolder(blobNames, "3");
    }

    private async Task CreateDropFolder(List<string> blobNames, string postFix)
    {
        if (!blobNames.Contains(Invariant($"drops/Enigmi{postFix}/config.json")))
        {
            await ContainerClient!.UploadBlobAsync(Invariant($"drops/Enigmi{postFix}/config.json"),
                new BinaryData(await File.ReadAllBytesAsync(@"TestAssets\SeedPuzzleCollection\config.json")));
        }

        if (!blobNames.Contains(Invariant($"drops/Enigmi{postFix}/Fluffy Monster.json")))
        {
            await ContainerClient!.UploadBlobAsync(Invariant($"drops/Enigmi{postFix}/Fluffy Monster.json"),
                new BinaryData(await File.ReadAllBytesAsync(@"TestAssets\SeedPuzzleCollection\Fluffy Monster.json")));
        }

        if (!blobNames.Contains(Invariant($"drops/Enigmi{postFix}/Fluffy Monster.jpg")))
        {
            await ContainerClient!.UploadBlobAsync(Invariant($"drops/Enigmi{postFix}/Fluffy Monster.jpg"),
                new BinaryData(await File.ReadAllBytesAsync(@"TestAssets\SeedPuzzleCollection\Fluffy Monster.jpg")));
        }
    }

    private async Task CleanTableStorage()
    {
        var serviceClient = new TableServiceClient(Setting.TableStorageConfig.ConnectionString);
        var tableNames = new List<string>() { Constants.GrainStorageProviderName, "OrleansProvider","PubSubStore","CheckPoint","ReminderStore" };

        foreach (var tableName in tableNames)
        {
            var tableClient = serviceClient.GetTableClient(FormattableString.Invariant($"{Setting.EnvironmentPrefix}{tableName}"));
            await tableClient.CreateIfNotExistsAsync();
            await DeleteAllEntitiesAsync(tableClient);
        }
    }

    private static async Task DeleteAllEntitiesAsync(TableClient tableClient)
    {
        AsyncPageable<TableEntity> entities = tableClient.QueryAsync<TableEntity>(select: new List<string>() { "PartitionKey", "RowKey" }, maxPerPage: 1000);
        var resultSegment = entities.AsPages();
        await foreach (var page in resultSegment)
        {
            await BatchManipulateEntities(tableClient, page.Values, TableTransactionActionType.Delete).ConfigureAwait(false);
        }
    }

    private static async Task<List<Response<IReadOnlyList<Response>>>> BatchManipulateEntities<T>(TableClient tableClient, IEnumerable<T> entities, TableTransactionActionType tableTransactionActionType) where T : class, ITableEntity, new()
    {
        var groups = entities.GroupBy(x => x.PartitionKey);
        var responses = new List<Response<IReadOnlyList<Response>>>();
        foreach (var group in groups)
        {
            List<TableTransactionAction> actions;
            var items = group.AsEnumerable();
            while (items.Any())
            {
                var batch = items.Take(100);
                items = items.Skip(100);

                actions = new List<TableTransactionAction>();
                actions.AddRange(batch.Select(e => new TableTransactionAction(tableTransactionActionType, e)));
                var response = await tableClient.SubmitTransactionAsync(actions).ConfigureAwait(false);
                responses.Add(response);
            }
        }
        return responses;
    }

    private async Task ClearBlobStorage()
    {
        await ClearBlobContainer(Invariant($"{Setting.EnvironmentPrefix.ToLowerInvariant()}"));
        await ClearBlobContainer(Invariant($"{Setting.EnvironmentPrefix}{Constants.GrainStorageProviderName}"));
    }

    private async Task ClearBlobContainer(string containerNameToClear)
    {
        var testUtility = new TestUtility(containerNameToClear);
        var blobNames = await testUtility.GetAllBlobNames();
        foreach (var blobId in blobNames)
        {
            await testUtility.ContainerClient!.DeleteBlobAsync(blobId);
        }
    }

    private static Mock<IPolicyVaultService> SetupPolicyVaultServiceMock()
    {
        var policyVaultServiceMock = new Mock<IPolicyVaultService>();
        policyVaultServiceMock.Setup(x => x.SetPolicyMnemonicAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        
        policyVaultServiceMock.Setup(x => x.GetPolicyMnemonicAsync(It.IsAny<string>()))
            .Returns(Task.FromResult("gain equal word blur next erosion virtual swing hammer cruel impulse cook normal yellow travel laptop unknown devote universe meat muscle clay outside dragon"));
        
        return policyVaultServiceMock;
    }

    private static Mock<IBlockchainService> SetupBlockchainServiceMock()
    {
        var blockChainMock = new Moq.Mock<IBlockchainService>();
        blockChainMock.Setup(x => x.GetSlotAndFeesAsync())
            .ReturnsAsync(new CardanoSlotAndFees(new CardanoSlot(10, DateTime.UtcNow), new CardanoNetworkFee(10, 50)));

        blockChainMock.Setup(x => x.SubmitTransactionAsync(It.IsAny<string>()))
            .ReturnsAsync("f927847f9d869ae39f3b35a190ed6f6854cc93f40b53dd217402e830c0a8f0df");

        blockChainMock.Setup(x => x.SubmitTransactionAsync("reject"))
            .Throws(new HttpRequestException("something bad just happened", null, HttpStatusCode.BadRequest));
        
        blockChainMock.Setup(x => x.SubmitTransactionAsync("successOnChain"))
            .ReturnsAsync("txIdWith7Confirmations");

        blockChainMock.Setup(x => x.GetTransactionAsync(It.IsAny<string>()))
            .ReturnsAsync(new Transaction("f927847f9d869ae39f3b35a190ed6f6854cc93f40b53dd217402e830c0a8f0df")
                { BlockHash = "aaa", BlockHeight = 100, BlockUtcTimestamp = DateTime.UtcNow, Slot = 10 });
        
        blockChainMock.Setup(x => x.GetTransactionAsync("txIdWith7Confirmations"))
            .ReturnsAsync(new Transaction("txIdWith7Confirmations")
                { BlockHash = "aaa", BlockHeight = 5, BlockUtcTimestamp = DateTime.UtcNow, Slot = 10 });

        uint? confirmationCount2 = 5;
        blockChainMock.Setup(x => x.GetConfirmationsAsync(5)).ReturnsAsync(confirmationCount2);

        uint? confirmationCount = 10;
        blockChainMock.Setup(x => x.GetConfirmationsAsync(It.IsAny<uint>())).ReturnsAsync(confirmationCount);
        return blockChainMock;
    }
}