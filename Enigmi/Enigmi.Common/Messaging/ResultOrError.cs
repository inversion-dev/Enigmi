namespace Enigmi.Common.Messaging;

public class ResultOrError<TResult> : IResultOrError<TResult>
{
	public TResult? Result { get; init; }

	public List<string> Errors { get; init; } = new List<string>();

	public bool HasErrors => Errors.Any();

	public static ResultOrError<TResult> Create() => new ResultOrError<TResult>();

	public static ResultOrError<TResult> Create(TResult result) => new ResultOrError<TResult> { Result = result };
}

public interface IResultOrError<out TResult>
{
	public TResult? Result { get; }

	public List<string> Errors { get; }

	public bool HasErrors { get; }
}