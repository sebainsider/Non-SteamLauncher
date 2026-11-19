using NonSteamLauncher;
using Xunit;

namespace NonSteamLauncher.Tests;

public class LauncherFilterTests
{
    [Theory]
    [InlineData("EpicGamesLauncher")]
    [InlineData("EpicGamesLauncher.exe")]
    [InlineData("EpicWebHelper")]
    [InlineData("EpicOnlineServicesHost")]
    [InlineData("EpicOnlineServicesHost.exe")]
    [InlineData("EOSOverlayRenderer-Win64-Shipping")]
    [InlineData("EOSOverlayRenderer-Win32-Shipping.exe")]
    [InlineData("EOSSDK-Win64-Shipping")]
    [InlineData("CrashReportClient")]
    [InlineData("CrashReportClient-Win64-Shipping.exe")]
    [InlineData("EasyAntiCheat_EOS")]
    [InlineData("EasyAntiCheat_x64.exe")]
    [InlineData("BEService")]
    [InlineData("EADesktop")]
    [InlineData("EADesktop.exe")]
    [InlineData("Origin")]
    [InlineData("UbisoftConnect")]
    [InlineData("upc.exe")]
    [InlineData("Battle.net")]
    [InlineData("GalaxyClient")]
    [InlineData("GOG Galaxy Notifications Renderer")]
    [InlineData("REDprelauncher")]
    [InlineData("REDprelauncher.exe")]
    [InlineData("REDlauncher")]
    [InlineData("REDlauncher.exe")]
    [InlineData("GameBar")]
    [InlineData("XboxGameBarWidgets")]
    [InlineData("RuntimeBroker")]
    [InlineData("msedge")]
    [InlineData("nvngx_update")]
    [InlineData("FileCoAuth")]
    [InlineData("git")]
    [InlineData("steam")]
    [InlineData("gamelaunchhelper")]
    [InlineData("gamelaunchhelper.exe")]
    [InlineData("XboxPcApp")]
    [InlineData("GamingServices")]
    [InlineData("cmd.exe")]
    [InlineData("powershell")]
    [InlineData("LauncherBridge")]
    [InlineData("SteamLauncherManager")]
    [InlineData("NonSteamLauncher")]
    [InlineData("Non-SteamLauncher")]
    [InlineData("NonSteamLauncher.exe")]
    public void IsLauncherOrSystemProcess_ReturnsTrue_ForKnownLaunchers(string processName)
    {
        Assert.True(LauncherFilter.IsLauncherOrSystemProcess(processName));
    }

    [Theory]
    [InlineData("AlanWake2")]
    [InlineData("AlanWake2.exe")]
    [InlineData("witcher3")]
    [InlineData("witcher3.exe")]
    [InlineData("Cyberpunk2077")]
    [InlineData("GTA5")]
    [InlineData("EASportsFC24")]
    [InlineData("DiabloIV.exe")]
    [InlineData("FortniteClient-Win64-Shipping")]
    [InlineData("ForzaHorizon5")]
    [InlineData("ForzaHorizon5.exe")]
    [InlineData("Palworld-WinGDK-Shipping")]
    public void IsLauncherOrSystemProcess_ReturnsFalse_ForGameProcesses(string processName)
    {
        Assert.False(LauncherFilter.IsLauncherOrSystemProcess(processName));
    }
}

