namespace Enigmi.Common.Exceptions;

public class ValidationException : ApplicationException
{
	public List<string> ValidationErrors { get; set; } = new List<string>();

	public ValidationException(IEnumerable<string> validationErrors) : base(string.Join(",", validationErrors))
	{
		ValidationErrors = validationErrors.ThrowIfNull().ToList();
	}
}