using Enigmi.Common;
using Enigmi.Common.Domain;
using Enigmi.Domain.Entities.PuzzlePieceDispenserAggregate.ValueObjects;
using Newtonsoft.Json;
using static System.FormattableString;

namespace Enigmi.Domain.Entities.PuzzlePieceDispenserAggregate;

public class PuzzlePieceDispenser : DomainEntity
{
	public PuzzlePieceDispenser(TimeSpan expiryTimespan)
	{
		ExpiryTimespan = expiryTimespan;
	}
	
	[JsonConstructor]
	private PuzzlePieceDispenser()
	{
		
	}

	[JsonProperty] 
	public string Id { get; private set; } = null!;

	[JsonProperty]
	public Guid PuzzleCollectionId { get; private set; }

	[JsonProperty]
	public int PuzzleSize { get; private set; }

	public PuzzlePieceDispenser(Guid puzzleCollectionId, int puzzleSize, TimeSpan expiryTimespan)
	{
		PuzzleCollectionId = puzzleCollectionId;
		PuzzleSize = puzzleSize;
		Id = GetId(puzzleCollectionId, puzzleSize);
		ExpiryTimespan = expiryTimespan;
	}
	
	private List<Reservation> _reservations = new List<Reservation>();

	[JsonProperty]
	public IEnumerable<Reservation> Reservations
	{
		get { return _reservations.AsReadOnly(); }
		private set { _reservations = value.ToList(); }
	}

	[JsonProperty] 
	public TimeSpan ExpiryTimespan { get; private set; }

	public static string GetId(Guid puzzleCollectionId, int puzzleSize) => Invariant($"{puzzleCollectionId}/{puzzleSize}");

	public static (Guid puzzleCollectionId, int puzzleSize) SplitId(string id)
	{
		var parts = id.Split("/");
		var puzzleCollectionId = Guid.Parse(parts[0]);
		var puzzleSize = int.Parse(parts[1]);

		return (puzzleCollectionId, puzzleSize);
	}

	[JsonProperty]
	public IEnumerable<string> AvailablePuzzlePieceIds
	{
		get { return _availablePuzzlePieceIds.AsReadOnly(); }
		private set { _availablePuzzlePieceIds = value.ToList(); }
	}

	private List<string> _availablePuzzlePieceIds = new List<string>();
	
	public void AddAvailablePuzzlePiece(string puzzlePieceId)
	{
		puzzlePieceId.ThrowIfNullOrWhitespace();
		_availablePuzzlePieceIds.Add(puzzlePieceId);
	}

	public IEnumerable<string> ReservePuzzlePieces(Guid reservationId, List<string> puzzlePieceIdsToReserve)
	{
		puzzlePieceIdsToReserve.ThrowIfNull();
		reservationId.ThrowIfEmpty();
		
		AddReservation(reservationId, puzzlePieceIdsToReserve);

		//TODO: what if too little reservation available. Thought here is that one returns as many as possible and let the user know.
		return puzzlePieceIdsToReserve;
	}

	public IEnumerable<string> GetPuzzlePieceIdsAvailableForReservation()
	{
		var reservedPuzzlePieceIds = _reservations.SelectMany(x => x.PuzzlePieceIds).ToList();
		return _availablePuzzlePieceIds.Except(reservedPuzzlePieceIds).ToList();
	}
	

	private Reservation AddReservation(Guid reservationId, IEnumerable<string> reservations)
	{
		var reservation = new Reservation(reservationId, reservations, DateTime.UtcNow)
		{
			ReservationState = ReservationState.Reserved
		};

		_reservations.Add(reservation);

		return reservation;
	}

	public void ReleaseExpiredReservations()
	{
		_reservations.RemoveAll(x => x.ReservationState == ReservationState.Reserved && x.ReservedUtcDate.Add(ExpiryTimespan) <= DateTime.UtcNow);
	}

	public void UpdateDispenserExpiresTimespan(TimeSpan dispenserExpiresTimespan)
	{
		ExpiryTimespan = dispenserExpiresTimespan;
	}

	public bool CommitReservation(Guid reservationId)
	{
		reservationId.ThrowIfEmpty();
		
		var reservation = _reservations.SingleOrDefault(x => x.Id == reservationId);
		if (reservation != null)
		{
			reservation.ReservationState = ReservationState.Commited;
			return true;
		}

		return false;
	}

	public void ReleaseOrder(Guid orderId)
	{
		orderId.ThrowIfEmpty();
		var reservation = _reservations.SingleOrDefault(x => x.Id == orderId);
		if (reservation != null)
		{
			_reservations.Remove(reservation);
		}
	}
}