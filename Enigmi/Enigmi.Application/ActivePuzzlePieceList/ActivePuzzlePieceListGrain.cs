using Enigmi.Application.ExtensionMethods;
using Enigmi.Application.Grains;
using Enigmi.Common;
using Enigmi.Common.Domain;
using Enigmi.Common.Messaging;
using Enigmi.Domain.Entities.ActivePuzzlePieceListAggregate.ValueObjects;
using Enigmi.Domain.Utils;
using Enigmi.Domain.ValueObjects;
using Enigmi.Grains.Shared.ActivePuzzlePieceList;
using Enigmi.Grains.Shared.ActivePuzzlePieceList.Messages;
using Enigmi.Grains.Shared.ActiveUtxoReservationsList;
using Enigmi.Grains.Shared.GrainSettings;
using Enigmi.Grains.Shared.PuzzleCollection;
using Enigmi.Grains.Shared.PuzzleDefinition;
using Enigmi.Grains.Shared.PuzzlePiece;
using Enigmi.Grains.Shared.UserWallet;
using Enigmi.Grains.Shared.UserWallet.Messages;
using Enigmi.Infrastructure.Extensions;
using Enigmi.Infrastructure.Services.SignalR;
using Enigmi.Messages.SignalRMessage;
using Orleans.Providers;

namespace Enigmi.Application.ActivePuzzlePieceList;

[StorageProvider(ProviderName = Constants.GrainStorageProviderName)]
public class ActivePuzzlePieceListGrain : GrainBase<Domain.Entities.ActivePuzzlePieceListAggregate.ActivePuzzlePieceList>, IActivePuzzlePieceListGrain
{
    private ISignalRHubContextStore SignalRHubContextStore { get; }

    private Settings Settings { get; }

    private List<ActivePuzzlePieceUpdate> SignalrMessages { get; } = new();

    public ActivePuzzlePieceListGrain(ISignalRHubContextStore signalRHubContextStore, Settings settings)
    {
        SignalRHubContextStore = signalRHubContextStore.ThrowIfNull();
        Settings = settings.ThrowIfNull();
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (State.DomainAggregate == null)
        {
            State.DomainAggregate = new Domain.Entities.ActivePuzzlePieceListAggregate.ActivePuzzlePieceList();
            await WriteStateAsync();
        }

        var puzzleDefinitionIds = State.DomainAggregate.PuzzleDefinitionDataDictionary.Select(x => x.Key).ToList();
        var puzzlePieceDefinitionIds = State.DomainAggregate.ActivePuzzlePieces.Select(x => x.PuzzlePieceDefinitionId).Distinct().ToList();
        QueueUpdatePuzzlePieceUpdateSignalrMessages(puzzleDefinitionIds, puzzlePieceDefinitionIds);

        RegisterTimer(ProcessMessageQueueHandler, this, TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(500));
        await base.OnActivateAsync(cancellationToken);
    }

    private async Task ProcessMessageQueueHandler(object arg)
    {
        await this.SelfInvokeAfter<IActivePuzzlePieceListGrain>(o => o.ProcessSignalRMessageQueue());
    }

    public override IEnumerable<string> ResolveSubscriptionNames(DomainEvent @event)
    {
        return string.Empty.ToSingletonList();
    }

