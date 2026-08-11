using System.ComponentModel.DataAnnotations;

namespace FPPSongInfo.Configuration;

internal sealed class OutputOptions : IValidatableObject
{
    internal const string SectionName = "Output";

    [Required]
    public string FilePath { get; init; } = string.Empty;

    [Required]
    public string FileName { get; init; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Path.IsPathRooted(FileName) || FileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            yield return new ValidationResult(
                "File name must be a valid relative file name.",
                [nameof(FileName)]);
        }
    }
}
