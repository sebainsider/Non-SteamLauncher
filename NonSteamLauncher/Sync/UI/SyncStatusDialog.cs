using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using NonSteamLauncher.Sync.Steam;

namespace NonSteamLauncher.Sync.UI;

public static class SyncStatusDialog
{
    public static SteamSyncResult Show(
        SteamSyncResult result,
        Func<IReadOnlyList<InstalledGame>, SteamSyncResult>? onSyncSelected = null,
        Func<SteamSyncResult>? onSettingsSaved = null,
        string? settingsFilePath = null)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            PrintConsoleFallback(result);
            return result;
        }

        try
        {
            return NativeDialogWindow.ShowDialog(result, onSyncSelected, onSettingsSaved, settingsFilePath);
        }
        catch (Exception)
        {
            PrintConsoleFallback(result);
            return result;
        }
    }

    private static void PrintConsoleFallback(SteamSyncResult result)
    {
        var strings = Localization.GetStrings();
        Console.WriteLine();
        if (result.Success)
        {
            Console.WriteLine("======================================================================");
            Console.WriteLine($"  Non-Steam Launcher: {strings.HeaderSuccess}");
            Console.WriteLine("======================================================================");
            if (result.AddedGames.Count > 0)
            {
                Console.WriteLine($"  {strings.CardHeaderAdded} ({result.AddedGames.Count}):");
                foreach (var g in result.AddedGames)
                {
                    Console.WriteLine($"   + [{g.Launcher}] {g.Title}");
                }
            }
            else if (result.PendingGames.Count > 0)
            {
                Console.WriteLine($"  { string.Format(strings.HeaderAvailableToAddFormat, result.PendingGames.Count) }:");
                foreach (var g in result.PendingGames)
                {
                    Console.WriteLine($"   [ ] [{g.Launcher}] {g.Title} ({strings.BadgeNotSynced})");
                }
            }
            else
            {
                Console.WriteLine($"  {strings.HeaderUpToDate}");
            }
            Console.WriteLine("======================================================================");
        }
        else
        {
            Console.WriteLine($"Sync Failed: {result.ErrorMessage}");
        }
    }

    private static class NativeDialogWindow
    {
        private const string WindowClassName = "NonSteamLauncher_Fluent2WinUI3Dialog_Class";

        // Logical Design Dimensions (96 DPI Base — 4px/8px Fluent 2 Grid, Content-Adaptive Height)
        private const int BaseClientWidth = 600;
        private const int MaxVisibleGameRows = 5;

        // Per-Monitor V2 DPI Awareness Contexts
        private static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new(-4);
        private static readonly IntPtr DPI_AWARENESS_CONTEXT_SYSTEM_AWARE = new(-2);

        // Win32 Window Styles
        private const uint WS_POPUP = 0x80000000;
        private const uint WS_CAPTION = 0x00C00000;
        private const uint WS_SYSMENU = 0x00080000;
        private const uint WS_VISIBLE = 0x10000000;

        private const uint CS_HREDRAW = 0x0002;
        private const uint CS_VREDRAW = 0x0001;

        // Win32 Messages
        private const uint WM_DESTROY = 0x0002;
        private const uint WM_PAINT = 0x000F;
        private const uint WM_CLOSE = 0x0010;
        private const uint WM_ERASEBKGND = 0x0014;
        private const uint WM_SYSCOLORCHANGE = 0x0015;
        private const uint WM_SETTINGCHANGE = 0x001A;
        private const uint WM_SETICON = 0x0080;
        private const uint WM_KEYDOWN = 0x0100;
        private const uint WM_CHAR = 0x0102;
        private const uint WM_TIMER = 0x0113;
        private const uint WM_MOUSEMOVE = 0x0200;
        private const uint WM_LBUTTONDOWN = 0x0201;
        private const uint WM_LBUTTONUP = 0x0202;
        private const uint WM_MOUSEWHEEL = 0x020A;
        private const uint WM_DPICHANGED = 0x02E0;
        private const uint WM_THEMECHANGED = 0x031A;
        private const uint WM_DWMCOLORIZATIONCOLORCHANGED = 0x0320;
        private const uint WM_APP_REFRESH_RESULT = 0x8001;

        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;

        // Virtual Keys
        private const int VK_BACK = 0x08;
        private const int VK_TAB = 0x09;
        private const int VK_RETURN = 0x0D;
        private const int VK_CONTROL = 0x11;
        private const int VK_ESCAPE = 0x1B;
        private const int VK_SPACE = 0x20;
        private const int VK_PRIOR = 0x21; // Page Up
        private const int VK_NEXT = 0x22;  // Page Down
        private const int VK_LEFT = 0x25;
        private const int VK_UP = 0x26;
        private const int VK_RIGHT = 0x27;
        private const int VK_DOWN = 0x28;
        private const uint CF_UNICODETEXT = 13;

        // DWM Attributes (Windows 11 Fluent 2 Integration)
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWA_BORDER_COLOR = 34;
        private const int DWMWA_CAPTION_COLOR = 35;
        private const int DWMWA_TEXT_COLOR = 36;
        private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

        private const int DWMWCP_ROUND = 2;
        private const int DWMSBT_MAINWINDOW = 2; // Mica System Backdrop

        // Registry & SystemParametersInfo Constants for System Adaptation
        private static readonly IntPtr HKEY_CURRENT_USER = unchecked((IntPtr)(int)0x80000001);
        private const uint RRF_RT_REG_DWORD = 0x00000010;
        private const uint RRF_RT_REG_BINARY = 0x00000008;
        private const uint SPI_GETWORKAREA = 0x0030;
        private const uint SPI_GETHIGHCONTRAST = 0x0042;
        private const uint HCF_HIGHCONTRASTON = 0x00000001;

        // Win32 System Color Indices (for High Contrast Mode)
        private const int COLOR_WINDOW = 5;
        private const int COLOR_WINDOWFRAME = 6;
        private const int COLOR_WINDOWTEXT = 8;
        private const int COLOR_HIGHLIGHT = 13;
        private const int COLOR_HIGHLIGHTTEXT = 14;
        private const int COLOR_3DFACE = 15;
        private const int COLOR_GRAYTEXT = 17;
        private const int COLOR_BTNTEXT = 18;

        // DrawText Flags
        private const uint DT_LEFT = 0x00000000;
        private const uint DT_CENTER = 0x00000001;
        private const uint DT_RIGHT = 0x00000002;
        private const uint DT_VCENTER = 0x00000004;
        private const uint DT_WORDBREAK = 0x00000010;
        private const uint DT_SINGLELINE = 0x00000020;
        private const uint DT_NOPREFIX = 0x00000800;
        private const uint DT_END_ELLIPSIS = 0x00008000;

        private const int TRANSPARENT = 1;
        private const uint SRCCOPY = 0x00CC0020;
        private const uint DI_NORMAL = 0x0003;

        // GDI+ Constants
        private const int SmoothingModeAntiAlias = 4;
        private const int PixelOffsetModeHighQuality = 2;
        private const int CompositingQualityHighQuality = 2;
        private const int FillModeAlternate = 0;
        private const int UnitPixel = 2;
        private const int LineCapRound = 2;
        private const int LineJoinRound = 2;
        private const int PixelFormat32bppARGB = 0x0026200A;

        // XInput Controller Constants
        private const ushort XINPUT_GAMEPAD_DPAD_UP = 0x0001;
        private const ushort XINPUT_GAMEPAD_DPAD_DOWN = 0x0002;
        private const ushort XINPUT_GAMEPAD_DPAD_LEFT = 0x0004;
        private const ushort XINPUT_GAMEPAD_DPAD_RIGHT = 0x0008;
        private const ushort XINPUT_GAMEPAD_START = 0x0010;
        private const ushort XINPUT_GAMEPAD_BACK = 0x0020;
        private const ushort XINPUT_GAMEPAD_A = 0x1000;
        private const ushort XINPUT_GAMEPAD_B = 0x2000;
        private const ushort XINPUT_GAMEPAD_X = 0x4000;
        private const ushort XINPUT_GAMEPAD_Y = 0x8000;

        // WinMM DirectInput / Joystick Constants (Fallback for PlayStation/Nintendo/Generic USB Joysticks)
        private const uint JOY_RETURNALL = 0x000000FF;
        private const uint JOY_POVCENTERED = 0xFFFF;

        // Win32 COLORREF (0x00BBGGRR) & GDI+ ARGB (0xAARRGGBB) Helpers
        private static uint Rgb(byte r, byte g, byte b) => (uint)(r | (g << 8) | (b << 16));
        private static byte GetR(uint col) => (byte)(col & 0xFF);
        private static byte GetG(uint col) => (byte)((col >> 8) & 0xFF);
        private static byte GetB(uint col) => (byte)((col >> 16) & 0xFF);

        private static uint ToArgb(uint colorRef, byte alpha = 255) =>
            ((uint)alpha << 24) | ((uint)GetR(colorRef) << 16) | ((uint)GetG(colorRef) << 8) | GetB(colorRef);

        private static uint BlendColor(uint fg, uint bg, double fgWeight)
        {
            byte r = (byte)Math.Clamp(Math.Round(GetR(fg) * fgWeight + GetR(bg) * (1.0 - fgWeight)), 0, 255);
            byte g = (byte)Math.Clamp(Math.Round(GetG(fg) * fgWeight + GetG(bg) * (1.0 - fgWeight)), 0, 255);
            byte b = (byte)Math.Clamp(Math.Round(GetB(fg) * fgWeight + GetB(bg) * (1.0 - fgWeight)), 0, 255);
            return Rgb(r, g, b);
        }

        private static double GetRelativeLuminance(uint col) =>
            (0.299 * GetR(col) + 0.587 * GetG(col) + 0.114 * GetB(col)) / 255.0;

        // =====================================================================
        // Microsoft Fluent 2 Design System Adaptive Palette (System-Driven)
        // =====================================================================
        private readonly record struct Fluent2Palette(
            bool IsLight,
            bool IsHighContrast,
            uint BgBase,
            uint BgFooter,
            uint LayerDefault,
            CardSurfaceColors Cards,
            uint TagFill,
            uint WindowBorder,
            uint Divider,
            uint CardStroke,
            uint RowStroke,
            uint TagStroke,
            uint ScrollTrack,
            uint ScrollThumb,
            uint TextPrimary,
            uint TextSecondary,
            uint TextTertiary,
            uint TextOnAccent,
            uint AccentDefault,
            uint AccentHover,
            uint AccentPressed,
            uint AccentBorderTop,
            uint AccentBadgeBg,
            uint SuccessIcon,
            uint SuccessBg,
            uint SuccessBorder,
            uint InfoIcon,
            uint InfoBg,
            uint InfoBorder,
            uint CriticalIcon,
            uint CriticalBg,
            uint CriticalBorder
        )
        {
            public uint CardDefault => Cards.Default;
            public uint CardHover => Cards.Hover;
        }

        private readonly record struct CardSurfaceColors(uint Default, uint Hover);

        private enum FocusZone
        {
            GameList,
            PrimaryButton,
            CloseButton
        }

        private enum InputGlyph
        {
            None,
            Confirm,
            Cancel,
            PrimaryAction,
            SecondaryAction
        }

        private sealed class GameRowItem
        {
            public required InstalledGame Game { get; init; }
            public bool IsSynced { get; set; }
            public bool IsSelected { get; set; }
        }

        // Interactive & System-Adaptive State
        private static Fluent2Palette _theme;
        private static uint _currentDpi = 96;
        private static double _dpiScale = 1.0;
        private static double _textScale = 1.0;
        private static IntPtr _gdipToken;
        private static IntPtr _hWindowIcon;
        private static IntPtr _hWindowIconSmall;
        private static readonly Dictionary<string, IntPtr> _gameIconCache = new(StringComparer.OrdinalIgnoreCase);

        private static RECT _btnPrimaryRect;
        private static RECT _btnCloseRect;
        private static RECT _btnSettingsRect;
        private static RECT _settingsToggleCardRect;
        private static RECT _apiKeyBoxRect;
        private static RECT _btnPasteRect;
        private static RECT _btnClearKeyRect;
        private static RECT _btnGetKeyRect;
        private static RECT _listAreaRect;
        private static int _visibleRows = 1;
        private static bool _hoverPrimary;
        private static bool _pressedPrimary;
        private static bool _hoverClose;
        private static bool _pressedClose;
        private static bool _hoverSettings;
        private static bool _hoverPaste;
        private static bool _hoverClearKey;
        private static bool _hoverGetKey;
        private static bool _hoverToggleCard;
        private static bool _isSettingsView;
        private static bool _apiKeyInputFocused;
        private static int _hoveredRowIndex = -1;

        private static FocusZone _focusedZone = FocusZone.GameList;
        private static int _focusedRowIndex;
        private static int _scrollOffset;
        private static readonly List<GameRowItem> _rows = new();
        private static int _totalRows => _rows.Count;

        // Gamepad / Joystick Edge-Detection & Repeat State
        private static ushort _prevGamepadButtons;
        private static bool _gamepadAxisReleased;
        private static int _gamepadDirCooldown;
        private static bool _initialButtonsCleared;
        private static bool _xinputFailed;
        private static bool _winmmFailed;

        private static SteamSyncResult? _activeResult;
        private static SteamSyncResult? _pendingBackgroundResult;
        private static Task? _activeBackgroundTask;
        private static Func<IReadOnlyList<InstalledGame>, SteamSyncResult>? _onSyncSelected;
        private static Func<SteamSyncResult>? _onSettingsSaved;
        private static string? _settingsFilePath;
        private static AppSettings _appSettings = new();
        private static LocalizedStrings _strings = null!;
        private static GamepadType _gamepadType = GamepadType.Keyboard;
        private static GamepadType _lastHardwareGamepad = GamepadType.Keyboard;
        private static int _gamepadDetectTickCounter;
        private static WndProcDelegate? _wndProcHolder;

        // S() scales layout geometry by monitor DPI; SF() scales typography by monitor DPI * Accessibility TextScaleFactor
        private static int S(int value) => (int)Math.Round(value * _dpiScale);
        private static float Sf(float value) => (float)(value * _dpiScale);
        private static int SF(int fontPx) => (int)Math.Round(fontPx * _dpiScale * _textScale);

        [StructLayout(LayoutKind.Sequential)]
        private struct GdiplusStartupInput
        {
            public uint GdiplusVersion;
            public IntPtr DebugEventCallback;
            public int SuppressBackgroundThread;
            public int SuppressExternalCodecs;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HIGHCONTRAST
        {
            public uint cbSize;
            public uint dwFlags;
            public IntPtr lpszDefaultScheme;
        }

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

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public int Width => Right - Left;
            public int Height => Bottom - Top;
            public bool Contains(int x, int y) => x >= Left && x <= Right && y >= Top && y <= Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE
        {
            public int cx;
            public int cy;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public POINT pt;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PAINTSTRUCT
        {
            public IntPtr hdc;
            public bool fErase;
            public RECT rcPaint;
            public bool fRestore;
            public bool fIncUpdate;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public byte[] rgbReserved;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WNDCLASSEXW
        {
            public uint cbSize;
            public uint style;
            public IntPtr lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            public string? lpszMenuName;
            public string lpszClassName;
            public IntPtr hIconSm;
        }

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        // =====================================================================
        // Explicit Unicode (W) Win32 & GDI+ Native Interop
        // =====================================================================
        [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandleW(string? lpModuleName);

        [DllImport("user32.dll")]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

        [DllImport("user32.dll")]
        private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);

        [DllImport("user32.dll", EntryPoint = "RegisterClassExW", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern ushort RegisterClassExW(ref WNDCLASSEXW lpwcx);

        [DllImport("user32.dll", EntryPoint = "CreateWindowExW", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateWindowExW(
            uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle,
            int x, int y, int nWidth, int nHeight,
            IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll", EntryPoint = "DefWindowProcW", CharSet = CharSet.Unicode)]
        private static extern IntPtr DefWindowProcW(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", EntryPoint = "GetMessageW", CharSet = CharSet.Unicode)]
        private static extern sbyte GetMessageW(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll")]
        private static extern bool TranslateMessage(ref MSG lpMsg);

        [DllImport("user32.dll", EntryPoint = "DispatchMessageW", CharSet = CharSet.Unicode)]
        private static extern IntPtr DispatchMessageW(ref MSG lpMsg);

        [DllImport("user32.dll", EntryPoint = "SendMessageW", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessageW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", EntryPoint = "PostMessageW", CharSet = CharSet.Unicode)]
        private static extern bool PostMessageW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", EntryPoint = "SetWindowTextW", CharSet = CharSet.Unicode)]
        private static extern bool SetWindowTextW(IntPtr hWnd, string lpString);

        [DllImport("user32.dll")]
        private static extern bool AdjustWindowRectEx(ref RECT lpRect, uint dwStyle, bool bMenu, uint dwExStyle);

        [DllImport("user32.dll")]
        private static extern bool AdjustWindowRectExForDpi(ref RECT lpRect, uint dwStyle, bool bMenu, uint dwExStyle, uint dpi);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForSystem();

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hwnd);

        [DllImport("user32.dll", EntryPoint = "LoadCursorW")]
        private static extern IntPtr LoadCursorW(IntPtr hInstance, int lpCursorName);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool UpdateWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern void PostQuitMessage(int nExitCode);

        [DllImport("user32.dll")]
        private static extern IntPtr BeginPaint(IntPtr hWnd, out PAINTSTRUCT lpPaint);

        [DllImport("user32.dll")]
        private static extern bool EndPaint(IntPtr hWnd, ref PAINTSTRUCT lpPaint);

        [DllImport("user32.dll")]
        private static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

        [DllImport("user32.dll")]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll")]
        private static extern bool DrawIconEx(
            IntPtr hdc, int xLeft, int yTop, IntPtr hIcon,
            int cxWidth, int cyWidth, uint istepIfAniCur, IntPtr hbrFlickerFreeDraw, uint diFlags);

        [DllImport("shell32.dll", EntryPoint = "ExtractAssociatedIconW", CharSet = CharSet.Unicode)]
        private static extern IntPtr ExtractAssociatedIconW(IntPtr hInst, string lpIconPath, ref ushort lpiIcon);

        [DllImport("user32.dll", EntryPoint = "LoadImageW")]
        private static extern IntPtr LoadImageFromResource(IntPtr hInst, IntPtr lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

        [DllImport("user32.dll", EntryPoint = "LoadImageW", CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadImageFromFile(IntPtr hInst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetricsForDpi(int nIndex, uint dpi);

        [DllImport("user32.dll")]
        private static extern uint GetSysColor(int nIndex);

        [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", CharSet = CharSet.Unicode)]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref HIGHCONTRAST pvParam, uint fWinIni);

        [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", CharSet = CharSet.Unicode)]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref RECT pvParam, uint fWinIni);

        [DllImport("user32.dll")]
        private static extern IntPtr SetTimer(IntPtr hWnd, IntPtr nIDEvent, uint uElapse, IntPtr lpTimerFunc);

        [DllImport("user32.dll")]
        private static extern bool KillTimer(IntPtr hWnd, IntPtr uIDEvent);

        [DllImport("user32.dll")]
        private static extern int FillRect(IntPtr hDC, ref RECT lprc, IntPtr hbr);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("advapi32.dll", EntryPoint = "RegGetValueW", CharSet = CharSet.Unicode)]
        private static extern int RegGetValue(
            IntPtr hkey, string lpSubKey, string lpValue, uint dwFlags,
            out uint pdwType, out uint pvData, ref uint pcbData);

        [DllImport("advapi32.dll", EntryPoint = "RegGetValueW", CharSet = CharSet.Unicode)]
        private static extern int RegGetValue(
            IntPtr hkey, string lpSubKey, string lpValue, uint dwFlags,
            out uint pdwType, [Out] byte[] pvData, ref uint pcbData);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int cx, int cy);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hdc, int x, int y, int cx, int cy, IntPtr hdcSrc, int x1, int y1, uint rop);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateSolidBrush(uint crColor);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        private static extern int SetBkMode(IntPtr hdc, int iBkMode);

        [DllImport("gdi32.dll")]
        private static extern uint SetTextColor(IntPtr hdc, uint crColor);

        [DllImport("gdi32.dll", EntryPoint = "CreateFontW", CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateFontW(
            int nHeight, int nWidth, int nEscapement, int nOrientation, int fnWeight,
            uint fdwItalic, uint fdwUnderline, uint fdwStrikeOut, uint fdwCharSet,
            uint fdwOutputPrecision, uint fdwClipPrecision, uint fdwQuality,
            uint fdwPitchAndFamily, string lpszFace);

        [DllImport("gdi32.dll", EntryPoint = "GetTextExtentPoint32W", CharSet = CharSet.Unicode)]
        private static extern bool GetTextExtentPoint32W(IntPtr hdc, string lpString, int c, out SIZE psizl);

        [DllImport("user32.dll", EntryPoint = "DrawTextW", CharSet = CharSet.Unicode)]
        private static extern int DrawTextW(IntPtr hDC, string lpchText, int cchText, ref RECT lprc, uint format);

        [DllImport("user32.dll")]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll")]
        private static extern bool CloseClipboard();

        [DllImport("user32.dll")]
        private static extern IntPtr GetClipboardData(uint uFormat);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll")]
        private static extern bool GlobalUnlock(IntPtr hMem);

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);

        private static string? TryReadClipboardText(IntPtr hWnd)
        {
            if (!OpenClipboard(hWnd))
                return null;

            try
            {
                IntPtr hData = GetClipboardData(CF_UNICODETEXT);
                if (hData == IntPtr.Zero)
                    return null;

                IntPtr pLocked = GlobalLock(hData);
                if (pLocked == IntPtr.Zero)
                    return null;

                try
                {
                    return Marshal.PtrToStringUni(pLocked)?.Trim();
                }
                finally
                {
                    GlobalUnlock(hData);
                }
            }
            catch
            {
                return null;
            }
            finally
            {
                CloseClipboard();
            }
        }

        // GDI+ Flat API P/Invoke (Anti-Aliased Vector Paths, Alpha Borders & Smooth Circles)
        [DllImport("gdiplus.dll")]
        private static extern int GdiplusStartup(out IntPtr token, ref GdiplusStartupInput input, IntPtr output);

        [DllImport("gdiplus.dll")]
        private static extern int GdipCreateFromHDC(IntPtr hdc, out IntPtr graphics);

        [DllImport("gdiplus.dll")]
        private static extern int GdipDeleteGraphics(IntPtr graphics);

        [DllImport("gdiplus.dll")]
        private static extern int GdipSetSmoothingMode(IntPtr graphics, int smoothingMode);

        [DllImport("gdiplus.dll")]
        private static extern int GdipSetPixelOffsetMode(IntPtr graphics, int pixelOffsetMode);

        [DllImport("gdiplus.dll")]
        private static extern int GdipSetCompositingQuality(IntPtr graphics, int compositingQuality);

        [DllImport("gdiplus.dll")]
        private static extern int GdipCreateSolidFill(uint colorArgb, out IntPtr brush);

        [DllImport("gdiplus.dll")]
        private static extern int GdipDeleteBrush(IntPtr brush);

        [DllImport("gdiplus.dll")]
        private static extern int GdipCreatePen1(uint colorArgb, float width, int unit, out IntPtr pen);

        [DllImport("gdiplus.dll")]
        private static extern int GdipSetPenStartCap(IntPtr pen, int startCap);

        [DllImport("gdiplus.dll")]
        private static extern int GdipSetPenEndCap(IntPtr pen, int endCap);

        [DllImport("gdiplus.dll")]
        private static extern int GdipSetPenLineJoin(IntPtr pen, int lineJoin);

        [DllImport("gdiplus.dll")]
        private static extern int GdipDeletePen(IntPtr pen);

        [DllImport("gdiplus.dll")]
        private static extern int GdipCreatePath(int brushMode, out IntPtr path);

        [DllImport("gdiplus.dll")]
        private static extern int GdipDeletePath(IntPtr path);

        [DllImport("gdiplus.dll")]
        private static extern int GdipAddPathArc(IntPtr path, float x, float y, float width, float height, float startAngle, float sweepAngle);

        [DllImport("gdiplus.dll")]
        private static extern int GdipAddPathLine(IntPtr path, float x1, float y1, float x2, float y2);

        [DllImport("gdiplus.dll")]
        private static extern int GdipClosePathFigure(IntPtr path);

        [DllImport("gdiplus.dll")]
        private static extern int GdipFillPath(IntPtr graphics, IntPtr brush, IntPtr path);

        [DllImport("gdiplus.dll")]
        private static extern int GdipDrawPath(IntPtr graphics, IntPtr pen, IntPtr path);

        [DllImport("gdiplus.dll")]
        private static extern int GdipFillEllipse(IntPtr graphics, IntPtr brush, float x, float y, float width, float height);

        [DllImport("gdiplus.dll")]
        private static extern int GdipDrawEllipse(IntPtr graphics, IntPtr pen, float x, float y, float width, float height);

        [DllImport("gdiplus.dll")]
        private static extern int GdipDrawLine(IntPtr graphics, IntPtr pen, float x1, float y1, float x2, float y2);

        [DllImport("gdiplus.dll")]
        private static extern int GdipCreateBitmapFromScan0(int width, int height, int stride, int format, IntPtr scan0, out IntPtr bitmap);

        [DllImport("gdiplus.dll")]
        private static extern int GdipGetImageGraphicsContext(IntPtr image, out IntPtr graphics);

        [DllImport("gdiplus.dll")]
        private static extern int GdipCreateHICONFromBitmap(IntPtr bitmap, out IntPtr hbmReturn);

        [DllImport("gdiplus.dll")]
        private static extern int GdipDisposeImage(IntPtr image);

        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
        private static extern uint XInputGetState14(uint dwUserIndex, out XINPUT_STATE pState);

        [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")]
        private static extern uint XInputGetState910(uint dwUserIndex, out XINPUT_STATE pState);

        [DllImport("winmm.dll")]
        private static extern uint joyGetPosEx(uint uJoyID, ref JOYINFOEX pji);

        // =====================================================================
        // GDI+ Anti-Aliased Vector Primitives
        // =====================================================================
        private static void EnsureGdiPlusStarted()
        {
            if (_gdipToken != IntPtr.Zero)
                return;

            try
            {
                var input = new GdiplusStartupInput { GdiplusVersion = 1 };
                GdiplusStartup(out _gdipToken, ref input, IntPtr.Zero);
            }
            catch
            {
                _gdipToken = IntPtr.Zero;
            }
        }

        private static IntPtr CreateRoundedRectPath(float x, float y, float w, float h, float radius)
        {
            GdipCreatePath(FillModeAlternate, out IntPtr path);
            if (path == IntPtr.Zero)
                return IntPtr.Zero;

            float d = Math.Min(radius * 2f, Math.Min(w, h));
            if (d <= 0.5f)
            {
                GdipAddPathLine(path, x, y, x + w, y);
                GdipAddPathLine(path, x + w, y, x + w, y + h);
                GdipAddPathLine(path, x + w, y + h, x, y + h);
                GdipClosePathFigure(path);
                return path;
            }

            GdipAddPathArc(path, x, y, d, d, 180f, 90f);
            GdipAddPathArc(path, x + w - d, y, d, d, 270f, 90f);
            GdipAddPathArc(path, x + w - d, y + h - d, d, d, 0f, 90f);
            GdipAddPathArc(path, x, y + h - d, d, d, 90f, 90f);
            GdipClosePathFigure(path);
            return path;
        }

        private static void DrawSmoothRoundedCard(
            IntPtr g,
            float x,
            float y,
            float w,
            float h,
            float radius,
            uint fillArgb,
            uint strokeArgb,
            float strokeWidth = 1f,
            uint shadowArgb = 0)
        {
            if (g == IntPtr.Zero || w <= 0 || h <= 0)
                return;

            if ((shadowArgb >> 24) > 0)
            {
                IntPtr shadowPath = CreateRoundedRectPath(x, y + Sf(1f), w, h, radius);
                if (shadowPath != IntPtr.Zero)
                {
                    if (GdipCreateSolidFill(shadowArgb, out IntPtr shadowBrush) == 0 && shadowBrush != IntPtr.Zero)
                    {
                        GdipFillPath(g, shadowBrush, shadowPath);
                        GdipDeleteBrush(shadowBrush);
                    }
                    GdipDeletePath(shadowPath);
                }
            }

            IntPtr path = CreateRoundedRectPath(x, y, w, h, radius);
            if (path == IntPtr.Zero)
                return;

            if ((fillArgb >> 24) > 0)
            {
                if (GdipCreateSolidFill(fillArgb, out IntPtr brush) == 0 && brush != IntPtr.Zero)
                {
                    GdipFillPath(g, brush, path);
                    GdipDeleteBrush(brush);
                }
            }

            if ((strokeArgb >> 24) > 0 && strokeWidth > 0f)
            {
                if (GdipCreatePen1(strokeArgb, strokeWidth, UnitPixel, out IntPtr pen) == 0 && pen != IntPtr.Zero)
                {
                    GdipDrawPath(g, pen, path);
                    GdipDeletePen(pen);
                }
            }

            GdipDeletePath(path);
        }

        private static void DrawSmoothCircle(IntPtr g, float x, float y, float diameter, uint fillArgb, uint strokeArgb = 0, float strokeWidth = 1f)
        {
            if (g == IntPtr.Zero || diameter <= 0)
                return;

            if ((fillArgb >> 24) > 0)
            {
                if (GdipCreateSolidFill(fillArgb, out IntPtr brush) == 0 && brush != IntPtr.Zero)
                {
                    GdipFillEllipse(g, brush, x, y, diameter, diameter);
                    GdipDeleteBrush(brush);
                }
            }

            if ((strokeArgb >> 24) > 0 && strokeWidth > 0f)
            {
                if (GdipCreatePen1(strokeArgb, strokeWidth, UnitPixel, out IntPtr pen) == 0 && pen != IntPtr.Zero)
                {
                    GdipDrawEllipse(g, pen, x, y, diameter, diameter);
                    GdipDeletePen(pen);
                }
            }
        }

        private static void DrawSmoothLine(IntPtr g, float x1, float y1, float x2, float y2, uint colorArgb, float width)
        {
            if (g == IntPtr.Zero || (colorArgb >> 24) == 0)
                return;

            if (GdipCreatePen1(colorArgb, width, UnitPixel, out IntPtr pen) == 0 && pen != IntPtr.Zero)
            {
                GdipSetPenStartCap(pen, LineCapRound);
                GdipSetPenEndCap(pen, LineCapRound);
                GdipSetPenLineJoin(pen, LineJoinRound);
                GdipDrawLine(g, pen, x1, y1, x2, y2);
                GdipDeletePen(pen);
            }
        }

        private static void DrawVectorCheckmark(IntPtr g, float centerX, float centerY, float size, uint colorArgb, float strokeW)
        {
            float x1 = centerX - size * 0.28f;
            float y1 = centerY + size * 0.02f;
            float x2 = centerX - size * 0.05f;
            float y2 = centerY + size * 0.25f;
            float x3 = centerX + size * 0.30f;
            float y3 = centerY - size * 0.20f;

            DrawSmoothLine(g, x1, y1, x2, y2, colorArgb, strokeW);
            DrawSmoothLine(g, x2, y2, x3, y3, colorArgb, strokeW);
        }

        private static void DrawVectorCross(IntPtr g, float centerX, float centerY, float size, uint colorArgb, float strokeW)
        {
            float half = size * 0.24f;
            DrawSmoothLine(g, centerX - half, centerY - half, centerX + half, centerY + half, colorArgb, strokeW);
            DrawSmoothLine(g, centerX + half, centerY - half, centerX - half, centerY + half, colorArgb, strokeW);
        }

        private static void DrawVectorSettingsGear(IntPtr g, float cx, float cy, float size, uint colorArgb)
        {
            float sw = Math.Max(1.5f, Sf(1.7f));
            float outerR = size * 0.34f;
            float bodyR = size * 0.24f;
            float holeR = size * 0.10f;

            // 8 gear teeth around center
            for (int i = 0; i < 8; i++)
            {
                double angle = i * (Math.PI / 4.0);
                float cos = (float)Math.Cos(angle);
                float sin = (float)Math.Sin(angle);
                DrawSmoothLine(
                    g,
                    cx + cos * (bodyR * 0.78f),
                    cy + sin * (bodyR * 0.78f),
                    cx + cos * outerR,
                    cy + sin * outerR,
                    colorArgb,
                    sw * 1.15f);
            }

            // Outer ring & inner hub
            DrawSmoothCircle(g, cx - bodyR, cy - bodyR, bodyR * 2f, 0, colorArgb, sw);
            DrawSmoothCircle(g, cx - holeR, cy - holeR, holeR * 2f, 0, colorArgb, sw * 0.9f);
        }

        private static string GetKeyboardGlyphLabel(InputGlyph glyph) => glyph switch
        {
            InputGlyph.Confirm => "Space",
            InputGlyph.PrimaryAction => "Enter",
            InputGlyph.Cancel => "Esc",
            InputGlyph.SecondaryAction => "Tab",
            _ => string.Empty
        };

        private static int MeasureInputGlyphBadgeWidth(IntPtr hdc, IntPtr fontBadge, int height, InputGlyph glyph)
        {
            if (glyph == InputGlyph.None)
                return 0;

            if (_gamepadType == GamepadType.Keyboard)
            {
                string keyLabel = GetKeyboardGlyphLabel(glyph);
                int textW = MeasureTextWidth(hdc, fontBadge, keyLabel);
                return Math.Max(height + S(6), textW + S(12));
            }

            return height;
        }

        private static int DrawInputGlyphBadge(
            IntPtr hdc,
            IntPtr g,
            int left,
            int centerY,
            int height,
            InputGlyph glyph,
            bool onPrimaryButton,
            IntPtr fontBadge)
        {
            if (glyph == InputGlyph.None)
                return 0;

            int top = centerY - height / 2;

            if (_gamepadType == GamepadType.Keyboard)
            {
                string keyLabel = GetKeyboardGlyphLabel(glyph);
                int textW = MeasureTextWidth(hdc, fontBadge, keyLabel);
                int keyW = Math.Max(height + S(6), textW + S(12));

                uint keyBaseFill = onPrimaryButton
                    ? ToArgb(Rgb(0, 0, 0), 75)
                    : ToArgb(_theme.TagStroke);
                uint keyTopFill = onPrimaryButton
                    ? ToArgb(Rgb(0, 0, 0), 45)
                    : ToArgb(_theme.LayerDefault);
                uint keyStroke = onPrimaryButton
                    ? ToArgb(_theme.TextOnAccent, 140)
                    : ToArgb(_theme.TagStroke);
                uint keyTextCol = onPrimaryButton
                    ? _theme.TextOnAccent
                    : _theme.TextPrimary;

                // Mechanical keycap 3D bottom lip + top cap surface
                DrawSmoothRoundedCard(g, left, top, keyW, height, Sf(4f), keyBaseFill, keyStroke, 1f);
                int bevelH = Math.Max(1, S(2));
                DrawSmoothRoundedCard(g, left, top, keyW, Math.Max(4, height - bevelH), Sf(3.5f), keyTopFill, keyStroke, 1f);

                SelectObject(hdc, fontBadge);
                SetTextColor(hdc, keyTextCol);
                var rcKey = new RECT
                {
                    Left = left,
                    Top = top,
                    Right = left + keyW,
                    Bottom = top + Math.Max(4, height - bevelH)
                };
                DrawTextW(hdc, keyLabel, -1, ref rcKey, DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);
                return keyW;
            }

            if (_gamepadType == GamepadType.PlayStation)
            {
                // PlayStation DualSense / DualShock circular charcoal button with vector geometry
                uint capFill = ToArgb(Rgb(32, 35, 42));
                uint capRim = ToArgb(Rgb(92, 98, 114));
                DrawSmoothCircle(g, left, top, height, capFill, capRim, Math.Max(1f, Sf(1.2f)));

                float cx = left + height / 2f;
                float cy = top + height / 2f;
                float r = height * 0.24f;
                float sw = Math.Max(1.6f, Sf(1.8f));

                switch (glyph)
                {
                    case InputGlyph.Confirm: // Cross (✕) - PlayStation Blue
                        {
                            uint col = ToArgb(Rgb(78, 142, 247));
                            DrawSmoothLine(g, cx - r, cy - r, cx + r, cy + r, col, sw);
                            DrawSmoothLine(g, cx + r, cy - r, cx - r, cy + r, col, sw);
                            break;
                        }
                    case InputGlyph.Cancel: // Circle (○) - PlayStation Coral Red
                        {
                            uint col = ToArgb(Rgb(235, 78, 85));
                            DrawSmoothCircle(g, cx - r, cy - r, r * 2f, 0, col, sw);
                            break;
                        }
                    case InputGlyph.PrimaryAction: // Square (□) - PlayStation Pink
                        {
                            uint col = ToArgb(Rgb(219, 101, 180));
                            DrawSmoothRoundedCard(g, cx - r, cy - r, r * 2f, r * 2f, Sf(1.2f), 0, col, sw);
                            break;
                        }
                    case InputGlyph.SecondaryAction: // Triangle (△) - PlayStation Mint Green
                        {
                            uint col = ToArgb(Rgb(56, 196, 152));
                            float topY = cy - r * 1.05f;
                            float botY = cy + r * 0.85f;
                            float halfBase = r * 1.05f;
                            DrawSmoothLine(g, cx, topY, cx + halfBase, botY, col, sw);
                            DrawSmoothLine(g, cx + halfBase, botY, cx - halfBase, botY, col, sw);
                            DrawSmoothLine(g, cx - halfBase, botY, cx, topY, col, sw);
                            break;
                        }
                }
                return height;
            }

            if (_gamepadType == GamepadType.Nintendo)
            {
                // Nintendo Switch Pro / Joy-Con button aesthetic
                uint capFill = ToArgb(Rgb(45, 45, 50));
                uint capRim = glyph switch
                {
                    InputGlyph.Confirm => ToArgb(Rgb(230, 0, 18)),       // Switch Neon Red
                    InputGlyph.PrimaryAction => ToArgb(Rgb(0, 160, 233)), // Switch Neon Blue
                    _ => ToArgb(Rgb(130, 134, 142))
                };
                string letter = glyph switch
                {
                    InputGlyph.Confirm => "B",
                    InputGlyph.Cancel => "A",
                    InputGlyph.PrimaryAction => "Y",
                    InputGlyph.SecondaryAction => "X",
                    _ => "A"
                };

                DrawSmoothCircle(g, left, top, height, capFill, capRim, Math.Max(1.2f, Sf(1.4f)));
                SelectObject(hdc, fontBadge);
                SetTextColor(hdc, Rgb(255, 255, 255));
                var rcBtn = new RECT { Left = left, Top = top, Right = left + height, Bottom = top + height };
                DrawTextW(hdc, letter, -1, ref rcBtn, DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);
                return height;
            }

            // Xbox Wireless Controller colored ABXY face buttons
            {
                (uint fillRgb, uint rimRgb, string letter) = glyph switch
                {
                    InputGlyph.Confirm => (Rgb(16, 124, 16), Rgb(68, 196, 68), "A"),       // Xbox Green
                    InputGlyph.Cancel => (Rgb(196, 43, 28), Rgb(242, 96, 82), "B"),        // Xbox Red
                    InputGlyph.PrimaryAction => (Rgb(0, 103, 192), Rgb(76, 174, 252), "X"), // Xbox Blue
                    InputGlyph.SecondaryAction => (Rgb(206, 140, 0), Rgb(245, 198, 36), "Y"), // Xbox Amber
                    _ => (Rgb(16, 124, 16), Rgb(68, 196, 68), "A")
                };

                DrawSmoothCircle(g, left, top, height, ToArgb(fillRgb), ToArgb(rimRgb), Math.Max(1.1f, Sf(1.3f)));
                SelectObject(hdc, fontBadge);
                SetTextColor(hdc, Rgb(255, 255, 255));
                var rcBtn = new RECT { Left = left, Top = top, Right = left + height, Bottom = top + height };
                DrawTextW(hdc, letter, -1, ref rcBtn, DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);
                return height;
            }
        }

        private static void DrawVectorGamepadGlyph(IntPtr g, float cx, float cy, float size, uint colorArgb)
        {
            float sw = Math.Max(1.4f, Sf(1.6f));
            float w = size * 0.62f;
            float h = size * 0.42f;
            DrawSmoothRoundedCard(g, cx - w / 2f, cy - h / 2f, w, h, h * 0.45f, 0, colorArgb, sw);
            float dx = cx - w * 0.20f;
            float r = size * 0.08f;
            DrawSmoothLine(g, dx - r, cy, dx + r, cy, colorArgb, sw * 0.85f);
            DrawSmoothLine(g, dx, cy - r, dx, cy + r, colorArgb, sw * 0.85f);
            float bx = cx + w * 0.20f;
            float dot = Math.Max(2f, size * 0.08f);
            DrawSmoothCircle(g, bx - dot * 0.5f, cy - dot * 1.1f, dot, colorArgb);
            DrawSmoothCircle(g, bx - dot * 0.5f, cy + dot * 0.2f, dot, colorArgb);
        }

        private const uint IMAGE_ICON = 1;
        private const uint LR_LOADFROMFILE = 0x0010;
        private const int SM_CXICON = 11;
        private const int SM_CXSMICON = 49;

        private static int GetDesiredIconSize(int metricIndex, uint dpi, int baseSizeAt96)
        {
            try
            {
                int val = GetSystemMetricsForDpi(metricIndex, dpi);
                if (val > 0)
                    return val;
            }
            catch { }

            return Math.Max(16, (int)Math.Round(baseSizeAt96 * (dpi / 96.0)));
        }

        private static IntPtr CreateFluentWindowIcon(int targetSizePx)
        {
            try
            {
                IntPtr hModule = GetModuleHandleW(null);
                string? exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath) &&
                    !exePath.EndsWith("dotnet.exe", StringComparison.OrdinalIgnoreCase))
                {
                    IntPtr hResIcon = LoadImageFromResource(hModule, (IntPtr)32512, IMAGE_ICON, targetSizePx, targetSizePx, 0);
                    if (hResIcon != IntPtr.Zero)
                        return hResIcon;
                }

                string baseDir = AppContext.BaseDirectory;
                string[] candidatePaths =
                {
                    Path.Combine(baseDir, "icon.ico"),
                    Path.Combine(baseDir, "..", "..", "..", "icon.ico"),
                    Path.Combine(baseDir, "..", "..", "..", "..", "icons", "Non-Steam-Launcher.ico")
                };

                foreach (string candidate in candidatePaths)
                {
                    if (File.Exists(candidate))
                    {
                        IntPtr hFileIcon = LoadImageFromFile(IntPtr.Zero, Path.GetFullPath(candidate), IMAGE_ICON, targetSizePx, targetSizePx, LR_LOADFROMFILE);
                        if (hFileIcon != IntPtr.Zero)
                            return hFileIcon;
                    }
                }

                if (!string.IsNullOrEmpty(exePath) &&
                    !exePath.EndsWith("dotnet.exe", StringComparison.OrdinalIgnoreCase) &&
                    File.Exists(exePath))
                {
                    ushort iconIdx = 0;
                    IntPtr exeIcon = ExtractAssociatedIconW(hModule, exePath, ref iconIdx);
                    if (exeIcon != IntPtr.Zero)
                        return exeIcon;
                }
            }
            catch { }

            EnsureGdiPlusStarted();
            if (_gdipToken == IntPtr.Zero)
                return IntPtr.Zero;

            int sz = Math.Max(16, targetSizePx);
            if (GdipCreateBitmapFromScan0(sz, sz, 0, PixelFormat32bppARGB, IntPtr.Zero, out IntPtr bmp) != 0 || bmp == IntPtr.Zero)
                return IntPtr.Zero;

            IntPtr hIcon = IntPtr.Zero;
            if (GdipGetImageGraphicsContext(bmp, out IntPtr g) == 0 && g != IntPtr.Zero)
            {
                GdipSetSmoothingMode(g, SmoothingModeAntiAlias);
                GdipSetPixelOffsetMode(g, PixelOffsetModeHighQuality);

                float scale = sz / 36.6666667f;
                float ox = 5.6666667f;
                float oy = 6.1666667f;

                DrawSmoothRoundedCard(
                    g,
                    (12.5f - ox) * scale,
                    (8.0f - oy) * scale,
                    27.5f * scale,
                    32.0f * scale,
                    3.0f * scale,
                    ToArgb(Rgb(22, 192, 226)),
                    0,
                    0f);

                DrawSmoothRoundedCard(
                    g,
                    (8.0f - ox) * scale,
                    (9.0f - oy) * scale,
                    31.0f * scale,
                    32.0f * scale,
                    3.0f * scale,
                    ToArgb(Rgb(10, 134, 227)),
                    0,
                    0f);

                DrawSmoothRoundedCard(
                    g,
                    (8.0f - ox) * scale,
                    (9.0f - oy) * scale,
                    6.4f * scale,
                    32.0f * scale,
                    2.5f * scale,
                    ToArgb(Rgb(60, 70, 83)),
                    0,
                    0f);

                uint white = ToArgb(Rgb(255, 255, 255));
                float dpadCx = (21.9f - ox) * scale;
                float dpadCy = (24.7f - oy) * scale;
                float arm = 3.8f * scale;
                float barW = Math.Max(1.5f, 2.5f * scale);
                DrawSmoothLine(g, dpadCx - arm, dpadCy, dpadCx + arm, dpadCy, white, barW);
                DrawSmoothLine(g, dpadCx, dpadCy - arm, dpadCx, dpadCy + arm, white, barW);

                float btnD = 4.8f * scale;
                DrawSmoothCircle(g, (33.7f - 2.4f - ox) * scale, (22.3f - 2.4f - oy) * scale, btnD, white);
                DrawSmoothCircle(g, (30.2f - 2.4f - ox) * scale, (27.7f - 2.4f - oy) * scale, btnD, white);

                GdipDeleteGraphics(g);
                GdipCreateHICONFromBitmap(bmp, out hIcon);
            }

            GdipDisposeImage(bmp);
            return hIcon;
        }

        private static void RefreshWindowIcons(IntPtr hWnd, uint dpi)
        {
            IntPtr oldBig = _hWindowIcon;
            IntPtr oldSmall = _hWindowIconSmall;

            int bigSize = GetDesiredIconSize(SM_CXICON, dpi, 32);
            int smallSize = GetDesiredIconSize(SM_CXSMICON, dpi, 16);

            _hWindowIcon = CreateFluentWindowIcon(bigSize);
            _hWindowIconSmall = CreateFluentWindowIcon(smallSize);

            if (hWnd != IntPtr.Zero)
            {
                if (_hWindowIconSmall != IntPtr.Zero)
                    SendMessageW(hWnd, WM_SETICON, (IntPtr)0, _hWindowIconSmall);
                if (_hWindowIcon != IntPtr.Zero)
                    SendMessageW(hWnd, WM_SETICON, (IntPtr)1, _hWindowIcon);
            }

            if (oldBig != IntPtr.Zero && oldBig != _hWindowIcon)
                DestroyIcon(oldBig);
            if (oldSmall != IntPtr.Zero && oldSmall != _hWindowIconSmall && oldSmall != oldBig)
                DestroyIcon(oldSmall);
        }

        // =====================================================================
        // High-DPI (100%–200%+) & Accessibility Text Scale Configuration
        // =====================================================================
        private static void EnsurePerMonitorDpiAwareness()
        {
            try
            {
                SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
            }
            catch { }

            try
            {
                if (SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2) == IntPtr.Zero)
                {
                    SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_SYSTEM_AWARE);
                }
            }
            catch { }
        }

        private static double QuerySystemTextScaleFactor()
        {
            try
            {
                uint size = sizeof(uint);
                int status = RegGetValue(
                    HKEY_CURRENT_USER,
                    @"Software\Microsoft\Accessibility",
                    "TextScaleFactor",
                    RRF_RT_REG_DWORD,
                    out _,
                    out uint factor,
                    ref size);

                if (status == 0 && factor >= 100 && factor <= 225)
                {
                    return Math.Clamp(factor / 100.0, 1.0, 1.25);
                }
            }
            catch { }

            return 1.0;
        }

        private static RECT GetMonitorWorkArea()
        {
            var workArea = new RECT();
            try
            {
                if (SystemParametersInfo(SPI_GETWORKAREA, 0, ref workArea, 0) && workArea.Width > 0 && workArea.Height > 0)
                {
                    return workArea;
                }
            }
            catch { }

            return new RECT
            {
                Left = 0,
                Top = 0,
                Right = GetSystemMetrics(0),
                Bottom = GetSystemMetrics(1)
            };
        }

        private static int GetRowHeight() => (int)Math.Round(S(48) * Math.Max(1.0, (_textScale - 1.0) * 0.55 + 1.0));

        private static int CalculateIdealClientHeight(int itemCount)
        {
            int effectiveCount = _isSettingsView ? Math.Max(itemCount, 3) : itemCount;
            int visibleSlots = Math.Clamp(effectiveCount, 1, MaxVisibleGameRows);
            int topPad = S(20);
            int heroBannerH = Math.Max(S(52), SF(46));
            int sectionGap = S(16);
            int sectionHeaderH = Math.Max(S(22), SF(20));
            int listTopGap = S(8);
            int rowH = GetRowHeight();
            int rowGap = S(6);
            int listContentH = visibleSlots * rowH + Math.Max(0, visibleSlots - 1) * rowGap;
            int listBottomPad = S(20);
            int footerH = S(60);

            return topPad + heroBannerH + sectionGap + sectionHeaderH + listTopGap + listContentH + listBottomPad + footerH;
        }

        private static void EnsureWindowHeightForCurrentView(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
                return;

            uint dwStyle = WS_POPUP | WS_CAPTION | WS_SYSMENU | WS_VISIBLE;
            CalculateOuterWindowBounds(dwStyle, _currentDpi, _totalRows, out int posX, out int posY, out int winWidth, out int winHeight);
            SetWindowPos(hWnd, IntPtr.Zero, posX, posY, winWidth, winHeight, SWP_NOZORDER | SWP_NOACTIVATE);
        }

        private static void CalculateOuterWindowBounds(uint dwStyle, uint dpi, int itemCount, out int posX, out int posY, out int winWidth, out int winHeight)
        {
            int targetClientH = CalculateIdealClientHeight(itemCount);
            var rcWindow = new RECT
            {
                Left = 0,
                Top = 0,
                Right = S(BaseClientWidth),
                Bottom = targetClientH
            };

            bool adjusted = false;
            try
            {
                adjusted = AdjustWindowRectExForDpi(ref rcWindow, dwStyle, false, 0, dpi);
            }
            catch { }

            if (!adjusted)
            {
                AdjustWindowRectEx(ref rcWindow, dwStyle, false, 0);
            }

            var workArea = GetMonitorWorkArea();
            int safeMargin = S(24);
            int maxAllowedW = Math.Max(S(400), workArea.Width - safeMargin * 2);
            int maxAllowedH = Math.Max(S(260), workArea.Height - safeMargin * 2);

            winWidth = Math.Min(rcWindow.Width, maxAllowedW);
            winHeight = Math.Min(rcWindow.Height, maxAllowedH);

            posX = workArea.Left + Math.Max(0, (workArea.Width - winWidth) / 2);
            posY = workArea.Top + Math.Max(0, (workArea.Height - winHeight) / 2);
        }

        // =====================================================================
        // OS Theme, Accent Color & High-Contrast Detection
        // =====================================================================
        private static Fluent2Palette DetectSystemTheme()
        {
            if (IsSystemHighContrastActive())
            {
                return BuildHighContrastPalette();
            }

            bool isLight = QuerySystemLightModePreference();
            uint accentDefault = QuerySystemAccentColor(isLight);

            return isLight
                ? BuildLightPalette(accentDefault)
                : BuildDarkPalette(accentDefault);
        }

        private static bool IsSystemHighContrastActive()
        {
            try
            {
                var hc = new HIGHCONTRAST { cbSize = (uint)Marshal.SizeOf<HIGHCONTRAST>() };
                if (SystemParametersInfo(SPI_GETHIGHCONTRAST, hc.cbSize, ref hc, 0))
                {
                    return (hc.dwFlags & HCF_HIGHCONTRASTON) != 0;
                }
            }
            catch { }
            return false;
        }

        private static bool QuerySystemLightModePreference()
        {
            const string personalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

            try
            {
                uint size = sizeof(uint);
                int status = RegGetValue(
                    HKEY_CURRENT_USER,
                    personalizeKey,
                    "AppsUseLightTheme",
                    RRF_RT_REG_DWORD,
                    out _,
                    out uint appsUseLight,
                    ref size);

                if (status == 0)
                    return appsUseLight != 0;

                size = sizeof(uint);
                status = RegGetValue(
                    HKEY_CURRENT_USER,
                    personalizeKey,
                    "SystemUsesLightTheme",
                    RRF_RT_REG_DWORD,
                    out _,
                    out uint systemUsesLight,
                    ref size);

                if (status == 0)
                    return systemUsesLight != 0;
            }
            catch { }

            return true;
        }

        private static uint QuerySystemAccentColor(bool isLight)
        {
            try
            {
                byte[] palette = new byte[32];
                uint size = (uint)palette.Length;
                int status = RegGetValue(
                    HKEY_CURRENT_USER,
                    @"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent",
                    "AccentPalette",
                    RRF_RT_REG_BINARY,
                    out _,
                    palette,
                    ref size);

                if (status == 0 && size >= 24)
                {
                    int offset = isLight ? 4 * 4 : 1 * 4;
                    byte r = palette[offset];
                    byte g = palette[offset + 1];
                    byte b = palette[offset + 2];
                    if (r != 0 || g != 0 || b != 0)
                    {
                        return Rgb(r, g, b);
                    }
                }
            }
            catch { }

            try
            {
                uint size = sizeof(uint);
                int status = RegGetValue(
                    HKEY_CURRENT_USER,
                    @"Software\Microsoft\Windows\DWM",
                    "AccentColor",
                    RRF_RT_REG_DWORD,
                    out _,
                    out uint dwmAccent,
                    ref size);

                if (status == 0 && (dwmAccent & 0x00FFFFFF) != 0)
                {
                    uint baseCol = dwmAccent & 0x00FFFFFF;
                    return isLight
                        ? BlendColor(baseCol, Rgb(0, 0, 0), 0.85)
                        : BlendColor(baseCol, Rgb(255, 255, 255), 0.75);
                }
            }
            catch { }

            return isLight ? Rgb(0, 103, 192) : Rgb(96, 205, 255);
        }

        private static Fluent2Palette BuildDarkPalette(uint accentDefault)
        {
            uint bgFooter = Rgb(24, 24, 24);
            uint textOnAccent = GetRelativeLuminance(accentDefault) > 0.52 ? Rgb(0, 0, 0) : Rgb(255, 255, 255);
            uint accentHover = BlendColor(accentDefault, bgFooter, 0.90);
            uint accentPressed = BlendColor(accentDefault, bgFooter, 0.80);
            uint accentBorderTop = BlendColor(accentDefault, Rgb(255, 255, 255), 0.84);
            uint accentBadgeBg = BlendColor(accentDefault, textOnAccent, 0.80);

            return new Fluent2Palette(
                IsLight: false,
                IsHighContrast: false,
                BgBase: Rgb(32, 32, 32),            // SolidBackgroundFillColorBase (#202020)
                BgFooter: bgFooter,                 // SolidBackgroundFillColorSecondary (#181818)
                LayerDefault: Rgb(39, 39, 39),      // LayerFillColorDefault (#272727)
                Cards: new CardSurfaceColors(Rgb(45, 45, 45), Rgb(54, 54, 54)),
                TagFill: Rgb(54, 54, 54),           // Subtle Badge Surface (#363636)
                WindowBorder: Rgb(51, 51, 51),      // SurfaceStrokeColorDefault (#333333)
                Divider: Rgb(48, 48, 48),           // DividerStrokeColorDefault (#303030)
                CardStroke: Rgb(58, 58, 58),        // CardStrokeColorDefault (#3A3A3A)
                RowStroke: Rgb(58, 58, 58),         // CardStrokeColorDefault (#3A3A3A)
                TagStroke: Rgb(70, 70, 70),         // ControlStrokeColorDefault
                ScrollTrack: Rgb(44, 44, 44),       // Scrollbar Track
                ScrollThumb: Rgb(116, 116, 116),    // Scrollbar Thumb
                TextPrimary: Rgb(255, 255, 255),    // TextFillColorPrimary (#FFFFFF)
                TextSecondary: Rgb(197, 197, 197),  // TextFillColorSecondary (#C5C5C5)
                TextTertiary: Rgb(138, 138, 138),   // TextFillColorTertiary (#8A8A8A)
                TextOnAccent: textOnAccent,
                AccentDefault: accentDefault,
                AccentHover: accentHover,
                AccentPressed: accentPressed,
                AccentBorderTop: accentBorderTop,
                AccentBadgeBg: accentBadgeBg,
                SuccessIcon: Rgb(108, 203, 95),     // SystemFillColorSuccess (#6CCB5F)
                SuccessBg: Rgb(30, 44, 30),         // SystemFillColorSuccessBackground (#1E2C1E)
                SuccessBorder: Rgb(48, 76, 48),
                InfoIcon: accentDefault,            // SystemFillColorAttention
                InfoBg: Rgb(43, 45, 48),            // Subtle elevated Fluent 2 InfoBar surface
                InfoBorder: Rgb(60, 62, 66),
                CriticalIcon: Rgb(255, 153, 164),   // SystemFillColorCritical (#FF99A4)
                CriticalBg: Rgb(50, 31, 33),        // SystemFillColorCriticalBackground (#321F21)
                CriticalBorder: Rgb(77, 44, 48)
            );
        }

        private static Fluent2Palette BuildLightPalette(uint accentDefault)
        {
            uint bgFooter = Rgb(238, 238, 238);
            uint textOnAccent = GetRelativeLuminance(accentDefault) > 0.58 ? Rgb(0, 0, 0) : Rgb(255, 255, 255);
            uint accentHover = BlendColor(accentDefault, bgFooter, 0.90);
            uint accentPressed = BlendColor(accentDefault, bgFooter, 0.80);
            uint accentBorderTop = BlendColor(accentDefault, Rgb(255, 255, 255), 0.86);
            uint accentBadgeBg = BlendColor(accentDefault, textOnAccent, 0.82);

            return new Fluent2Palette(
                IsLight: true,
                IsHighContrast: false,
                BgBase: Rgb(243, 243, 243),         // SolidBackgroundFillColorBase (#F3F3F3)
                BgFooter: bgFooter,                 // SolidBackgroundFillColorSecondary (#EEEEEE)
                LayerDefault: Rgb(249, 249, 249),   // LayerFillColorDefault (#F9F9F9)
                Cards: new CardSurfaceColors(Rgb(255, 255, 255), Rgb(246, 246, 246)),
                TagFill: Rgb(243, 243, 243),        // Subtle Badge Surface (#F3F3F3)
                WindowBorder: Rgb(229, 229, 229),   // SurfaceStrokeColorDefault (#E5E5E5)
                Divider: Rgb(229, 229, 229),        // DividerStrokeColorDefault (#E5E5E5)
                CardStroke: Rgb(229, 229, 229),     // CardStrokeColorDefault (#E5E5E5)
                RowStroke: Rgb(229, 229, 229),      // CardStrokeColorDefault (#E5E5E5)
                TagStroke: Rgb(218, 218, 218),      // Subtle Tag Stroke
                ScrollTrack: Rgb(234, 234, 234),    // Scrollbar Track
                ScrollThumb: Rgb(146, 146, 146),    // Scrollbar Thumb
                TextPrimary: Rgb(26, 26, 26),       // TextFillColorPrimary (#1A1A1A)
                TextSecondary: Rgb(92, 92, 92),     // TextFillColorSecondary (#5C5C5C)
                TextTertiary: Rgb(138, 138, 138),   // TextFillColorTertiary (#8A8A8A)
                TextOnAccent: textOnAccent,
                AccentDefault: accentDefault,
                AccentHover: accentHover,
                AccentPressed: accentPressed,
                AccentBorderTop: accentBorderTop,
                AccentBadgeBg: accentBadgeBg,
                SuccessIcon: Rgb(15, 123, 15),      // SystemFillColorSuccess (#0F7B0F)
                SuccessBg: Rgb(223, 246, 221),      // SystemFillColorSuccessBackground (#DFF6DD)
                SuccessBorder: Rgb(190, 230, 187),
                InfoIcon: accentDefault,            // SystemFillColorAttention
                InfoBg: Rgb(255, 255, 255),         // Clean WinUI 3 Card Surface for Up-To-Date InfoBar
                InfoBorder: Rgb(229, 229, 229),
                CriticalIcon: Rgb(196, 43, 28),     // SystemFillColorCritical (#C42B1C)
                CriticalBg: Rgb(253, 231, 233),     // SystemFillColorCriticalBackground (#FDE7E9)
                CriticalBorder: Rgb(241, 187, 188)
            );
        }

        private static Fluent2Palette BuildHighContrastPalette()
        {
            uint winBg = GetSysColor(COLOR_WINDOW);
            uint winText = GetSysColor(COLOR_WINDOWTEXT);
            uint highlight = GetSysColor(COLOR_HIGHLIGHT);
            uint highlightText = GetSysColor(COLOR_HIGHLIGHTTEXT);
            uint frame = GetSysColor(COLOR_WINDOWFRAME);
            uint grayText = GetSysColor(COLOR_GRAYTEXT);
            uint face = GetSysColor(COLOR_3DFACE);
            uint btnText = GetSysColor(COLOR_BTNTEXT);
            bool isLight = GetRelativeLuminance(winBg) > 0.5;

            return new Fluent2Palette(
                IsLight: isLight,
                IsHighContrast: true,
                BgBase: winBg,
                BgFooter: face,
                LayerDefault: winBg,
                Cards: new CardSurfaceColors(winBg, highlight),
                TagFill: face,
                WindowBorder: frame,
                Divider: frame,
                CardStroke: frame,
                RowStroke: frame,
                TagStroke: btnText,
                ScrollTrack: face,
                ScrollThumb: highlight,
                TextPrimary: winText,
                TextSecondary: winText,
                TextTertiary: grayText,
                TextOnAccent: highlightText,
                AccentDefault: highlight,
                AccentHover: highlight,
                AccentPressed: highlight,
                AccentBorderTop: winText,
                AccentBadgeBg: winBg,
                SuccessIcon: highlight,
                SuccessBg: winBg,
                SuccessBorder: highlight,
                InfoIcon: highlight,
                InfoBg: winBg,
                InfoBorder: highlight,
                CriticalIcon: highlight,
                CriticalBg: winBg,
                CriticalBorder: highlight
            );
        }

        private static void ApplyWindowThemeAttributes(IntPtr hWnd, Fluent2Palette palette)
        {
            int useDark = palette.IsLight ? 0 : 1;
            DwmSetWindowAttribute(hWnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));
            DwmSetWindowAttribute(hWnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref useDark, sizeof(int));

            int roundCorner = DWMWCP_ROUND;
            DwmSetWindowAttribute(hWnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref roundCorner, sizeof(int));

            int captionColor = (int)palette.BgBase;
            DwmSetWindowAttribute(hWnd, DWMWA_CAPTION_COLOR, ref captionColor, sizeof(int));

            int textColor = (int)palette.TextPrimary;
            DwmSetWindowAttribute(hWnd, DWMWA_TEXT_COLOR, ref textColor, sizeof(int));

            int borderColor = (int)palette.WindowBorder;
            DwmSetWindowAttribute(hWnd, DWMWA_BORDER_COLOR, ref borderColor, sizeof(int));

            int backdropMica = DWMSBT_MAINWINDOW;
            DwmSetWindowAttribute(hWnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdropMica, sizeof(int));
        }

        private static void RebuildRowItemsFromResult(SteamSyncResult result)
        {
            _rows.Clear();
            if (!result.Success)
                return;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Unsynced / Pending games first (unchecked by default so user chooses what to add)
            foreach (var g in result.PendingGames)
            {
                string key = GameDetector.NormalizeTitle(g.Title);
                if (string.IsNullOrEmpty(key)) key = g.Title;
                if (seen.Add(key))
                {
                    _rows.Add(new GameRowItem
                    {
                        Game = g,
                        IsSynced = false,
                        IsSelected = false
                    });
                }
            }

            // 2. Newly added games (if SyncSelectedGames was just run)
            foreach (var g in result.AddedGames)
            {
                string key = GameDetector.NormalizeTitle(g.Title);
                if (string.IsNullOrEmpty(key)) key = g.Title;
                if (seen.Add(key))
                {
                    _rows.Add(new GameRowItem
                    {
                        Game = g,
                        IsSynced = true,
                        IsSelected = false
                    });
                }
            }

            // 3. Existing synced games
            foreach (var g in result.ExistingGames)
            {
                string key = GameDetector.NormalizeTitle(g.Title);
                if (string.IsNullOrEmpty(key)) key = g.Title;
                if (seen.Add(key))
                {
                    _rows.Add(new GameRowItem
                    {
                        Game = g,
                        IsSynced = true,
                        IsSelected = false
                    });
                }
            }
        }

        private static int GetUnsyncedCount() => _rows.Count(r => !r.IsSynced);
        private static int GetSelectedUnsyncedCount() => _rows.Count(r => !r.IsSynced && r.IsSelected);

        public static SteamSyncResult ShowDialog(
            SteamSyncResult result,
            Func<IReadOnlyList<InstalledGame>, SteamSyncResult>? onSyncSelected,
            Func<SteamSyncResult>? onSettingsSaved = null,
            string? settingsFilePath = null)
        {
            EnsurePerMonitorDpiAwareness();
            EnsureGdiPlusStarted();

            _activeResult = result;
            _onSyncSelected = onSyncSelected;
            _onSettingsSaved = onSettingsSaved;
            _settingsFilePath = settingsFilePath;
            _appSettings = AppSettings.Load(settingsFilePath);
            _hoverPrimary = false;
            _pressedPrimary = false;
            _hoverClose = false;
            _pressedClose = false;
            _hoverSettings = false;
            _hoverPaste = false;
            _hoverClearKey = false;
            _hoverGetKey = false;
            _hoverToggleCard = false;
            _isSettingsView = false;
            _apiKeyInputFocused = false;
            _hoveredRowIndex = -1;
            _scrollOffset = 0;
            _prevGamepadButtons = 0;
            _gamepadAxisReleased = true;
            _gamepadDirCooldown = 0;
            _initialButtonsCleared = false;
            _xinputFailed = false;
            _winmmFailed = false;
            _strings = Localization.GetStrings();
            _gamepadType = GamepadDetector.DetectConnectedGamepad();
            _lastHardwareGamepad = _gamepadType;
            _gamepadDetectTickCounter = 0;
            _theme = DetectSystemTheme();
            _textScale = QuerySystemTextScaleFactor();

            RebuildRowItemsFromResult(result);
            _visibleRows = Math.Clamp(_totalRows, 1, MaxVisibleGameRows);
            _focusedRowIndex = 0;
            _focusedZone = _totalRows > 0 ? FocusZone.GameList : FocusZone.CloseButton;

            try
            {
                uint sysDpi = GetDpiForSystem();
                _currentDpi = sysDpi > 0 ? sysDpi : 96;
                _dpiScale = _currentDpi / 96.0;
            }
            catch
            {
                _currentDpi = 96;
                _dpiScale = 1.0;
            }

            uint dwStyle = WS_POPUP | WS_CAPTION | WS_SYSMENU | WS_VISIBLE;

            CalculateOuterWindowBounds(dwStyle, _currentDpi, _totalRows, out int posX, out int posY, out int winWidth, out int winHeight);

            IntPtr hInstance = GetModuleHandleW(null);
            _wndProcHolder = WndProc;

            RefreshWindowIcons(IntPtr.Zero, _currentDpi);

            var wndClass = new WNDCLASSEXW
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
                style = CS_HREDRAW | CS_VREDRAW,
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcHolder),
                hInstance = hInstance,
                hIcon = _hWindowIcon,
                hCursor = LoadCursorW(IntPtr.Zero, 32512), // IDC_ARROW
                hbrBackground = IntPtr.Zero,
                lpszClassName = WindowClassName,
                hIconSm = _hWindowIconSmall != IntPtr.Zero ? _hWindowIconSmall : _hWindowIcon
            };

            RegisterClassExW(ref wndClass);

            IntPtr hWnd = CreateWindowExW(
                0,
                WindowClassName,
                _strings.WindowTitle,
                dwStyle,
                posX, posY, winWidth, winHeight,
                IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero
            );

            if (hWnd == IntPtr.Zero)
            {
                CleanupIcons();
                return _activeResult ?? result;
            }

            SetWindowTextW(hWnd, _strings.WindowTitle);
            if (_hWindowIconSmall != IntPtr.Zero)
                SendMessageW(hWnd, WM_SETICON, (IntPtr)0, _hWindowIconSmall);
            if (_hWindowIcon != IntPtr.Zero)
                SendMessageW(hWnd, WM_SETICON, (IntPtr)1, _hWindowIcon);

            try
            {
                uint winDpi = GetDpiForWindow(hWnd);
                if (winDpi > 0 && winDpi != _currentDpi)
                {
                    _currentDpi = winDpi;
                    _dpiScale = winDpi / 96.0;
                    RefreshWindowIcons(hWnd, _currentDpi);
                    CalculateOuterWindowBounds(dwStyle, _currentDpi, _totalRows, out posX, out posY, out winWidth, out winHeight);
                    SetWindowPos(hWnd, IntPtr.Zero, posX, posY, winWidth, winHeight, SWP_NOZORDER | SWP_NOACTIVATE);
                }
            }
            catch { }

            ApplyWindowThemeAttributes(hWnd, _theme);

            // Poll Gamepad / Joystick at ~30 FPS
            SetTimer(hWnd, (IntPtr)1, 33, IntPtr.Zero);

            ShowWindow(hWnd, 1);
            UpdateWindow(hWnd);

            while (GetMessageW(out var msg, IntPtr.Zero, 0, 0) > 0)
            {
                TranslateMessage(ref msg);
                DispatchMessageW(ref msg);
            }

            CleanupIcons();
            return _activeResult ?? result;
        }

        private static void CleanupIcons()
        {
            if (_hWindowIconSmall != IntPtr.Zero && _hWindowIconSmall != _hWindowIcon)
            {
                DestroyIcon(_hWindowIconSmall);
            }
            _hWindowIconSmall = IntPtr.Zero;

            if (_hWindowIcon != IntPtr.Zero)
            {
                DestroyIcon(_hWindowIcon);
                _hWindowIcon = IntPtr.Zero;
            }

            foreach (var kvp in _gameIconCache)
            {
                if (kvp.Value != IntPtr.Zero)
                {
                    DestroyIcon(kvp.Value);
                }
            }
            _gameIconCache.Clear();
        }

        private static IntPtr GetOrExtractGameIcon(InstalledGame game)
        {
            string? candidate = !string.IsNullOrWhiteSpace(game.IconPath) && File.Exists(game.IconPath)
                ? game.IconPath
                : (!string.IsNullOrWhiteSpace(game.ExecutablePath) && File.Exists(game.ExecutablePath) ? game.ExecutablePath : null);

            if (string.IsNullOrEmpty(candidate))
                return IntPtr.Zero;

            if (_gameIconCache.TryGetValue(candidate, out IntPtr cached))
                return cached;

            IntPtr extracted = IntPtr.Zero;
            try
            {
                ushort iconIndex = 0;
                extracted = ExtractAssociatedIconW(GetModuleHandleW(null), candidate, ref iconIndex);
            }
            catch
            {
                extracted = IntPtr.Zero;
            }

            _gameIconCache[candidate] = extracted;
            return extracted;
        }

        private static IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            switch (msg)
            {
                case WM_ERASEBKGND:
                    return (IntPtr)1;

                case WM_SETTINGCHANGE:
                case WM_SYSCOLORCHANGE:
                case WM_THEMECHANGED:
                case WM_DWMCOLORIZATIONCOLORCHANGED:
                    {
                        _strings = Localization.GetStrings();
                        SetWindowTextW(hWnd, _strings.WindowTitle);
                        double newTextScale = QuerySystemTextScaleFactor();
                        var updatedTheme = DetectSystemTheme();
                        if (updatedTheme != _theme || Math.Abs(newTextScale - _textScale) > 0.01)
                        {
                            _theme = updatedTheme;
                            _textScale = newTextScale;
                            ApplyWindowThemeAttributes(hWnd, _theme);
                            InvalidateRect(hWnd, IntPtr.Zero, false);
                        }
                        return IntPtr.Zero;
                    }

                case WM_DPICHANGED:
                    {
                        uint newDpi = (ushort)((wParam.ToInt64() >> 16) & 0xFFFF);
                        if (newDpi > 0)
                        {
                            _currentDpi = newDpi;
                            _dpiScale = newDpi / 96.0;
                            RefreshWindowIcons(hWnd, _currentDpi);
                        }
                        if (lParam != IntPtr.Zero)
                        {
                            var suggested = Marshal.PtrToStructure<RECT>(lParam);
                            SetWindowPos(
                                hWnd,
                                IntPtr.Zero,
                                suggested.Left,
                                suggested.Top,
                                suggested.Width,
                                suggested.Height,
                                SWP_NOZORDER | SWP_NOACTIVATE);
                        }
                        InvalidateRect(hWnd, IntPtr.Zero, false);
                        return IntPtr.Zero;
                    }

                case WM_PAINT:
                    {
                        IntPtr hdc = BeginPaint(hWnd, out var ps);
                        GetClientRect(hWnd, out var clientRect);

                        int width = Math.Max(1, clientRect.Width);
                        int height = Math.Max(1, clientRect.Height);

                        IntPtr memDc = CreateCompatibleDC(hdc);
                        IntPtr memBmp = CreateCompatibleBitmap(hdc, width, height);
                        IntPtr oldBmp = SelectObject(memDc, memBmp);

                        PaintFluent2Content(memDc, width, height, _activeResult);

                        BitBlt(hdc, 0, 0, width, height, memDc, 0, 0, SRCCOPY);

                        SelectObject(memDc, oldBmp);
                        DeleteObject(memBmp);
                        DeleteDC(memDc);

                        EndPaint(hWnd, ref ps);
                        return IntPtr.Zero;
                    }

                case WM_TIMER:
                    {
                        HandleGamepadTick(hWnd);
                        return IntPtr.Zero;
                    }

                case WM_MOUSEMOVE:
                    {
                        int x = (short)(lParam.ToInt64() & 0xFFFF);
                        int y = (short)((lParam.ToInt64() >> 16) & 0xFFFF);

                        bool hasPrimary = _isSettingsView || GetUnsyncedCount() > 0;
                        bool newHovPrimary = hasPrimary && _btnPrimaryRect.Contains(x, y);
                        bool newHovClose = _btnCloseRect.Contains(x, y);
                        bool newHovSettings = _btnSettingsRect.Contains(x, y);
                        bool newHovPaste = _isSettingsView && _btnPasteRect.Contains(x, y);
                        bool newHovClear = _isSettingsView && _btnClearKeyRect.Contains(x, y);
                        bool newHovGetKey = _isSettingsView && _btnGetKeyRect.Contains(x, y);
                        bool newHovToggle = _isSettingsView && _settingsToggleCardRect.Contains(x, y);
                        int newHovRow = _isSettingsView ? -1 : HitTestRow(x, y);

                        if (newHovPrimary != _hoverPrimary ||
                            newHovClose != _hoverClose ||
                            newHovSettings != _hoverSettings ||
                            newHovPaste != _hoverPaste ||
                            newHovClear != _hoverClearKey ||
                            newHovGetKey != _hoverGetKey ||
                            newHovToggle != _hoverToggleCard ||
                            newHovRow != _hoveredRowIndex)
                        {
                            _hoverPrimary = newHovPrimary;
                            _hoverClose = newHovClose;
                            _hoverSettings = newHovSettings;
                            _hoverPaste = newHovPaste;
                            _hoverClearKey = newHovClear;
                            _hoverGetKey = newHovGetKey;
                            _hoverToggleCard = newHovToggle;
                            _hoveredRowIndex = newHovRow;
                            if (!newHovPrimary)
                                _pressedPrimary = false;
                            if (!newHovClose)
                                _pressedClose = false;
                            InvalidateRect(hWnd, IntPtr.Zero, false);
                        }
                        return IntPtr.Zero;
                    }

                case WM_LBUTTONDOWN:
                    {
                        int x = (short)(lParam.ToInt64() & 0xFFFF);
                        int y = (short)((lParam.ToInt64() >> 16) & 0xFFFF);

                        bool hasPrimary = _isSettingsView || GetUnsyncedCount() > 0;
                        if (_btnSettingsRect.Contains(x, y))
                        {
                            if (_isSettingsView)
                            {
                                SaveAndExitSettingsView(hWnd);
                            }
                            else
                            {
                                _isSettingsView = true;
                                _apiKeyInputFocused = true;
                                _focusedZone = FocusZone.PrimaryButton;
                                EnsureWindowHeightForCurrentView(hWnd);
                                InvalidateRect(hWnd, IntPtr.Zero, false);
                            }
                            return IntPtr.Zero;
                        }

                        if (_isSettingsView)
                        {
                            if (_settingsToggleCardRect.Contains(x, y))
                            {
                                _appSettings.DownloadArtwork = !_appSettings.DownloadArtwork;
                                _appSettings.Save(_settingsFilePath);
                                InvalidateRect(hWnd, IntPtr.Zero, false);
                                return IntPtr.Zero;
                            }
                            if (_btnPasteRect.Contains(x, y))
                            {
                                string? clip = TryReadClipboardText(hWnd);
                                if (!string.IsNullOrWhiteSpace(clip))
                                {
                                    _appSettings.SteamGridDbApiKey = clip.Trim();
                                    _appSettings.Save(_settingsFilePath);
                                }
                                _apiKeyInputFocused = true;
                                InvalidateRect(hWnd, IntPtr.Zero, false);
                                return IntPtr.Zero;
                            }
                            if (_btnClearKeyRect.Contains(x, y))
                            {
                                _appSettings.SteamGridDbApiKey = string.Empty;
                                _appSettings.Save(_settingsFilePath);
                                _apiKeyInputFocused = true;
                                InvalidateRect(hWnd, IntPtr.Zero, false);
                                return IntPtr.Zero;
                            }
                            if (_btnGetKeyRect.Contains(x, y))
                            {
                                try
                                {
                                    Process.Start(new ProcessStartInfo("https://www.steamgriddb.com/profile/preferences/api") { UseShellExecute = true });
                                }
                                catch { }
                                return IntPtr.Zero;
                            }
                            _apiKeyInputFocused = _apiKeyBoxRect.Contains(x, y);
                        }

                        if (hasPrimary && _btnPrimaryRect.Contains(x, y))
                        {
                            _pressedPrimary = true;
                            _focusedZone = FocusZone.PrimaryButton;
                            InvalidateRect(hWnd, IntPtr.Zero, false);
                        }
                        else if (_btnCloseRect.Contains(x, y))
                        {
                            _pressedClose = true;
                            _focusedZone = FocusZone.CloseButton;
                            InvalidateRect(hWnd, IntPtr.Zero, false);
                        }
                        else if (!_isSettingsView)
                        {
                            int hitRow = HitTestRow(x, y);
                            if (hitRow >= 0 && hitRow < _totalRows)
                            {
                                _focusedZone = FocusZone.GameList;
                                _focusedRowIndex = hitRow;
                                ToggleRowSelection(hitRow);
                                InvalidateRect(hWnd, IntPtr.Zero, false);
                            }
                        }
                        return IntPtr.Zero;
                    }

                case WM_LBUTTONUP:
                    {
                        int x = (short)(lParam.ToInt64() & 0xFFFF);
                        int y = (short)((lParam.ToInt64() >> 16) & 0xFFFF);

                        bool wasPressedPrimary = _pressedPrimary;
                        bool wasPressedClose = _pressedClose;
                        _pressedPrimary = false;
                        _pressedClose = false;

                        bool hasPrimary = _isSettingsView || GetUnsyncedCount() > 0;
                        if (hasPrimary && wasPressedPrimary && _btnPrimaryRect.Contains(x, y))
                        {
                            ExecutePrimaryAction(hWnd);
                            return IntPtr.Zero;
                        }

                        if (_btnCloseRect.Contains(x, y) && wasPressedClose)
                        {
                            if (_isSettingsView)
                            {
                                SaveAndExitSettingsView(hWnd);
                            }
                            else
                            {
                                CloseDialog(hWnd);
                            }
                            return IntPtr.Zero;
                        }

                        if (wasPressedPrimary || wasPressedClose)
                        {
                            InvalidateRect(hWnd, IntPtr.Zero, false);
                        }
                        return IntPtr.Zero;
                    }

                case WM_APP_REFRESH_RESULT:
                    {
                        var pending = _pendingBackgroundResult;
                        if (pending != null)
                        {
                            _activeResult = pending;
                            foreach (var kvp in _gameIconCache)
                            {
                                if (kvp.Value != IntPtr.Zero)
                                    DestroyIcon(kvp.Value);
                            }
                            _gameIconCache.Clear();
                            RebuildRowItemsFromResult(_activeResult);
                            InvalidateRect(hWnd, IntPtr.Zero, false);
                        }
                        return IntPtr.Zero;
                    }

                case WM_MOUSEWHEEL:
                    {
                        if (_isSettingsView)
                            return IntPtr.Zero;

                        short wheelDelta = (short)((wParam.ToInt64() >> 16) & 0xFFFF);
                        int steps = wheelDelta > 0 ? -1 : 1;
                        if (ScrollBy(steps))
                        {
                            InvalidateRect(hWnd, IntPtr.Zero, false);
                        }
                        return IntPtr.Zero;
                    }

                case WM_CHAR:
                    {
                        if (_isSettingsView && _apiKeyInputFocused)
                        {
                            char ch = (char)wParam.ToInt32();
                            if (ch == '\b')
                            {
                                string current = _appSettings.SteamGridDbApiKey ?? string.Empty;
                                if (current.Length > 0)
                                {
                                    _appSettings.SteamGridDbApiKey = current[..^1];
                                    _appSettings.Save(_settingsFilePath);
                                    InvalidateRect(hWnd, IntPtr.Zero, false);
                                }
                            }
                            else if (char.IsLetterOrDigit(ch))
                            {
                                string current = _appSettings.SteamGridDbApiKey ?? string.Empty;
                                if (current.Length < 64)
                                {
                                    _appSettings.SteamGridDbApiKey = current + ch;
                                    _appSettings.Save(_settingsFilePath);
                                    InvalidateRect(hWnd, IntPtr.Zero, false);
                                }
                            }
                            return IntPtr.Zero;
                        }
                        return IntPtr.Zero;
                    }

                case WM_KEYDOWN:
                    {
                        int key = wParam.ToInt32();
                        HandleKeyDown(hWnd, key);
                        return IntPtr.Zero;
                    }

                case WM_CLOSE:
                    CloseDialog(hWnd);
                    return IntPtr.Zero;

                case WM_DESTROY:
                    KillTimer(hWnd, (IntPtr)1);
                    PostQuitMessage(0);
                    return IntPtr.Zero;

                default:
                    return DefWindowProcW(hWnd, msg, wParam, lParam);
            }
        }

        private static void CloseDialog(IntPtr hWnd)
        {
            KillTimer(hWnd, (IntPtr)1);
            DestroyWindow(hWnd);
        }

        private static void ToggleRowSelection(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _totalRows)
                return;

            var row = _rows[rowIndex];
            if (!row.IsSynced)
            {
                row.IsSelected = !row.IsSelected;
            }
        }

        private static void ToggleSelectAllUnsynced()
        {
            int unsynced = GetUnsyncedCount();
            if (unsynced == 0)
                return;

            bool shouldSelectAll = GetSelectedUnsyncedCount() < unsynced;
            foreach (var row in _rows)
            {
                if (!row.IsSynced)
                {
                    row.IsSelected = shouldSelectAll;
                }
            }
        }

        private static void StartBackgroundArtworkRefresh(IntPtr hWnd)
        {
            var callback = _onSettingsSaved;
            if (callback == null)
                return;

            _activeBackgroundTask = Task.Run(() =>
            {
                try
                {
                    var updated = callback();
                    if (updated != null && hWnd != IntPtr.Zero)
                    {
                        _pendingBackgroundResult = updated;
                        PostMessageW(hWnd, WM_APP_REFRESH_RESULT, IntPtr.Zero, IntPtr.Zero);
                    }
                }
                catch { }
            });
        }

        private static void SaveAndExitSettingsView(IntPtr hWnd)
        {
            _appSettings.Save(_settingsFilePath);
            _isSettingsView = false;
            _apiKeyInputFocused = false;
            _hoverPaste = false;
            _hoverClearKey = false;
            _hoverGetKey = false;
            _hoverToggleCard = false;

            _focusedZone = _totalRows > 0 ? FocusZone.GameList : FocusZone.CloseButton;
            EnsureWindowHeightForCurrentView(hWnd);
            InvalidateRect(hWnd, IntPtr.Zero, false);
        }

        private static void ExecutePrimaryAction(IntPtr hWnd)
        {
            if (_isSettingsView)
            {
                SaveAndExitSettingsView(hWnd);
                return;
            }

            int unsyncedCount = GetUnsyncedCount();
            if (unsyncedCount == 0)
            {
                CloseDialog(hWnd);
                return;
            }

            int selectedCount = GetSelectedUnsyncedCount();
            if (selectedCount == 0)
            {
                // Select all unsynced games when none are selected yet
                foreach (var r in _rows)
                {
                    if (!r.IsSynced)
                        r.IsSelected = true;
                }
                InvalidateRect(hWnd, IntPtr.Zero, false);
                return;
            }

            if (_onSyncSelected != null)
            {
                var toSync = _rows
                    .Where(r => !r.IsSynced && r.IsSelected)
                    .Select(r => r.Game)
                    .ToList();

                _activeResult = _onSyncSelected(toSync);
                if (_activeResult != null)
                {
                    RebuildRowItemsFromResult(_activeResult);
                    if (GetUnsyncedCount() == 0)
                    {
                        _focusedZone = FocusZone.CloseButton;
                    }
                    else
                    {
                        _focusedRowIndex = Math.Clamp(_focusedRowIndex, 0, Math.Max(0, _totalRows - 1));
                    }
                }

                StartBackgroundArtworkRefresh(hWnd);
            }
            else
            {
                foreach (var r in _rows.Where(r => !r.IsSynced && r.IsSelected))
                {
                    r.IsSynced = true;
                    r.IsSelected = false;
                }
                if (GetUnsyncedCount() == 0)
                {
                    _focusedZone = FocusZone.CloseButton;
                }
            }

            InvalidateRect(hWnd, IntPtr.Zero, false);
        }

        private static void HandleKeyDown(IntPtr hWnd, int key)
        {
            if (!_isSettingsView && _gamepadType != GamepadType.Keyboard)
            {
                _gamepadType = GamepadType.Keyboard;
                InvalidateRect(hWnd, IntPtr.Zero, false);
            }

            if (_isSettingsView)
            {
                if (key == VK_ESCAPE)
                {
                    SaveAndExitSettingsView(hWnd);
                    return;
                }
                if (key == VK_RETURN)
                {
                    SaveAndExitSettingsView(hWnd);
                    return;
                }
                if (key == 'V' && (GetKeyState(VK_CONTROL) & 0x8000) != 0)
                {
                    string? clip = TryReadClipboardText(hWnd);
                    if (!string.IsNullOrWhiteSpace(clip))
                    {
                        _appSettings.SteamGridDbApiKey = clip.Trim();
                        _appSettings.Save(_settingsFilePath);
                        InvalidateRect(hWnd, IntPtr.Zero, false);
                    }
                    return;
                }
            }

            switch (key)
            {
                case VK_ESCAPE:
                    CloseDialog(hWnd);
                    break;

                case VK_UP:
                    if (NavigateVertical(-1))
                        InvalidateRect(hWnd, IntPtr.Zero, false);
                    break;

                case VK_DOWN:
                    if (NavigateVertical(1))
                        InvalidateRect(hWnd, IntPtr.Zero, false);
                    break;

                case VK_LEFT:
                    if (NavigateHorizontal(-1))
                        InvalidateRect(hWnd, IntPtr.Zero, false);
                    break;

                case VK_RIGHT:
                    if (NavigateHorizontal(1))
                        InvalidateRect(hWnd, IntPtr.Zero, false);
                    break;

                case VK_TAB:
                    CycleFocusZone();
                    InvalidateRect(hWnd, IntPtr.Zero, false);
                    break;

                case VK_PRIOR:
                    if (_totalRows > 0)
                    {
                        _focusedZone = FocusZone.GameList;
                        _focusedRowIndex = Math.Max(0, _focusedRowIndex - _visibleRows);
                        EnsureFocusedRowVisible();
                        InvalidateRect(hWnd, IntPtr.Zero, false);
                    }
                    break;

                case VK_NEXT:
                    if (_totalRows > 0)
                    {
                        _focusedZone = FocusZone.GameList;
                        _focusedRowIndex = Math.Min(_totalRows - 1, _focusedRowIndex + _visibleRows);
                        EnsureFocusedRowVisible();
                        InvalidateRect(hWnd, IntPtr.Zero, false);
                    }
                    break;

                case VK_SPACE:
                    ActivateFocusedElement(hWnd);
                    break;

                case VK_RETURN:
                    if (_focusedZone == FocusZone.GameList && GetSelectedUnsyncedCount() > 0 && _rows[_focusedRowIndex].IsSelected)
                    {
                        ExecutePrimaryAction(hWnd);
                    }
                    else
                    {
                        ActivateFocusedElement(hWnd);
                    }
                    break;
            }
        }

        private static void ActivateFocusedElement(IntPtr hWnd)
        {
            switch (_focusedZone)
            {
                case FocusZone.GameList:
                    if (_focusedRowIndex >= 0 && _focusedRowIndex < _totalRows)
                    {
                        var row = _rows[_focusedRowIndex];
                        if (!row.IsSynced)
                        {
                            row.IsSelected = !row.IsSelected;
                            InvalidateRect(hWnd, IntPtr.Zero, false);
                        }
                        else if (GetUnsyncedCount() == 0)
                        {
                            CloseDialog(hWnd);
                        }
                    }
                    break;

                case FocusZone.PrimaryButton:
                    ExecutePrimaryAction(hWnd);
                    break;

                case FocusZone.CloseButton:
                    CloseDialog(hWnd);
                    break;
            }
        }

        private static bool NavigateVertical(int delta)
        {
            bool hasPrimary = GetUnsyncedCount() > 0;

            if (delta < 0)
            {
                if (_focusedZone == FocusZone.PrimaryButton || _focusedZone == FocusZone.CloseButton)
                {
                    if (_totalRows > 0)
                    {
                        _focusedZone = FocusZone.GameList;
                        _focusedRowIndex = _totalRows - 1;
                        EnsureFocusedRowVisible();
                        return true;
                    }
                    return false;
                }

                if (_focusedZone == FocusZone.GameList && _focusedRowIndex > 0)
                {
                    _focusedRowIndex--;
                    EnsureFocusedRowVisible();
                    return true;
                }
                return false;
            }
            else if (delta > 0)
            {
                if (_focusedZone == FocusZone.GameList)
                {
                    if (_focusedRowIndex < _totalRows - 1)
                    {
                        _focusedRowIndex++;
                        EnsureFocusedRowVisible();
                        return true;
                    }

                    _focusedZone = hasPrimary ? FocusZone.PrimaryButton : FocusZone.CloseButton;
                    return true;
                }
            }

            return false;
        }

        private static bool NavigateHorizontal(int delta)
        {
            bool hasPrimary = GetUnsyncedCount() > 0;
            if (!hasPrimary)
                return false;

            if (_focusedZone == FocusZone.PrimaryButton && delta > 0)
            {
                _focusedZone = FocusZone.CloseButton;
                return true;
            }

            if (_focusedZone == FocusZone.CloseButton && delta < 0)
            {
                _focusedZone = FocusZone.PrimaryButton;
                return true;
            }

            if (_focusedZone == FocusZone.GameList && delta > 0)
            {
                _focusedZone = FocusZone.PrimaryButton;
                return true;
            }

            return false;
        }

        private static void CycleFocusZone()
        {
            bool hasPrimary = GetUnsyncedCount() > 0;
            if (_focusedZone == FocusZone.GameList)
            {
                _focusedZone = hasPrimary ? FocusZone.PrimaryButton : FocusZone.CloseButton;
            }
            else if (_focusedZone == FocusZone.PrimaryButton)
            {
                _focusedZone = FocusZone.CloseButton;
            }
            else
            {
                _focusedZone = _totalRows > 0 ? FocusZone.GameList : FocusZone.CloseButton;
            }
        }

        private static void EnsureFocusedRowVisible()
        {
            if (_focusedRowIndex < _scrollOffset)
            {
                _scrollOffset = _focusedRowIndex;
            }
            else if (_focusedRowIndex >= _scrollOffset + _visibleRows)
            {
                _scrollOffset = Math.Max(0, _focusedRowIndex - _visibleRows + 1);
            }
        }

        private static bool ScrollBy(int delta)
        {
            int maxOffset = Math.Max(0, _totalRows - _visibleRows);
            int next = Math.Clamp(_scrollOffset + delta, 0, maxOffset);
            if (next == _scrollOffset)
                return false;
            _scrollOffset = next;
            return true;
        }

        private static int HitTestRow(int x, int y)
        {
            if (!_listAreaRect.Contains(x, y) || _totalRows == 0)
                return -1;

            int rowH = GetRowHeight();
            int rowGap = S(6);
            int relY = y - _listAreaRect.Top;
            if (relY < 0)
                return -1;

            int slot = relY / (rowH + rowGap);
            int inSlotY = relY % (rowH + rowGap);
            if (slot < 0 || slot >= _visibleRows || inSlotY > rowH)
                return -1;

            int actualIndex = _scrollOffset + slot;
            return actualIndex < _totalRows ? actualIndex : -1;
        }

        // =====================================================================
        // Gamepad / Joystick Polling (XInput + WinMM DirectInput Fallback)
        // =====================================================================
        private static void HandleGamepadTick(IntPtr hWnd)
        {
            if (_gamepadDirCooldown > 0)
                _gamepadDirCooldown--;

            // Periodically check for controller hot-plug / unplug (~every 660ms at 30 FPS)
            _gamepadDetectTickCounter++;
            if (_gamepadDetectTickCounter >= 20)
            {
                _gamepadDetectTickCounter = 0;
                var hwGamepad = GamepadDetector.DetectConnectedGamepad();
                if (hwGamepad != _lastHardwareGamepad)
                {
                    _lastHardwareGamepad = hwGamepad;
                    if (_gamepadType != hwGamepad)
                    {
                        _gamepadType = hwGamepad;
                        InvalidateRect(hWnd, IntPtr.Zero, false);
                    }
                }
            }

            if (!TryReadGamepadInput(out ushort buttons, out int dirX, out int dirY))
                return;

            // If user interacts with the controller after using the keyboard, switch aesthetics to controller
            if ((buttons != 0 || dirX != 0 || dirY != 0) && _gamepadType == GamepadType.Keyboard)
            {
                var hwGamepad = GamepadDetector.DetectConnectedGamepad();
                _gamepadType = hwGamepad != GamepadType.Keyboard ? hwGamepad : GamepadType.Xbox;
                _lastHardwareGamepad = _gamepadType;
                InvalidateRect(hWnd, IntPtr.Zero, false);
            }

            // Wait for any initially held buttons to be released first
            if (!_initialButtonsCleared)
            {
                if (buttons == 0 && dirX == 0 && dirY == 0)
                {
                    _initialButtonsCleared = true;
                }
                _prevGamepadButtons = buttons;
                return;
            }

            ushort newlyPressed = (ushort)(buttons & ~_prevGamepadButtons);
            _prevGamepadButtons = buttons;

            // 1. Directional navigation (D-Pad or Analog Stick)
            if (dirX == 0 && dirY == 0)
            {
                _gamepadAxisReleased = true;
                _gamepadDirCooldown = 0;
            }
            else if (_gamepadAxisReleased || _gamepadDirCooldown == 0)
            {
                bool changed = false;
                if (dirY != 0)
                {
                    changed = NavigateVertical(dirY);
                }
                else if (dirX != 0)
                {
                    changed = NavigateHorizontal(dirX);
                }

                _gamepadAxisReleased = false;
                _gamepadDirCooldown = 5; // ~165ms repeat interval at 30 FPS

                if (changed)
                {
                    InvalidateRect(hWnd, IntPtr.Zero, false);
                }
            }

            // 2. Action Buttons (Edge-triggered)
            if ((newlyPressed & XINPUT_GAMEPAD_B) != 0 || (newlyPressed & XINPUT_GAMEPAD_BACK) != 0)
            {
                CloseDialog(hWnd);
                return;
            }

            if ((newlyPressed & XINPUT_GAMEPAD_A) != 0)
            {
                ActivateFocusedElement(hWnd);
                return;
            }

            if ((newlyPressed & XINPUT_GAMEPAD_X) != 0 || (newlyPressed & XINPUT_GAMEPAD_START) != 0)
            {
                ExecutePrimaryAction(hWnd);
                return;
            }

            if ((newlyPressed & XINPUT_GAMEPAD_Y) != 0)
            {
                ToggleSelectAllUnsynced();
                InvalidateRect(hWnd, IntPtr.Zero, false);
            }
        }

        private static bool TryReadGamepadInput(out ushort buttons, out int dirX, out int dirY)
        {
            buttons = 0;
            dirX = 0;
            dirY = 0;

            // 1. Try XInput (Xbox & XInput-compatible controllers)
            if (!_xinputFailed)
            {
                for (uint i = 0; i < 4; i++)
                {
                    uint err;
                    XINPUT_STATE state;

                    try
                    {
                        err = XInputGetState14(i, out state);
                    }
                    catch (DllNotFoundException)
                    {
                        try
                        {
                            err = XInputGetState910(i, out state);
                        }
                        catch
                        {
                            _xinputFailed = true;
                            break;
                        }
                    }
                    catch
                    {
                        _xinputFailed = true;
                        break;
                    }

                    if (err == 0)
                    {
                        ushort b = state.Gamepad.wButtons;
                        buttons = b;

                        if ((b & XINPUT_GAMEPAD_DPAD_UP) != 0 || state.Gamepad.sThumbLY > 16000)
                            dirY = -1;
                        else if ((b & XINPUT_GAMEPAD_DPAD_DOWN) != 0 || state.Gamepad.sThumbLY < -16000)
                            dirY = 1;

                        if ((b & XINPUT_GAMEPAD_DPAD_LEFT) != 0 || state.Gamepad.sThumbLX < -16000)
                            dirX = -1;
                        else if ((b & XINPUT_GAMEPAD_DPAD_RIGHT) != 0 || state.Gamepad.sThumbLX > 16000)
                            dirX = 1;

                        return true;
                    }
                }
            }

            // 2. Fallback to WinMM joyGetPosEx (PlayStation DualSense/DualShock, Switch Pro, DirectInput Joysticks)
            if (!_winmmFailed)
            {
                for (uint j = 0; j < 2; j++)
                {
                    try
                    {
                        var ji = new JOYINFOEX
                        {
                            dwSize = (uint)Marshal.SizeOf<JOYINFOEX>(),
                            dwFlags = JOY_RETURNALL
                        };

                        if (joyGetPosEx(j, ref ji) == 0)
                        {
                            ushort mappedButtons = 0;
                            // Standard DirectInput / PlayStation / Generic button mapping:
                            // Button 1 (0x01) = Square/X, Button 2 (0x02) = Cross/A, Button 3 (0x04) = Circle/B, Button 4 (0x08) = Triangle/Y
                            // For PlayStation (detected via VID_054C): Cross is index 1 (0x02), Circle is index 2 (0x04), Square is index 0 (0x01), Triangle is index 3 (0x08)
                            if (_gamepadType == GamepadType.PlayStation)
                            {
                                if ((ji.dwButtons & 0x0002) != 0) mappedButtons |= XINPUT_GAMEPAD_A;     // Cross
                                if ((ji.dwButtons & 0x0004) != 0) mappedButtons |= XINPUT_GAMEPAD_B;     // Circle
                                if ((ji.dwButtons & 0x0001) != 0) mappedButtons |= XINPUT_GAMEPAD_X;     // Square
                                if ((ji.dwButtons & 0x0008) != 0) mappedButtons |= XINPUT_GAMEPAD_Y;     // Triangle
                            }
                            else
                            {
                                if ((ji.dwButtons & 0x0001) != 0) mappedButtons |= XINPUT_GAMEPAD_A;     // Button 1
                                if ((ji.dwButtons & 0x0002) != 0) mappedButtons |= XINPUT_GAMEPAD_B;     // Button 2
                                if ((ji.dwButtons & 0x0004) != 0) mappedButtons |= XINPUT_GAMEPAD_X;     // Button 3
                                if ((ji.dwButtons & 0x0008) != 0) mappedButtons |= XINPUT_GAMEPAD_Y;     // Button 4
                            }

                            if ((ji.dwButtons & 0x0100) != 0) mappedButtons |= XINPUT_GAMEPAD_BACK;
                            if ((ji.dwButtons & 0x0200) != 0) mappedButtons |= XINPUT_GAMEPAD_START;

                            buttons = mappedButtons;

                            // Check POV Hat (in hundredths of a degree: 0=Up, 9000=Right, 18000=Down, 27000=Left)
                            if (ji.dwPOV != JOY_POVCENTERED && ji.dwPOV <= 36000)
                            {
                                if (ji.dwPOV >= 31500 || ji.dwPOV <= 4500)
                                    dirY = -1;
                                else if (ji.dwPOV >= 13500 && ji.dwPOV <= 22500)
                                    dirY = 1;

                                if (ji.dwPOV >= 4500 && ji.dwPOV <= 13500)
                                    dirX = 1;
                                else if (ji.dwPOV >= 22500 && ji.dwPOV <= 31500)
                                    dirX = -1;
                            }

                            // Check Analog Stick X / Y (center = 32767)
                            if (dirY == 0)
                            {
                                if (ji.dwYpos < 16000) dirY = -1;
                                else if (ji.dwYpos > 49000) dirY = 1;
                            }
                            if (dirX == 0)
                            {
                                if (ji.dwXpos < 16000) dirX = -1;
                                else if (ji.dwXpos > 49000) dirX = 1;
                            }

                            return true;
                        }
                    }
                    catch
                    {
                        _winmmFailed = true;
                        break;
                    }
                }
            }

            return false;
        }

        private static int MeasureTextWidth(IntPtr hdc, IntPtr font, string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            IntPtr oldFont = SelectObject(hdc, font);
            bool ok = GetTextExtentPoint32W(hdc, text, text.Length, out SIZE size);
            SelectObject(hdc, oldFont);
            return ok ? size.cx : text.Length * S(8);
        }

        private static void PaintFluent2Content(IntPtr hdc, int clientW, int clientH, SteamSyncResult? result)
        {
            SetBkMode(hdc, TRANSPARENT);

            int marginX = S(24);
            int footerHeight = S(60);
            int footerTop = Math.Max(S(160), clientH - footerHeight);

            // 1. Base Layer & ContentDialog Command Space (Footer) Surfaces
            var rcBody = new RECT { Left = 0, Top = 0, Right = clientW, Bottom = footerTop };
            IntPtr hBodyBrush = CreateSolidBrush(_theme.BgBase);
            FillRect(hdc, ref rcBody, hBodyBrush);
            DeleteObject(hBodyBrush);

            var rcFooter = new RECT { Left = 0, Top = footerTop, Right = clientW, Bottom = clientH };
            IntPtr hFooterBrush = CreateSolidBrush(_theme.BgFooter);
            FillRect(hdc, ref rcFooter, hFooterBrush);
            DeleteObject(hFooterBrush);

            GdipCreateFromHDC(hdc, out IntPtr g);
            if (g != IntPtr.Zero)
            {
                GdipSetSmoothingMode(g, SmoothingModeAntiAlias);
                GdipSetPixelOffsetMode(g, PixelOffsetModeHighQuality);
                GdipSetCompositingQuality(g, CompositingQualityHighQuality);

                // 1px Fluent 2 Footer Divider Stroke
                DrawSmoothLine(g, 0f, footerTop, clientW, footerTop, ToArgb(_theme.Divider), Math.Max(1f, (float)Math.Round(_dpiScale * 0.8)));
            }

            // 2. Fluent 2 Type Ramp Fonts
            IntPtr fontTitle = CreateFluentFont(-SF(16), 600, "Segoe UI Variable Display");
            IntPtr fontBodyStrong = CreateFluentFont(-SF(14), 600, "Segoe UI Variable Text");
            IntPtr fontBody = CreateFluentFont(-SF(14), 400, "Segoe UI Variable Text");
            IntPtr fontCaptionStrong = CreateFluentFont(-SF(12), 600, "Segoe UI Variable Text");
            IntPtr fontCaption = CreateFluentFont(-SF(12), 400, "Segoe UI Variable Text");

            // 3. Fluent 2 Hero Status Card (WinUI 3 InfoBar)
            int heroTop = S(20);
            int heroHeight = Math.Max(S(52), SF(46));
            int heroBottom = heroTop + heroHeight;
            DrawFluentHeroBanner(hdc, g, marginX, heroTop, clientW - marginX, heroBottom, result, fontTitle);

            // 4. Section Header & Rounded Counter Pill
            string sectionHeader = _isSettingsView ? _strings.SettingsTitle : _strings.CardHeaderDetected;
            string countText = $"{_totalRows}";
            int countPillW = Math.Max(S(26), MeasureTextWidth(hdc, fontCaptionStrong, countText) + S(14));
            int countPillH = Math.Max(S(20), SF(18));

            int secTop = heroBottom + S(16);
            int secHeight = Math.Max(S(22), SF(20));
            int secBottom = secTop + secHeight;

            if (!_isSettingsView && result != null && result.Success)
            {
                int pillRight = clientW - marginX;
                int pillLeft = pillRight - countPillW;
                int pillTop = secTop + (secHeight - countPillH) / 2;

                uint pillFill = _theme.IsHighContrast
                    ? ToArgb(_theme.TagFill)
                    : ToArgb(_theme.AccentDefault, _theme.IsLight ? (byte)24 : (byte)36);
                uint pillStroke = _theme.IsHighContrast
                    ? ToArgb(_theme.TagStroke)
                    : ToArgb(_theme.AccentDefault, _theme.IsLight ? (byte)55 : (byte)70);

                DrawSmoothRoundedCard(g, pillLeft, pillTop, countPillW, countPillH, countPillH / 2f, pillFill, pillStroke, 1f);

                SelectObject(hdc, fontCaptionStrong);
                SetTextColor(hdc, _theme.IsHighContrast ? _theme.TextPrimary : _theme.AccentDefault);
                var rcSecCount = new RECT { Left = pillLeft, Top = pillTop, Right = pillRight, Bottom = pillTop + countPillH };
                DrawTextW(hdc, countText, -1, ref rcSecCount, DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);
            }

            SelectObject(hdc, fontCaptionStrong);
            SetTextColor(hdc, _theme.TextSecondary);
            var rcSecLabel = new RECT
            {
                Left = marginX + S(2),
                Top = secTop,
                Right = clientW - marginX - (countPillW + S(8)),
                Bottom = secBottom
            };
            DrawTextW(hdc, sectionHeader, -1, ref rcSecLabel, DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX);

            // 5. Fluent 2 Interactive SettingsCard List Area
            _listAreaRect = new RECT
            {
                Left = marginX,
                Top = secBottom + S(8),
                Right = clientW - marginX,
                Bottom = footerTop - S(16)
            };

            if (_isSettingsView)
            {
                DrawFluentSettingsPanel(hdc, g, _listAreaRect, fontBodyStrong, fontBody, fontCaptionStrong);
            }
            else if (result != null && result.Success)
            {
                if (_totalRows == 0)
                {
                    DrawSmoothRoundedCard(
                        g,
                        _listAreaRect.Left,
                        _listAreaRect.Top,
                        _listAreaRect.Width,
                        Math.Max(S(48), _listAreaRect.Height),
                        Sf(8f),
                        ToArgb(_theme.CardDefault),
                        ToArgb(_theme.CardStroke),
                        1f);

                    SelectObject(hdc, fontBody);
                    SetTextColor(hdc, _theme.TextTertiary);
                    var rcEmpty = new RECT
                    {
                        Left = _listAreaRect.Left + S(16),
                        Top = _listAreaRect.Top,
                        Right = _listAreaRect.Right - S(16),
                        Bottom = _listAreaRect.Top + Math.Max(S(48), _listAreaRect.Height)
                    };
                    DrawTextW(hdc, _strings.EmptyMessage, -1, ref rcEmpty, DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX);
                }
                else
                {
                    int rowH = GetRowHeight();
                    int rowGap = S(6);
                    int availH = Math.Max(rowH, _listAreaRect.Height);

                    _visibleRows = Math.Max(1, (availH + rowGap) / (rowH + rowGap));
                    int maxOffset = Math.Max(0, _totalRows - _visibleRows);
                    if (_scrollOffset > maxOffset)
                        _scrollOffset = maxOffset;

                    bool hasScroll = _totalRows > _visibleRows;
                    int rightCardEdge = _listAreaRect.Right - (hasScroll ? S(12) : 0);

                    int visibleCount = Math.Min(_visibleRows, _totalRows - _scrollOffset);
                    int rowY = _listAreaRect.Top;

                    for (int i = 0; i < visibleCount; i++)
                    {
                        int itemIdx = _scrollOffset + i;
                        var rowItem = _rows[itemIdx];
                        bool isHovered = itemIdx == _hoveredRowIndex;
                        bool isFocused = _focusedZone == FocusZone.GameList && itemIdx == _focusedRowIndex;

                        DrawGameSettingsCard(
                            hdc,
                            g,
                            _listAreaRect.Left,
                            rowY,
                            rightCardEdge,
                            rowY + rowH,
                            rowItem,
                            isHovered,
                            isFocused,
                            fontBodyStrong,
                            fontCaptionStrong);

                        rowY += rowH + rowGap;
                    }

                    if (hasScroll)
                    {
                        DrawFluentScrollbar(
                            g,
                            _listAreaRect.Right - S(5),
                            _listAreaRect.Top + S(2),
                            _listAreaRect.Right - S(1),
                            rowY - rowGap - S(2),
                            _totalRows);
                    }
                }
            }
            else
            {
                DrawSmoothRoundedCard(
                    g,
                    _listAreaRect.Left,
                    _listAreaRect.Top,
                    _listAreaRect.Width,
                    Math.Max(S(48), _listAreaRect.Height),
                    Sf(8f),
                    ToArgb(_theme.CardDefault),
                    ToArgb(_theme.CriticalBorder),
                    1f);

                SelectObject(hdc, fontBody);
                SetTextColor(hdc, _theme.CriticalIcon);
                var rcErr = new RECT
                {
                    Left = _listAreaRect.Left + S(16),
                    Top = _listAreaRect.Top + S(12),
                    Right = _listAreaRect.Right - S(16),
                    Bottom = _listAreaRect.Bottom - S(12)
                };
                DrawTextW(hdc, result?.ErrorMessage ?? _strings.HeaderError, -1, ref rcErr, DT_LEFT | DT_WORDBREAK | DT_NOPREFIX);
            }

            // 6. Command Space Footer Buttons (Settings Icon Bottom-Left + Primary Add/SelectAll + Close)
            int unsyncedCount = GetUnsyncedCount();
            int selectedCount = GetSelectedUnsyncedCount();
            bool showPrimaryBtn = _isSettingsView || unsyncedCount > 0;

            int btnH = Math.Max(S(32), SF(30));
            int btnTop = footerTop + (footerHeight - btnH) / 2;

            // Icon-only Settings button on bottom-left of footer
            _btnSettingsRect = new RECT
            {
                Left = marginX,
                Top = btnTop,
                Right = marginX + btnH,
                Bottom = btnTop + btnH
            };

            uint gearFill = _isSettingsView
                ? ToArgb(_theme.AccentDefault, _theme.IsLight ? (byte)32 : (byte)48)
                : (_hoverSettings ? ToArgb(_theme.CardHover) : ToArgb(_theme.CardDefault));
            uint gearStroke = (_isSettingsView || _hoverSettings)
                ? ToArgb(_theme.AccentDefault)
                : ToArgb(_theme.TagStroke);
            uint gearIconCol = _isSettingsView
                ? ToArgb(_theme.AccentDefault)
                : ToArgb(_theme.TextPrimary);

            DrawSmoothRoundedCard(
                g,
                _btnSettingsRect.Left,
                _btnSettingsRect.Top,
                _btnSettingsRect.Width,
                _btnSettingsRect.Height,
                Sf(6f),
                gearFill,
                gearStroke,
                1f);

            DrawVectorSettingsGear(
                g,
                _btnSettingsRect.Left + _btnSettingsRect.Width / 2f,
                _btnSettingsRect.Top + _btnSettingsRect.Height / 2f,
                _btnSettingsRect.Height * 0.56f,
                gearIconCol);

            int badgeH = Math.Max(S(18), SF(17));
            int closeBadgeW = MeasureInputGlyphBadgeWidth(hdc, fontCaptionStrong, badgeH, InputGlyph.Cancel);
            int closeTextW = MeasureTextWidth(hdc, fontBodyStrong, _strings.ActionClose);
            int closeBtnW = Math.Max(S(104), closeTextW + closeBadgeW + S(34));
            _btnCloseRect = new RECT
            {
                Left = clientW - marginX - closeBtnW,
                Top = btnTop,
                Right = clientW - marginX,
                Bottom = btnTop + btnH
            };

            int buttonsLeftEdge = _btnCloseRect.Left;

            if (showPrimaryBtn)
            {
                string primaryLabel = _isSettingsView
                    ? _strings.SettingsSaveBackButton
                    : (selectedCount > 0
                        ? string.Format(_strings.ActionAddSelectedFormat, selectedCount)
                        : _strings.ActionSelectAll);

                int primaryBadgeW = MeasureInputGlyphBadgeWidth(hdc, fontCaptionStrong, badgeH, InputGlyph.PrimaryAction);
                int primaryTextW = MeasureTextWidth(hdc, fontBodyStrong, primaryLabel);
                int primaryBtnW = Math.Max(S(142), primaryTextW + primaryBadgeW + S(34));
                int primaryRight = _btnCloseRect.Left - S(8);
                _btnPrimaryRect = new RECT
                {
                    Left = primaryRight - primaryBtnW,
                    Top = btnTop,
                    Right = primaryRight,
                    Bottom = btnTop + btnH
                };
                buttonsLeftEdge = _btnPrimaryRect.Left;

                DrawFluentButton(
                    hdc,
                    g,
                    _btnPrimaryRect,
                    primaryLabel,
                    isPrimaryAccent: true,
                    isHovered: _hoverPrimary,
                    isPressed: _pressedPrimary,
                    isFocused: _focusedZone == FocusZone.PrimaryButton,
                    fontBodyStrong,
                    InputGlyph.PrimaryAction,
                    fontCaptionStrong);

                DrawFluentButton(
                    hdc,
                    g,
                    _btnCloseRect,
                    _strings.ActionClose,
                    isPrimaryAccent: false,
                    isHovered: _hoverClose,
                    isPressed: _pressedClose,
                    isFocused: _focusedZone == FocusZone.CloseButton,
                    fontBodyStrong,
                    InputGlyph.Cancel,
                    fontCaptionStrong);
            }
            else
            {
                _btnPrimaryRect = default;
                DrawFluentButton(
                    hdc,
                    g,
                    _btnCloseRect,
                    _strings.ActionClose,
                    isPrimaryAccent: true,
                    isHovered: _hoverClose,
                    isPressed: _pressedClose,
                    isFocused: _focusedZone == FocusZone.CloseButton,
                    fontBodyStrong,
                    InputGlyph.Cancel,
                    fontCaptionStrong);
            }

            // 7. Controller / Joystick Navigation Legend on Footer Left (next to Settings Icon)
            DrawControllerHintLegend(hdc, g, _btnSettingsRect.Right + S(12), footerTop, buttonsLeftEdge - S(12), clientH, unsyncedCount > 0, fontCaption, fontCaptionStrong);

            if (g != IntPtr.Zero)
            {
                GdipDeleteGraphics(g);
            }

            DeleteObject(fontTitle);
            DeleteObject(fontBodyStrong);
            DeleteObject(fontBody);
            DeleteObject(fontCaptionStrong);
            DeleteObject(fontCaption);
        }

        private static void DrawFluentSettingsPanel(
            IntPtr hdc,
            IntPtr g,
            RECT area,
            IntPtr fontBodyStrong,
            IntPtr fontBody,
            IntPtr fontCaptionStrong)
        {
            int cardGap = S(8);
            int toggleCardH = GetRowHeight();
            _settingsToggleCardRect = new RECT
            {
                Left = area.Left,
                Top = area.Top,
                Right = area.Right,
                Bottom = area.Top + toggleCardH
            };

            uint toggleBg = _hoverToggleCard ? _theme.CardHover : _theme.CardDefault;
            DrawSmoothRoundedCard(
                g,
                _settingsToggleCardRect.Left,
                _settingsToggleCardRect.Top,
                _settingsToggleCardRect.Width,
                _settingsToggleCardRect.Height,
                Sf(8f),
                ToArgb(toggleBg),
                ToArgb(_theme.CardStroke),
                1f);

            int checkSize = Math.Max(S(20), SF(18));
            int checkX = _settingsToggleCardRect.Left + S(14);
            int checkY = _settingsToggleCardRect.Top + (_settingsToggleCardRect.Height - checkSize) / 2;

            if (_appSettings.DownloadArtwork)
            {
                DrawSmoothRoundedCard(
                    g,
                    checkX,
                    checkY,
                    checkSize,
                    checkSize,
                    Sf(4f),
                    ToArgb(_theme.AccentDefault),
                    ToArgb(_theme.AccentDefault),
                    1f);
                DrawVectorCheckmark(
                    g,
                    checkX + checkSize / 2f,
                    checkY + checkSize / 2f,
                    checkSize,
                    ToArgb(_theme.TextOnAccent),
                    Math.Max(1.6f, Sf(1.8f)));
            }
            else
            {
                DrawSmoothRoundedCard(
                    g,
                    checkX,
                    checkY,
                    checkSize,
                    checkSize,
                    Sf(4f),
                    ToArgb(_theme.LayerDefault),
                    ToArgb(_theme.TextTertiary),
                    Math.Max(1.2f, Sf(1.3f)));
            }

            SelectObject(hdc, fontBodyStrong);
            SetTextColor(hdc, _theme.TextPrimary);
            var rcToggleText = new RECT
            {
                Left = checkX + checkSize + S(12),
                Top = _settingsToggleCardRect.Top,
                Right = _settingsToggleCardRect.Right - S(14),
                Bottom = _settingsToggleCardRect.Bottom
            };
            DrawTextW(hdc, _strings.SettingsDownloadToggle, -1, ref rcToggleText, DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX);

            // Card 2: SteamGridDB API Key Card
            int keyCardTop = _settingsToggleCardRect.Bottom + cardGap;
            int keyCardH = Math.Max(S(88), SF(80));
            var rcKeyCard = new RECT
            {
                Left = area.Left,
                Top = keyCardTop,
                Right = area.Right,
                Bottom = keyCardTop + keyCardH
            };

            DrawSmoothRoundedCard(
                g,
                rcKeyCard.Left,
                rcKeyCard.Top,
                rcKeyCard.Width,
                rcKeyCard.Height,
                Sf(8f),
                ToArgb(_theme.CardDefault),
                ToArgb(_theme.CardStroke),
                1f);

            int headerRowTop = rcKeyCard.Top + S(10);
            int smallBtnH = Math.Max(S(24), SF(22));

            // Right-aligned mini buttons: [Obtener API Key] [Borrar] [Pegar]
            int pasteW = Math.Max(S(58), MeasureTextWidth(hdc, fontCaptionStrong, _strings.SettingsPasteButton) + S(20));
            int clearW = Math.Max(S(58), MeasureTextWidth(hdc, fontCaptionStrong, _strings.SettingsClearButton) + S(20));
            int getKeyW = Math.Max(S(96), MeasureTextWidth(hdc, fontCaptionStrong, _strings.SettingsGetKeyButton) + S(20));

            int pasteRight = rcKeyCard.Right - S(14);
            _btnPasteRect = new RECT
            {
                Left = pasteRight - pasteW,
                Top = headerRowTop,
                Right = pasteRight,
                Bottom = headerRowTop + smallBtnH
            };

            int clearRight = _btnPasteRect.Left - S(6);
            _btnClearKeyRect = new RECT
            {
                Left = clearRight - clearW,
                Top = headerRowTop,
                Right = clearRight,
                Bottom = headerRowTop + smallBtnH
            };

            int getKeyRight = _btnClearKeyRect.Left - S(6);
            _btnGetKeyRect = new RECT
            {
                Left = getKeyRight - getKeyW,
                Top = headerRowTop,
                Right = getKeyRight,
                Bottom = headerRowTop + smallBtnH
            };

            DrawFluentButton(hdc, g, _btnGetKeyRect, _strings.SettingsGetKeyButton, false, _hoverGetKey, false, false, fontCaptionStrong);
            DrawFluentButton(hdc, g, _btnClearKeyRect, _strings.SettingsClearButton, false, _hoverClearKey, false, false, fontCaptionStrong);
            DrawFluentButton(hdc, g, _btnPasteRect, _strings.SettingsPasteButton, true, _hoverPaste, false, false, fontCaptionStrong);

            SelectObject(hdc, fontBodyStrong);
            SetTextColor(hdc, _theme.TextPrimary);
            var rcKeyLabel = new RECT
            {
                Left = rcKeyCard.Left + S(14),
                Top = headerRowTop,
                Right = _btnGetKeyRect.Left - S(8),
                Bottom = headerRowTop + smallBtnH
            };
            DrawTextW(hdc, _strings.SettingsApiKeyLabel, -1, ref rcKeyLabel, DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX);

            // API Key Input Box
            int boxTop = headerRowTop + smallBtnH + S(10);
            int boxH = Math.Max(S(32), SF(28));
            _apiKeyBoxRect = new RECT
            {
                Left = rcKeyCard.Left + S(14),
                Top = boxTop,
                Right = rcKeyCard.Right - S(14),
                Bottom = boxTop + boxH
            };

            uint boxStroke = _apiKeyInputFocused ? _theme.AccentDefault : _theme.TagStroke;
            float boxStrokeW = _apiKeyInputFocused ? Math.Max(1.5f, Sf(1.6f)) : 1f;
            DrawSmoothRoundedCard(
                g,
                _apiKeyBoxRect.Left,
                _apiKeyBoxRect.Top,
                _apiKeyBoxRect.Width,
                _apiKeyBoxRect.Height,
                Sf(4f),
                ToArgb(_theme.LayerDefault),
                ToArgb(boxStroke),
                boxStrokeW);

            string currentKey = _appSettings.SteamGridDbApiKey ?? string.Empty;
            bool hasKeyText = !string.IsNullOrWhiteSpace(currentKey);
            string displayKey = hasKeyText
                ? (_apiKeyInputFocused ? currentKey + "|" : currentKey)
                : _strings.SettingsApiKeyPlaceholder;

            SelectObject(hdc, fontBody);
            SetTextColor(hdc, hasKeyText ? _theme.TextPrimary : _theme.TextTertiary);
            var rcBoxText = new RECT
            {
                Left = _apiKeyBoxRect.Left + S(10),
                Top = _apiKeyBoxRect.Top,
                Right = _apiKeyBoxRect.Right - S(10),
                Bottom = _apiKeyBoxRect.Bottom
            };
            DrawTextW(hdc, displayKey, -1, ref rcBoxText, DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX);
        }

        private static void DrawControllerHintLegend(
            IntPtr hdc,
            IntPtr g,
            int left,
            int top,
            int right,
            int bottom,
            bool hasUnsynced,
            IntPtr fontCaption,
            IntPtr fontCaptionStrong)
        {
            if (_isSettingsView || !hasUnsynced || right - left < S(60))
                return;

            int cy = (top + bottom) / 2;
            int badgeH = Math.Max(S(18), SF(17));
            int curX = left;

            // 1. Toggle badge + label (e.g. (A) / (✕) / (B) / [Space] Marcar)
            int w1 = DrawInputGlyphBadge(hdc, g, curX, cy, badgeH, InputGlyph.Confirm, false, fontCaptionStrong);
            curX += w1 + S(6);

            SelectObject(hdc, fontCaption);
            SetTextColor(hdc, _theme.TextSecondary);
            int toggleTextW = MeasureTextWidth(hdc, fontCaption, _strings.HintToggle);
            if (curX + toggleTextW > right)
                return;

            var rcToggle = new RECT
            {
                Left = curX,
                Top = top,
                Right = Math.Min(right, curX + toggleTextW + S(4)),
                Bottom = bottom
            };
            DrawTextW(hdc, _strings.HintToggle, -1, ref rcToggle, DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);
            curX += toggleTextW + S(14);

            // 2. Select All badge + label when a gamepad is active (e.g. (Y) / (△) / (X))
            if (_gamepadType != GamepadType.Keyboard)
            {
                int badge2W = MeasureInputGlyphBadgeWidth(hdc, fontCaptionStrong, badgeH, InputGlyph.SecondaryAction);
                int addTextW = MeasureTextWidth(hdc, fontCaption, _strings.ActionSelectAll);
                if (curX + badge2W + S(6) + addTextW <= right)
                {
                    int w2 = DrawInputGlyphBadge(hdc, g, curX, cy, badgeH, InputGlyph.SecondaryAction, false, fontCaptionStrong);
                    curX += w2 + S(6);

                    SelectObject(hdc, fontCaption);
                    SetTextColor(hdc, _theme.TextSecondary);
                    var rcAll = new RECT
                    {
                        Left = curX,
                        Top = top,
                        Right = right,
                        Bottom = bottom
                    };
                    DrawTextW(hdc, _strings.ActionSelectAll, -1, ref rcAll, DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX);
                }
            }
        }

        private static void DrawFluentHeroBanner(
            IntPtr hdc,
            IntPtr g,
            int left,
            int top,
            int right,
            int bottom,
            SteamSyncResult? result,
            IntPtr fontTitle)
        {
            uint bgCol;
            uint borderCol;
            uint iconCol;
            bool isError = false;
            bool showLeftAccentStrip = false;
            string statusMessage;

            int unsyncedCount = GetUnsyncedCount();

            if (result != null && result.Success)
            {
                if (result.AddedGames.Count > 0 && unsyncedCount == 0)
                {
                    bgCol = _theme.SuccessBg;
                    borderCol = _theme.SuccessBorder;
                    iconCol = _theme.SuccessIcon;
                    statusMessage = $"{StripLeadingSymbol(_strings.HeaderSuccess)} ({result.AddedGames.Count})";
                }
                else if (unsyncedCount > 0)
                {
                    bgCol = _theme.InfoBg;
                    borderCol = _theme.InfoBorder;
                    iconCol = _theme.InfoIcon;
                    showLeftAccentStrip = true;
                    statusMessage = string.Format(_strings.HeaderAvailableToAddFormat, unsyncedCount);
                }
                else
                {
                    bgCol = _theme.SuccessBg;
                    borderCol = _theme.SuccessBorder;
                    iconCol = _theme.SuccessIcon;
                    statusMessage = StripLeadingSymbol(_strings.HeaderUpToDate);
                }
            }
            else
            {
                bgCol = _theme.CriticalBg;
                borderCol = _theme.CriticalBorder;
                iconCol = _theme.CriticalIcon;
                isError = true;
                statusMessage = StripLeadingSymbol(_strings.HeaderError);
            }

            uint shadowCol = _theme.IsHighContrast
                ? 0
                : (_theme.IsLight ? ToArgb(Rgb(0, 0, 0), 10) : ToArgb(Rgb(0, 0, 0), 32));

            DrawSmoothRoundedCard(
                g,
                left,
                top,
                right - left,
                bottom - top,
                Sf(8f),
                ToArgb(bgCol),
                ToArgb(borderCol),
                1f,
                shadowCol);

            if (showLeftAccentStrip && !_theme.IsHighContrast)
            {
                float stripW = Math.Max(3f, Sf(3.5f));
                float stripH = (bottom - top) - Sf(16f);
                DrawSmoothRoundedCard(
                    g,
                    left + Sf(6f),
                    top + Sf(8f),
                    stripW,
                    stripH,
                    stripW / 2f,
                    ToArgb(iconCol),
                    0);
            }

            int emblemSize = Math.Max(S(26), SF(24));
            int emblemX = left + S(18);
            int emblemY = top + (bottom - top - emblemSize) / 2;

            DrawSmoothCircle(g, emblemX, emblemY, emblemSize, ToArgb(iconCol));

            uint glyphCol = GetRelativeLuminance(iconCol) > 0.55 ? ToArgb(Rgb(0, 0, 0)) : ToArgb(Rgb(255, 255, 255));
            float cx = emblemX + emblemSize / 2f;
            float cy = emblemY + emblemSize / 2f;
            float strokeW = Math.Max(1.8f, Sf(2.0f));

            if (isError)
            {
                DrawVectorCross(g, cx, cy, emblemSize, glyphCol, strokeW);
            }
            else
            {
                DrawVectorCheckmark(g, cx, cy, emblemSize, glyphCol, strokeW);
            }

            SelectObject(hdc, fontTitle);
            SetTextColor(hdc, _theme.TextPrimary);
            var rcTitle = new RECT
            {
                Left = emblemX + emblemSize + S(14),
                Top = top,
                Right = right - S(16),
                Bottom = bottom
            };
            DrawTextW(hdc, statusMessage, -1, ref rcTitle, DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX);
        }

        private static void DrawGameSettingsCard(
            IntPtr hdc,
            IntPtr g,
            int left,
            int top,
            int right,
            int bottom,
            GameRowItem row,
            bool isHovered,
            bool isFocused,
            IntPtr fontBodyStrong,
            IntPtr fontCaptionStrong)
        {
            var game = row.Game;
            bool highlightSurface = isHovered || isFocused;
            uint rowBg = highlightSurface ? _theme.CardHover : _theme.CardDefault;
            uint rowTextCol = (_theme.IsHighContrast && highlightSurface) ? _theme.TextOnAccent : _theme.TextPrimary;
            uint strokeCol = isFocused
                ? (_theme.IsHighContrast ? _theme.TextOnAccent : _theme.AccentDefault)
                : _theme.RowStroke;
            float strokeW = isFocused ? Math.Max(1.8f, Sf(2f)) : 1f;

            uint shadowCol = _theme.IsHighContrast
                ? 0
                : (_theme.IsLight ? ToArgb(Rgb(0, 0, 0), 8) : ToArgb(Rgb(0, 0, 0), 28));

            DrawSmoothRoundedCard(
                g,
                left,
                top,
                right - left,
                bottom - top,
                Sf(8f),
                ToArgb(rowBg),
                ToArgb(strokeCol),
                strokeW,
                shadowCol);

            // Focused indicator bar on left edge for unmistakable Couch Gaming joystick focus
            if (isFocused && !_theme.IsHighContrast)
            {
                float barW = Math.Max(3f, Sf(3.5f));
                float barH = (bottom - top) - Sf(16f);
                DrawSmoothRoundedCard(
                    g,
                    left + Sf(4f),
                    top + Sf(8f),
                    barW,
                    barH,
                    barW / 2f,
                    ToArgb(_theme.AccentDefault),
                    0);
            }

            // 1. Left Selection CheckBox (for unsynced games) or Synced Checkmark Badge (for synced games)
            int checkSize = Math.Max(S(20), SF(18));
            int checkX = left + S(14);
            int checkY = top + (bottom - top - checkSize) / 2;

            if (row.IsSynced)
            {
                DrawSmoothCircle(g, checkX, checkY, checkSize, ToArgb(_theme.SuccessIcon));
                uint glyphCol = GetRelativeLuminance(_theme.SuccessIcon) > 0.55 ? ToArgb(Rgb(0, 0, 0)) : ToArgb(Rgb(255, 255, 255));
                DrawVectorCheckmark(
                    g,
                    checkX + checkSize / 2f,
                    checkY + checkSize / 2f,
                    checkSize,
                    glyphCol,
                    Math.Max(1.6f, Sf(1.8f)));
            }
            else if (row.IsSelected)
            {
                DrawSmoothRoundedCard(
                    g,
                    checkX,
                    checkY,
                    checkSize,
                    checkSize,
                    Sf(4f),
                    ToArgb(_theme.AccentDefault),
                    ToArgb(_theme.AccentDefault),
                    1f);
                DrawVectorCheckmark(
                    g,
                    checkX + checkSize / 2f,
                    checkY + checkSize / 2f,
                    checkSize,
                    ToArgb(_theme.TextOnAccent),
                    Math.Max(1.6f, Sf(1.8f)));
            }
            else
            {
                uint boxFill = _theme.IsHighContrast ? ToArgb(_theme.BgBase) : ToArgb(_theme.LayerDefault);
                uint boxStroke = isFocused
                    ? ToArgb(_theme.AccentDefault)
                    : ToArgb(_theme.TextTertiary);
                DrawSmoothRoundedCard(
                    g,
                    checkX,
                    checkY,
                    checkSize,
                    checkSize,
                    Sf(4f),
                    boxFill,
                    boxStroke,
                    Math.Max(1.2f, Sf(1.3f)));
            }

            uint brandColor = _theme.IsHighContrast
                ? rowTextCol
                : GetLauncherColor(game.Launcher, _theme.IsLight);

            // 2. App / Launcher Icon Squircle Tile (32x32)
            int tileSize = Math.Max(S(32), SF(28));
            int tileX = checkX + checkSize + S(10);
            int tileY = top + (bottom - top - tileSize) / 2;

            IntPtr hGameIcon = GetOrExtractGameIcon(game);
            if (hGameIcon != IntPtr.Zero)
            {
                uint neutralTileBg = _theme.IsHighContrast
                    ? ToArgb(_theme.TagFill)
                    : ToArgb(_theme.LayerDefault);
                uint neutralTileStroke = _theme.IsHighContrast
                    ? ToArgb(_theme.TagStroke)
                    : ToArgb(_theme.CardStroke);
                DrawSmoothRoundedCard(g, tileX, tileY, tileSize, tileSize, Sf(6f), neutralTileBg, neutralTileStroke, 1f);

                int icoSize = Math.Max(16, tileSize - S(8));
                int icoX = tileX + (tileSize - icoSize) / 2;
                int icoY = tileY + (tileSize - icoSize) / 2;
                DrawIconEx(hdc, icoX, icoY, hGameIcon, icoSize, icoSize, 0, IntPtr.Zero, DI_NORMAL);
            }
            else
            {
                uint tileBg = _theme.IsHighContrast
                    ? ToArgb(_theme.TagFill)
                    : ToArgb(brandColor, _theme.IsLight ? (byte)22 : (byte)36);
                uint tileStroke = _theme.IsHighContrast
                    ? ToArgb(_theme.TagStroke)
                    : ToArgb(brandColor, _theme.IsLight ? (byte)48 : (byte)68);
                DrawSmoothRoundedCard(g, tileX, tileY, tileSize, tileSize, Sf(6f), tileBg, tileStroke, 1f);
                DrawVectorGamepadGlyph(g, tileX + tileSize / 2f, tileY + tileSize / 2f, tileSize, ToArgb(brandColor));
            }

            // 3. Right-Aligned Launcher Pill Badge
            float dotSize = Math.Max(6f, Sf(6f));
            int launcherTextW = MeasureTextWidth(hdc, fontCaptionStrong, game.Launcher);
            int tagW = Math.Clamp(S(10) + (int)Math.Ceiling(dotSize) + S(6) + launcherTextW + S(12), S(76), S(148));
            int tagH = Math.Max(S(24), SF(20));
            int tagRight = right - S(12);
            int tagLeft = tagRight - tagW;
            int tagTop = top + (bottom - top - tagH) / 2;
            int tagBottom = tagTop + tagH;

            uint badgeFill = _theme.IsHighContrast
                ? ToArgb(_theme.TagFill)
                : ToArgb(brandColor, _theme.IsLight ? (byte)20 : (byte)34);
            uint badgeStroke = _theme.IsHighContrast
                ? ToArgb(_theme.TagStroke)
                : ToArgb(brandColor, _theme.IsLight ? (byte)52 : (byte)76);

            DrawSmoothRoundedCard(g, tagLeft, tagTop, tagW, tagH, tagH / 2f, badgeFill, badgeStroke, 1f);

            float dotX = tagLeft + Sf(10f);
            float dotY = tagTop + (tagH - dotSize) / 2f;
            DrawSmoothCircle(g, dotX, dotY, dotSize, ToArgb(brandColor));

            SelectObject(hdc, fontCaptionStrong);
            SetTextColor(hdc, _theme.IsHighContrast ? rowTextCol : brandColor);
            var rcTagText = new RECT
            {
                Left = (int)Math.Round(dotX + dotSize) + S(6),
                Top = tagTop,
                Right = tagRight - S(8),
                Bottom = tagBottom
            };
            DrawTextW(hdc, game.Launcher, -1, ref rcTagText, DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX);

            // 4. Sync Status Pill Badge (Sincronizado / Seleccionado / No sincronizado) to the left of Launcher Pill
            string statusText = row.IsSynced
                ? _strings.BadgeSynced
                : (row.IsSelected ? _strings.BadgeSelected : _strings.BadgeNotSynced);

            int statusTextW = MeasureTextWidth(hdc, fontCaptionStrong, statusText);
            int statusW = statusTextW + S(18);
            int statusRight = tagLeft - S(8);
            int statusLeft = statusRight - statusW;

            uint statusFill;
            uint statusStroke;
            uint statusTextCol;

            if (_theme.IsHighContrast)
            {
                statusFill = ToArgb(_theme.TagFill);
                statusStroke = ToArgb(_theme.TagStroke);
                statusTextCol = rowTextCol;
            }
            else if (row.IsSynced)
            {
                statusFill = ToArgb(_theme.SuccessBg);
                statusStroke = ToArgb(_theme.SuccessBorder);
                statusTextCol = _theme.SuccessIcon;
            }
            else if (row.IsSelected)
            {
                statusFill = ToArgb(_theme.AccentDefault, _theme.IsLight ? (byte)24 : (byte)40);
                statusStroke = ToArgb(_theme.AccentDefault, _theme.IsLight ? (byte)70 : (byte)96);
                statusTextCol = _theme.AccentDefault;
            }
            else
            {
                statusFill = ToArgb(_theme.TagFill);
                statusStroke = ToArgb(_theme.TagStroke);
                statusTextCol = _theme.TextSecondary;
            }

            DrawSmoothRoundedCard(g, statusLeft, tagTop, statusW, tagH, tagH / 2f, statusFill, statusStroke, 1f);

            SelectObject(hdc, fontCaptionStrong);
            SetTextColor(hdc, statusTextCol);
            var rcStatusText = new RECT
            {
                Left = statusLeft + S(6),
                Top = tagTop,
                Right = statusRight - S(6),
                Bottom = tagBottom
            };
            DrawTextW(hdc, statusText, -1, ref rcStatusText, DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);

            // 5. Vertically Centered Game Title
            SelectObject(hdc, fontBodyStrong);
            SetTextColor(hdc, rowTextCol);
            var rcGameTitle = new RECT
            {
                Left = tileX + tileSize + S(12),
                Top = top,
                Right = statusLeft - S(10),
                Bottom = bottom
            };
            DrawTextW(hdc, game.Title, -1, ref rcGameTitle, DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX);
        }

        private static void DrawFluentScrollbar(IntPtr g, int left, int top, int right, int bottom, int totalItems)
        {
            float w = Math.Max(3f, right - left);
            float trackH = bottom - top;
            if (trackH <= 8f)
                return;

            DrawSmoothRoundedCard(g, left, top, w, trackH, w / 2f, ToArgb(_theme.ScrollTrack, 120), 0);

            float thumbH = Math.Max(Sf(24f), (float)Math.Round(trackH * (_visibleRows / (double)totalItems)));
            int maxOffset = Math.Max(1, totalItems - _visibleRows);
            float thumbTop = top + (float)Math.Round((trackH - thumbH) * (_scrollOffset / (double)maxOffset));

            DrawSmoothRoundedCard(g, left, thumbTop, w, thumbH, w / 2f, ToArgb(_theme.ScrollThumb), 0);
        }

        private static void DrawFluentButton(
            IntPtr hdc,
            IntPtr g,
            RECT btnRect,
            string label,
            bool isPrimaryAccent,
            bool isHovered,
            bool isPressed,
            bool isFocused,
            IntPtr fontBodyStrong,
            InputGlyph glyph = InputGlyph.None,
            IntPtr fontBadge = default)
        {
            uint fillCol;
            uint borderCol;
            uint textCol;

            if (isPrimaryAccent)
            {
                fillCol = isPressed
                    ? _theme.AccentPressed
                    : (isHovered ? _theme.AccentHover : _theme.AccentDefault);
                borderCol = isPressed ? _theme.AccentPressed : _theme.AccentBorderTop;
                textCol = _theme.TextOnAccent;
            }
            else
            {
                fillCol = isPressed
                    ? _theme.LayerDefault
                    : (isHovered ? _theme.CardHover : _theme.CardDefault);
                borderCol = _theme.TagStroke;
                textCol = _theme.TextPrimary;
            }

            uint shadowCol = (_theme.IsHighContrast || isPressed)
                ? 0
                : ToArgb(Rgb(0, 0, 0), _theme.IsLight ? (byte)20 : (byte)40);

            DrawSmoothRoundedCard(
                g,
                btnRect.Left,
                btnRect.Top,
                btnRect.Width,
                btnRect.Height,
                Sf(6f),
                ToArgb(fillCol),
                ToArgb(borderCol),
                1f,
                shadowCol);

            // Outer 2px Focus Ring when navigated via Gamepad / Keyboard
            if (isFocused)
            {
                uint focusRingCol = _theme.IsHighContrast
                    ? ToArgb(_theme.TextPrimary)
                    : ToArgb(isPrimaryAccent ? _theme.TextPrimary : _theme.AccentDefault);
                float pad = Sf(2.5f);
                DrawSmoothRoundedCard(
                    g,
                    btnRect.Left - pad,
                    btnRect.Top - pad,
                    btnRect.Width + pad * 2f,
                    btnRect.Height + pad * 2f,
                    Sf(8f),
                    0,
                    focusRingCol,
                    Math.Max(1.6f, Sf(1.8f)));
            }

            int labelLeft = btnRect.Left + S(10);
            if (glyph != InputGlyph.None && fontBadge != IntPtr.Zero)
            {
                int badgeH = Math.Max(S(18), SF(17));
                int badgeLeft = btnRect.Left + S(10);
                int badgeCy = (btnRect.Top + btnRect.Bottom) / 2;
                int drawnW = DrawInputGlyphBadge(hdc, g, badgeLeft, badgeCy, badgeH, glyph, isPrimaryAccent, fontBadge);
                labelLeft = badgeLeft + drawnW + S(6);
            }

            SelectObject(hdc, fontBodyStrong);
            SetTextColor(hdc, textCol);
            var rcLabel = new RECT
            {
                Left = labelLeft,
                Top = btnRect.Top,
                Right = btnRect.Right - S(10),
                Bottom = btnRect.Bottom
            };
            DrawTextW(hdc, label, -1, ref rcLabel, DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);
        }

        private static IntPtr CreateFluentFont(int pixelHeight, int weight, string preferredFace)
        {
            IntPtr hFont = CreateFontW(
                pixelHeight, 0, 0, 0, weight,
                0, 0, 0, 1, 0, 0, 5, 0, preferredFace);

            if (hFont == IntPtr.Zero)
            {
                hFont = CreateFontW(
                    pixelHeight, 0, 0, 0, weight,
                    0, 0, 0, 1, 0, 0, 5, 0, "Segoe UI");
            }
            return hFont;
        }

        private static string StripLeadingSymbol(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            string trimmed = text.TrimStart('✔', '✖', '✓', '✕', ' ');
            return trimmed.Length > 0 ? trimmed : text;
        }

        private static uint GetLauncherColor(string launcher, bool isLightTheme)
        {
            if (launcher.Contains("Epic", StringComparison.OrdinalIgnoreCase))
                return isLightTheme ? Rgb(0, 102, 204) : Rgb(72, 168, 255);

            if (launcher.Contains("Ubisoft", StringComparison.OrdinalIgnoreCase))
                return isLightTheme ? Rgb(0, 95, 199) : Rgb(76, 166, 255);

            if (launcher.Contains("EA", StringComparison.OrdinalIgnoreCase) ||
                launcher.Contains("Origin", StringComparison.OrdinalIgnoreCase))
                return isLightTheme ? Rgb(209, 58, 36) : Rgb(255, 118, 92);

            if (launcher.Contains("Battle", StringComparison.OrdinalIgnoreCase))
                return isLightTheme ? Rgb(0, 116, 204) : Rgb(64, 196, 255);

            if (launcher.Contains("GOG", StringComparison.OrdinalIgnoreCase))
                return isLightTheme ? Rgb(126, 49, 168) : Rgb(186, 110, 235);

            if (launcher.Contains("Xbox", StringComparison.OrdinalIgnoreCase))
                return isLightTheme ? Rgb(16, 124, 16) : Rgb(56, 196, 56);

            return _theme.AccentDefault;
        }
    }
}
