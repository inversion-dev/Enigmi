using CardanoSharp.Wallet.CIPs.CIP14.Extensions;
using Domain.ValueObjects;
using Enigmi.Common;
using Enigmi.Common.Domain;
using Enigmi.Common.Messaging;
using Enigmi.Common.Utils;
using Enigmi.Domain.Entities.PuzzleCollectionAggregate.Events;
using Enigmi.Domain.Entities.PuzzleDefinitionAggregate;
using Enigmi.Domain.Entities.PuzzleDefinitionAggregate.Events;
using Enigmi.Domain.Entities.PuzzlePieceDispenserAggregate;
using Enigmi.Grains.Shared.PuzzleCollection;
using Enigmi.Grains.Shared.PuzzleDefinition;
using Enigmi.Grains.Shared.PuzzleDefinition.Messages;
using Enigmi.Grains.Shared.PuzzlePiece;
using Enigmi.Grains.Shared.PuzzlePiece.Messages;
using Enigmi.Grains.Shared.PuzzlePieceDispenser;
using Enigmi.Infrastructure.Services.BlobStorage;
using Enigmi.Infrastructure.Services.ImageProcessing;
using Microsoft.Extensions.Logging;
using Orleans.Providers;
using Orleans.Runtime;
using Enums = Enigmi.Common.Domain.Enums;

namespace Enigmi.Application.Grains;

[StorageProvider(ProviderName = Constants.GrainStorageProviderName)]
public class PuzzleDefinitionGrain : GrainBase<PuzzleDefinition>, IPuzzleDefinitionGrain
{
    private ILogger<PuzzleDefinitionGrain> Logger { get; }

    private IBlobStorageService BlobStorageService { get; }

    private IImageProcessingService ImageProcessingService { get; }

    public PuzzleDefinitionGrain(
        IBlobStorageService blobStorageService,
        IImageProcessingService imageProcessingService,
        ILogger<PuzzleDefinitionGrain> logger)
    {
        Logger = logger;
        BlobStorageService = blobStorageService.ThrowIfNull();
        ImageProcessingService = imageProcessingService.ThrowIfNull();
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (State.DomainAggregate != null)
        {
            await Subscribe<PuzzlePiecePolicyAdded>(State.DomainAggregate.PuzzleCollectionId.ToString(), OnPuzzlePiecePolicyAdded);
        }

        await base.OnActivateAsync(cancellationToken);
    }

    public Task<ResultOrError<PingPuzzleDefinitionResponse>> Ping(PingPuzzleDefinitionCommand command)
    {
        return Task.FromResult(new PingPuzzleDefinitionResponse().ToSuccessResponse());
    }

    public Task<PuzzleDefinition?> GetPuzzleDefinition()
    {
        return Task.FromResult(State.DomainAggregate);
    }

    public async Task<ResultOrError<Constants.Unit>> ResetState()
    {
        if (State.DomainAggregate != null)
        {
            foreach (var puzzlePieceId in State.DomainAggregate.PuzzlePieceIds)
            {
                var puzzlePieceGrain = GrainFactory.GetGrain<IPuzzlePieceGrain>(puzzlePieceId);
                await puzzlePieceGrain.ResetState();
            }

            await this.UnsubscribeAll();
            await this.ClearStateAsync();
        }

        return new Constants.Unit().ToSuccessResponse();
    }