    public async Task<ResultOrError<UpdateActivePuzzlePiecesResponse>> UpdateActivePuzzlePieces(UpdateActivePuzzlePiecesCommand command)
    {
        command.ThrowIfNull();
        command.PuzzlePieces.ThrowIfNull();
        State.DomainAggregate.ThrowIfNull();

        var missingPuzzleDefinitionIds = DetermineMissingPuzzleDefinitionIds(command);

        if (missingPuzzleDefinitionIds.Any())
        {
            await LoadMissingLookupPuzzleDefinitionInformation(missingPuzzleDefinitionIds);
        }

        var userWalletPuzzlePieces = command.PuzzlePieces.Select(x => ConvertToUserWalletPuzzlePiece(command.StakingAddress, command.Nickname, x)).ToList();

        var activePuzzlePieces = State.DomainAggregate.ActivePuzzlePieces.ToList();

        var existingPuzzlePieceIds = activePuzzlePieces.Where(x => x.StakingAddress == command.StakingAddress).Select(x => x.PuzzlePieceId).ToList();
        var incomingPuzzlePieceIds = command.PuzzlePieces.Select(x => x.PuzzlePieceId).ToList();

        var puzzlePieceIdsToAdd = incomingPuzzlePieceIds.Except(existingPuzzlePieceIds);
        var puzzlePieceIdsToRemove = existingPuzzlePieceIds.Except(incomingPuzzlePieceIds);

        var removedPuzzlePieces = activePuzzlePieces.Where(x => puzzlePieceIdsToRemove.Contains(x.PuzzlePieceId)).ToList();
        State.DomainAggregate.RemovePuzzlePieces(removedPuzzlePieces, command.StakingAddress);

        var addedPuzzlePieces = userWalletPuzzlePieces.Where(x => puzzlePieceIdsToAdd.Contains(x.PuzzlePieceId)).ToList();
        State.DomainAggregate.AddPuzzlePieces(addedPuzzlePieces);

        var affectedPuzzleDefinitionIds = addedPuzzlePieces.Select(x => x.PuzzleDefinitionId).Union(removedPuzzlePieces.Select(x => x.PuzzleDefinitionId)).Distinct().ToList();
        var affectedPuzzlePieceDefinitionIds = addedPuzzlePieces.Select(x => x.PuzzlePieceDefinitionId)
            .Union(removedPuzzlePieces.Select(y => y.PuzzlePieceDefinitionId).ToList())
            .Distinct()
            .ToList();

        QueueUpdatePuzzlePieceUpdateSignalrMessages(affectedPuzzleDefinitionIds, affectedPuzzlePieceDefinitionIds);
        await WriteStateAsync();

        return new UpdateActivePuzzlePiecesResponse().ToSuccessResponse();
    }

    private HashSet<Guid> DetermineMissingPuzzleDefinitionIds(UpdateActivePuzzlePiecesCommand command)
    {
        State.DomainAggregate.ThrowIfNull();
        var missingPuzzleDefinitionIds = new HashSet<Guid>();
        foreach (var puzzlePiece in command.PuzzlePieces)
        {
            if (!State.DomainAggregate.PuzzleDefinitionDataDictionary.ContainsKey(puzzlePiece.PuzzleDefinitionId))
            {
                missingPuzzleDefinitionIds.Add(puzzlePiece.PuzzleDefinitionId);
            }
        }

        return missingPuzzleDefinitionIds;
    }

