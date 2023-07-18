using Enigmi.HostSetup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Enigmi.InternalHost;

public class Program
{
	public static void Main()
	{
		var host = new HostBuilder()
			.ConfigureFunctionsWorkerDefaults()
			.ConfigureAppConfiguration((context, builder) =>
			{
				var root = context.HostingEnvironment.ContentRootPath;
				builder
					.SetBasePath(root)
					.AddJsonFiles();
			})

			.ConfigureServices((context, services) =>
			{
				services.ConfigureServices(context.Configuration);
			})
			.Build();

		host.Run();
	}
}