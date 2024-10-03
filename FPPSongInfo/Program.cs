using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;

namespace FPPSongInfo;

public static class Program
{
    public static async Task Main(string[] args)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
        builder.Configuration.AddJsonFile("./appdata/appsettings.user.json", optional:true, reloadOnChange: true);
        builder.Services.AddWindowsService(options =>
        {
            options.ServiceName = "FPP Song Info";
        });

		LoggerProviderOptions.RegisterProviderOptions<EventLogSettings, EventLogLoggerProvider>(builder.Services);

		builder.Services.ConfigureServicesFromConfig(builder.Configuration);
        var host = builder.Build();
        await host.StartAsync();
        await host.WaitForShutdownAsync();
    }
}