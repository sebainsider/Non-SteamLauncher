using System.Text;

namespace NonSteamLauncher.Sync.Steam;

public static class VdfBinaryReader
{
    public static List<SteamShortcut> ReadShortcuts(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        return ReadShortcuts(reader);
    }

    public static List<SteamShortcut> ReadShortcuts(byte[] data)
    {
        using var ms = new MemoryStream(data);
        return ReadShortcuts(ms);
    }

    public static List<SteamShortcut> ReadShortcuts(BinaryReader reader)
    {
        var shortcuts = new List<SteamShortcut>();

        if (reader.BaseStream.Position >= reader.BaseStream.Length)
            return shortcuts;

        // Root map: expect 0x00 then "shortcuts"
        byte rootType = reader.ReadByte();
        if (rootType != 0x00)
            throw new FormatException($"Invalid VDF binary header: expected 0x00, got 0x{rootType:X2}");

        string rootName = ReadNullTerminatedString(reader);
        if (!rootName.Equals("shortcuts", StringComparison.OrdinalIgnoreCase))
            throw new FormatException($"Invalid VDF root name: expected 'shortcuts', got '{rootName}'");

        // Read shortcut entries
        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            byte entryType = reader.ReadByte();
            if (entryType == 0x08) // End of "shortcuts" map
            {
                break;
            }

            if (entryType != 0x00)
                throw new FormatException($"Expected 0x00 for shortcut entry, got 0x{entryType:X2} at pos {reader.BaseStream.Position - 1}");

            string entryIndex = ReadNullTerminatedString(reader);
            var shortcut = ReadShortcutEntry(reader);
            shortcuts.Add(shortcut);
        }

        return shortcuts;
    }

    private static SteamShortcut ReadShortcutEntry(BinaryReader reader)
    {
        var shortcut = new SteamShortcut();

        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            byte type = reader.ReadByte();
            if (type == 0x08) // End of this shortcut entry
            {
                break;
            }

            string key = ReadNullTerminatedString(reader);

            switch (type)
            {
                case 0x01: // String
                    string strVal = ReadNullTerminatedString(reader);
                    ApplyStringProperty(shortcut, key, strVal);
                    break;

                case 0x02: // Int32
                    int intVal = reader.ReadInt32();
                    ApplyIntProperty(shortcut, key, intVal);
                    break;

                case 0x00: // Sub-map (e.g. tags)
                    if (key.Equals("tags", StringComparison.OrdinalIgnoreCase))
                    {
                        shortcut.Tags = ReadTagsSubMap(reader);
                    }
                    else
                    {
                        SkipSubMap(reader);
                    }
                    break;

                default:
                    throw new FormatException($"Unknown VDF property type 0x{type:X2} for key '{key}' at pos {reader.BaseStream.Position}");
            }
        }

        return shortcut;
    }

    private static List<string> ReadTagsSubMap(BinaryReader reader)
    {
        var tags = new List<string>();

        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            byte type = reader.ReadByte();
            if (type == 0x08) // End of tags
                break;

            string key = ReadNullTerminatedString(reader);
            if (type == 0x01)
            {
                string tagVal = ReadNullTerminatedString(reader);
                tags.Add(tagVal);
            }
            else if (type == 0x02)
            {
                reader.ReadInt32();
            }
            else if (type == 0x00)
            {
                SkipSubMap(reader);
            }
        }

        return tags;
    }

    private static void SkipSubMap(BinaryReader reader)
    {
        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            byte type = reader.ReadByte();
            if (type == 0x08)
                break;

            ReadNullTerminatedString(reader); // key
            if (type == 0x01)
            {
                ReadNullTerminatedString(reader);
            }
            else if (type == 0x02)
            {
                reader.ReadInt32();
            }
            else if (type == 0x00)
            {
                SkipSubMap(reader);
            }
        }
    }

    private static void ApplyStringProperty(SteamShortcut s, string key, string val)
    {
        switch (key)
        {
            case "AppName": s.AppName = val; break;
            case "Exe": s.Exe = val; break;
            case "StartDir": s.StartDir = val; break;
            case "icon": s.Icon = val; break;
            case "ShortcutPath": s.ShortcutPath = val; break;
            case "LaunchOptions": s.LaunchOptions = val; break;
            case "DevkitGameID": s.DevkitGameID = val; break;
            case "FlatpakAppID": s.FlatpakAppID = val; break;
            default: s.ExtraProperties[key] = val; break;
        }
    }

    private static void ApplyIntProperty(SteamShortcut s, string key, int val)
    {
        switch (key)
        {
            case "appid": s.AppId = unchecked((uint)val); break;
            case "IsHidden": s.IsHidden = val; break;
            case "AllowDesktopConfig": s.AllowDesktopConfig = val; break;
            case "AllowOverlay": s.AllowOverlay = val; break;
            case "OpenVR": s.OpenVR = val; break;
            case "Devkit": s.Devkit = val; break;
            case "DevkitOverrideAppId": s.DevkitOverrideAppId = val; break;
            case "LastPlayTime": s.LastPlayTime = val; break;
            default: s.ExtraProperties[key] = val; break;
        }
    }

    public static string ReadNullTerminatedString(BinaryReader reader)
    {
        var bytes = new List<byte>();
        while (true)
        {
            if (reader.BaseStream.Position >= reader.BaseStream.Length)
                break;

            byte b = reader.ReadByte();
            if (b == 0x00)
                break;
            bytes.Add(b);
        }
        return Encoding.UTF8.GetString(bytes.ToArray());
    }
}