    public async Task<ResultOrError<CreatePuzzleDefinitionResponse>> CreatePuzzleDefinition(CreatePuzzleDefinitionCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        Logger.LogInformation("CreatePuzzleDefinition with {command}", command);

        if (State.DomainAggregate != null)
            throw new ApplicationException($"{State.DomainAggregate.GetType().Name} with id '{this.GetGrainId().GetGuidKey()}' has already been created'.");

        var config = await BlobStorageService.DownloadBlob<PuzzleDefinitionDto>(command.ConfigSourceBlobPath);

        Guid puzzleDefinitionId = this.GetGrainId().GetGuidKey();
        var puzzleDefinition = new PuzzleDefinition(puzzleDefinitionId, command.PuzzleCollectionId, config.Title, config.PuzzleSize, config.NumberOfPuzzlePiecesTarget);

        State.DomainAggregate = puzzleDefinition;
        await Subscribe<PuzzlePiecePolicyAdded>(State.DomainAggregate.PuzzleCollectionId.ToString(), OnPuzzlePiecePolicyAdded);

        Logger.LogInformation("Processing image for {PuzzleCollectionId}", command.PuzzleCollectionId);
        await ProcessImage(command.ImageSourceBlobPath);

        if (!string.IsNullOrEmpty(config.BlockchainMetadataJson))
        {
            puzzleDefinition.SetBlockchainMetadataJson(config.BlockchainMetadataJson);
        }

        if (config.NumberOfAllowedBuilds > 0)
        {
            puzzleDefinition.SetNumberOfAllowedBuilds(config.NumberOfAllowedBuilds);
        }

        Logger.LogInformation("Generate puzzle piece definition images for {PuzzleCollectionId}", command.PuzzleCollectionId);
        await this.GeneratePuzzlePieceDefinitionImages();

        await WriteStateAsync();
        Logger.LogInformation("Created puzzle definition for {PuzzleCollectionId}", command.PuzzleCollectionId);

        return new CreatePuzzleDefinitionResponse().ToSuccessResponse();
    }

    private async Task ProcessImage(string sourceBlobPath)
    {
        await CopySource(sourceBlobPath);
        using var normalizedMemoryStream = await CreateNormalizedImage(sourceBlobPath);
        await CreateThumbnail(normalizedMemoryStream);
    }

    private async Task CopySource(string sourceBlobPath)
    {
        var destination = this.State.DomainAggregate!.GetOriginalBlobPath();
        await BlobStorageService.CopyBlobAsync(sourceBlobPath, destination);
        this.State.DomainAggregate.SetOriginalImageBlobPath(destination);
    }

    private async Task<MemoryStream> CreateNormalizedImage(string sourceBlobPath)
    {
        var destination = this.State.DomainAggregate!.GetNormalizedBlobPath();

        var memoryStream = new MemoryStream();
        await BlobStorageService.DownloadBlobToStream(sourceBlobPath, memoryStream);

        var originalBytes = memoryStream.ToArray();

        var maxLength = 800;
        var squareImage = ImageProcessingService.NormalizeImage(originalBytes, maxLength);
        await BlobStorageService.UploadFileAsync(destination, squareImage, true);

        this.State.DomainAggregate.SetNormalizedImage(new(destination, new Dimension(maxLength, maxLength)));
        return new MemoryStream(squareImage);
    }

    private async Task CreateThumbnail(MemoryStream memoryStream)
    {
        var destination = this.State.DomainAggregate!.GetThumbnailBlobPath();

        var thumbnailImageBytes = this.ImageProcessingService.ResizeImage(memoryStream.ToArray(), 300, 300);
        await BlobStorageService.UploadFileAsync(destination, thumbnailImageBytes.ToArray(), true);

        this.State.DomainAggregate.SetThumbnail(new(destination, new(300, 300)));
    }

    private async Task GeneratePuzzlePieceDefinitionImages()
    {
        var puzzleDefinition = State.DomainAggregate.ThrowIfNull();
        puzzleDefinition.NormalizedImage.ThrowIfNull();

        using var memoryStream = new MemoryStream();
        await BlobStorageService.DownloadBlobToStream(puzzleDefinition.NormalizedImage.Value.BlobPath!, memoryStream);

        ParallelOptions options = new ParallelOptions()
        {
            TaskScheduler = TaskScheduler.Current
        };

        await Parallel.ForEachAsync(puzzleDefinition.PuzzlePieceDefinitions, options, async (puzzlePieceDefinition, _) =>
        {
            var puzzlePieceImage = GeneratePuzzlePieceImage(memoryStream.ToArray(), puzzlePieceDefinition);
            await BlobStorageService.UploadFileAsync(puzzlePieceDefinition.ImageBlobPath, puzzlePieceImage, true);
        });
    }

