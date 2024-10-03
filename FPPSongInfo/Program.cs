namespace FPPSongInfo;

public static class Program
{
    public static async Task Main(string[] args)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
        builder.Configuration.AddJsonFile("./appdata/appsettings.user.json", optional:true, reloadOnChange: true);
        builder.Services.ConfigureServicesFromConfig(builder.Configuration);
        var host = builder.Build();
        await host.StartAsync();
        await host.WaitForShutdownAsync();
    }
}