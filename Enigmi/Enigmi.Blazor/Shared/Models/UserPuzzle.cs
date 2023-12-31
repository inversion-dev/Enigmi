﻿using Enigmi.Common;

namespace Enigmi.Blazor.Shared.Models;

public class UserPuzzle
{
    public Guid PuzzleId { get; }

    public string PuzzleTitle { get; private set; }

    public string CollectionTitle { get; private set; }

    public List<UserPuzzlePiece> PuzzlePieces { get; private set; }

    public int PuzzleSize { get; private set; }

    public int NumberOfAllowedBuilds { get; private set; }

    public int NumberOfCompletedBuilds { get; private set; }    

    public UserPuzzle(
        Guid puzzleId,
        string puzzleTitle,
        string collectionTitle,
        int numberOfAllowedBuilds,
        int numberOfCompletedBuilds,
        int puzzleSize,
        List<UserPuzzlePiece> puzzlePieces)
    {        
        PuzzleId = puzzleId.ThrowIfEmpty();
        PuzzleTitle = puzzleTitle.ThrowIfNullOrWhitespace();
        CollectionTitle = collectionTitle.ThrowIfNullOrWhitespace();
        NumberOfAllowedBuilds = numberOfAllowedBuilds.ThrowIf(x => x < 0, "Must be equal or larger than 0");
        NumberOfCompletedBuilds = numberOfCompletedBuilds.ThrowIf(x => x < 0, "Must be equal or larger than 0");
        PuzzleSize = puzzleSize.ThrowIf(x => x < 1, "Must be larger than 0");
        PuzzlePieces = puzzlePieces.ThrowIfNull();
    }
}

public class UserPuzzleComparer : IEqualityComparer<UserPuzzle>
{
    public bool Equals(UserPuzzle? x, UserPuzzle? y)
    {
        return (x?.PuzzleId ?? Guid.Empty) == (y?.PuzzleId ?? Guid.Empty);
    }

    public int GetHashCode(UserPuzzle obj)
    {
        return obj.PuzzleId.GetHashCode();
    }
}
