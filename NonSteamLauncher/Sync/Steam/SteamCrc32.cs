using System.Text;

namespace NonSteamLauncher.Sync.Steam;

public static class SteamCrc32
{
    private static readonly uint[] Table;

    static SteamCrc32()
    {
        const uint poly = 0xEDB88320;
        Table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint crc = i;
            for (int j = 8; j > 0; j--)
            {
                if ((crc & 1) == 1)
                    crc = (crc >> 1) ^ poly;
                else
                    crc >>= 1;
            }
            Table[i] = crc;
        }
    }

    public static uint Compute(byte[] bytes)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte b in bytes)
        {
            byte tableIndex = (byte)((crc & 0xFF) ^ b);
            crc = (crc >> 8) ^ Table[tableIndex];
        }
        return ~crc;
    }

    public static uint GenerateAppId(string exe, string appName)
    {
        string cleanExe = exe.Trim('"');
        string input = cleanExe + appName;
        byte[] bytes = Encoding.UTF8.GetBytes(input);
        uint crc = Compute(bytes);
        return crc | 0x80000000;
    }
}