    private async Task LoadMissingLookupPuzzleDefinitionInformation(HashSet<Guid> missingDefinitionIds)
    {
        var puzzleDefinitions = new List<Enigmi.Domain.Entities.PuzzleDefinitionAggregate.PuzzleDefinition>();
        var puzzleCollections = new List<Domain.Entities.PuzzleCollectionAggregate.PuzzleCollection>();
        State.DomainAggregate.ThrowIfNull();

        foreach (var puzzleDefinitionId in missingDefinitionIds)
        {
            var puzzleDefinitionGrain = GrainFactory.GetGrain<IPuzzleDefinitionGrain>(puzzleDefinitionId);
            var puzzleDefinition = await puzzleDefinitionGrain.GetPuzzleDefinition();
            puzzleDefinition.ThrowIfNull();
            puzzleDefinitions.Add(puzzleDefinition);

            if (!puzzleCollections.Any(x => x.Id == puzzleDefinition.PuzzleCollectionId))
            {
                var puzzleCollectionGrain = GrainFactory.GetGrain<IPuzzleCollectionGrain>(puzzleDefinition.PuzzleCollectionId);
                var puzzleCollection = await puzzleCollectionGrain.GetPuzzleCollection();
                puzzleCollection.ThrowIfNull();
                puzzleCollections.Add(puzzleCollection);
            }
        }

        foreach (var puzzleDefinition in puzzleDefinitions)
        {
            var puzzlePieceDefinitions = new List<PuzzleDefinition.PuzzlePieceDefinition>();
            var puzzleCollection = puzzleCollections.Single(x => x.Id == puzzleDefinition.PuzzleCollectionId);

            foreach (var puzzlePieceDefinition in puzzleDefinition.PuzzlePieceDefinitions)
            {
                puzzlePieceDefinitions.Add(new PuzzleDefinition.PuzzlePieceDefinition(
                        puzzlePieceDefinition.Id,
                        puzzleDefinition.Id,
                        Invariant($"{Settings.BlobstorageConfig.CustomDomainRootUrl}{BlobPathHelper.PrependBlobPathIfRequired(Settings, puzzlePieceDefinition.ImageBlobPath)}"),
                        puzzlePieceDefinition.GridX,
                        puzzlePieceDefinition.GridY
                    )
                );
            }

            var data = new PuzzleDefinition(
                puzzleDefinition.Title,
                puzzleDefinition.PuzzleSize,
                puzzleDefinition.NumberOfAllowedBuilds - puzzleDefinition.NumberOfCompletedBuilds,
                puzzleDefinition.NumberOfCompletedBuilds,
                puzzlePieceDefinitions,
                puzzleCollection.Id,
                puzzleCollection.Title
            );

            State.DomainAggregate.AddPuzzleDefinition(puzzleDefinition.Id, data);
        }
    }

    private static UserWalletPuzzlePiece ConvertToUserWalletPuzzlePiece(string stakingAddress, string nickName, UpdateActivePuzzlePiecesCommand.PuzzlePiece x)
    {
        return new UserWalletPuzzlePiece(
            stakingAddress,
            nickName,
            x.PuzzlePieceId,
            x.PuzzleDefinitionId,
            x.PuzzlePieceDefinitionId
        );
    }

    private void QueueUpdatePuzzlePieceUpdateSignalrMessages(List<Guid> puzzleDefinitionIds, List<Guid> puzzlePieceDefinitionIds)
    {
        State.DomainAggregate.ThrowIfNull();

        foreach (var puzzleDefinitionId in puzzleDefinitionIds)
        {
            var puzzleDefinition = State.DomainAggregate.PuzzleDefinitionDataDictionary[puzzleDefinitionId];
            var affectedStakingAddresses = State.DomainAggregate.ActivePuzzlePieces.Where(x => x.PuzzleDefinitionId == puzzleDefinitionId).Select(x => x.StakingAddress)
                .Distinct()
                .ToList();

            var puzzlePieceDefinitions = puzzleDefinition.PuzzlePieceDefinitions
                .Where(x => puzzlePieceDefinitionIds.Contains(x.Id)).ToList();

            foreach (var puzzlePieceDefinition in puzzlePieceDefinitions)
            {
                var activePuzzlePieces = State.DomainAggregate.ActivePuzzlePieces.Where(x => x.PuzzlePieceDefinitionId == puzzlePieceDefinition.Id)
                    .ToList();                    

                foreach (var stakingAddress in affectedStakingAddresses)
                {
                    var signalrMessage = new ActivePuzzlePieceUpdate(
                        stakingAddress,
                        puzzlePieceDefinition.Id,
                        puzzlePieceDefinition.PuzzleDefinitionId,
                        puzzlePieceDefinition.X,
                        puzzlePieceDefinition.Y,
                        DateTime.UtcNow,
                        activePuzzlePieces.Count(),
                        activePuzzlePieces.Count(x => x.StakingAddress == stakingAddress)
                    );

                    SignalrMessages.Add(signalrMessage);
                }
            }
        }
    }

