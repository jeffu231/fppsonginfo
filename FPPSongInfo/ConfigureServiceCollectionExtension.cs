using FPPSongInfo.Mqtt;
using FPPSongInfo.Service;

namespace FPPSongInfo;

public static class ConfigureServiceCollectionExtension
{
    public static IServiceCollection ConfigureServicesFromConfig(this IServiceCollection services,
        IConfiguration config)
    {
        Console.WriteLine("ConfigureServicesFromConfig");
        services.AddSingleton<IMqttClient, MqttClient>();
        services.AddSingleton<ISongInfoWriter, SongInfoWriter>();
        services.AddHostedService<FppConsumerService>();
        services.AddHostedService<RadioAutomationConsumerService>();

        return services;
    }
}
