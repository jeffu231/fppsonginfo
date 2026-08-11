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
}
