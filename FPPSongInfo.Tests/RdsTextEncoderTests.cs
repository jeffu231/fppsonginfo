using System.Text;
using FPPSongInfo.Rds;

namespace FPPSongInfo.Tests;

public sealed class RdsTextEncoderTests
{
    [Fact]
    public void NormalizesTransliteratesAndReplacesUnsupportedCharacters()
    {
        var bytes = RdsTextEncoder.Encode("  Café\tDéjà  vu 😀 ", 64);

        Assert.Equal("Cafe Deja vu ?", Encoding.ASCII.GetString(bytes));
    }

    [Fact]
    public void ReplacesNonprintableCharacters()
    {
        var bytes = RdsTextEncoder.Encode("A\u0001B", 64);

        Assert.Equal("A?B", Encoding.ASCII.GetString(bytes));
    }

    [Fact]
    public void TruncatesDynamicProgramServiceToEncodedByteLimit()
    {
        var bytes = RdsTextEncoder.EncodeDynamicProgramService(new string('a', 73));

        Assert.Equal(RdsTextEncoder.DynamicProgramServiceMaximumLength, bytes.Length);
        Assert.All(bytes, value => Assert.Equal((byte)'a', value));
    }

    [Fact]
    public void PadsStaticProgramServiceAndRadioTextWithSpaces()
    {
        var programService = RdsTextEncoder.EncodeStaticProgramService("LTSHOW");
        var radioText = RdsTextEncoder.EncodeRadioText("Artist - Title");

        Assert.Equal("LTSHOW  ", Encoding.ASCII.GetString(programService));
        Assert.Equal(RdsTextEncoder.RadioTextMaximumLength, radioText.Length);
        Assert.Equal("Artist - Title", Encoding.ASCII.GetString(radioText[..14]));
        Assert.All(radioText[14..], value => Assert.Equal((byte)' ', value));
    }
}
