using Enigmi.Application.ExtensionMethods;
using Enigmi.Application.Grains;
using Enigmi.Common;
using Enigmi.Common.Domain;
using Enigmi.Common.Messaging;
using Enigmi.Domain.Entities.ActivePuzzlePieceListAggregate.ValueObjects;
using Enigmi.Domain.Utils;
using Enigmi.Grains.Shared.ActivePuzzlePieceList;
using Enigmi.Grains.Shared.ActivePuzzlePieceList.Messages;
using Enigmi.Grains.Shared.PuzzleDefinition;
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

    public override string ResolveSubscriptionName(DomainEvent @event)
    {
        return string.Empty;
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

        var userWalletPuzzlePieces = command.PuzzlePieces.Select(x => ConvertToUserWalletPuzzlePiece(command.StakingAddress, x)).ToList();

        var activePuzzlePieces = State.DomainAggregate.ActivePuzzlePieces.ToList();

        var existingPuzzlePieceIds = activePuzzlePieces.Where(x => x.StakingAddress == command.StakingAddress).Select(x => x.PuzzlePieceId).ToList();
        var incomingPuzzlePieceIds = command.PuzzlePieces.Select(x => x.PuzzlePieceId).ToList();

        var puzzlePieceIdsToAdd = incomingPuzzlePieceIds.Except(existingPuzzlePieceIds);
        var puzzlePieceIdsToRemove = existingPuzzlePieceIds.Except(incomingPuzzlePieceIds);

        var removedPuzzlePieces = activePuzzlePieces.Where(x => puzzlePieceIdsToRemove.Contains(x.PuzzlePieceId)).ToList();
        State.DomainAggregate.RemovePuzzlePieces(removedPuzzlePieces);

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
        State.DomainAggregate.ThrowIfNull();

        foreach (var puzzleDefinitionId in missingDefinitionIds)
        {
            var puzzleDefinitionGrain = GrainFactory.GetGrain<IPuzzleDefinitionGrain>(puzzleDefinitionId);
            var puzzleDefinition = await puzzleDefinitionGrain.GetPuzzleDefinition();
            puzzleDefinition.ThrowIfNull();
            puzzleDefinitions.Add(puzzleDefinition);
        }

        foreach (var puzzleDefinition in puzzleDefinitions)
        {
            var puzzlePieceDefinitions = new List<PuzzleDefinition.PuzzlePieceDefinition>();

            foreach (var puzzlePieceDefinition in puzzleDefinition.PuzzlePieceDefinitions)
            {
                puzzlePieceDefinitions.Add(new PuzzleDefinition.PuzzlePieceDefinition(
                        puzzlePieceDefinition.Id,
                        puzzleDefinition.Id,
                        Invariant($"{Settings.BlobstorageConfig.BlobStorageHost}{BlobPathHelper.PrependBlobPathIfRequired(Settings, puzzlePieceDefinition.ImageBlobPath)}"),
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
                puzzlePieceDefinitions
            );

            State.DomainAggregate.AddPuzzleDefinition(puzzleDefinition.Id, data);
        }
    }

    private static UserWalletPuzzlePiece ConvertToUserWalletPuzzlePiece(string stakingAddress, UpdateActivePuzzlePiecesCommand.PuzzlePiece x)
    {
        return new UserWalletPuzzlePiece(
            stakingAddress,
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
                var puzzlePieceCount = State.DomainAggregate.ActivePuzzlePieces.Count(x => x.PuzzlePieceDefinitionId == puzzlePieceDefinition.Id);                    

                foreach (var stakeAddress in affectedStakingAddresses)
                {
                    var signalrMessage = new ActivePuzzlePieceUpdate(
                        stakeAddress,
                        puzzlePieceDefinition.Id,
                        puzzlePieceDefinition.PuzzleDefinitionId,
                        puzzlePieceDefinition.X,
                        puzzlePieceDefinition.Y,
                        DateTime.UtcNow,
                        puzzlePieceCount
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

        return GetStateForPuzzlePieces(walletPuzzlePieces, stakingAddress);
    }

    private Task<GetStateResponse> GetStateForPuzzlePieces(List<UserWalletPuzzlePiece> walletPuzzlePieces, string requestingStakingAddress)
    {
        var puzzlePieces = new List<GetStateResponse.PuzzlePiece>();
        var puzzleDefinitions = new List<GetStateResponse.PuzzleDefinition>();
        var puzzleDefinitionIds = walletPuzzlePieces.Select(x => x.PuzzleDefinitionId).Distinct().ToList();
        State.DomainAggregate.ThrowIfNull();

        foreach (var puzzleDefinitionId in puzzleDefinitionIds)
        {
            var puzzleDefinitionData = State.DomainAggregate.PuzzleDefinitionDataDictionary[puzzleDefinitionId];

            puzzleDefinitions.Add(new GetStateResponse.PuzzleDefinition(puzzleDefinitionId,
                puzzleDefinitionData.PuzzleName,
                puzzleDefinitionData.Size,
                puzzleDefinitionData.AvailablePuzzleBuilds,
                puzzleDefinitionData.NumberOfCompletedBuilds)
            );

            foreach (var puzzlePieceDefinition in puzzleDefinitionData.PuzzlePieceDefinitions)
            {
                var filteredActivePuzzlePieces = State.DomainAggregate.ActivePuzzlePieces
                    .Where(x => x.PuzzlePieceDefinitionId == puzzlePieceDefinition.Id)
                    .ToList();

                var requestingOwnerPuzzlePieceIds = filteredActivePuzzlePieces.Where(x => x.StakingAddress == requestingStakingAddress)
                    .Select(x => x.PuzzlePieceId).ToList();

                puzzlePieces.Add(new GetStateResponse.PuzzlePiece(
                    puzzlePieceDefinition.Id,
                    puzzlePieceDefinition.PuzzleDefinitionId,
                    puzzlePieceDefinition.ImageUrl,
                    puzzlePieceDefinition.X,
                    puzzlePieceDefinition.Y,
                    requestingOwnerPuzzlePieceIds,
                    filteredActivePuzzlePieces.Count));
            }
        }

        return Task.FromResult(new GetStateResponse(puzzlePieces, puzzleDefinitions));
    }

    public Task<GetStateResponse> GetActivePuzzlePieces(IEnumerable<string> puzzlePieceIds, string requestingStakingAddress)
    {
        puzzlePieceIds.ThrowIfNull();
        State.DomainAggregate.ThrowIfNull();
        var puzzlePieces = State.DomainAggregate.ActivePuzzlePieces.Where(x => puzzlePieceIds.Contains(x.PuzzlePieceId)).ToList();
        return GetStateForPuzzlePieces(puzzlePieces, requestingStakingAddress);
    }    

    public Task<GetPotentialTradeRequest> FindPotentialTrades(string initiatingStakingAddress, Guid puzzlePieceDefinitionId)
    {
        initiatingStakingAddress.ThrowIfNullOrWhitespace();
        puzzlePieceDefinitionId.ThrowIfEmpty();
        State.DomainAggregate.ThrowIfNull();

        var pieceIdsOwnedByInitiatingStakingAddress = State.DomainAggregate.ActivePuzzlePieces
            .Where(x => x.StakingAddress == initiatingStakingAddress)
            .ToList();

        var availableCounterpartyPuzzlePieceIds = State.DomainAggregate.ActivePuzzlePieces
            .Where(x => x.PuzzlePieceDefinitionId == puzzlePieceDefinitionId && x.StakingAddress != initiatingStakingAddress)
            .ToList();
        
        var potentialTradesList = new List<TradeDetail>();
        foreach (var counterpartyPieceId in availableCounterpartyPuzzlePieceIds)
        {
            foreach (var pieceIdOwnedByInitiatingStakingAddress in pieceIdsOwnedByInitiatingStakingAddress)
            {
                var pieceIdsOwnedByCounterPartyStakingAddress = State.DomainAggregate.ActivePuzzlePieces
                    .Where(x => x.StakingAddress == counterpartyPieceId.StakingAddress 
                                && x.PuzzlePieceDefinitionId == counterpartyPieceId.PuzzlePieceDefinitionId)
                    .ToList();
                
                var potentialTrade = GetPotentialTrade(pieceIdOwnedByInitiatingStakingAddress.PuzzlePieceId, 
                    counterpartyPieceId.PuzzlePieceId, 
                    pieceIdsOwnedByInitiatingStakingAddress, 
                    pieceIdsOwnedByCounterPartyStakingAddress);
                potentialTradesList.Add(potentialTrade);
            }
        }

        var top5Trades = potentialTradesList.OrderByDescending(pt => pt.Rating).Take(5).ToList();
        return Task.FromResult(new GetPotentialTradeRequest(top5Trades));
    }

    private TradeDetail GetPotentialTrade(string initiatingPuzzlePieceId, string counterpartyPuzzlePieceId,
        List<UserWalletPuzzlePiece> allPiecesOwnedByInitiatingStakingAddress,
        List<UserWalletPuzzlePiece> pieceIdsOwnedByCounterPartyStakingAddress)
    {
        initiatingPuzzlePieceId.ThrowIfNull();
        State.DomainAggregate.ThrowIfNull();

        var initiatingPuzzlePiece = allPiecesOwnedByInitiatingStakingAddress.Single(x => x.PuzzlePieceId == initiatingPuzzlePieceId);
        var counterPuzzlePiece = pieceIdsOwnedByCounterPartyStakingAddress.Single(x => x.PuzzlePieceId == counterpartyPuzzlePieceId);
        var allPiecesOwnedByCounterStakingAddress = State.DomainAggregate.ActivePuzzlePieces
            .Where(x => x.StakingAddress == counterPuzzlePiece.StakingAddress).ToList();

        var initiatingStakingAddressRating = CalcPuzzlePieceTradeRating(initiatingPuzzlePiece, allPiecesOwnedByInitiatingStakingAddress, allPiecesOwnedByCounterStakingAddress, counterPuzzlePiece.PuzzlePieceId);
        var counterStakingAddressRating = CalcPuzzlePieceTradeRating(counterPuzzlePiece, allPiecesOwnedByCounterStakingAddress, allPiecesOwnedByInitiatingStakingAddress, initiatingPuzzlePiece.PuzzlePieceId);
        
        var ownedTradePuzzlePiece = ConvertToTradePuzzlePiece(initiatingPuzzlePieceId, initiatingStakingAddressRating);
        var counterTradePuzzlePiece = ConvertToTradePuzzlePiece(counterpartyPuzzlePieceId, counterStakingAddressRating);

        var finalRating = (initiatingStakingAddressRating + counterStakingAddressRating) / 2M; 
        return new TradeDetail(ownedTradePuzzlePiece, counterTradePuzzlePiece, finalRating);
    }

    private decimal CalcPuzzlePieceTradeRating(UserWalletPuzzlePiece initiatingPuzzlePiece,
        List<UserWalletPuzzlePiece> allPiecesOwnedByInitiatingStakingAddress, 
        List<UserWalletPuzzlePiece> allPiecesOwnedByCounterPartyStakingAddress,
        string counterPartyPuzzlePieceId
        )
    {
        State.DomainAggregate.ThrowIfNull();
        var initiatingPuzzlePiecePuzzleDefinition = State.DomainAggregate.PuzzleDefinitionDataDictionary[initiatingPuzzlePiece.PuzzleDefinitionId];
        
        var rating = 0M;
        var isDuplicateLeaving = false;
        var isReceivingDuplicate = false;
        
        //10: Leaving Puzzle piece is a duplicate.
        if (allPiecesOwnedByInitiatingStakingAddress.Count(x => x.PuzzlePieceDefinitionId == initiatingPuzzlePiece.PuzzlePieceDefinitionId) > 1)
        {
            isDuplicateLeaving = true;
            rating = 10;
        }
        
        var numberOfPiecesOwnedWithinPuzzle = NumberOfPuzzlePiecesOwnedForPuzzleDefinition(allPiecesOwnedByInitiatingStakingAddress, initiatingPuzzlePiece);
        
        //0: Trade will result in a complete puzzle no longer being complete
        if (!isDuplicateLeaving)
        {
            if (initiatingPuzzlePiecePuzzleDefinition.Size == numberOfPiecesOwnedWithinPuzzle)
            {
                return 0;
            }
        }

        //0: Trade will result in a puzzle gaining a new duplicate piece 
        var counterPuzzlePiece = allPiecesOwnedByCounterPartyStakingAddress.Single(x => x.PuzzlePieceId == counterPartyPuzzlePieceId);
        if (allPiecesOwnedByInitiatingStakingAddress.Any(x => x.PuzzlePieceDefinitionId == counterPuzzlePiece.PuzzlePieceDefinitionId
            && x.PuzzlePieceId != initiatingPuzzlePiece.PuzzlePieceId))
        {
            rating = 0;
            isReceivingDuplicate = true;
        }

        if (isDuplicateLeaving && isReceivingDuplicate)
        {
            return 5; //It might be the case that both 0 and 10 is true. Ie you could give out a duplicate (10) and get a new duplicate. This should result in (0+10)/2 = 5. ie its not good or bad ...
        }
        else if (isDuplicateLeaving || isReceivingDuplicate)
        {
            return rating;
        }
        
        //1 - 9
        var missingPiecesValue = (initiatingPuzzlePiecePuzzleDefinition.Size - numberOfPiecesOwnedWithinPuzzle) / Convert.ToDecimal(initiatingPuzzlePiecePuzzleDefinition.Size);
        var ownedPiecesValue = numberOfPiecesOwnedWithinPuzzle / Convert.ToDecimal(initiatingPuzzlePiecePuzzleDefinition.Size);

        return (ownedPiecesValue + missingPiecesValue) / 2M * 10M;
    }

    private int NumberOfPuzzlePiecesOwnedForPuzzleDefinition(List<UserWalletPuzzlePiece> allPiecesOwnedByInitiatingStakingAddress,
        UserWalletPuzzlePiece initiatingPuzzlePiece)
    {
        State.DomainAggregate.ThrowIfNull();
        
        //var puzzlePieceDefinition = State.DomainAggregate.PuzzleDefinitionDataDictionary[initiatingPuzzlePiece.PuzzleDefinitionId];

        var distinctOwnedPuzzlePieceDefinitionsCount = allPiecesOwnedByInitiatingStakingAddress
            .Where(x => x.PuzzleDefinitionId == initiatingPuzzlePiece.PuzzleDefinitionId)
            .Select(x => x.PuzzlePieceDefinitionId)
            .Distinct()
            .Count();

        return distinctOwnedPuzzlePieceDefinitionsCount;

        // if (distinctOwnedPuzzlePieceDefinitionsCount == puzzlePieceDefinition.Size)
        // {
        //     return true;
        // }
        //
        // return false;
    }

    private TradePuzzlePiece ConvertToTradePuzzlePiece(string initiatingPuzzlePieceId,
        decimal rating)
    {
        State.DomainAggregate.ThrowIfNull();
        
        var ownedPuzzlePiece = State.DomainAggregate.ActivePuzzlePieces.Single(x => x.PuzzlePieceId == initiatingPuzzlePieceId);
        var puzzleDefinition = State.DomainAggregate.PuzzleDefinitionDataDictionary[ownedPuzzlePiece.PuzzleDefinitionId];
        var ownedTradePuzzlePiece = new TradePuzzlePiece(initiatingPuzzlePieceId, ownedPuzzlePiece.PuzzleDefinitionId, puzzleDefinition.PuzzleName, Guid.NewGuid(), "", ownedPuzzlePiece.StakingAddress, rating);
        return ownedTradePuzzlePiece;
    }
}

