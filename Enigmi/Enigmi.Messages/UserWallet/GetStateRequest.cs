using Enigmi.Common.Messaging;

namespace Enigmi.Messages.UserWallet;

public record GetStateRequest(string? StakeAddress, Guid? OrderId)  : Request<GetStateResponse>
{
    public override Enums.AccessMechanism AccessMechanism { get; } = Enums.AccessMechanism.Authorized;
}

public record GetStateResponse(List<GetStateResponse.PuzzlePiece> PuzzlePieces, 
    List<GetStateResponse.PuzzleDefinition> PuzzleDefinitions) : RequestResponse
{
    public record PuzzlePiece(Guid PuzzlePieceDefinitionId, Guid PuzzleDefinitionId, String ImageUrl, int X, int Y, bool IsOwned, List<string> OwnedPuzzlePieceIds, int PuzzlePieceCount);

    public record PuzzleDefinition(Guid Id, String Title, int PuzzleSize, int NumberOfAllowedBuilds, int NumberOfCompletedBuilds);
}