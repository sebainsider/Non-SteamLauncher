using System.Globalization;
using System.Runtime.InteropServices;
using NonSteamLauncher.Sync.Steam;

namespace NonSteamLauncher.Sync.UI;

public static class SyncStatusDialog
{
    public static void Show(SteamSyncResult result)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            PrintConsoleFallback(result);
            return;
        }

        try
        {
            NativeDialogWindow.ShowDialog(result);
        }
        catch (Exception)
        {
            PrintConsoleFallback(result);
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
        private const string WindowClassName = "NonSteamLauncher_Fluent2SyncDialog_Class";

        // Logical Design Dimensions (96 DPI Base — 4px/8px Fluent 2 Grid, Couch-Gaming Ready)
        private const int BaseClientWidth = 600;
        private const int BaseClientHeight = 456;

        // Per-Monitor V2 DPI Awareness Contexts
        private static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new(-4);
        private static readonly IntPtr DPI_AWARENESS_CONTEXT_SYSTEM_AWARE = new(-2);

        // Win32 Window Styles
        private const uint WS_POPUP = 0x80000000;
        private const uint WS_CAPTION = 0x00C00000;
        private const uint WS_SYSMENU = 0x00080000;
        private const uint WS_MINIMIZEBOX = 0x00020000;
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
        private const uint WM_KEYDOWN = 0x0100;
        private const uint WM_TIMER = 0x0113;
        private const uint WM_MOUSEMOVE = 0x0200;
        private const uint WM_LBUTTONDOWN = 0x0201;
        private const uint WM_LBUTTONUP = 0x0202;
        private const uint WM_MOUSEWHEEL = 0x020A;
        private const uint WM_DPICHANGED = 0x02E0;
        private const uint WM_THEMECHANGED = 0x031A;
        private const uint WM_DWMCOLORIZATIONCOLORCHANGED = 0x0320;

        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;

        // Virtual Keys
        private const int VK_ESCAPE = 0x1B;
        private const int VK_RETURN = 0x0D;
        private const int VK_SPACE = 0x20;
        private const int VK_PRIOR = 0x21; // Page Up
        private const int VK_NEXT = 0x22;  // Page Down
        private const int VK_UP = 0x26;
        private const int VK_DOWN = 0x28;

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
        private const int PS_SOLID = 0;
        private const int PS_NULL = 5;

        // XInput Controller Constants
        private const ushort XINPUT_GAMEPAD_DPAD_UP = 0x0001;
        private const ushort XINPUT_GAMEPAD_DPAD_DOWN = 0x0002;
        private const ushort XINPUT_GAMEPAD_START = 0x0010;
        private const ushort XINPUT_GAMEPAD_BACK = 0x0020;
        private const ushort XINPUT_GAMEPAD_A = 0x1000;
        private const ushort XINPUT_GAMEPAD_B = 0x2000;
        private const ushort XINPUT_GAMEPAD_X = 0x4000;
        private const ushort XINPUT_GAMEPAD_Y = 0x8000;

        // Win32 RGB helpers: 0x00bbggrr
        private static uint Rgb(byte r, byte g, byte b) => (uint)(r | (g << 8) | (b << 16));
        private static byte GetR(uint col) => (byte)(col & 0xFF);
        private static byte GetG(uint col) => (byte)((col >> 8) & 0xFF);
        private static byte GetB(uint col) => (byte)((col >> 16) & 0xFF);

        private static uint BlendColor(uint fg, uint bg, double fgWeight)
        {
            byte r = (byte)Math.Clamp(Math.Round(GetR(fg) * fgWeight + GetR(bg) * (1.0 - fgWeight)), 0, 255);
            byte g = (byte)Math.Clamp(Math.Round(GetG(fg) * fgWeight + GetG(bg) * (1.0 - fgWeight)), 0, 255);
            byte b = (byte)Math.Clamp(Math.Round(GetB(fg) * fgWeight + GetB(bg) * (1.0 - fgWeight)), 0, 255);
            return Rgb(r, g, b);
        }

        private static double GetRelativeLuminance(uint col)
        {
            return (0.299 * GetR(col) + 0.587 * GetG(col) + 0.114 * GetB(col)) / 255.0;
        }

        // =====================================================================
        // Microsoft Fluent 2 Design System Adaptive Palette (System-Driven)
        // =====================================================================
        private readonly record struct Fluent2Palette(
            bool IsLight,
            bool IsHighContrast,
            uint BgBase,
            uint BgFooter,
            uint LayerDefault,
            uint CardDefault,
            uint CardHover,
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
        );

        // Interactive & System-Adaptive State
        private static Fluent2Palette _theme;
        private static uint _currentDpi = 96;
        private static double _dpiScale = 1.0;
        private static double _textScale = 1.0;
        private static RECT _btnCloseRect;
        private static RECT _listCardRect;
        private static int _visibleRows = 5;
        private static bool _hoverClose;
        private static bool _pressedClose;
        private static int _hoveredRowIndex = -1;
        private static int _scrollOffset;
        private static int _totalRows;
        private static int _gamepadScrollCooldown;
        private static SteamSyncResult? _activeResult;
        private static LocalizedStrings _strings = null!;
        private static GamepadType _gamepadType = GamepadType.Keyboard;
        private static bool _xinputFailed;
        private static WndProcDelegate? _wndProcHolder;

        // S() scales layout geometry by monitor DPI; SF() scales typography by monitor DPI * Accessibility TextScaleFactor
        private static int S(int value) => (int)Math.Round(value * _dpiScale);
        private static int SF(int fontPx) => (int)Math.Round(fontPx * _dpiScale * _textScale);

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

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct WNDCLASSEX
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

        [DllImport("user32.dll")]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

        [DllImport("user32.dll")]
        private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr CreateWindowEx(
            uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle,
            int x, int y, int nWidth, int nHeight,
            IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

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

        [DllImport("user32.dll")]
        private static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool UpdateWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern sbyte GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll")]
        private static extern bool TranslateMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        private static extern IntPtr DispatchMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        private static extern void PostQuitMessage(int nExitCode);

        [DllImport("user32.dll")]
        private static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr BeginPaint(IntPtr hWnd, out PAINTSTRUCT lpPaint);

        [DllImport("user32.dll")]
        private static extern bool EndPaint(IntPtr hWnd, ref PAINTSTRUCT lpPaint);

        [DllImport("user32.dll")]
        private static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

        [DllImport("user32.dll")]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        private static extern uint GetSysColor(int nIndex);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref HIGHCONTRAST pvParam, uint fWinIni);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref RECT pvParam, uint fWinIni);

        [DllImport("user32.dll")]
        private static extern IntPtr SetTimer(IntPtr hWnd, IntPtr nIDEvent, uint uElapse, IntPtr lpTimerFunc);

        [DllImport("user32.dll")]
        private static extern bool KillTimer(IntPtr hWnd, IntPtr uIDEvent);

        [DllImport("user32.dll")]
        private static extern int FillRect(IntPtr hDC, ref RECT lprc, IntPtr hbr);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        private static extern int RegGetValue(
            IntPtr hkey,
            string lpSubKey,
            string lpValue,
            uint dwFlags,
            out uint pdwType,
            out uint pvData,
            ref uint pcbData);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        private static extern int RegGetValue(
            IntPtr hkey,
            string lpSubKey,
            string lpValue,
            uint dwFlags,
            out uint pdwType,
            [Out] byte[] pvData,
            ref uint pcbData);

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
        private static extern IntPtr CreatePen(int fnPenStyle, int nWidth, uint crColor);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        private static extern int SetBkMode(IntPtr hdc, int iBkMode);

        [DllImport("gdi32.dll")]
        private static extern uint SetTextColor(IntPtr hdc, uint crColor);

        [DllImport("gdi32.dll")]
        private static extern bool RoundRect(IntPtr hdc, int left, int top, int right, int bottom, int width, int height);

        [DllImport("gdi32.dll")]
        private static extern bool Ellipse(IntPtr hdc, int left, int top, int right, int bottom);

        [DllImport("gdi32.dll")]
        private static extern bool MoveToEx(IntPtr hdc, int x, int y, IntPtr lpPoint);

        [DllImport("gdi32.dll")]
        private static extern bool LineTo(IntPtr hdc, int x, int y);

        [DllImport("gdi32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr CreateFont(
            int nHeight, int nWidth, int nEscapement, int nOrientation, int fnWeight,
            uint fdwItalic, uint fdwUnderline, uint fdwStrikeOut, uint fdwCharSet,
            uint fdwOutputPrecision, uint fdwClipPrecision, uint fdwQuality,
            uint fdwPitchAndFamily, string lpszFace);

        [DllImport("gdi32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetTextExtentPoint32(IntPtr hdc, string lpString, int c, out SIZE psizl);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int DrawText(IntPtr hDC, string lpchText, int cchText, ref RECT lprc, uint format);

        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
        private static extern uint XInputGetState14(uint dwUserIndex, out XINPUT_STATE pState);

        [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")]
        private static extern uint XInputGetState910(uint dwUserIndex, out XINPUT_STATE pState);

        // =====================================================================
        // High-DPI (100%–200%+) & Accessibility Text Scale Configuration
        // =====================================================================
        private static void EnsurePerMonitorDpiAwareness()
        {
            try
            {
                SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
            }
            catch
            {
                // Process DPI awareness may already be set; thread context below handles it
            }

            try
            {
                if (SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2) == IntPtr.Zero)
                {
                    SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_SYSTEM_AWARE);
                }
            }
            catch
            {
                // Ignore on older Windows builds
            }
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
                    // Clamp smoothly so text remains legible without breaking container geometry
                    return Math.Clamp(factor / 100.0, 1.0, 1.25);
                }
            }
            catch
            {
                // Default 1.0
            }

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
            catch
            {
                // Fallback to primary screen metrics
            }

            return new RECT
            {
                Left = 0,
                Top = 0,
                Right = GetSystemMetrics(0),
                Bottom = GetSystemMetrics(1)
            };
        }

        private static void CalculateOuterWindowBounds(uint dwStyle, uint dpi, out int posX, out int posY, out int winWidth, out int winHeight)
        {
            var rcWindow = new RECT
            {
                Left = 0,
                Top = 0,
                Right = S(BaseClientWidth),
                Bottom = S(BaseClientHeight)
            };

            bool adjusted = false;
            try
            {
                adjusted = AdjustWindowRectExForDpi(ref rcWindow, dwStyle, false, 0, dpi);
            }
            catch
            {
                // Fallback for older OS builds
            }

            if (!adjusted)
            {
                AdjustWindowRectEx(ref rcWindow, dwStyle, false, 0);
            }

            var workArea = GetMonitorWorkArea();
            int safeMargin = S(24);
            int maxAllowedW = Math.Max(S(420), workArea.Width - safeMargin * 2);
            int maxAllowedH = Math.Max(S(340), workArea.Height - safeMargin * 2);

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
            catch
            {
                // Ignore if SPI is unavailable
            }
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
                {
                    return appsUseLight != 0;
                }

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
                {
                    return systemUsesLight != 0;
                }
            }
            catch
            {
                // Fallback below
            }

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
                    int offset = isLight ? 4 * 4 : 1 * 4; // AccentDark1 (index 4) or AccentLight2 (index 1)
                    byte r = palette[offset];
                    byte g = palette[offset + 1];
                    byte b = palette[offset + 2];
                    if (r != 0 || g != 0 || b != 0)
                    {
                        return Rgb(r, g, b);
                    }
                }
            }
            catch
            {
                // Fallback to DWM AccentColor
            }

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
            catch
            {
                // Fallback to default Windows 11 Fluent 2 Blue
            }

            return isLight ? Rgb(0, 103, 192) : Rgb(96, 205, 255);
        }

        private static Fluent2Palette BuildDarkPalette(uint accentDefault)
        {
            uint bgFooter = Rgb(24, 24, 24);
            uint textOnAccent = GetRelativeLuminance(accentDefault) > 0.52 ? Rgb(0, 0, 0) : Rgb(255, 255, 255);
            uint accentHover = BlendColor(accentDefault, bgFooter, 0.90);
            uint accentPressed = BlendColor(accentDefault, bgFooter, 0.80);
            uint accentBorderTop = BlendColor(accentDefault, Rgb(255, 255, 255), 0.82);
            uint accentBadgeBg = BlendColor(accentDefault, textOnAccent, 0.80);

            return new Fluent2Palette(
                IsLight: false,
                IsHighContrast: false,
                BgBase: Rgb(32, 32, 32),            // SolidBackgroundFillColorBase (#202020)
                BgFooter: bgFooter,                 // SolidBackgroundFillColorSecondary (#181818)
                LayerDefault: Rgb(39, 39, 39),      // LayerFillColorDefault (#272727)
                CardDefault: Rgb(45, 45, 45),       // CardBackgroundFillColorDefault (#2D2D2D)
                CardHover: Rgb(54, 54, 54),         // CardBackgroundFillColorSecondary (#363636)
                TagFill: Rgb(56, 56, 56),           // Subtle Badge Surface (#383838)
                WindowBorder: Rgb(51, 51, 51),      // SurfaceStrokeColorDefault (#333333)
                Divider: Rgb(48, 48, 48),           // DividerStrokeColorDefault (#303030)
                CardStroke: Rgb(58, 58, 58),        // CardStrokeColorDefault (#3A3A3A)
                RowStroke: Rgb(62, 62, 62),         // Subtle Row Border (#3E3E3E)
                TagStroke: Rgb(72, 72, 72),         // ControlStrokeColorDefault (#484848)
                ScrollTrack: Rgb(46, 46, 46),       // Scrollbar Track (#2E2E2E)
                ScrollThumb: Rgb(110, 110, 110),    // Scrollbar Thumb (#6E6E6E)
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
                SuccessBorder: Rgb(45, 71, 45),
                InfoIcon: accentDefault,            // SystemFillColorAttention
                InfoBg: BlendColor(accentDefault, Rgb(32, 32, 32), 0.14),
                InfoBorder: BlendColor(accentDefault, Rgb(58, 58, 58), 0.28),
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
            uint accentBorderTop = BlendColor(accentDefault, Rgb(0, 0, 0), 0.88);
            uint accentBadgeBg = BlendColor(accentDefault, textOnAccent, 0.82);

            return new Fluent2Palette(
                IsLight: true,
                IsHighContrast: false,
                BgBase: Rgb(243, 243, 243),         // SolidBackgroundFillColorBase (#F3F3F3)
                BgFooter: bgFooter,                 // SolidBackgroundFillColorSecondary (#EEEEEE)
                LayerDefault: Rgb(249, 249, 249),   // LayerFillColorDefault (#F9F9F9)
                CardDefault: Rgb(255, 255, 255),    // CardBackgroundFillColorDefault (#FFFFFF)
                CardHover: Rgb(246, 246, 246),      // CardBackgroundFillColorSecondary (#F6F6F6)
                TagFill: Rgb(240, 240, 240),        // Subtle Badge Surface (#F0F0F0)
                WindowBorder: Rgb(229, 229, 229),   // SurfaceStrokeColorDefault (#E5E5E5)
                Divider: Rgb(229, 229, 229),        // DividerStrokeColorDefault (#E5E5E5)
                CardStroke: Rgb(229, 229, 229),     // CardStrokeColorDefault (#E5E5E5)
                RowStroke: Rgb(224, 224, 224),      // Subtle Row Border (#E0E0E0)
                TagStroke: Rgb(214, 214, 214),      // ControlStrokeColorDefault (#D6D6D6)
                ScrollTrack: Rgb(234, 234, 234),    // Scrollbar Track (#EAEAEA)
                ScrollThumb: Rgb(138, 138, 138),    // Scrollbar Thumb (#8A8A8A)
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
                SuccessBorder: Rgb(187, 229, 184),
                InfoIcon: accentDefault,            // SystemFillColorAttention
                InfoBg: BlendColor(accentDefault, Rgb(255, 255, 255), 0.10),
                InfoBorder: BlendColor(accentDefault, Rgb(229, 229, 229), 0.25),
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
                CardDefault: winBg,
                CardHover: highlight,
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

        public static void ShowDialog(SteamSyncResult result)
        {
            // Enable Per-Monitor DPI Awareness V2 before querying metrics or creating the window
            EnsurePerMonitorDpiAwareness();

            _activeResult = result;
            _hoverClose = false;
            _pressedClose = false;
            _hoveredRowIndex = -1;
            _scrollOffset = 0;
            _visibleRows = 5;
            _gamepadScrollCooldown = 0;
            _xinputFailed = false;
            _strings = Localization.GetStrings();
            _gamepadType = GamepadDetector.DetectConnectedGamepad();
            _theme = DetectSystemTheme();
            _textScale = QuerySystemTextScaleFactor();

            IReadOnlyList<InstalledGame> displayList = result.Success
                ? (result.AddedGames.Count > 0 ? result.AddedGames : result.ExistingGames)
                : Array.Empty<InstalledGame>();
            _totalRows = displayList.Count;

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

            uint dwStyle = WS_POPUP | WS_CAPTION | WS_SYSMENU | WS_MINIMIZEBOX | WS_VISIBLE;

            CalculateOuterWindowBounds(dwStyle, _currentDpi, out int posX, out int posY, out int winWidth, out int winHeight);

            IntPtr hInstance = Marshal.GetHINSTANCE(typeof(SyncStatusDialog).Module);
            _wndProcHolder = WndProc;

            var wndClass = new WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
                style = CS_HREDRAW | CS_VREDRAW,
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcHolder),
                hInstance = hInstance,
                hCursor = LoadCursor(IntPtr.Zero, 32512), // IDC_ARROW
                hbrBackground = IntPtr.Zero, // Handled completely via double-buffered WM_PAINT
                lpszClassName = WindowClassName
            };

            RegisterClassEx(ref wndClass);

            IntPtr hWnd = CreateWindowEx(
                0,
                WindowClassName,
                _strings.WindowTitle,
                dwStyle,
                posX, posY, winWidth, winHeight,
                IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero
            );

            if (hWnd == IntPtr.Zero)
                return;

            // Verify actual monitor DPI (e.g. secondary TV monitor at 150% or 200%) and re-adjust if different
            try
            {
                uint winDpi = GetDpiForWindow(hWnd);
                if (winDpi > 0 && winDpi != _currentDpi)
                {
                    _currentDpi = winDpi;
                    _dpiScale = winDpi / 96.0;
                    CalculateOuterWindowBounds(dwStyle, _currentDpi, out posX, out posY, out winWidth, out winHeight);
                    SetWindowPos(hWnd, IntPtr.Zero, posX, posY, winWidth, winHeight, SWP_NOZORDER | SWP_NOACTIVATE);
                }
            }
            catch
            {
                // Fallback to system DPI scale
            }

            // Apply adaptive Windows 11 DWM Fluent 2 Attributes (Light, Dark, Accent & High Contrast)
            ApplyWindowThemeAttributes(hWnd, _theme);

            // Poll Gamepad at ~30 FPS
            SetTimer(hWnd, (IntPtr)1, 33, IntPtr.Zero);

            ShowWindow(hWnd, 1);
            UpdateWindow(hWnd);

            while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
        }

        private static IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            switch (msg)
            {
                case WM_ERASEBKGND:
                    // Suppress background erase to prevent flicker; double buffer handles full client paint
                    return (IntPtr)1;

                case WM_SETTINGCHANGE:
                case WM_SYSCOLORCHANGE:
                case WM_THEMECHANGED:
                case WM_DWMCOLORIZATIONCOLORCHANGED:
                    {
                        _strings = Localization.GetStrings();
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
                        int scrollDelta = PollGamepad(out bool shouldClose);
                        if (shouldClose)
                        {
                            KillTimer(hWnd, (IntPtr)1);
                            DestroyWindow(hWnd);
                            return IntPtr.Zero;
                        }
                        if (scrollDelta != 0 && ScrollBy(scrollDelta))
                        {
                            InvalidateRect(hWnd, IntPtr.Zero, false);
                        }
                        return IntPtr.Zero;
                    }

                case WM_MOUSEMOVE:
                    {
                        int x = (short)(lParam.ToInt64() & 0xFFFF);
                        int y = (short)((lParam.ToInt64() >> 16) & 0xFFFF);

                        bool newHovClose = _btnCloseRect.Contains(x, y);
                        int newHovRow = HitTestRow(x, y);

                        if (newHovClose != _hoverClose || newHovRow != _hoveredRowIndex)
                        {
                            _hoverClose = newHovClose;
                            _hoveredRowIndex = newHovRow;
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

                        if (_btnCloseRect.Contains(x, y))
                        {
                            _pressedClose = true;
                            InvalidateRect(hWnd, IntPtr.Zero, false);
                        }
                        return IntPtr.Zero;
                    }

                case WM_LBUTTONUP:
                    {
                        int x = (short)(lParam.ToInt64() & 0xFFFF);
                        int y = (short)((lParam.ToInt64() >> 16) & 0xFFFF);

                        bool wasPressed = _pressedClose;
                        _pressedClose = false;

                        if (_btnCloseRect.Contains(x, y) || wasPressed && _btnCloseRect.Contains(x, y))
                        {
                            KillTimer(hWnd, (IntPtr)1);
                            DestroyWindow(hWnd);
                            return IntPtr.Zero;
                        }

                        if (wasPressed)
                        {
                            InvalidateRect(hWnd, IntPtr.Zero, false);
                        }
                        return IntPtr.Zero;
                    }

                case WM_MOUSEWHEEL:
                    {
                        short wheelDelta = (short)((wParam.ToInt64() >> 16) & 0xFFFF);
                        int steps = wheelDelta > 0 ? -1 : 1;
                        if (ScrollBy(steps))
                        {
                            InvalidateRect(hWnd, IntPtr.Zero, false);
                        }
                        return IntPtr.Zero;
                    }

                case WM_KEYDOWN:
                    {
                        int key = wParam.ToInt32();
                        if (key == VK_ESCAPE || key == VK_RETURN || key == VK_SPACE)
                        {
                            KillTimer(hWnd, (IntPtr)1);
                            DestroyWindow(hWnd);
                            return IntPtr.Zero;
                        }
                        if (key == VK_UP && ScrollBy(-1))
                        {
                            InvalidateRect(hWnd, IntPtr.Zero, false);
                        }
                        else if (key == VK_DOWN && ScrollBy(1))
                        {
                            InvalidateRect(hWnd, IntPtr.Zero, false);
                        }
                        else if (key == VK_PRIOR && ScrollBy(-_visibleRows))
                        {
                            InvalidateRect(hWnd, IntPtr.Zero, false);
                        }
                        else if (key == VK_NEXT && ScrollBy(_visibleRows))
                        {
                            InvalidateRect(hWnd, IntPtr.Zero, false);
                        }
                        return IntPtr.Zero;
                    }

                case WM_CLOSE:
                    KillTimer(hWnd, (IntPtr)1);
                    DestroyWindow(hWnd);
                    return IntPtr.Zero;

                case WM_DESTROY:
                    KillTimer(hWnd, (IntPtr)1);
                    PostQuitMessage(0);
                    return IntPtr.Zero;

                default:
                    return DefWindowProc(hWnd, msg, wParam, lParam);
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

        private static int GetRowHeight() => (int)Math.Round(S(38) * Math.Max(1.0, (_textScale - 1.0) * 0.6 + 1.0));

        private static int HitTestRow(int x, int y)
        {
            if (!_listCardRect.Contains(x, y) || _totalRows == 0)
                return -1;

            int pad = S(8);
            int rowH = GetRowHeight();
            int rowGap = S(4);
            int startY = _listCardRect.Top + pad;

            int relY = y - startY;
            if (relY < 0)
                return -1;

            int slot = relY / (rowH + rowGap);
            int inSlotY = relY % (rowH + rowGap);
            if (slot < 0 || slot >= _visibleRows || inSlotY > rowH)
                return -1;

            int actualIndex = _scrollOffset + slot;
            return actualIndex < _totalRows ? actualIndex : -1;
        }

        private static int PollGamepad(out bool shouldClose)
        {
            shouldClose = false;
            if (_xinputFailed)
                return 0;

            if (_gamepadScrollCooldown > 0)
                _gamepadScrollCooldown--;

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
                        return 0;
                    }
                }
                catch
                {
                    _xinputFailed = true;
                    return 0;
                }

                if (err == 0)
                {
                    ushort b = state.Gamepad.wButtons;
                    const ushort anyCloseButton = XINPUT_GAMEPAD_A | XINPUT_GAMEPAD_B | XINPUT_GAMEPAD_START | XINPUT_GAMEPAD_BACK | XINPUT_GAMEPAD_X | XINPUT_GAMEPAD_Y;

                    if ((b & anyCloseButton) != 0)
                    {
                        shouldClose = true;
                        return 0;
                    }

                    if (_gamepadScrollCooldown == 0)
                    {
                        bool up = (b & XINPUT_GAMEPAD_DPAD_UP) != 0 || state.Gamepad.sThumbLY > 16000;
                        bool down = (b & XINPUT_GAMEPAD_DPAD_DOWN) != 0 || state.Gamepad.sThumbLY < -16000;
                        if (up)
                        {
                            _gamepadScrollCooldown = 5;
                            return -1;
                        }
                        if (down)
                        {
                            _gamepadScrollCooldown = 5;
                            return 1;
                        }
                    }
                }
            }

            return 0;
        }

        private static int MeasureTextWidth(IntPtr hdc, IntPtr font, string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            IntPtr oldFont = SelectObject(hdc, font);
            bool ok = GetTextExtentPoint32(hdc, text, text.Length, out SIZE size);
            SelectObject(hdc, oldFont);
            return ok ? size.cx : text.Length * S(8);
        }

        private static void PaintFluent2Content(IntPtr hdc, int clientW, int clientH, SteamSyncResult? result)
        {
            SetBkMode(hdc, TRANSPARENT);

            int marginX = S(24);
            int footerHeight = S(68);
            int footerTop = Math.Max(S(220), clientH - footerHeight);

            // 1. Base Layer & ContentDialog Command Space (Footer) Surfaces
            var rcBody = new RECT { Left = 0, Top = 0, Right = clientW, Bottom = footerTop };
            IntPtr hBodyBrush = CreateSolidBrush(_theme.BgBase);
            FillRect(hdc, ref rcBody, hBodyBrush);
            DeleteObject(hBodyBrush);

            var rcFooter = new RECT { Left = 0, Top = footerTop, Right = clientW, Bottom = clientH };
            IntPtr hFooterBrush = CreateSolidBrush(_theme.BgFooter);
            FillRect(hdc, ref rcFooter, hFooterBrush);
            DeleteObject(hFooterBrush);

            // 1px Fluent 2 Footer Divider Stroke
            IntPtr hDivPen = CreatePen(PS_SOLID, Math.Max(1, (int)Math.Round(_dpiScale * 0.75)), _theme.Divider);
            IntPtr oldPen = SelectObject(hdc, hDivPen);
            MoveToEx(hdc, 0, footerTop, IntPtr.Zero);
            LineTo(hdc, clientW, footerTop);
            SelectObject(hdc, oldPen);
            DeleteObject(hDivPen);

            // 2. Fluent 2 Type Ramp Fonts (Scaled by DPI and Accessibility TextScaleFactor)
            IntPtr fontTitle = CreateFluentFont(-SF(20), 600, "Segoe UI Variable Display");
            IntPtr fontBodyStrong = CreateFluentFont(-SF(14), 600, "Segoe UI Variable Text");
            IntPtr fontBody = CreateFluentFont(-SF(14), 400, "Segoe UI Variable Text");
            IntPtr fontCaptionStrong = CreateFluentFont(-SF(12), 600, "Segoe UI Variable Text");
            IntPtr fontCaption = CreateFluentFont(-SF(12), 400, "Segoe UI Variable Text");

            // 3. Dialog Header (Subtitle Ramp: 20px Semibold)
            int titleTop = S(16);
            int titleHeight = Math.Max(S(30), SF(28));
            SelectObject(hdc, fontTitle);
            SetTextColor(hdc, _theme.TextPrimary);
            var rcTitle = new RECT { Left = marginX, Top = titleTop, Right = clientW - marginX, Bottom = titleTop + titleHeight };
            DrawText(hdc, "Non-Steam Launcher", -1, ref rcTitle, DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);

            // 4. Fluent 2 InfoBar (Status Banner)
            int infoBarTop = rcTitle.Bottom + S(8);
            int infoBarHeight = Math.Max(S(44), SF(36));
            int infoBarBottom = infoBarTop + infoBarHeight;
            DrawFluentInfoBar(hdc, marginX, infoBarTop, clientW - marginX, infoBarBottom, result, fontBodyStrong, fontCaptionStrong);

            // 5. Section Header & Item Count Badge
            IReadOnlyList<InstalledGame> displayList = result != null && result.Success
                ? (result.AddedGames.Count > 0 ? result.AddedGames : result.ExistingGames)
                : Array.Empty<InstalledGame>();

            string rawHeader = result != null && result.Success && result.AddedGames.Count > 0
                ? _strings.CardHeaderAdded
                : _strings.CardHeaderCurrent;

            string sectionHeader = FormatFluentSectionLabel(rawHeader);
            string countText = $"{displayList.Count}";
            int countWidth = MeasureTextWidth(hdc, fontCaption, countText) + S(12);

            int secTop = infoBarBottom + S(12);
            int secHeight = Math.Max(S(20), SF(18));
            int secBottom = secTop + secHeight;

            SelectObject(hdc, fontCaptionStrong);
            SetTextColor(hdc, _theme.TextSecondary);
            var rcSecLabel = new RECT
            {
                Left = marginX + S(2),
                Top = secTop,
                Right = clientW - marginX - countWidth - S(8),
                Bottom = secBottom
            };
            DrawText(hdc, sectionHeader, -1, ref rcSecLabel, DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX);

            if (result != null && result.Success)
            {
                SelectObject(hdc, fontCaption);
                SetTextColor(hdc, _theme.TextTertiary);
                var rcSecCount = new RECT
                {
                    Left = clientW - marginX - countWidth,
                    Top = secTop,
                    Right = clientW - marginX - S(2),
                    Bottom = secBottom
                };
                DrawText(hdc, countText, -1, ref rcSecCount, DT_RIGHT | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);
            }

            // 6. Fluent 2 Elevated Card List Container (Dynamically sized to fit between section label and footer)
            _listCardRect = new RECT
            {
                Left = marginX,
                Top = secBottom + S(6),
                Right = clientW - marginX,
                Bottom = footerTop - S(16)
            };

            DrawRoundedBox(hdc, _listCardRect.Left, _listCardRect.Top, _listCardRect.Right, _listCardRect.Bottom, S(8), _theme.LayerDefault, _theme.CardStroke);

            if (result != null && result.Success)
            {
                if (displayList.Count == 0)
                {
                    SelectObject(hdc, fontBody);
                    SetTextColor(hdc, _theme.TextTertiary);
                    var rcEmpty = new RECT
                    {
                        Left = _listCardRect.Left + S(16),
                        Top = _listCardRect.Top + S(16),
                        Right = _listCardRect.Right - S(16),
                        Bottom = _listCardRect.Bottom - S(16)
                    };
                    DrawText(hdc, _strings.EmptyMessage, -1, ref rcEmpty, DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX);
                }
                else
                {
                    int pad = S(8);
                    int rowH = GetRowHeight();
                    int rowGap = S(4);
                    int availH = Math.Max(rowH, _listCardRect.Height - pad * 2);

                    // Dynamically calculate how many full rows fit at the current DPI / TextScale
                    _visibleRows = Math.Max(1, (availH + rowGap) / (rowH + rowGap));
                    int maxOffset = Math.Max(0, displayList.Count - _visibleRows);
                    if (_scrollOffset > maxOffset)
                        _scrollOffset = maxOffset;

                    bool hasScroll = displayList.Count > _visibleRows;
                    int rightInner = _listCardRect.Right - pad - (hasScroll ? S(12) : 0);

                    int visibleCount = Math.Min(_visibleRows, displayList.Count - _scrollOffset);
                    int rowY = _listCardRect.Top + pad;

                    for (int i = 0; i < visibleCount; i++)
                    {
                        int itemIdx = _scrollOffset + i;
                        var game = displayList[itemIdx];
                        bool isHovered = itemIdx == _hoveredRowIndex;

                        DrawGameRow(
                            hdc,
                            _listCardRect.Left + pad,
                            rowY,
                            rightInner,
                            rowY + rowH,
                            game,
                            isHovered,
                            fontBodyStrong,
                            fontCaptionStrong);

                        rowY += rowH + rowGap;
                    }

                    if (hasScroll)
                    {
                        DrawFluentScrollbar(
                            hdc,
                            _listCardRect.Right - S(10),
                            _listCardRect.Top + pad,
                            _listCardRect.Right - S(6),
                            _listCardRect.Bottom - pad,
                            displayList.Count);
                    }
                }
            }
            else
            {
                SelectObject(hdc, fontBody);
                SetTextColor(hdc, _theme.CriticalIcon);
                var rcErr = new RECT
                {
                    Left = _listCardRect.Left + S(16),
                    Top = _listCardRect.Top + S(16),
                    Right = _listCardRect.Right - S(16),
                    Bottom = _listCardRect.Bottom - S(16)
                };
                DrawText(hdc, result?.ErrorMessage ?? _strings.HeaderError, -1, ref rcErr, DT_LEFT | DT_WORDBREAK | DT_NOPREFIX);
            }

            // 7. Fluent 2 Primary Accent Button in Command Space (Dynamically measured for DPI & Localization)
            string glyphText = GetInputGlyphText(out bool isCircleGlyph);
            int badgeH = Math.Max(S(20), SF(18));
            int badgeW = isCircleGlyph ? badgeH : Math.Max(S(28), MeasureTextWidth(hdc, fontCaptionStrong, glyphText) + S(12));
            int actionTextW = MeasureTextWidth(hdc, fontBodyStrong, _strings.ActionClose);

            int btnW = Math.Max(S(148), S(14) + badgeW + S(10) + actionTextW + S(20));
            int btnH = Math.Max(S(36), SF(32));
            int btnTop = footerTop + (footerHeight - btnH) / 2;
            _btnCloseRect = new RECT
            {
                Left = clientW - marginX - btnW,
                Top = btnTop,
                Right = clientW - marginX,
                Bottom = btnTop + btnH
            };

            // 8. Footer Left Caption (Fits cleanly to the left of the dynamically-sized button)
            SelectObject(hdc, fontCaption);
            SetTextColor(hdc, _theme.TextTertiary);
            var rcFooterLeft = new RECT
            {
                Left = marginX,
                Top = footerTop,
                Right = _btnCloseRect.Left - S(16),
                Bottom = clientH
            };
            string footerText = displayList.Count > _visibleRows
                ? string.Format(_strings.MoreGamesFormat, displayList.Count - _visibleRows)
                : _strings.WindowTitle;
            DrawText(hdc, footerText, -1, ref rcFooterLeft, DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX);

            DrawFluentPrimaryButton(hdc, _btnCloseRect, glyphText, isCircleGlyph, badgeW, badgeH, fontBodyStrong, fontCaptionStrong);

            // Cleanup GDI Fonts
            DeleteObject(fontTitle);
            DeleteObject(fontBodyStrong);
            DeleteObject(fontBody);
            DeleteObject(fontCaptionStrong);
            DeleteObject(fontCaption);
        }

        private static void DrawFluentInfoBar(
            IntPtr hdc,
            int left,
            int top,
            int right,
            int bottom,
            SteamSyncResult? result,
            IntPtr fontBodyStrong,
            IntPtr fontCaptionStrong)
        {
            uint bgCol;
            uint borderCol;
            uint iconCol;
            string badgeSymbol;
            string statusMessage;

            if (result != null && result.Success)
            {
                if (result.AddedGames.Count > 0)
                {
                    bgCol = _theme.SuccessBg;
                    borderCol = _theme.SuccessBorder;
                    iconCol = _theme.SuccessIcon;
                    badgeSymbol = "✓";
                    statusMessage = $"{StripLeadingSymbol(_strings.HeaderSuccess)} ({result.AddedGames.Count})";
                }
                else
                {
                    bgCol = _theme.InfoBg;
                    borderCol = _theme.InfoBorder;
                    iconCol = _theme.InfoIcon;
                    badgeSymbol = "✓";
                    statusMessage = StripLeadingSymbol(_strings.HeaderUpToDate);
                }
            }
            else
            {
                bgCol = _theme.CriticalBg;
                borderCol = _theme.CriticalBorder;
                iconCol = _theme.CriticalIcon;
                badgeSymbol = "✕";
                statusMessage = StripLeadingSymbol(_strings.HeaderError);
            }

            DrawRoundedBox(hdc, left, top, right, bottom, S(6), bgCol, borderCol);

            // Status Icon Circle Badge (Fluent 2 InfoBar Icon anatomy)
            int iconSize = Math.Max(S(20), SF(18));
            int iconX = left + S(14);
            int iconY = top + (bottom - top - iconSize) / 2;

            IntPtr hIconBrush = CreateSolidBrush(iconCol);
            IntPtr hIconPen = CreatePen(PS_SOLID, 1, iconCol);
            SelectObject(hdc, hIconBrush);
            SelectObject(hdc, hIconPen);
            Ellipse(hdc, iconX, iconY, iconX + iconSize, iconY + iconSize);
            DeleteObject(hIconBrush);
            DeleteObject(hIconPen);

            uint badgeTextCol = GetRelativeLuminance(iconCol) > 0.55 ? Rgb(0, 0, 0) : Rgb(255, 255, 255);
            SelectObject(hdc, fontCaptionStrong);
            SetTextColor(hdc, badgeTextCol);
            var rcIcon = new RECT { Left = iconX, Top = iconY, Right = iconX + iconSize, Bottom = iconY + iconSize };
            DrawText(hdc, badgeSymbol, -1, ref rcIcon, DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);

            // Status Text
            SelectObject(hdc, fontBodyStrong);
            SetTextColor(hdc, _theme.TextPrimary);
            var rcMsg = new RECT { Left = iconX + iconSize + S(12), Top = top, Right = right - S(14), Bottom = bottom };
            DrawText(hdc, statusMessage, -1, ref rcMsg, DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX);
        }

        private static void DrawGameRow(
            IntPtr hdc,
            int left,
            int top,
            int right,
            int bottom,
            InstalledGame game,
            bool isHovered,
            IntPtr fontBodyStrong,
            IntPtr fontCaptionStrong)
        {
            uint rowBg = isHovered ? _theme.CardHover : _theme.CardDefault;
            uint rowTextCol = (_theme.IsHighContrast && isHovered) ? _theme.TextOnAccent : _theme.TextPrimary;
            DrawRoundedBox(hdc, left, top, right, bottom, S(6), rowBg, _theme.RowStroke);

            uint brandColor = _theme.IsHighContrast
                ? rowTextCol
                : GetLauncherColor(game.Launcher, _theme.IsLight);

            // Left Fluent 2 Accent Indicator Pill
            int pillW = Math.Max(3, S(3));
            int pillLeft = left + S(10);
            int pillTop = top + S(9);
            int pillRight = pillLeft + pillW;
            int pillBottom = bottom - S(9);
            DrawRoundedBox(hdc, pillLeft, pillTop, pillRight, pillBottom, Math.Max(2, S(2)), brandColor, brandColor);

            // Dynamically measured Right-aligned Subtle Launcher Tag Pill
            int dotSize = Math.Max(6, S(6));
            int launcherTextW = MeasureTextWidth(hdc, fontCaptionStrong, game.Launcher);
            int tagW = Math.Clamp(S(8) + dotSize + S(6) + launcherTextW + S(10), S(78), S(148));
            int tagH = Math.Max(S(22), SF(20));
            int tagRight = right - S(10);
            int tagLeft = tagRight - tagW;
            int tagTop = top + (bottom - top - tagH) / 2;
            int tagBottom = tagTop + tagH;

            DrawRoundedBox(hdc, tagLeft, tagTop, tagRight, tagBottom, S(4), _theme.TagFill, _theme.TagStroke);

            // Brand dot inside Launcher Tag
            int dotX = tagLeft + S(8);
            int dotY = tagTop + (tagH - dotSize) / 2;
            IntPtr hDotBrush = CreateSolidBrush(brandColor);
            IntPtr hDotPen = CreatePen(PS_SOLID, 1, brandColor);
            SelectObject(hdc, hDotBrush);
            SelectObject(hdc, hDotPen);
            Ellipse(hdc, dotX, dotY, dotX + dotSize, dotY + dotSize);
            DeleteObject(hDotBrush);
            DeleteObject(hDotPen);

            SelectObject(hdc, fontCaptionStrong);
            SetTextColor(hdc, _theme.TextSecondary);
            var rcTagText = new RECT { Left = dotX + dotSize + S(6), Top = tagTop, Right = tagRight - S(6), Bottom = tagBottom };
            DrawText(hdc, game.Launcher, -1, ref rcTagText, DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX);

            // Game Title (Primary Hierarchy on the Left)
            SelectObject(hdc, fontBodyStrong);
            SetTextColor(hdc, rowTextCol);
            var rcGameTitle = new RECT
            {
                Left = pillRight + S(10),
                Top = top,
                Right = tagLeft - S(12),
                Bottom = bottom
            };
            DrawText(hdc, game.Title, -1, ref rcGameTitle, DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX);
        }

        private static void DrawFluentScrollbar(IntPtr hdc, int left, int top, int right, int bottom, int totalItems)
        {
            DrawRoundedBox(hdc, left, top, right, bottom, S(2), _theme.ScrollTrack, _theme.ScrollTrack);

            int trackH = bottom - top;
            int thumbH = Math.Max(S(24), (int)Math.Round(trackH * (_visibleRows / (double)totalItems)));
            int maxOffset = Math.Max(1, totalItems - _visibleRows);
            int thumbTop = top + (int)Math.Round((trackH - thumbH) * (_scrollOffset / (double)maxOffset));

            DrawRoundedBox(hdc, left, thumbTop, right, thumbTop + thumbH, S(2), _theme.ScrollThumb, _theme.ScrollThumb);
        }

        private static string GetInputGlyphText(out bool isCircle)
        {
            isCircle = _gamepadType != GamepadType.Keyboard;
            return _gamepadType switch
            {
                GamepadType.PlayStation => "○",
                GamepadType.Nintendo => "B",
                GamepadType.Xbox => "B",
                _ => "Esc"
            };
        }

        private static void DrawFluentPrimaryButton(
            IntPtr hdc,
            RECT btnRect,
            string glyphText,
            bool isCircle,
            int badgeW,
            int badgeH,
            IntPtr fontBodyStrong,
            IntPtr fontCaptionStrong)
        {
            uint fillCol = _pressedClose
                ? _theme.AccentPressed
                : (_hoverClose ? _theme.AccentHover : _theme.AccentDefault);

            uint borderCol = _pressedClose ? _theme.AccentPressed : _theme.AccentBorderTop;

            DrawRoundedBox(hdc, btnRect.Left, btnRect.Top, btnRect.Right, btnRect.Bottom, S(6), fillCol, borderCol);

            int badgeX = btnRect.Left + S(12);
            int badgeY = btnRect.Top + (btnRect.Height - badgeH) / 2;

            if (isCircle)
            {
                IntPtr hBrush = CreateSolidBrush(_theme.AccentBadgeBg);
                IntPtr hPen = CreatePen(PS_SOLID, 1, _theme.AccentBadgeBg);
                SelectObject(hdc, hBrush);
                SelectObject(hdc, hPen);
                Ellipse(hdc, badgeX, badgeY, badgeX + badgeW, badgeY + badgeH);
                DeleteObject(hBrush);
                DeleteObject(hPen);
            }
            else
            {
                DrawRoundedBox(hdc, badgeX, badgeY, badgeX + badgeW, badgeY + badgeH, S(4), _theme.AccentBadgeBg, _theme.AccentBadgeBg);
            }

            SelectObject(hdc, fontCaptionStrong);
            SetTextColor(hdc, _theme.TextOnAccent);
            var rcGlyph = new RECT { Left = badgeX, Top = badgeY, Right = badgeX + badgeW, Bottom = badgeY + badgeH };
            DrawText(hdc, glyphText, -1, ref rcGlyph, DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);

            // Button Action Text
            SelectObject(hdc, fontBodyStrong);
            SetTextColor(hdc, _theme.TextOnAccent);
            var rcLabel = new RECT
            {
                Left = badgeX + badgeW + S(8),
                Top = btnRect.Top,
                Right = btnRect.Right - S(12),
                Bottom = btnRect.Bottom
            };
            DrawText(hdc, _strings.ActionClose, -1, ref rcLabel, DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);
        }

        private static void DrawRoundedBox(IntPtr hdc, int left, int top, int right, int bottom, int radius, uint fillColor, uint strokeColor)
        {
            IntPtr hBrush = CreateSolidBrush(fillColor);
            IntPtr hPen = strokeColor == fillColor
                ? CreatePen(PS_NULL, 0, 0)
                : CreatePen(PS_SOLID, 1, strokeColor);

            IntPtr oldBrush = SelectObject(hdc, hBrush);
            IntPtr oldPen = SelectObject(hdc, hPen);

            int pad = strokeColor == fillColor ? 1 : 0;
            RoundRect(hdc, left, top, right + pad, bottom + pad, radius * 2, radius * 2);

            SelectObject(hdc, oldBrush);
            SelectObject(hdc, oldPen);
            DeleteObject(hBrush);
            DeleteObject(hPen);
        }

        private static IntPtr CreateFluentFont(int pixelHeight, int weight, string preferredFace)
        {
            IntPtr hFont = CreateFont(
                pixelHeight, 0, 0, 0, weight,
                0, 0, 0, 1, 0, 0, 5, 0, preferredFace);

            if (hFont == IntPtr.Zero)
            {
                hFont = CreateFont(
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

        private static string FormatFluentSectionLabel(string rawHeader)
        {
            if (string.IsNullOrWhiteSpace(rawHeader))
                return string.Empty;

            // Convert legacy ALL-CAPS headers into Fluent 2 Sentence case
            string clean = rawHeader.Trim().TrimEnd(':').Trim();
            if (clean.Length <= 1)
                return clean;

            string lower = clean.ToLower(CultureInfo.CurrentUICulture);
            return char.ToUpper(lower[0], CultureInfo.CurrentUICulture) + lower[1..];
        }

        private static uint GetLauncherColor(string launcher, bool isLightTheme)
        {
            if (launcher.Contains("Epic", StringComparison.OrdinalIgnoreCase))
                return isLightTheme ? Rgb(0, 102, 204) : Rgb(56, 152, 255);

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
                return isLightTheme ? Rgb(16, 124, 16) : Rgb(46, 184, 46);

            return _theme.AccentDefault;
        }
    }
}
