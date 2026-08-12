using System.Globalization;
using System.Text;

namespace FPPSongInfo.Rds;

internal static class RdsTextEncoder
{
    internal const int DynamicProgramServiceMaximumLength = 72;
    internal const int RadioTextMaximumLength = 64;
    private const int StaticProgramServiceLength = 8;

    internal static byte[] EncodeDynamicProgramService(string? value) =>
        Encode(value, DynamicProgramServiceMaximumLength);

    internal static byte[] EncodeRadioText(string? value) =>
        PadWithSpaces(Encode(value, RadioTextMaximumLength), RadioTextMaximumLength);

    internal static byte[] EncodeStaticProgramService(string? value) =>
        PadWithSpaces(Encode(value, StaticProgramServiceLength), StaticProgramServiceLength);

    internal static byte[] Encode(string? value, int maximumLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumLength);

        var bytes = new List<byte>(Math.Min(value?.Length ?? 0, maximumLength));
        var pendingSpace = false;
        var previousWasBasicLatin = false;

        foreach (var rune in (value ?? string.Empty).Normalize(NormalizationForm.FormD).EnumerateRunes())
        {
            if (Rune.IsWhiteSpace(rune))
            {
                pendingSpace = bytes.Count > 0;
                previousWasBasicLatin = false;
                continue;
            }

            if (IsCombiningMark(rune))
            {
                if (!previousWasBasicLatin)
                {
                    AppendByte(bytes, (byte)'?', maximumLength);
                }

                continue;
            }

            if (pendingSpace)
            {
                AppendByte(bytes, (byte)' ', maximumLength);
                pendingSpace = false;
            }

            if (rune.Value is >= 0x20 and <= 0x7e)
            {
                AppendByte(bytes, (byte)rune.Value, maximumLength);
                previousWasBasicLatin = true;
                continue;
            }

            AppendByte(bytes, (byte)'?', maximumLength);
            previousWasBasicLatin = false;
        }

        return [.. bytes];
    }

    private static void AppendByte(List<byte> bytes, byte value, int maximumLength)
    {
        if (bytes.Count < maximumLength)
        {
            bytes.Add(value);
        }
    }

    private static bool IsCombiningMark(Rune rune) => Rune.GetUnicodeCategory(rune) is
        UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark;

    private static byte[] PadWithSpaces(byte[] bytes, int length)
    {
        var padded = new byte[length];
        padded.AsSpan().Fill((byte)' ');
        bytes.CopyTo(padded, 0);
        return padded;
    }
}
