using System.Text.Json.Serialization;

namespace FPPSongInfo.Service;

internal sealed record SongInfo(
    [property: JsonPropertyName("artist")] string Artist = "",
    [property: JsonPropertyName("title")] string Title = "",
    [property: JsonPropertyName("album")] string Album = "");
