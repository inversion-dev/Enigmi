using Enigmi.Common.Messaging;

namespace Enigmi.Messages.SignalRMessage;

public record ActivePuzzlePieceUpdate(string StakingAddress, Guid PuzzlePieceDefinitionId, Guid PuzzleDefinitionId, int X, int Y, DateTime UtcTimestamp, int PuzzlePieceCount) : ISignalRMessage;
