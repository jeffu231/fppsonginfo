using System.ComponentModel.DataAnnotations;

namespace FPPSongInfo.Configuration;

internal sealed class FppOptions
{
    internal const string SectionName = "FPP";

    [Required]
    [TopicFragment]
    public string SongTopic { get; init; } = string.Empty;
}
