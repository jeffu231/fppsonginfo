using System.ComponentModel.DataAnnotations;
using System.Text;

namespace FPPSongInfo.Configuration;

internal sealed class RdsOptions : IValidatableObject
{
    private const int MaximumAdditionalRadioTextMessages = 3;
    private const int MaximumDynamicProgramServiceLength = 72;
    private const int MaximumLabelPeriodRawValue = 255;
    private const int MaximumLoopDelayRawValue = 255;
    private const int MaximumRadioTextLength = 64;
    private const int MaximumStaticProgramServiceLength = 8;
    private const double LabelPeriodSecondsPerRawValue = 0.54;
    private const double LoopDelaySecondsPerRawValue = 2.7;

    internal const string SectionName = "Rds";

    public bool Enabled { get; init; }

    public string PortName { get; init; } = string.Empty;

    public bool Slow { get; init; }

    public string StaticProgramService
    {
        get;
        init => field = NormalizeWhitespace(value);
    } = string.Empty;

    public string DynamicProgramService
    {
        get;
        init => field = NormalizeWhitespace(value);
    } = string.Empty;

    public DynamicPsMode DynamicPsMode { get; init; } = DynamicPsMode.SpaceSeparatedScrolling;

    public TimeSpan LabelPeriod { get; init; } = TimeSpan.FromMilliseconds(2500);

    public TimeSpan LoopDelay { get; init; } = TimeSpan.FromMilliseconds(5400);

    public TimeSpan RadioTextRotationInterval { get; init; } = TimeSpan.FromSeconds(30);

    public List<string> AdditionalRadioTextMessages { get; init; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enabled)
        {
            yield break;
        }

        if (string.IsNullOrWhiteSpace(PortName))
        {
            yield return Error("Port name is required when RDS is enabled.", nameof(PortName));
        }

        if (StaticProgramService.Length is < 1 or > MaximumStaticProgramServiceLength)
        {
            yield return Error(
                $"Static Program Service must contain between 1 and {MaximumStaticProgramServiceLength} normalized characters.",
                nameof(StaticProgramService));
        }

        if (DynamicProgramService.Length is < 1 or > MaximumDynamicProgramServiceLength)
        {
            yield return Error(
                $"Dynamic Program Service must contain between 1 and {MaximumDynamicProgramServiceLength} normalized characters.",
                nameof(DynamicProgramService));
        }

        if (!Enum.IsDefined(DynamicPsMode))
        {
            yield return Error("Dynamic PS mode must be a value from 0 through 3.", nameof(DynamicPsMode));
        }

        if (!IsWithinHardwareRange(LabelPeriod, LabelPeriodSecondsPerRawValue, MaximumLabelPeriodRawValue))
        {
            yield return Error("Label period must be non-negative and within the MRDS192 hardware range.", nameof(LabelPeriod));
        }

        if (!IsWithinHardwareRange(LoopDelay, LoopDelaySecondsPerRawValue, MaximumLoopDelayRawValue))
        {
            yield return Error("Loop delay must be non-negative and within the MRDS192 hardware range.", nameof(LoopDelay));
        }

        if (RadioTextRotationInterval <= TimeSpan.Zero)
        {
            yield return Error("RadioText rotation interval must be greater than zero.", nameof(RadioTextRotationInterval));
        }

        if (AdditionalRadioTextMessages.Count > MaximumAdditionalRadioTextMessages)
        {
            yield return Error(
                $"At most {MaximumAdditionalRadioTextMessages} additional RadioText messages are supported.",
                nameof(AdditionalRadioTextMessages));
        }

        if (AdditionalRadioTextMessages.Any(message => GetEncodedLength(message) > MaximumRadioTextLength))
        {
            yield return Error(
                $"Each additional RadioText message must be at most {MaximumRadioTextLength} encoded characters.",
                nameof(AdditionalRadioTextMessages));
        }
    }

    private static ValidationResult Error(string message, string memberName) => new(message, [memberName]);

    private static int GetEncodedLength(string? value) => Encoding.Latin1.GetByteCount(value ?? string.Empty);

    private static bool IsWithinHardwareRange(TimeSpan value, double secondsPerRawValue, int maximumRawValue) =>
        value >= TimeSpan.Zero && value <= TimeSpan.FromSeconds(maximumRawValue * secondsPerRawValue);

    private static string NormalizeWhitespace(string? value) => string.Join(
        ' ',
        (value ?? string.Empty).Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
