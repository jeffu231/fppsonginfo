using System.ComponentModel.DataAnnotations;

namespace FPPSongInfo.Configuration;

internal sealed class MqttOptions : IValidatableObject
{
    internal const string SectionName = "Mqtt";

    public bool Enabled { get; init; }

    [Range(1, 65535)]
    public int Port { get; init; } = 1883;

    public string Broker { get; init; } = string.Empty;

    public string RootTopic { get; init; } = string.Empty;

    public string ClientId { get; init; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enabled)
        {
            yield break;
        }

        if (string.IsNullOrWhiteSpace(Broker))
        {
            yield return new ValidationResult(
                "Broker is required when MQTT is enabled.",
                [nameof(Broker)]);
        }

        if (string.IsNullOrWhiteSpace(RootTopic))
        {
            yield return new ValidationResult(
                "Root topic is required when MQTT is enabled.",
                [nameof(RootTopic)]);
        }

        if (string.IsNullOrWhiteSpace(ClientId))
        {
            yield return new ValidationResult(
                "Client ID is required when MQTT is enabled.",
                [nameof(ClientId)]);
        }
    }
}
