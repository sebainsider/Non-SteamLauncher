using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace NonSteamLauncher.Sync;

public class AppSettings
{
    [JsonPropertyName("steamGridDbApiKey")]
    public string? SteamGridDbApiKey { get; set; }

    [JsonPropertyName("downloadArtwork")]
    public bool DownloadArtwork { get; set; } = true;

    public static string GetDefaultSettingsFilePath()
    {
        try
        {
            string baseDir = AppContext.BaseDirectory;
            if (!string.IsNullOrEmpty(baseDir))
            {
                string portablePath = Path.Combine(baseDir, "settings.json");
                if (File.Exists(portablePath))
                {
                    return portablePath;
                }
            }
        }
        catch { }

        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrEmpty(localAppData))
        {
            localAppData = AppContext.BaseDirectory;
        }

        return Path.Combine(localAppData, "NonSteamLauncher", "settings.json");
    }

    public static AppSettings Load(string? customFilePath = null)
    {
        var settings = new AppSettings();
        string path = !string.IsNullOrWhiteSpace(customFilePath)
            ? customFilePath
            : GetDefaultSettingsFilePath();

        if (File.Exists(path))
        {
            try
            {
                string json = File.ReadAllText(path);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded != null)
                {
                    settings = loaded;
                }
            }
            catch
            {
                // Ignore malformed settings file and use defaults
            }
        }

        // Fallback 1: Environment variable STEAMGRIDDB_API_KEY
        if (string.IsNullOrWhiteSpace(settings.SteamGridDbApiKey))
        {
            string? envKey = Environment.GetEnvironmentVariable("STEAMGRIDDB_API_KEY");
            if (!string.IsNullOrWhiteSpace(envKey))
            {
                settings.SteamGridDbApiKey = envKey.Trim();
            }
        }

        // Fallback 2: Auto-import API key from BoilR config (%APPDATA%\boilr\config.toml) if user migrated from BoilR
        if (string.IsNullOrWhiteSpace(settings.SteamGridDbApiKey))
        {
            string? boilrKey = TryReadBoilrApiKey();
            if (!string.IsNullOrWhiteSpace(boilrKey))
            {
                settings.SteamGridDbApiKey = boilrKey;
            }
        }

        return settings;
    }

    public void Save(string? customFilePath = null)
    {
        string path = !string.IsNullOrWhiteSpace(customFilePath)
            ? customFilePath
            : GetDefaultSettingsFilePath();

        try
        {
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            string json = JsonSerializer.Serialize(this, jsonOptions);
            File.WriteAllText(path, json);
        }
        catch
        {
            // Ignore write errors in restricted environments
        }
    }

    public static string? TryReadBoilrApiKey()
    {
        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrEmpty(appData))
                return null;

            string boilrConfig = Path.Combine(appData, "boilr", "config.toml");
            if (!File.Exists(boilrConfig))
                return null;

            string content = File.ReadAllText(boilrConfig);
            var match = Regex.Match(content, @"auth_key\s*=\s*[""']([a-zA-Z0-9]{16,64})[""']", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }
        }
        catch { }

        return null;
    }
}
