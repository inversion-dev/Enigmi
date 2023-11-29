namespace Enigmi.Domain.Entities.UserWalletAggregate;

public enum Reserver
{
	NotSet = 0,
	Order = 1,
	Trade = 2
}

public enum OnlineState
{
	NotSet = 0,
	Online = 1,
	Offline = 2
}

public enum ReservationState
{
	NotSet = 0,
	Reserved = 1,
	Released = 2
}