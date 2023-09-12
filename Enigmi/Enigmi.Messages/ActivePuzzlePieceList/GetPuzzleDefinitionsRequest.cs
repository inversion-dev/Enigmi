using Enigmi.Common.Messaging;

namespace Enigmi.Messages.ActivePuzzlePieceList;

public record GetPuzzleDefinitionsRequest(IEnumerable<Guid> PuzzlePieceDefinitionIds) : Request<GetPuzzleDefinitionsResponse>
{
    public override Enums.AccessMechanism AccessMechanism { get; } = Enums.AccessMechanism.Authorized;
}

public record GetPuzzleDefinitionsResponse(List<GetPuzzleDefinitionsResponse.PuzzleDefinition> PuzzleDefinitions) : RequestResponse
{
    public record PuzzleDefinition(Guid Id, String Title, String PuzzleCollectionTitle, int PuzzleSize, int NumberOfAllowedBuilds, int NumberOfCompletedBuilds
        , IEnumerable<PuzzlePieceDefinition> PuzzlePieceDefinitions);
    
    public record PuzzlePieceDefinition(Guid Id, Guid PuzzleDefinitionId, String ImageUrl);
    
}