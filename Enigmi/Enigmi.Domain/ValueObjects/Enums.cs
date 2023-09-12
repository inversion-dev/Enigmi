namespace Enigmi.Domain.ValueObjects;

public enum ActivationStatus
{
    Draft = 1,
    Published = 2,
    Closed = 3
}

public enum TradeState
{
    New = 1,
    Cancelled = 2,
    CounterpartySigned = 3, 
    FullySigned = 4,
    Submitted = 5,
    SubmissionFailed = 6,
    Completed = 7
}