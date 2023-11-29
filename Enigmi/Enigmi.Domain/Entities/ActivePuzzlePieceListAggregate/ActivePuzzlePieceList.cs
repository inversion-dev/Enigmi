using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Linq;
using Enigmi.Common;
using Enigmi.Common.Domain;
using Enigmi.Domain.Entities.ActivePuzzlePieceListAggregate.ValueObjects;
using Enigmi.Domain.ValueObjects;
using Newtonsoft.Json;

namespace Enigmi.Domain.Entities.ActivePuzzlePieceListAggregate;

public class ActivePuzzlePieceList : DomainEntity
{
    private List<UserWalletPuzzlePiece> _activePuzzlePieces = new();
    
    [JsonProperty]
    public IEnumerable<UserWalletPuzzlePiece> ActivePuzzlePieces
    {
        get { return _activePuzzlePieces.AsReadOnly(); }
        private set { _activePuzzlePieces = value.ToList(); }
    }
    
    [JsonProperty]
    private Dictionary<Guid, PuzzleDefinition> _puzzleDefinitionDataDictionary = new();

    [JsonIgnore]
    public ReadOnlyDictionary<Guid, PuzzleDefinition> PuzzleDefinitionDataDictionary
    {
        get { return _puzzleDefinitionDataDictionary.AsReadOnly(); }
        private set { _puzzleDefinitionDataDictionary = value.ToDictionary(kvp => kvp.Key, kvp => kvp.Value); }
    }

    public void AddPuzzlePieces(List<UserWalletPuzzlePiece> puzzlePieces)
    {
        puzzlePieces.ThrowIfNull();
        _activePuzzlePieces.AddRange(puzzlePieces);
    }
    
    public void RemovePuzzlePieces(List<UserWalletPuzzlePiece> puzzlePieces, string stakingAddress)
    {
        puzzlePieces.ThrowIfNull();
        _activePuzzlePieces.RemoveAll(x => puzzlePieces.Any(y => y.PuzzlePieceId == x.PuzzlePieceId) && x.StakingAddress == stakingAddress);
    }

    public void AddPuzzleDefinition(Guid puzzleDefinitionId, PuzzleDefinition puzzleDefinition)
    {
        puzzleDefinitionId.ThrowIfEmpty();
        puzzleDefinition.ThrowIfNull();
        _puzzleDefinitionDataDictionary.Add(puzzleDefinitionId, puzzleDefinition);
    }

