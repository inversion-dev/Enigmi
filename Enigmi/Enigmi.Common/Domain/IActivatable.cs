using static Enigmi.Common.Domain.Enums;

namespace Enigmi.Common.Domain;

public interface IActivatable
{
	public ActivationState ActivationStateId { get; }
}