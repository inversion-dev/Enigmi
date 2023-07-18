using Enigmi.Application.Handlers;
using Enigmi.Messages.System;
using System.Reflection;

namespace Enigmi.Application.Services;

public class AssemblyProvider
{
	public IEnumerable<Assembly> GetMessageAssemblies()
	{
		yield return typeof(PingRequest).Assembly;
	}

	public IEnumerable<Assembly> GetValidatorAssemblies()
	{
		yield return typeof(Handler<,>).Assembly;
	}

	public Assembly GetDomainEntitiesAssembly()
	{
		return typeof(Constants).Assembly;
	}
}