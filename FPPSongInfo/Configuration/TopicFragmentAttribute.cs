using System.ComponentModel.DataAnnotations;

namespace FPPSongInfo.Configuration;

[AttributeUsage(AttributeTargets.Property)]
internal sealed class TopicFragmentAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) =>
        value is string topic &&
        topic.StartsWith("/", StringComparison.Ordinal) &&
        topic == topic.Trim() &&
        !topic.Contains('#') &&
        !topic.Contains('+');
}
