using FPPSongInfo.Configuration;
using FPPSongInfo.Mqtt;
using FPPSongInfo.Rds;
using FPPSongInfo.Service;

namespace FPPSongInfo;

internal static class ConfigureServiceCollectionExtension
{
    internal static IServiceCollection ConfigureServicesFromConfig(this IServiceCollection services,
        IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(config);

        services.AddOptions<MqttOptions>()
            .BindConfiguration(MqttOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<FppOptions>()
            .BindConfiguration(FppOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<RadioAutomationOptions>()
            .BindConfiguration(RadioAutomationOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<OutputOptions>()
            .BindConfiguration(OutputOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<RdsOptions>()
            .BindConfiguration(RdsOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        var outputEnabled = config.GetSection(OutputOptions.SectionName).Get<OutputOptions>()?.Enabled != false;
        if (outputEnabled)
        {
            services.AddSingleton<FileSongInfoSink>();
            services.AddSingleton<ISongInfoSink>(serviceProvider => serviceProvider.GetRequiredService<FileSongInfoSink>());
        }

        services.AddSingleton<ISongInfoPublisher, SongInfoPublisher>();

        var rdsEnabled = config.GetSection(RdsOptions.SectionName).Get<RdsOptions>()?.Enabled == true;
        if (rdsEnabled)
        {
            services.AddSingleton<TimeProvider>(TimeProvider.System);
            services.AddSingleton<IMrds192Bus, SerialControlLineMrds192Bus>();
            services.AddSingleton<IMrds192DeviceClient, Mrds192DeviceClient>();
            services.AddSingleton<RdsUpdaterService>();
            services.AddSingleton<ISongInfoSink>(serviceProvider => serviceProvider.GetRequiredService<RdsUpdaterService>());
            services.AddSingleton<IHostedService>(serviceProvider => serviceProvider.GetRequiredService<RdsUpdaterService>());
        }

        var mqttEnabled = config.GetSection(MqttOptions.SectionName).Get<MqttOptions>()?.Enabled == true;
        if (mqttEnabled)
        {
            services.AddSingleton<IMqttClient, MqttClient>();
            services.AddHostedService<MqttConnectionHostedService>();
            services.AddHostedService<FppConsumerService>();
            services.AddHostedService<RadioAutomationConsumerService>();
        }

        return services;
    }
}
