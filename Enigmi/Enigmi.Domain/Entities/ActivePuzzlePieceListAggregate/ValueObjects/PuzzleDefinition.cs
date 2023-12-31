﻿namespace Enigmi.Domain.Entities.ActivePuzzlePieceListAggregate.ValueObjects;

public record PuzzleDefinition(string PuzzleName, int Size, int AvailablePuzzleBuilds, int NumberOfCompletedBuilds, List<PuzzleDefinition.PuzzlePieceDefinition> PuzzlePieceDefinitions, Guid PuzzleCollectionId, string PuzzleCollectionTitle)
{
    public record PuzzlePieceDefinition(Guid Id, Guid PuzzleDefinitionId, string ImageUrl, int X, int Y);
}