    public List<UserWalletTradeDetailList> FindPotentialTrades(string initiatingStakingAddress,
        Guid puzzlePieceDefinitionId,
        int maxNumberOfTradesToReturn,
        int maxStakingAddressAddressReturnCount, 
        IEnumerable<string> reservedPuzzlePieceIds)
    {
        initiatingStakingAddress.ThrowIfNullOrWhitespace();
        puzzlePieceDefinitionId.ThrowIfEmpty();

        var piecesOwnedByInitiatingStakingAddress = ActivePuzzlePieces
            .Where(x => x.StakingAddress == initiatingStakingAddress)
            .ToList();

        var availableCounterpartyPuzzlePieces = ActivePuzzlePieces
            .Where(x => x.PuzzlePieceDefinitionId == puzzlePieceDefinitionId && x.StakingAddress != initiatingStakingAddress
                    && !reservedPuzzlePieceIds.Contains(x.PuzzlePieceId))
            .ToList();
        
        var potentialTradesList = new ConcurrentBag<TradeDetail>();
        var allPuzzlePieceCombinations = new List<(UserWalletPuzzlePiece counterPartyPuzzlePiece, UserWalletPuzzlePiece ownerPuzzlePiece)>();
        
        foreach (var counterpartyPiece in availableCounterpartyPuzzlePieces)
        {
            foreach (var pieceOwnedByInitiatingStakingAddress in piecesOwnedByInitiatingStakingAddress)
            {
                allPuzzlePieceCombinations.Add((counterpartyPiece, pieceOwnedByInitiatingStakingAddress));
            }
        }
        
        Parallel.ForEach(allPuzzlePieceCombinations,  (combination, _) =>
        {
            var relevantPiecesOwnedByCounterpartyStakingAddress = ActivePuzzlePieces
                .Where(x => x.StakingAddress == combination.counterPartyPuzzlePiece.StakingAddress
                                                            && x.PuzzlePieceDefinitionId == combination.counterPartyPuzzlePiece.PuzzlePieceDefinitionId)
                .ToList();

            var potentialTrade = GetPotentialTrade(combination.ownerPuzzlePiece.PuzzlePieceId,
                 combination.counterPartyPuzzlePiece.PuzzlePieceId,
                piecesOwnedByInitiatingStakingAddress,
                relevantPiecesOwnedByCounterpartyStakingAddress,
                 combination.counterPartyPuzzlePiece.StakingAddress);

            if (potentialTrade == null)
            {
                return;
            }
            
            potentialTradesList.Add(potentialTrade);
        });

        var topCounterTradeStakingAddresses = potentialTradesList
            .OrderBy(x => x.Rating)
            .Select(x => x.CounterpartyPuzzlePiece.StakingAddress)
            .Distinct()
            .Take(maxStakingAddressAddressReturnCount);

        var topTrades = potentialTradesList
            .Where(x => topCounterTradeStakingAddresses.Contains(x.CounterpartyPuzzlePiece.StakingAddress))
            .GroupBy(x => x.CounterpartyPuzzlePiece.StakingAddress)
            .Select(x => new 
            { 
                CounterStakingAddress = x.Key,
                Trades = x.DistinctBy(y => new 
                    {                      
                        y.CounterpartyPuzzlePiece.StakingAddress, 
                        y.InitiatingPiece.PuzzlePieceDefinitionId 
                    }).OrderByDescending(pt => pt.Rating)
                    .Take(maxNumberOfTradesToReturn).ToList() 
            }).ToList();        

        return topTrades.Select(tradePerStakingAddress => new UserWalletTradeDetailList(tradePerStakingAddress.CounterStakingAddress, tradePerStakingAddress.Trades)).ToList();
    }
    
    public TradeDetail? GetPotentialTrade(string initiatingPuzzlePieceId,
        string counterpartyPuzzlePieceId,
        List<UserWalletPuzzlePiece> allPiecesOwnedByInitiatingStakingAddress,
        List<UserWalletPuzzlePiece> relevantPiecesOwnedByCounterpartyStakingAddress,
        string counterpartyPieceStakingAddress)
    {
        initiatingPuzzlePieceId.ThrowIfNull();

        var initiatingPuzzlePiece = allPiecesOwnedByInitiatingStakingAddress.SingleOrDefault(x => x.PuzzlePieceId == initiatingPuzzlePieceId);
        var counterPuzzlePiece = relevantPiecesOwnedByCounterpartyStakingAddress.SingleOrDefault(x => x.PuzzlePieceId == counterpartyPuzzlePieceId);

        if (initiatingPuzzlePiece == null || counterPuzzlePiece == null)
        {
            return null;
        }

        var allPiecesOwnedByCounterStakingAddress = ActivePuzzlePieces
            .Where(x => x.StakingAddress == counterPuzzlePiece.StakingAddress).ToList();

        var initiatingStakingAddressRating = CalcPuzzlePieceTradeRating(initiatingPuzzlePiece, allPiecesOwnedByInitiatingStakingAddress, allPiecesOwnedByCounterStakingAddress, counterPuzzlePiece.PuzzlePieceId);
        var counterStakingAddressRating = CalcPuzzlePieceTradeRating(counterPuzzlePiece, allPiecesOwnedByCounterStakingAddress, allPiecesOwnedByInitiatingStakingAddress, initiatingPuzzlePiece.PuzzlePieceId);
                
        var initiatingParty = ConvertToTradePuzzlePiece(
            initiatingPuzzlePieceId, 
            initiatingStakingAddressRating, 
            initiatingPuzzlePiece.StakingAddress, 
            initiatingPuzzlePiece.PuzzleDefinitionId, 
            counterPuzzlePiece.PuzzleDefinitionId);
        
        var counterParty = ConvertToTradePuzzlePiece(
            counterpartyPuzzlePieceId, 
            counterStakingAddressRating, 
            counterpartyPieceStakingAddress,
            counterPuzzlePiece.PuzzleDefinitionId,
            initiatingPuzzlePiece.PuzzleDefinitionId);

        var finalRating = (initiatingStakingAddressRating + counterStakingAddressRating) / 2M;
        
        return new TradeDetail(initiatingParty, counterParty, finalRating);
    }

