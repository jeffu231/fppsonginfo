using System.ComponentModel.DataAnnotations;

namespace FPPSongInfo.Configuration;

internal sealed class RadioAutomationOptions
{
    internal const string SectionName = "RadioAutomation";

    [Required]
    [TopicFragment]
    public string SongTopic { get; init; } = string.Empty;
}
