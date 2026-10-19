using System.Text;
using NonSteamLauncher.Sync.Steam;
using Xunit;

namespace NonSteamLauncher.Tests;

public class VdfBinaryTests
{
    [Fact]
    public void Roundtrip_SingleShortcut_PreservesAllFields()
    {
        var original = new SteamShortcut
        {
            AppId = 12345678,
            AppName = "Alan Wake 2",
            Exe = "\"C:\\Tools\\NonSteamLauncher.exe\"",
            StartDir = "\"C:\\Tools\\\"",
            Icon = "C:\\Games\\AlanWake2\\AlanWake2.exe",
            LaunchOptions = "--launch \"com.epicgames.launcher://apps/Item\" --close-launcher --disable-overlay",
            IsHidden = 0,
            AllowDesktopConfig = 1,
            AllowOverlay = 1,
            OpenVR = 0,
            Devkit = 0,
            Tags = new List<string> { "Epic Games", "Horror" }
        };

        byte[] serialized = VdfBinaryWriter.WriteShortcuts(new[] { original });
        var deserializedList = VdfBinaryReader.ReadShortcuts(serialized);

        Assert.Single(deserializedList);
        var item = deserializedList[0];

        Assert.Equal(original.AppId, item.AppId);
        Assert.Equal(original.AppName, item.AppName);
        Assert.Equal(original.Exe, item.Exe);
        Assert.Equal(original.StartDir, item.StartDir);
        Assert.Equal(original.Icon, item.Icon);
        Assert.Equal(original.LaunchOptions, item.LaunchOptions);
        Assert.Equal(original.IsHidden, item.IsHidden);
        Assert.Equal(original.AllowDesktopConfig, item.AllowDesktopConfig);
        Assert.Equal(original.AllowOverlay, item.AllowOverlay);
        Assert.Equal(2, item.Tags.Count);
        Assert.Equal("Epic Games", item.Tags[0]);
        Assert.Equal("Horror", item.Tags[1]);
    }

    [Fact]
    public void Roundtrip_MultipleShortcuts_PreservesIndicesAndOrder()
    {
        var shortcuts = new List<SteamShortcut>
        {
            new() { AppId = 1001, AppName = "Diablo IV", Exe = "C:\\Tools\\NonSteamLauncher.exe", Tags = new() { "Battle.net" } },
            new() { AppId = 1002, AppName = "Far Cry 6", Exe = "C:\\Tools\\NonSteamLauncher.exe", Tags = new() { "Ubisoft Connect" } },
            new() { AppId = 1003, AppName = "Cyberpunk 2077", Exe = "C:\\GOG\\Cyberpunk.exe", Tags = new() { "GOG Galaxy" } }
        };

        byte[] serialized = VdfBinaryWriter.WriteShortcuts(shortcuts);
        var deserialized = VdfBinaryReader.ReadShortcuts(serialized);

        Assert.Equal(3, deserialized.Count);
        Assert.Equal("Diablo IV", deserialized[0].AppName);
        Assert.Equal("Far Cry 6", deserialized[1].AppName);
        Assert.Equal("Cyberpunk 2077", deserialized[2].AppName);
    }

    [Fact]
    public void ReadShortcuts_EmptyStream_ReturnsEmptyList()
    {
        var shortcuts = VdfBinaryReader.ReadShortcuts(Array.Empty<byte>());
        Assert.Empty(shortcuts);
    }

    [Fact]
    public void SteamCrc32_GeneratesConsistentAppId()
    {
        string exe = "\"C:\\Tools\\NonSteamLauncher.exe\"";
        string appName = "Alan Wake 2";

        uint appId1 = SteamCrc32.GenerateAppId(exe, appName);
        uint appId2 = SteamCrc32.GenerateAppId(exe, appName);

        Assert.Equal(appId1, appId2);
        // High bit must be set for Steam non-steam shortcut AppIDs
        Assert.True((appId1 & 0x80000000) != 0);
    }
}
