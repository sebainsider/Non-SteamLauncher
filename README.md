# Non-Steam Launcher 🚀

<p align="center">
  <img src="NonSteamLauncher/icon.png" width="128" alt="Non-Steam Launcher Icon" /><br>
  <strong>v1.5.2</strong>
</p>

**Non-Steam Launcher** (official project name: **Non-SteamLauncher**) is a lightweight, zero-dependency .NET 9 utility for Windows designed to solve Steam's inability to detect when games launched via third-party launchers (Epic Games Store, EA App, Ubisoft Connect, Battle.net, GOG Galaxy, etc.) exit.

---

## 💡 The Problem & The Solution

### The Problem
When you add a non-Steam game or custom launcher shortcut to Steam, Steam launches the launcher (e.g. Epic Games Launcher). When the game starts, the launcher process stays running in the background. As far as Steam is concerned, the "game" has started and never finishes, keeping your status locked to "In-Game" forever.

### The Solution (Smart Auto-Detection 🧠)
Non-Steam Launcher automates process detection with zero configuration required for 95%+ of games:

1. **Snapshot**: Takes a snapshot of all active system processes.
2. **Launch**: Executes the specified game URI or launcher command.
3. **Smart Auto-Detect**: Monitors process creation, ignores known background launcher processes (`EpicGamesLauncher`, `EADesktop`, `UbisoftConnect`, `Battle.net`, etc.), and detects the newly spawned game executable (e.g. `AlanWake2.exe`).
4. **Lifetime Tracking**: Tracks the game process until all instances exit, then exits cleanly with exit code `0`, allowing Steam to accurately record play time and update your status!

---

## ⚡ Quick Start & Usage

### Basic Usage (Auto-Detection)
```cmd
NonSteamLauncher.exe "com.epicgames.launcher://apps/Item?action=launch"
```
*(You can also explicitly pass `--launch` / `-l`)*:
```cmd
NonSteamLauncher.exe --launch "com.epicgames.launcher://apps/Item?action=launch" --close-launcher
```

### Options & Parameters

| Parameter | Short / Aliases | Required | Description | Default |
|---|---|---|---|---|
| `--launch` | `-l` | **Yes*** | Launcher URI, command line, or executable path to launch (*can also be passed directly as 1st positional argument) | N/A |
| `--process` | `-p` | No | Explicit process name (without `.exe`) to monitor. Override auto-detection if needed | Auto-detect |
| `--timeout` | `-t` | No | Maximum time in seconds to wait for the target game process to start | `60` |
| `--sync-delay` | `-s`, `-d`, `--delay` | No | Delay in seconds after game exits before closing the launcher, allowing cloud saves to sync | `5` |
| `--close-launcher` | `-c` | No | Terminate third-party launcher processes (Epic, EA App, Ubisoft, Battle.net, etc.) when game exits | `false` |
| `--disable-overlay` | `-o`, `--kill-overlay`, `--no-overlay` | No | Terminate third-party launcher overlay processes (e.g. `EOSOverlayRenderer`) to suppress unwanted in-game popups and overlays | `false` |
| `--verbose` | `-v` | No | Enable detailed debug logging output | `false` |
| `--help` | `-h`, `/?` | No | Display usage help message and exit | N/A |

> **Note on Syntax**: Options can be passed as `--option value`, `-o value`, or `--option=value`. If passing arguments without flags, the 1st positional argument is treated as `--launch` command and the 2nd positional argument (if provided) is treated as `--process`.



---

## 🎮 Steam Integration Examples

To use Non-Steam Launcher with Steam:

1. Download or publish `NonSteamLauncher.exe`.
2. Place `NonSteamLauncher.exe` in a convenient directory (e.g., `C:\Tools\NonSteamLauncher.exe`).
3. In Steam, click **Games** -> **Add a Non-Steam Game to My Library...**
4. Select `NonSteamLauncher.exe`.
5. Right-click the newly added shortcut in Steam -> **Properties**.
6. Set the **Target** and **Launch Options** as shown in the examples below:

---

### 1. Epic Games Store
Launch games via Epic Games Store URIs (add `--close-launcher` / `-c` to close Epic when done, and `--disable-overlay` / `-o` to disable Epic Online Services in-game overlay):

- **Target**: `"C:\Tools\NonSteamLauncher.exe"`
- **Launch Options**: `"com.epicgames.launcher://apps/6f438871317448e8a83d42042079148d%3A5f6c8d37a1f54460a5e8f49ef2c4a9a0%3AFrogmores?action=launch&silent=true" --close-launcher --disable-overlay`

*Non-Steam Launcher will automatically snapshot processes, launch Epic, suppress intrusive overlay popups (`EOSOverlayRenderer`), detect `AlanWake2.exe`, track it to completion, and close Epic Launcher.*

---

### 2. EA App
Launch games via EA App protocol URIs or executables:

- **Target**: `"C:\Tools\NonSteamLauncher.exe"`
- **Launch Options**: `"origin2://game/launch?offerIds=1000001&authCode=" --close-launcher`

*(Optional fallback if auto-detect is bypassed)*:
```cmd
NonSteamLauncher.exe "origin2://game/launch?offerIds=1000001" --process "EASportsFC24" --close-launcher
```

---

### 3. Ubisoft Connect
Launch games via Ubisoft Connect URIs (`uplay://launch/<GameID>/0`):

- **Target**: `"C:\Tools\NonSteamLauncher.exe"`
- **Launch Options**: `"uplay://launch/5105/0" --close-launcher`

---

### 4. Battle.net
Launch games via Battle.net URIs (`battlenet://`):

- **Target**: `"C:\Tools\NonSteamLauncher.exe"`
- **Launch Options**: `"battlenet://Fen" --close-launcher`

---

## 🛠️ Building & Publishing

Non-Steam Launcher is built using **.NET 9** and targets Windows. It is compiled as a self-contained, single-file executable with no external dependencies required on the target machine.

### Build & Run Tests
```bash
dotnet build NonSteamLauncher.sln
dotnet test NonSteamLauncher.sln
```

### Publish Lightweight Executable (Windows x64 - ~182 KB)
```bash
dotnet publish NonSteamLauncher/NonSteamLauncher.csproj \
  -c Release \
  -r win-x64 \
  --self-contained false \
  -p:PublishSingleFile=true \
  -p:EnableCompressionInSingleFile=false
```

### Publish Self-Contained Executable (Windows x64 - ~35 MB)
```bash
dotnet publish NonSteamLauncher/NonSteamLauncher.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:EnableCompressionInSingleFile=true
```

The published executable will be generated at:
<<<<<<< HEAD
`SteamLauncherManager/bin/Release/net9.0/win-x64/publish/NonSteamLauncher.exe`
=======
`NonSteamLauncher/bin/Release/net9.0/win-x64/publish/NonSteamLauncher.exe`
>>>>>>> 55f1879 (Rename project to Non-Steam Launcher (NonSteamLauncher))

---

## 📄 License
MIT License. Free for personal and commercial use.