    public async Task ProcessSignalRMessageQueue()
    {
        var groupedByPuzzleDefinition = SignalrMessages.GroupBy(x => new { x.PuzzlePieceDefinitionId, x.StakingAddress })
            .Select(x => new { Key = x.Key, List = x.ToList() }).ToList();

        foreach (var groupedMessage in groupedByPuzzleDefinition)
        {
            var latestMessage = groupedMessage.List.OrderByDescending(x => x.UtcTimestamp).First();
            await SignalRHubContextStore.MessageHubContext!.Clients.Users(new[] { groupedMessage.Key.StakingAddress }).SendAsync(latestMessage);
        }

        SignalrMessages.Clear();
    }

    public Task<GetStateResponse> GetActivePuzzlePieces(string stakingAddress)
    {
        State.DomainAggregate.ThrowIfNull();
        stakingAddress.ThrowIfNullOrEmpty();
        var walletPuzzlePieces = State.DomainAggregate.ActivePuzzlePieces.Where(x => x.StakingAddress == stakingAddress).ToList();

        return GetStateForPuzzlePieces(walletPuzzlePieces);
    }

    private Task<GetStateResponse> GetStateForPuzzlePieces(List<UserWalletPuzzlePiece> walletPuzzlePieces)
    {
        var puzzlePieces = new List<GetStateResponse.PuzzlePiece>();
        var puzzleDefinitions = new List<GetStateResponse.PuzzleDefinition>();
        var puzzleDefinitionIds = walletPuzzlePieces.Select(x => x.PuzzleDefinitionId).Distinct().ToList();
        State.DomainAggregate.ThrowIfNull();

        var stakingAddress = string.Empty;
        if (walletPuzzlePieces.Any())
        {
            stakingAddress = walletPuzzlePieces.First().StakingAddress;
        }

        foreach (var puzzleDefinitionId in puzzleDefinitionIds)
        {
            var puzzleDefinitionData = State.DomainAggregate.PuzzleDefinitionDataDictionary[puzzleDefinitionId];

            puzzleDefinitions.Add(new GetStateResponse.PuzzleDefinition(puzzleDefinitionId,
                puzzleDefinitionData.PuzzleName,
                puzzleDefinitionData.PuzzleCollectionTitle,
                puzzleDefinitionData.Size,
                puzzleDefinitionData.AvailablePuzzleBuilds,
                puzzleDefinitionData.NumberOfCompletedBuilds)
            );

            foreach (var puzzlePieceDefinition in puzzleDefinitionData.PuzzlePieceDefinitions)
            {
                var activePuzzlePiecesForPieceDefinition = State.DomainAggregate.ActivePuzzlePieces
                    .Where(x => x.PuzzlePieceDefinitionId == puzzlePieceDefinition.Id)
                    .ToList();

                var requestingOwnerPuzzlePieceIds = activePuzzlePiecesForPieceDefinition
                    .Where(x => x.StakingAddress == stakingAddress 
                                && walletPuzzlePieces.Any(p => p.PuzzlePieceId == x.PuzzlePieceId) )
                    .Select(x => x.PuzzlePieceId).ToList();

                puzzlePieces.Add(new GetStateResponse.PuzzlePiece(
                    puzzlePieceDefinition.Id,
                    puzzlePieceDefinition.PuzzleDefinitionId,
                    puzzlePieceDefinition.ImageUrl,
                    puzzlePieceDefinition.X,
                    puzzlePieceDefinition.Y,
                    requestingOwnerPuzzlePieceIds,
                    activePuzzlePiecesForPieceDefinition.Count));
            }
        }

        return Task.FromResult(new GetStateResponse(puzzlePieces, puzzleDefinitions));
    }

    public Task<GetStateResponse> GetActivePuzzlePieces(IEnumerable<string> puzzlePieceIds)
    {
        puzzlePieceIds.ThrowIfNull();

        State.DomainAggregate.ThrowIfNull();
        var puzzlePieces = State.DomainAggregate.ActivePuzzlePieces.Where(x => puzzlePieceIds.Contains(x.PuzzlePieceId)).ToList();
        return GetStateForPuzzlePieces(puzzlePieces);
    }

