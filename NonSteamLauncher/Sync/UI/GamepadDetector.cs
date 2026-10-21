using System.Runtime.InteropServices;

namespace NonSteamLauncher.Sync.UI;

public enum GamepadType
{
    Xbox,
    PlayStation,
    Nintendo,
    Keyboard
}

public static class GamepadDetector
{
    private const uint RIDI_DEVICENAME = 0x20000007;

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTDEVICELIST
    {
        public IntPtr hDevice;
        public uint dwType; // 0 = Mouse, 1 = Keyboard, 2 = HID
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputDeviceList(
        [Out] RAWINPUTDEVICELIST[]? pRawInputDeviceList,
        ref uint puiNumDevices,
        uint cbSize);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern uint GetRawInputDeviceInfo(
        IntPtr hDevice,
        uint uiCommand,
        IntPtr pData,
        ref uint pcbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_GAMEPAD
    {
        public ushort wButtons;
        public byte bLeftTrigger;
        public byte bRightTrigger;
        public short sThumbLX;
        public short sThumbLY;
        public short sThumbRX;
        public short sThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_STATE
    {
        public uint dwPacketNumber;
        public XINPUT_GAMEPAD Gamepad;
    }

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    private static extern uint XInputGetState14(uint dwUserIndex, out XINPUT_STATE pState);

    [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")]
    private static extern uint XInputGetState910(uint dwUserIndex, out XINPUT_STATE pState);

    [StructLayout(LayoutKind.Sequential)]
    private struct JOYINFOEX
    {
        public uint dwSize;
        public uint dwFlags;
        public uint dwXpos;
        public uint dwYpos;
        public uint dwZpos;
        public uint dwRpos;
        public uint dwUpos;
        public uint dwVpos;
        public uint dwButtons;
        public uint dwButtonNumber;
        public uint dwPOV;
        public uint dwReserved1;
        public uint dwReserved2;
    }

    [DllImport("winmm.dll")]
    private static extern uint joyGetPosEx(uint uJoyID, ref JOYINFOEX pji);

    public static GamepadType DetectGamepadFamily()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return GamepadType.Xbox;

        try
        {
            uint deviceCount = 0;
            uint structSize = (uint)Marshal.SizeOf<RAWINPUTDEVICELIST>();

            if (GetRawInputDeviceList(null, ref deviceCount, structSize) == 0 && deviceCount > 0)
            {
                var devices = new RAWINPUTDEVICELIST[deviceCount];
                if (GetRawInputDeviceList(devices, ref deviceCount, structSize) != uint.MaxValue)
                {
                    foreach (var dev in devices)
                    {
                        if (dev.dwType == 2) // RIM_TYPEHID
                        {
                            string? devName = GetDeviceName(dev.hDevice);
                            if (devName != null)
                            {
                                if (devName.Contains("vid_054c", StringComparison.OrdinalIgnoreCase))
                                    return GamepadType.PlayStation;

                                if (devName.Contains("vid_057e", StringComparison.OrdinalIgnoreCase) ||
                                    devName.Contains("vid_20d6", StringComparison.OrdinalIgnoreCase))
                                    return GamepadType.Nintendo;
                            }
                        }
                    }
                }
            }
        }
        catch { }

        return GamepadType.Xbox;
    }

    public static GamepadType DetectConnectedGamepad()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return GamepadType.Keyboard;

        try
        {
            bool hasActiveXInput = false;
            for (uint i = 0; i < 4; i++)
            {
                uint err;
                try
                {
                    err = XInputGetState14(i, out _);
                }
                catch (DllNotFoundException)
                {
                    try { err = XInputGetState910(i, out _); }
                    catch { break; }
                }
                catch { break; }

                if (err == 0) // ERROR_SUCCESS
                {
                    hasActiveXInput = true;
                    break;
                }
            }

            // Inspect RawInput devices to distinguish standalone controllers from composite keyboards/dongles (e.g. MI_04)
            bool hasStandalonePlayStation = false;
            bool hasCompositePlayStationKeyboard = false;
            bool hasNintendo = false;

            uint deviceCount = 0;
            uint structSize = (uint)Marshal.SizeOf<RAWINPUTDEVICELIST>();

            if (GetRawInputDeviceList(null, ref deviceCount, structSize) == 0 && deviceCount > 0)
            {
                var devices = new RAWINPUTDEVICELIST[deviceCount];
                if (GetRawInputDeviceList(devices, ref deviceCount, structSize) != uint.MaxValue)
                {
                    foreach (var dev in devices)
                    {
                        string? devName = GetDeviceName(dev.hDevice);
                        if (devName == null)
                            continue;

                        if (dev.dwType == 1 && devName.Contains("vid_054c", StringComparison.OrdinalIgnoreCase))
                        {
                            hasCompositePlayStationKeyboard = true;
                        }
                        else if (dev.dwType == 2)
                        {
                            if (devName.Contains("vid_054c", StringComparison.OrdinalIgnoreCase))
                                hasStandalonePlayStation = true;

                            if (devName.Contains("vid_057e", StringComparison.OrdinalIgnoreCase) ||
                                devName.Contains("vid_20d6", StringComparison.OrdinalIgnoreCase))
                                hasNintendo = true;
                        }
                    }
                }
            }

            if (hasActiveXInput)
            {
                if (hasStandalonePlayStation)
                    return GamepadType.PlayStation;
                if (hasNintendo)
                    return GamepadType.Nintendo;
                return GamepadType.Xbox;
            }

            // If there is only a composite keyboard/dongle (MI_04) and no XInput controller, default to Keyboard
            if (hasCompositePlayStationKeyboard)
                return GamepadType.Keyboard;

            if (hasStandalonePlayStation || hasNintendo)
            {
                for (uint j = 0; j < 4; j++)
                {
                    try
                    {
                        var ji = new JOYINFOEX
                        {
                            dwSize = (uint)Marshal.SizeOf<JOYINFOEX>(),
                            dwFlags = 0xFF
                        };
                        if (joyGetPosEx(j, ref ji) == 0)
                        {
                            return hasStandalonePlayStation ? GamepadType.PlayStation : GamepadType.Nintendo;
                        }
                    }
                    catch { break; }
                }
            }
        }
        catch
        {
            // Ignore detection errors, fallback to keyboard
        }

        return GamepadType.Keyboard;
    }

    private static string? GetDeviceName(IntPtr hDevice)
    {
        uint nameLen = 0;
        GetRawInputDeviceInfo(hDevice, RIDI_DEVICENAME, IntPtr.Zero, ref nameLen);
        if (nameLen == 0)
            return null;

        IntPtr pName = Marshal.AllocHGlobal((int)(nameLen * 2));
        try
        {
            if (GetRawInputDeviceInfo(hDevice, RIDI_DEVICENAME, pName, ref nameLen) != uint.MaxValue)
            {
                return Marshal.PtrToStringAuto(pName);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(pName);
        }
        return null;
    }
}
