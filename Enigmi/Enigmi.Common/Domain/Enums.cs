namespace Enigmi.Common.Domain;

public static class Enums
{
	public enum ActivationState : byte
	{
		Draft = 0,
		Activated = 1,
		Deactivated = 2
	}

	public enum CommandEventLogState : byte
	{
		Unhandled = 0,
		Handled = 1,
		Failed = 2
	}

    public enum PublicationState
    {
        NotSet = 0,
        Unpublished = 1,
        Published = 2
    }

}