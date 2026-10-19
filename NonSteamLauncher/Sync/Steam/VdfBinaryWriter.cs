using System.Text;

namespace NonSteamLauncher.Sync.Steam;

public static class VdfBinaryWriter
{
    public static void WriteShortcuts(Stream stream, IReadOnlyList<SteamShortcut> shortcuts)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        WriteShortcuts(writer, shortcuts);
    }

    public static byte[] WriteShortcuts(IReadOnlyList<SteamShortcut> shortcuts)
    {
        using var ms = new MemoryStream();
        WriteShortcuts(ms, shortcuts);
        return ms.ToArray();
    }

    public static void WriteShortcuts(BinaryWriter writer, IReadOnlyList<SteamShortcut> shortcuts)
    {
        // Root map: 0x00 + "shortcuts\0"
        writer.Write((byte)0x00);
        WriteNullTerminatedString(writer, "shortcuts");

        for (int i = 0; i < shortcuts.Count; i++)
        {
            var s = shortcuts[i];

            // Entry map: 0x00 + "{i}\0"
            writer.Write((byte)0x00);
            WriteNullTerminatedString(writer, i.ToString());

            // appid (Int32)
            writer.Write((byte)0x02);
            WriteNullTerminatedString(writer, "appid");
            writer.Write(unchecked((int)s.AppId));

            // AppName (String)
            WriteString(writer, "AppName", s.AppName);

            // Exe (String)
            WriteString(writer, "Exe", s.Exe);

            // StartDir (String)
            WriteString(writer, "StartDir", s.StartDir);

            // icon (String)
            WriteString(writer, "icon", s.Icon);

            // ShortcutPath (String)
            WriteString(writer, "ShortcutPath", s.ShortcutPath);

            // LaunchOptions (String)
            WriteString(writer, "LaunchOptions", s.LaunchOptions);

            // IsHidden (Int32)
            WriteInt(writer, "IsHidden", s.IsHidden);

            // AllowDesktopConfig (Int32)
            WriteInt(writer, "AllowDesktopConfig", s.AllowDesktopConfig);

            // AllowOverlay (Int32)
            WriteInt(writer, "AllowOverlay", s.AllowOverlay);

            // OpenVR (Int32)
            WriteInt(writer, "OpenVR", s.OpenVR);

            // Devkit (Int32)
            WriteInt(writer, "Devkit", s.Devkit);

            // DevkitGameID (String)
            WriteString(writer, "DevkitGameID", s.DevkitGameID);

            // DevkitOverrideAppId (Int32)
            WriteInt(writer, "DevkitOverrideAppId", s.DevkitOverrideAppId);

            // LastPlayTime (Int32)
            WriteInt(writer, "LastPlayTime", s.LastPlayTime);

            // FlatpakAppID (String)
            WriteString(writer, "FlatpakAppID", s.FlatpakAppID);

            // tags (Sub-map)
            writer.Write((byte)0x00);
            WriteNullTerminatedString(writer, "tags");
            for (int t = 0; t < s.Tags.Count; t++)
            {
                WriteString(writer, t.ToString(), s.Tags[t]);
            }
            writer.Write((byte)0x08); // End of tags

            // ExtraProperties if any
            foreach (var kvp in s.ExtraProperties)
            {
                if (kvp.Value is string str)
                {
                    WriteString(writer, kvp.Key, str);
                }
                else if (kvp.Value is int num)
                {
                    WriteInt(writer, kvp.Key, num);
                }
            }

            // End of shortcut entry
            writer.Write((byte)0x08);
        }

        // End of "shortcuts" map
        writer.Write((byte)0x08);

        // End of document
        writer.Write((byte)0x08);
    }

    private static void WriteString(BinaryWriter writer, string key, string value)
    {
        writer.Write((byte)0x01);
        WriteNullTerminatedString(writer, key);
        WriteNullTerminatedString(writer, value ?? string.Empty);
    }

    private static void WriteInt(BinaryWriter writer, string key, int value)
    {
        writer.Write((byte)0x02);
        WriteNullTerminatedString(writer, key);
        writer.Write(value);
    }

    private static void WriteNullTerminatedString(BinaryWriter writer, string text)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        writer.Write(bytes);
        writer.Write((byte)0x00);
    }
}