    private decimal CalcPuzzlePieceTradeRating(UserWalletPuzzlePiece initiatingPuzzlePiece,
        List<UserWalletPuzzlePiece> allPiecesOwnedByInitiatingStakingAddress, 
        List<UserWalletPuzzlePiece> allPiecesOwnedByCounterPartyStakingAddress,
        string counterPartyPuzzlePieceId
        )
    {
        var initiatingPuzzlePiecePuzzleDefinition = PuzzleDefinitionDataDictionary[initiatingPuzzlePiece.PuzzleDefinitionId];
        
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
        var distinctOwnedPuzzlePieceDefinitionsCount = allPiecesOwnedByInitiatingStakingAddress
            .Where(x => x.PuzzleDefinitionId == initiatingPuzzlePiece.PuzzleDefinitionId)
            .Select(x => x.PuzzlePieceDefinitionId)
            .Distinct()
            .Count();

        return distinctOwnedPuzzlePieceDefinitionsCount;
    }
    
    private TradePuzzlePiece ConvertToTradePuzzlePiece(
        string puzzlePieceId, 
        decimal rating, 
        string stakingAddress, 
        Guid outgoingPuzzleDefinitionId, 
        Guid incomingPuzzleDefinitionId)
    {
        var ownedPuzzlePiece = ActivePuzzlePieces.Single(x => x.PuzzlePieceId == puzzlePieceId);
        
        var outgoingPuzzlePieceDefinitionIds = ActivePuzzlePieces
            .Where(x => x.PuzzleDefinitionId == outgoingPuzzleDefinitionId && x.StakingAddress == stakingAddress)
            .Select(x => x.PuzzlePieceDefinitionId)
            .GroupBy(x => x)
            .Select(x => ( x.Key, x.Count()))
            .ToList();
        
        
        var incomingPuzzlePieceDefinitionIds = ActivePuzzlePieces
            .Where(x => x.PuzzleDefinitionId == incomingPuzzleDefinitionId && x.StakingAddress == stakingAddress)
            .Select(x => x.PuzzlePieceDefinitionId)
            .GroupBy(x => x)
            .Select(x => ( x.Key, x.Count()))
            .ToList();

        var puzzleDefinition = PuzzleDefinitionDataDictionary[ownedPuzzlePiece.PuzzleDefinitionId];
        
        var ownedTradePuzzlePiece = new TradePuzzlePiece(
            puzzlePieceId, 
            ownedPuzzlePiece.PuzzlePieceDefinitionId, 
            ownedPuzzlePiece.PuzzleDefinitionId, 
            puzzleDefinition.PuzzleName, 
            puzzleDefinition.PuzzleCollectionId, 
            puzzleDefinition.PuzzleCollectionTitle, 
            ownedPuzzlePiece.StakingAddress,
            ownedPuzzlePiece.Nickname,
            rating,
            outgoingPuzzlePieceDefinitionIds,
            incomingPuzzlePieceDefinitionIds
            );
        
        return ownedTradePuzzlePiece;
    }
}