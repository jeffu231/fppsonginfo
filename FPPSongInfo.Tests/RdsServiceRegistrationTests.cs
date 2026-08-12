using FPPSongInfo.Configuration;
using FPPSongInfo.Service;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FPPSongInfo.Tests;

public sealed class RdsServiceRegistrationTests
{
    [Fact]
    public void RegistersOneRdsUpdaterAsBothSinkAndHostedService()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
            [
                new KeyValuePair<string, string?>("Output:Enabled", "false"),
                new KeyValuePair<string, string?>("Rds:Enabled", "true"),
                new KeyValuePair<string, string?>("Rds:PortName", "COM3"),
                new KeyValuePair<string, string?>("Rds:StaticProgramService", "LTSHOW"),
                new KeyValuePair<string, string?>("Rds:DynamicProgramService", "Compound Radio")
            ])
            .Build();
        var services = new ServiceCollection();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.ConfigureServicesFromConfig(configuration);
        using var provider = services.BuildServiceProvider();

        var updater = provider.GetRequiredService<RdsUpdaterService>();
        var sink = Assert.Single(provider.GetServices<ISongInfoSink>().OfType<RdsUpdaterService>());
        var hostedService = Assert.Single(provider.GetServices<IHostedService>().OfType<RdsUpdaterService>());

        Assert.Same(updater, sink);
        Assert.Same(updater, hostedService);
    }
}
