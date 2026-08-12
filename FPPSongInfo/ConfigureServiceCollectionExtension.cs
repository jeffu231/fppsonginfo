using FPPSongInfo.Configuration;
using FPPSongInfo.Mqtt;
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
        services.AddSingleton<ISongInfoWriter, SongInfoWriter>();

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