    public Task<ResultOrError<GetPuzzleDefinitionsResponse>> GetPuzzleDefinitions(IEnumerable<Guid> puzzlePieceDefinitionIds)
    {
        puzzlePieceDefinitionIds.ThrowIfNull();

        State.DomainAggregate.ThrowIfNull();
        var puzzleDefinitions = State.DomainAggregate.PuzzleDefinitionDataDictionary.Where(x => puzzlePieceDefinitionIds.Contains(x.Key)).ToList();
        var response = new GetPuzzleDefinitionsResponse(puzzleDefinitions.Select(x => new GetPuzzleDefinitionsResponse.PuzzleDefinition(
            x.Key,
            x.Value.PuzzleName,
            x.Value.PuzzleCollectionTitle,
            x.Value.Size,
            x.Value.AvailablePuzzleBuilds,
            x.Value.NumberOfCompletedBuilds,
            x.Value.PuzzlePieceDefinitions.Select(p => new GetPuzzleDefinitionsResponse.PuzzlePieceDefinition(
                p.Id,
                p.PuzzleDefinitionId,
                p.ImageUrl
            )).ToList()
        )).ToList());

        return Task.FromResult(response.ToSuccessResponse());
    }

    public async Task<GetPotentialTradeResponse> FindPotentialTrades(string initiatingStakingAddress, Guid puzzlePieceDefinitionId)
    {
        State.DomainAggregate.ThrowIfNull();
        var grainSettingsGrain = GrainFactory.GetGrain<IGrainSettingsGrain>(Constants.SingletonGrain);
        var settings = await grainSettingsGrain.GetSettings();
        
        var activeUtxoReservationsListGrain = GrainFactory.GetGrain<IActiveUtxoReservationsListGrain>(Constants.ActiveUtxoReservationsListGrainKey);
        var reservedPuzzlePieces = await activeUtxoReservationsListGrain.GetReservedPuzzlePieces();
        
        var topTrades = State.DomainAggregate.FindPotentialTrades(
                    initiatingStakingAddress.ThrowIfNullOrWhitespace(), 
                    puzzlePieceDefinitionId.ThrowIfEmpty(),
                    settings.ActivePuzzlePieceList.MaxTradeDetailsReturnCount,
                    settings.ActivePuzzlePieceList.MaxStakingAddressAddressReturnCount,
                    reservedPuzzlePieces
                    );
        
        return new GetPotentialTradeResponse(topTrades);
    }

    public async Task<TradeDetail?> GetPotentialTrade(
        string initiatingStakingAddress,
        string initiatingPuzzlePieceId,
        string counterpartyPuzzlePieceId,
        string counterpartyPieceStakingAddress)
    {
        State.DomainAggregate.ThrowIfNull();
        
        var puzzlePieceGrain = GrainFactory.GetGrain<IPuzzlePieceGrain>(counterpartyPuzzlePieceId);
        var counterPuzzlePiece = await puzzlePieceGrain.GetPuzzlePiece();
        counterPuzzlePiece.ThrowIfNull();

        var relevantPiecesOwnedByCounterpartyStakingAddress =
            State.DomainAggregate.ActivePuzzlePieces.Where(x => x.StakingAddress == counterpartyPieceStakingAddress
                && x.PuzzlePieceDefinitionId == counterPuzzlePiece.PuzzlePieceDefinitionId).ToList();
        
        var allPiecesOwnedByInitiatingStakingAddress = State.DomainAggregate.ActivePuzzlePieces.Where(x => x.StakingAddress == initiatingStakingAddress).ToList();
        
        return State.DomainAggregate.GetPotentialTrade(initiatingPuzzlePieceId, 
            counterpartyPuzzlePieceId, 
            allPiecesOwnedByInitiatingStakingAddress, 
            relevantPiecesOwnedByCounterpartyStakingAddress,
            counterpartyPieceStakingAddress);
    }
}