    private byte[] GeneratePuzzlePieceImage(byte[] imageBytes, PuzzlePieceDefinition puzzlePieceDefinition)
    {
        var (position, dimension) = State.DomainAggregate!.GetPuzzlePieceDefinitionPosition(puzzlePieceDefinition);

        return this.ImageProcessingService.CropImage(imageBytes, position.X, position.Y, dimension.Width, dimension.Height);
    }

    private class PuzzleDefinitionDto
    {
        public string Title { get; set; } = null!;

        public string? PolicyId { get; set; }

        public int PuzzleSize { get; set; }

        public Enums.PublicationState State { get; set; }

        public int NumberOfAllowedBuilds { get; set; }

        public int NumberOfPuzzlePiecesTarget { get; set; }

        public string? BlockchainMetadataJson { get; set; }
    }

    private async Task OnPuzzlePiecePolicyAdded(PuzzlePiecePolicyAdded @event)
    {
        var puzzleDefinition = State.DomainAggregate;
        puzzleDefinition.ThrowIfNull();
        
        await GeneratePuzzlePieces();
        puzzleDefinition.MarkAsPublished();

        await this.WriteStateAsync();
    }

    private async Task GeneratePuzzlePieces()
    {
        var puzzleDefinition = State.DomainAggregate;
        puzzleDefinition.ThrowIfNull();

        //TODO Please note for future reference the message at the top: https://dev.azure.com/Inversion-Limited/Enigmi/_workitems/edit/154
        var puzzleCollectionGrain = GrainFactory.GetGrain<IPuzzleCollectionGrain>(puzzleDefinition.PuzzleCollectionId);
        var puzzleCollection = await puzzleCollectionGrain.GetPuzzleCollection();
        puzzleCollection.ThrowIfNull();
        puzzleCollection.PuzzlePolicy.ThrowIfNull();
        puzzleCollection.PuzzlePolicy.PolicyId.ThrowIfNullOrWhitespace();

        foreach (var puzzlePieceDefinition in puzzleDefinition.PuzzlePieceDefinitions)
        {
            for (int i = 0; i < puzzleDefinition.NumberOfPuzzlePiecesTarget; i++)
            {
                var assetName = Guid.NewGuid().ToString("N");
                var blockchainAssetId = CardanoHelper.GetAssetId(puzzleCollection.PuzzlePiecePolicy.PolicyId, assetName);
                var puzzlePieceId = AssetExtensions.ToAssetFingerprint(blockchainAssetId);
                var puzzlePieceGrain = this.GrainFactory.GetGrain<IPuzzlePieceGrain>(puzzlePieceId);
                await puzzlePieceGrain.CreatePuzzlePiece(new CreatePuzzlePieceCommand(puzzlePieceDefinition.Id,
                    puzzleDefinition.Id, blockchainAssetId.ThrowIfNullOrWhitespace()));
                puzzleDefinition.AddPuzzlePieceId(puzzlePieceId);
            }
        }

        var puzzlePieceDispenserGrain = GrainFactory.GetGrain<IPuzzlePieceDispenserGrain>(
            PuzzlePieceDispenser.GetId(puzzleDefinition.PuzzleCollectionId, puzzleDefinition.PuzzleSize));
        await puzzlePieceDispenserGrain.AddStock(puzzleDefinition.PuzzlePieceIds);
    }

    public override string ResolveSubscriptionName(DomainEvent @event)
    {
        @event.ThrowIfNull();
        State.DomainAggregate.ThrowIfNull();

        string subscriptionName = @event switch
        {
            PuzzleDefinitionPublished => State.DomainAggregate.PuzzleCollectionId.ToString(),
            _ => string.Empty,
        };

        return subscriptionName;
    }
}