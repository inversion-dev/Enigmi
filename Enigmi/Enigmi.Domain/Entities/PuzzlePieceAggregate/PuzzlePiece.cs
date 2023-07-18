using CSharpVitamins;
using Enigmi.Common;
using Enigmi.Common.Domain;
using Enigmi.Common.Utils;
using Newtonsoft.Json;
using static System.FormattableString;
using ApplicationException = Enigmi.Common.Exceptions.ApplicationException;

namespace Enigmi.Domain.Entities.PuzzlePieceAggregate;

public class PuzzlePiece : DomainEntity
{
    public PuzzlePiece(string id, string blockchainAssetId, Guid puzzlePieceDefinitionId, Guid puzzleDefinitionId)
    {
        PuzzlePieceDefinitionId = puzzlePieceDefinitionId.ThrowIfEmpty();
        Id = id.ThrowIfNullOrWhitespace();
        BlockchainAssetId = blockchainAssetId.ThrowIfNullOrWhitespace();
        PuzzleDefinitionId = puzzleDefinitionId.ThrowIfEmpty();

        var (_, assetNameBytes) = CardanoHelper.ConvertAssetIdToPolicyIdAndAssetName(blockchainAssetId);
        AssetName = CardanoHelper.ConvertAssetNameToString(assetNameBytes);
    }

    [JsonConstructor]
    private PuzzlePiece()
    {
        
    }

    [JsonProperty]
    public Guid PuzzleDefinitionId { get; set; }

    [JsonProperty] 
    public string Id { get; private set; } = null!;

    [JsonProperty] 
    public string BlockchainAssetId { get; private set; } = null!;
    
    [JsonProperty] 
    public string AssetName { get; private set; } = null!;

    [JsonProperty]
    public Guid PuzzlePieceDefinitionId { get; private set; }

    private Guid ConvertAssetNameToGuid
    {
        get
        {
            if (!Guid.TryParseExact(AssetName, "N", out var guid))
            {
                throw new ApplicationException(Invariant($"Could not convert {AssetName} to GUID"));
            }

            return guid;
        }
    }

    public string BlobImagePath => Invariant($"/pub/piece/{new ShortGuid(ConvertAssetNameToGuid)}.jpg");
}