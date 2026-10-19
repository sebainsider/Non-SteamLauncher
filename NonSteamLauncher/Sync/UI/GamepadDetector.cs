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

    public static GamepadType DetectConnectedGamepad()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return GamepadType.Keyboard;

        try
        {
            // 1. Check RawInput HID devices for Sony (0x054C) or Nintendo (0x057E)
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
                            uint nameLen = 0;
                            GetRawInputDeviceInfo(dev.hDevice, RIDI_DEVICENAME, IntPtr.Zero, ref nameLen);
                            if (nameLen > 0)
                            {
                                IntPtr pName = Marshal.AllocHGlobal((int)(nameLen * 2));
                                try
                                {
                                    if (GetRawInputDeviceInfo(dev.hDevice, RIDI_DEVICENAME, pName, ref nameLen) != uint.MaxValue)
                                    {
                                        string? devName = Marshal.PtrToStringAuto(pName);
                                        if (devName != null)
                                        {
                                            if (devName.Contains("vid_054c", StringComparison.OrdinalIgnoreCase))
                                                return GamepadType.PlayStation;

                                            if (devName.Contains("vid_057e", StringComparison.OrdinalIgnoreCase))
                                                return GamepadType.Nintendo;
                                        }
                                    }
                                }
                                finally
                                {
                                    Marshal.FreeHGlobal(pName);
                                }
                            }
                        }
                    }
                }
            }

            // 2. Check XInput (Xbox & generic XInput gamepads)
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
                    return GamepadType.Xbox;
                }
            }
        }
        catch
        {
            // Ignore detection errors, fallback to keyboard
        }

        return GamepadType.Keyboard;
    }
}
