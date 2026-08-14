using System.ComponentModel.DataAnnotations;

namespace FPPSongInfo.Configuration;

internal sealed class OutputOptions : IValidatableObject
{
    internal const string SectionName = "Output";

    public bool Enabled { get; init; } = true;

    public string FilePath { get; init; } = string.Empty;

    public string FileName { get; init; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enabled)
        {
            yield break;
        }

        if (string.IsNullOrWhiteSpace(FilePath))
        {
            yield return new ValidationResult(
                "File path is required when file output is enabled.",
                [nameof(FilePath)]);
        }

        if (string.IsNullOrWhiteSpace(FileName))
        {
            yield return new ValidationResult(
                "File name is required when file output is enabled.",
                [nameof(FileName)]);
            yield break;
        }

        if (Path.IsPathRooted(FileName) || FileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            yield return new ValidationResult(
                "File name must be a valid relative file name.",
                [nameof(FileName)]);
        }
    }
}
