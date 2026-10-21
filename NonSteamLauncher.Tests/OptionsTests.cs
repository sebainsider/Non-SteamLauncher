using NonSteamLauncher;
using Xunit;

namespace NonSteamLauncher.Tests;

public class OptionsTests
{
    [Fact]
    public void Parse_WithNoArgs_DefaultsToSyncSteam()
    {
        var (options, error) = Options.Parse(Array.Empty<string>());

        Assert.Null(error);
        Assert.NotNull(options);
        Assert.True(options.SyncSteam);
        Assert.False(options.Silent);
        Assert.False(options.ShowHelp);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("/?")]
    public void Parse_WithHelpFlag_ShowsHelp(string flag)
    {
        var (options, error) = Options.Parse(new[] { flag });

        Assert.Null(error);
        Assert.NotNull(options);
        Assert.True(options.ShowHelp);
    }

    [Theory]
    [InlineData("--silent")]
    [InlineData("--quiet")]
    [InlineData("-q")]
    public void Parse_WithSilentFlags_SetsSilentProperty(string flag)
    {
        var (options, error) = Options.Parse(new[] { flag });

        Assert.Null(error);
        Assert.NotNull(options);
        Assert.True(options.Silent);
        Assert.True(options.SyncSteam);
    }

    [Fact]
    public void Parse_WithLaunchCommand_ReturnsOptions()
    {
        var args = new[] { "--launch", "com.epicgames.launcher://apps/Item?action=launch" };
        var (options, error) = Options.Parse(args);

        Assert.Null(error);
        Assert.NotNull(options);
        Assert.Equal("com.epicgames.launcher://apps/Item?action=launch", options.LaunchCommand);
        Assert.Equal(60, options.TimeoutSeconds);
        Assert.Null(options.ProcessName);
        Assert.False(options.Verbose);
    }

    [Fact]
    public void Parse_WithShortLaunchArg_ReturnsOptions()
    {
        var args = new[] { "-l", "com.epicgames.launcher://apps/Item" };
        var (options, error) = Options.Parse(args);

        Assert.Null(error);
        Assert.NotNull(options);
        Assert.Equal("com.epicgames.launcher://apps/Item", options.LaunchCommand);
    }

    [Fact]
    public void Parse_WithProcessName_StripsExeExtension()
    {
        var args = new[] { "--launch", "myuri://", "--process", "AlanWake2.exe" };
        var (options, error) = Options.Parse(args);

        Assert.Null(error);
        Assert.NotNull(options);
        Assert.Equal("AlanWake2", options.ProcessName);
    }

    [Fact]
    public void Parse_WithCustomTimeoutAndVerbose_ParsesCorrectly()
    {
        var args = new[] { "--launch", "myuri://", "--timeout", "120", "--verbose" };
        var (options, error) = Options.Parse(args);

        Assert.Null(error);
        Assert.NotNull(options);
        Assert.Equal(120, options.TimeoutSeconds);
        Assert.True(options.Verbose);
    }

    [Fact]
    public void Parse_InvalidTimeout_ReturnsError()
    {
        var args = new[] { "--launch", "myuri://", "--timeout", "invalid" };
        var (options, error) = Options.Parse(args);

        Assert.NotNull(error);
        Assert.Null(options);
        Assert.Contains("Invalid timeout value", error);
    }

    [Fact]
    public void Parse_MissingLaunchCommand_ReturnsError()
    {
        var args = new[] { "--process", "AlanWake2" };
        var (options, error) = Options.Parse(args);

        Assert.NotNull(error);
        Assert.Null(options);
        Assert.Contains("Missing required argument", error);
    }

    [Fact]
    public void Parse_WithCloseLauncherFlag_SetsCloseLauncherProperty()
    {
        var args = new[] { "--launch", "com.epicgames.launcher://apps/Item", "--close-launcher" };
        var (options, error) = Options.Parse(args);

        Assert.Null(error);
        Assert.NotNull(options);
        Assert.True(options.CloseLauncher);

        var argsShort = new[] { "--launch", "com.epicgames.launcher://apps/Item", "-c" };
        var (optionsShort, errorShort) = Options.Parse(argsShort);

        Assert.Null(errorShort);
        Assert.NotNull(optionsShort);
        Assert.True(optionsShort.CloseLauncher);
    }

    [Fact]
    public void Parse_WithSyncDelayFlag_SetsSyncDelayProperty()
    {
        var args = new[] { "--launch", "com.epicgames.launcher://apps/Item", "--sync-delay", "10" };
        var (options, error) = Options.Parse(args);

        Assert.Null(error);
        Assert.NotNull(options);
        Assert.Equal(10, options.SyncDelaySeconds);

        var argsShort = new[] { "--launch", "com.epicgames.launcher://apps/Item", "-s", "8" };
        var (optionsShort, errorShort) = Options.Parse(argsShort);

        Assert.Null(errorShort);
        Assert.NotNull(optionsShort);
        Assert.Equal(8, optionsShort.SyncDelaySeconds);
    }

    [Theory]
    [InlineData("--disable-overlay")]
    [InlineData("-o")]
    [InlineData("--kill-overlay")]
    [InlineData("--no-overlay")]
    public void Parse_WithDisableOverlayFlags_SetsDisableOverlayProperty(string flag)
    {
        var args = new[] { "--launch", "com.epicgames.launcher://apps/Item", flag };
        var (options, error) = Options.Parse(args);

        Assert.Null(error);
        Assert.NotNull(options);
        Assert.True(options.DisableOverlay);
    }

    [Theory]
    [InlineData("--sync-steam")]
    [InlineData("--sync")]
    public void Parse_WithSyncSteam_DoesNotRequireLaunchCommand(string flag)
    {
        var args = new[] { flag };
        var (options, error) = Options.Parse(args);

        Assert.Null(error);
        Assert.NotNull(options);
        Assert.True(options.SyncSteam);
    }

    [Theory]
    [InlineData("--list-games")]
    [InlineData("--scan")]
    public void Parse_WithListGames_DoesNotRequireLaunchCommand(string flag)
    {
        var args = new[] { flag };
        var (options, error) = Options.Parse(args);

        Assert.Null(error);
        Assert.NotNull(options);
        Assert.True(options.ListGames);
    }

    [Fact]
    public void Parse_WithSteamPathAndDryRun_ParsesCorrectly()
    {
        var args = new[] { "--sync-steam", "--steam-path", "C:\\CustomSteam", "--dry-run" };
        var (options, error) = Options.Parse(args);

        Assert.Null(error);
        Assert.NotNull(options);
        Assert.True(options.SyncSteam);
        Assert.Equal("C:\\CustomSteam", options.SteamPath);
        Assert.True(options.DryRun);
    }

    [Fact]
    public void Parse_WithSgdbKeyAndNoArtwork_ParsesCorrectly()
    {
        var args = new[] { "--sgdb-key", "abc123key", "--no-artwork", "--settings", "C:\\custom\\settings.json" };
        var (options, error) = Options.Parse(args);

        Assert.Null(error);
        Assert.NotNull(options);
        Assert.True(options.SyncSteam);
        Assert.Equal("abc123key", options.SteamGridDbApiKey);
        Assert.True(options.SkipArtwork);
        Assert.Equal("C:\\custom\\settings.json", options.SettingsFilePath);
    }
}
