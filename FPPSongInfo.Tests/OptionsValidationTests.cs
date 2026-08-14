using System.ComponentModel.DataAnnotations;
using FPPSongInfo.Configuration;

namespace FPPSongInfo.Tests;

public sealed class OptionsValidationTests
{
    [Fact]
    public void RejectsEnabledMqttWithoutConnectionSettings()
    {
        var results = new List<ValidationResult>();
        var options = new MqttOptions { Enabled = true, Port = 1883 };

        var isValid = Validator.TryValidateObject(options, new ValidationContext(options), results, true);

        Assert.False(isValid);
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(MqttOptions.Broker)));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(MqttOptions.RootTopic)));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(MqttOptions.ClientId)));
    }

    [Fact]
    public void RejectsInvalidOutputFileName()
    {
        var results = new List<ValidationResult>();
        var options = new OutputOptions { FilePath = "output", FileName = Path.GetFullPath("CurrentSong.txt") };

        var isValid = Validator.TryValidateObject(options, new ValidationContext(options), results, true);

        Assert.False(isValid);
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(OutputOptions.FileName)));
    }

    [Fact]
    public void AcceptsInvalidOutputPathWhenFileOutputIsDisabled()
    {
        var results = new List<ValidationResult>();
        var options = new OutputOptions { Enabled = false, FilePath = "", FileName = Path.GetFullPath("CurrentSong.txt") };

        var isValid = Validator.TryValidateObject(options, new ValidationContext(options), results, true);

        Assert.True(isValid);
        Assert.Empty(results);
    }

    [Fact]
    public void AcceptsMissingRdsPortWhenRdsIsDisabled()
    {
        var results = new List<ValidationResult>();
        var options = new RdsOptions { Enabled = false, PortName = "" };

        var isValid = Validator.TryValidateObject(options, new ValidationContext(options), results, true);

        Assert.True(isValid);
        Assert.Empty(results);
    }

    [Fact]
    public void NormalizesAndValidatesEnabledRdsOptions()
    {
        var results = new List<ValidationResult>();
        var options = new RdsOptions
        {
            Enabled = true,
            PortName = "COM3",
            StaticProgramService = "LT  SHOW",
            DynamicProgramService = "Compound  Radio",
            AdditionalRadioTextMessages = [new string('a', 65)]
        };

        var isValid = Validator.TryValidateObject(options, new ValidationContext(options), results, true);

        Assert.False(isValid);
        Assert.Equal("LT SHOW", options.StaticProgramService);
        Assert.Equal("Compound Radio", options.DynamicProgramService);
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(RdsOptions.AdditionalRadioTextMessages)));
    }

    [Fact]
    public void RejectsEnabledRdsWithoutPortOrWithInvalidTiming()
    {
        var results = new List<ValidationResult>();
        var options = new RdsOptions
        {
            Enabled = true,
            LabelPeriod = TimeSpan.FromSeconds(138),
            LoopDelay = TimeSpan.FromSeconds(-1),
            RadioTextRotationInterval = TimeSpan.Zero
        };

        var isValid = Validator.TryValidateObject(options, new ValidationContext(options), results, true);

        Assert.False(isValid);
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(RdsOptions.PortName)));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(RdsOptions.LabelPeriod)));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(RdsOptions.LoopDelay)));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(RdsOptions.RadioTextRotationInterval)));
    }

    [Fact]
    public void RejectsAnUnsupportedMrds192ControlLineTransport()
    {
        var results = new List<ValidationResult>();
        var options = new RdsOptions
        {
            Enabled = true,
            PortName = "COM3",
            StaticProgramService = "LTSHOW",
            DynamicProgramService = "Compound Radio",
            ControlLineTransport = (Mrds192ControlLineTransport)99
        };

        var isValid = Validator.TryValidateObject(options, new ValidationContext(options), results, true);

        Assert.False(isValid);
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(RdsOptions.ControlLineTransport)));
    }
}
