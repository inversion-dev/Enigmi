namespace Enigmi.Domain.Entities.OrderAggregate;

public enum OrderState
{
	Draft = 0,
	TransactionCreated = 1,
	TransactionSigned = 2,
	TransactionSubmitted = 3,
	TransactionSubmissionFailed = 4,
	Completed = 5,
	Cancelled = 6,
}

[Flags]
public enum SignedState
{
	None = 0,
	SystemSigned = 1,
	UserSigned = 2
}