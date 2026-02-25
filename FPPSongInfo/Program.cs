namespace FPPSongInfo;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;

public static class Program
{
    public static async Task Main(string[] args)
    {
		HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
		builder.Services.AddWindowsService(options =>
	    {
		    options.ServiceName = "FPP Song Info";
	    });

        builder.Logging.AddConfiguration(
            builder.Configuration.GetSection("Logging"));

        LoggerProviderOptions.RegisterProviderOptions<
		    EventLogSettings, EventLogLoggerProvider>(builder.Services);

		builder.Services.ConfigureServicesFromConfig(builder.Configuration);

        var host = builder.Build();
        await host.StartAsync();
        await host.WaitForShutdownAsync();
    }
}