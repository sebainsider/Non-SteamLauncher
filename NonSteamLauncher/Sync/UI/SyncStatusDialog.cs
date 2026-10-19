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
        private const string WindowClassName = "NonSteamLauncher_SyncDialog_Class";

        // Win32 Constants
        private const uint WS_POPUP = 0x80000000;
        private const uint WS_CAPTION = 0x00C00000;
        private const uint WS_SYSMENU = 0x00080000;
        private const uint WS_MINIMIZEBOX = 0x00020000;
        private const uint WS_VISIBLE = 0x10000000;

        private const uint CS_HREDRAW = 0x0002;
        private const uint CS_VREDRAW = 0x0001;

        private const uint WM_DESTROY = 0x0002;
        private const uint WM_PAINT = 0x000F;
        private const uint WM_CLOSE = 0x0010;
        private const uint WM_KEYDOWN = 0x0100;
        private const uint WM_MOUSEMOVE = 0x0200;
        private const uint WM_LBUTTONDOWN = 0x0201;
        private const uint WM_LBUTTONUP = 0x0202;
        private const uint WM_TIMER = 0x0113;

        private const int VK_ESCAPE = 0x1B;
        private const int VK_RETURN = 0x0D;
        private const int VK_SPACE = 0x20;

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;

        private const int DT_LEFT = 0x00000000;
        private const int DT_CENTER = 0x00000001;
        private const int DT_VCENTER = 0x00000004;
        private const int DT_SINGLELINE = 0x00000020;
        private const int DT_WORD_ELLIPSIS = 0x00040000;

        private const int TRANSPARENT = 1;

        // XInput Controller Constants
        private const ushort XINPUT_GAMEPAD_START = 0x0010;
        private const ushort XINPUT_GAMEPAD_BACK = 0x0020;
        private const ushort XINPUT_GAMEPAD_A = 0x1000;
        private const ushort XINPUT_GAMEPAD_B = 0x2000;
        private const ushort XINPUT_GAMEPAD_X = 0x4000;
        private const ushort XINPUT_GAMEPAD_Y = 0x8000;

        // Button geometry (Single Steam-styled close button)
        private static readonly RECT BtnClose = new() { Left = 390, Top = 390, Right = 530, Bottom = 428 };

        private static bool _hoverClose;
        private static SteamSyncResult? _activeResult;
        private static LocalizedStrings _strings = null!;
        private static GamepadType _gamepadType = GamepadType.Keyboard;
        private static bool _xinputFailed;

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

            public bool Contains(int x, int y) => x >= Left && x <= Right && y >= Top && y <= Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
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

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr CreateWindowEx(
            uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle,
            int x, int y, int nWidth, int nHeight,
            IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

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
        private static extern IntPtr SetTimer(IntPtr hWnd, IntPtr nIDEvent, uint uElapse, IntPtr lpTimerFunc);

        [DllImport("user32.dll")]
        private static extern bool KillTimer(IntPtr hWnd, IntPtr uIDEvent);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

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

        [DllImport("gdi32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr CreateFont(
            int nHeight, int nWidth, int nEscapement, int nOrientation, int fnWeight,
            uint fdwItalic, uint fdwUnderline, uint fdwStrikeOut, uint fdwCharSet,
            uint fdwOutputPrecision, uint fdwClipPrecision, uint fdwQuality,
            uint fdwPitchAndFamily, string lpszFace);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int DrawText(IntPtr hDC, string lpchText, int cchText, ref RECT lprc, uint format);

        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
        private static extern uint XInputGetState14(uint dwUserIndex, out XINPUT_STATE pState);

        [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")]
        private static extern uint XInputGetState910(uint dwUserIndex, out XINPUT_STATE pState);

        // Win32 RGB color helper: 0x00bbggrr
        private static uint Rgb(byte r, byte g, byte b) => (uint)(r | (g << 8) | (b << 16));

        // Steam Dark Colors
        private static readonly uint ColBg = Rgb(23, 26, 33);             // #171a21
        private static readonly uint ColCardBg = Rgb(30, 35, 45);         // #1e232d
        private static readonly uint ColCardBorder = Rgb(46, 56, 71);     // #2e3847
        private static readonly uint ColSteamBlue = Rgb(102, 192, 244);   // #66c0f4
        private static readonly uint ColWhite = Rgb(255, 255, 255);       // #ffffff
        private static readonly uint ColMuted = Rgb(143, 152, 160);       // #8f98a0
        private static readonly uint ColSuccess = Rgb(92, 184, 92);       // #5cb85c
        private static readonly uint ColError = Rgb(217, 83, 79);         // #d9534f
        private static readonly uint ColBtnPrimary = Rgb(33, 75, 107);     // #214b6b
        private static readonly uint ColBtnPrimaryHov = Rgb(50, 110, 155);// #326e9b

        // Controller Button Colors
        private static readonly uint ColGlyphRed = Rgb(209, 50, 50);      // B button / PS Circle: #d13232
        private static readonly uint ColGlyphKey = Rgb(46, 56, 71);      // Key Esc badge: #2e3847

        private static WndProcDelegate? _wndProcHolder;

        public static void ShowDialog(SteamSyncResult result)
        {
            _activeResult = result;
            _hoverClose = false;
            _xinputFailed = false;
            _strings = Localization.GetStrings();
            _gamepadType = GamepadDetector.DetectConnectedGamepad();

            const int width = 560;
            const int height = 450;

            int screenW = GetSystemMetrics(0);
            int screenH = GetSystemMetrics(1);
            int posX = Math.Max(0, (screenW - width) / 2);
            int posY = Math.Max(0, (screenH - height) / 2);

            IntPtr hInstance = Marshal.GetHINSTANCE(typeof(SyncStatusDialog).Module);

            _wndProcHolder = WndProc;

            var wndClass = new WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
                style = CS_HREDRAW | CS_VREDRAW,
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcHolder),
                hInstance = hInstance,
                hbrBackground = CreateSolidBrush(ColBg),
                lpszClassName = WindowClassName
            };

            RegisterClassEx(ref wndClass);

            uint dwStyle = WS_POPUP | WS_CAPTION | WS_SYSMENU | WS_MINIMIZEBOX | WS_VISIBLE;
            IntPtr hWnd = CreateWindowEx(
                0,
                WindowClassName,
                _strings.WindowTitle,
                dwStyle,
                posX, posY, width, height,
                IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero
            );

            if (hWnd == IntPtr.Zero)
                return;

            // Apply Immersive Dark Mode
            int darkTrue = 1;
            DwmSetWindowAttribute(hWnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkTrue, sizeof(int));
            DwmSetWindowAttribute(hWnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref darkTrue, sizeof(int));

            // Windows 11 Rounded Corners
            int roundCorner = DWMWCP_ROUND;
            DwmSetWindowAttribute(hWnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref roundCorner, sizeof(int));

            // Poll Gamepad every 33 ms (~30 FPS)
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
                case WM_PAINT:
                    {
                        IntPtr hdc = BeginPaint(hWnd, out var ps);
                        PaintContent(hdc, _activeResult);
                        EndPaint(hWnd, ref ps);
                        return IntPtr.Zero;
                    }

                case WM_TIMER:
                    {
                        if (PollGamepadClose())
                        {
                            KillTimer(hWnd, (IntPtr)1);
                            DestroyWindow(hWnd);
                            return IntPtr.Zero;
                        }
                        return IntPtr.Zero;
                    }

                case WM_MOUSEMOVE:
                    {
                        int x = (short)(lParam.ToInt64() & 0xFFFF);
                        int y = (short)((lParam.ToInt64() >> 16) & 0xFFFF);

                        bool newHov = BtnClose.Contains(x, y);
                        if (newHov != _hoverClose)
                        {
                            _hoverClose = newHov;
                            InvalidateRect(hWnd, IntPtr.Zero, false);
                        }
                        return IntPtr.Zero;
                    }

                case WM_LBUTTONUP:
                    {
                        int x = (short)(lParam.ToInt64() & 0xFFFF);
                        int y = (short)((lParam.ToInt64() >> 16) & 0xFFFF);

                        if (BtnClose.Contains(x, y))
                        {
                            KillTimer(hWnd, (IntPtr)1);
                            DestroyWindow(hWnd);
                            return IntPtr.Zero;
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

        private static bool PollGamepadClose()
        {
            if (_xinputFailed) return false;

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
                        return false;
                    }
                }
                catch
                {
                    _xinputFailed = true;
                    return false;
                }

                if (err == 0) // ERROR_SUCCESS
                {
                    ushort b = state.Gamepad.wButtons;
                    const ushort anyCloseButton = XINPUT_GAMEPAD_A | XINPUT_GAMEPAD_B | XINPUT_GAMEPAD_START | XINPUT_GAMEPAD_BACK | XINPUT_GAMEPAD_X | XINPUT_GAMEPAD_Y;

                    if ((b & anyCloseButton) != 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void PaintContent(IntPtr hdc, SteamSyncResult? result)
        {
            SetBkMode(hdc, TRANSPARENT);

            // 1. Header (Title & Subtitle)
            var fontTitle = CreateFont(22, 0, 0, 0, 700, 0, 0, 0, 1, 0, 0, 5, 0, "Segoe UI");
            var fontSubtitle = CreateFont(13, 0, 0, 0, 600, 0, 0, 0, 1, 0, 0, 5, 0, "Segoe UI");
            var fontNormal = CreateFont(13, 0, 0, 0, 400, 0, 0, 0, 1, 0, 0, 5, 0, "Segoe UI");
            var fontBold = CreateFont(13, 0, 0, 0, 700, 0, 0, 0, 1, 0, 0, 5, 0, "Segoe UI");
            var fontBadge = CreateFont(11, 0, 0, 0, 700, 0, 0, 0, 1, 0, 0, 5, 0, "Segoe UI");
            var fontGlyph = CreateFont(12, 0, 0, 0, 700, 0, 0, 0, 1, 0, 0, 5, 0, "Segoe UI");
            var fontBtn = CreateFont(14, 0, 0, 0, 700, 0, 0, 0, 1, 0, 0, 5, 0, "Segoe UI");

            SelectObject(hdc, fontTitle);
            SetTextColor(hdc, ColSteamBlue);
            var rcTitle = new RECT { Left = 24, Top = 18, Right = 530, Bottom = 45 };
            DrawText(hdc, "Non-Steam Launcher", -1, ref rcTitle, DT_LEFT | DT_SINGLELINE);

            SelectObject(hdc, fontSubtitle);
            var rcSub = new RECT { Left = 24, Top = 45, Right = 530, Bottom = 68 };
            if (result != null && result.Success)
            {
                if (result.AddedGames.Count > 0)
                {
                    SetTextColor(hdc, ColSuccess);
                    DrawText(hdc, $"{_strings.HeaderSuccess} ({result.AddedGames.Count})", -1, ref rcSub, DT_LEFT | DT_SINGLELINE);
                }
                else
                {
                    SetTextColor(hdc, ColSteamBlue);
                    DrawText(hdc, _strings.HeaderUpToDate, -1, ref rcSub, DT_LEFT | DT_SINGLELINE);
                }
            }
            else
            {
                SetTextColor(hdc, ColError);
                DrawText(hdc, _strings.HeaderError, -1, ref rcSub, DT_LEFT | DT_SINGLELINE);
            }

            // 2. Card Background for Game List
            IntPtr hCardBrush = CreateSolidBrush(ColCardBg);
            IntPtr hCardPen = CreatePen(0, 1, ColCardBorder);
            SelectObject(hdc, hCardBrush);
            SelectObject(hdc, hCardPen);
            RoundRect(hdc, 24, 78, 530, 375, 12, 12);
            DeleteObject(hCardBrush);
            DeleteObject(hCardPen);

            // 3. Card Contents
            if (result != null && result.Success)
            {
                var displayList = result.AddedGames.Count > 0 ? result.AddedGames : result.ExistingGames;
                string headerMsg = result.AddedGames.Count > 0
                    ? _strings.CardHeaderAdded
                    : _strings.CardHeaderCurrent;

                SelectObject(hdc, fontBadge);
                SetTextColor(hdc, ColMuted);
                var rcCardHeader = new RECT { Left = 40, Top = 90, Right = 515, Bottom = 108 };
                DrawText(hdc, headerMsg, -1, ref rcCardHeader, DT_LEFT | DT_SINGLELINE);

                if (displayList.Count == 0)
                {
                    SelectObject(hdc, fontNormal);
                    SetTextColor(hdc, ColMuted);
                    var rcEmpty = new RECT { Left = 40, Top = 125, Right = 515, Bottom = 160 };
                    DrawText(hdc, _strings.EmptyMessage, -1, ref rcEmpty, DT_LEFT | DT_SINGLELINE);
                }
                else
                {
                    int itemY = 115;
                    int maxItems = Math.Min(displayList.Count, 6);

                    for (int i = 0; i < maxItems; i++)
                    {
                        var game = displayList[i];
                        uint badgeColor = GetLauncherColor(game.Launcher);

                        // Platform Badge Pill
                        IntPtr hBadgeBrush = CreateSolidBrush(badgeColor);
                        IntPtr hBadgePen = CreatePen(0, 1, badgeColor);
                        SelectObject(hdc, hBadgeBrush);
                        SelectObject(hdc, hBadgePen);

                        int badgeW = 105;
                        RoundRect(hdc, 40, itemY, 40 + badgeW, itemY + 24, 8, 8);
                        DeleteObject(hBadgeBrush);
                        DeleteObject(hBadgePen);

                        // Badge text
                        SelectObject(hdc, fontBadge);
                        SetTextColor(hdc, ColWhite);
                        var rcBadgeText = new RECT { Left = 40, Top = itemY, Right = 40 + badgeW, Bottom = itemY + 24 };
                        DrawText(hdc, game.Launcher, -1, ref rcBadgeText, DT_CENTER | DT_VCENTER | DT_SINGLELINE);

                        // Game Title
                        SelectObject(hdc, fontBold);
                        SetTextColor(hdc, ColWhite);
                        var rcGameTitle = new RECT { Left = 40 + badgeW + 14, Top = itemY, Right = 510, Bottom = itemY + 24 };
                        DrawText(hdc, game.Title, -1, ref rcGameTitle, DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_WORD_ELLIPSIS);

                        itemY += 34;
                    }

                    if (displayList.Count > 6)
                    {
                        SelectObject(hdc, fontNormal);
                        SetTextColor(hdc, ColMuted);
                        var rcMore = new RECT { Left = 40, Top = itemY + 4, Right = 510, Bottom = itemY + 24 };
                        DrawText(hdc, string.Format(_strings.MoreGamesFormat, displayList.Count - 6), -1, ref rcMore, DT_LEFT | DT_SINGLELINE);
                    }
                }
            }
            else
            {
                SelectObject(hdc, fontNormal);
                SetTextColor(hdc, ColError);
                var rcErr = new RECT { Left = 40, Top = 110, Right = 510, Bottom = 300 };
                DrawText(hdc, result?.ErrorMessage ?? _strings.HeaderError, -1, ref rcErr, DT_LEFT);
            }

            // 4. Subtle Left Footer Branding
            SelectObject(hdc, fontNormal);
            SetTextColor(hdc, ColMuted);
            var rcBrand = new RECT { Left = 26, Top = 398, Right = 250, Bottom = 422 };
            DrawText(hdc, "Non-Steam Launcher", -1, ref rcBrand, DT_LEFT | DT_VCENTER | DT_SINGLELINE);

            // 5. Single Steam-style Action Button with Controller Glyph
            uint colBtn = _hoverClose ? ColBtnPrimaryHov : ColBtnPrimary;
            IntPtr hBtnBrush = CreateSolidBrush(colBtn);
            IntPtr hBtnPen = CreatePen(0, 1, _hoverClose ? ColSteamBlue : ColCardBorder);
            SelectObject(hdc, hBtnBrush);
            SelectObject(hdc, hBtnPen);
            RoundRect(hdc, BtnClose.Left, BtnClose.Top, BtnClose.Right, BtnClose.Bottom, 8, 8);
            DeleteObject(hBtnBrush);
            DeleteObject(hBtnPen);

            // Draw Button Glyph according to detected controller
            string glyphText;
            bool isCircleGlyph;
            uint glyphColor;

            switch (_gamepadType)
            {
                case GamepadType.PlayStation:
                    glyphText = "○";
                    isCircleGlyph = true;
                    glyphColor = ColGlyphRed;
                    break;
                case GamepadType.Nintendo:
                    glyphText = "B";
                    isCircleGlyph = true;
                    glyphColor = ColGlyphRed;
                    break;
                case GamepadType.Xbox:
                    glyphText = "B";
                    isCircleGlyph = true;
                    glyphColor = ColGlyphRed;
                    break;
                default:
                    glyphText = "Esc";
                    isCircleGlyph = false;
                    glyphColor = ColGlyphKey;
                    break;
            }

            int glyphX = BtnClose.Left + 16;
            int glyphY = BtnClose.Top + 9;
            int glyphW = isCircleGlyph ? 20 : 28;
            int glyphH = 20;

            IntPtr hGlyphBrush = CreateSolidBrush(glyphColor);
            IntPtr hGlyphPen = CreatePen(0, 1, glyphColor);
            SelectObject(hdc, hGlyphBrush);
            SelectObject(hdc, hGlyphPen);

            if (isCircleGlyph)
            {
                Ellipse(hdc, glyphX, glyphY, glyphX + glyphW, glyphY + glyphH);
            }
            else
            {
                RoundRect(hdc, glyphX, glyphY, glyphX + glyphW, glyphY + glyphH, 6, 6);
            }
            DeleteObject(hGlyphBrush);
            DeleteObject(hGlyphPen);

            SelectObject(hdc, fontGlyph);
            SetTextColor(hdc, ColWhite);
            var rcGlyph = new RECT { Left = glyphX, Top = glyphY, Right = glyphX + glyphW, Bottom = glyphY + glyphH };
            DrawText(hdc, glyphText, -1, ref rcGlyph, DT_CENTER | DT_VCENTER | DT_SINGLELINE);

            // Action Label text (e.g. "Close" / "Cerrar")
            SelectObject(hdc, fontBtn);
            SetTextColor(hdc, ColWhite);
            var rcBtnText = new RECT { Left = glyphX + glyphW + 10, Top = BtnClose.Top, Right = BtnClose.Right - 8, Bottom = BtnClose.Bottom };
            DrawText(hdc, _strings.ActionClose, -1, ref rcBtnText, DT_LEFT | DT_VCENTER | DT_SINGLELINE);

            // Cleanup Fonts
            DeleteObject(fontTitle);
            DeleteObject(fontSubtitle);
            DeleteObject(fontNormal);
            DeleteObject(fontBold);
            DeleteObject(fontBadge);
            DeleteObject(fontGlyph);
            DeleteObject(fontBtn);
        }

        private static uint GetLauncherColor(string launcher)
        {
            if (launcher.Contains("Epic", StringComparison.OrdinalIgnoreCase))
                return Rgb(0, 120, 242);    // Epic Blue: #0078f2

            if (launcher.Contains("Ubisoft", StringComparison.OrdinalIgnoreCase))
                return Rgb(0, 112, 255);    // Ubisoft Cyan: #0070ff

            if (launcher.Contains("EA", StringComparison.OrdinalIgnoreCase) ||
                launcher.Contains("Origin", StringComparison.OrdinalIgnoreCase))
                return Rgb(235, 75, 55);    // EA Orange-Red: #eb4b37

            if (launcher.Contains("Battle", StringComparison.OrdinalIgnoreCase))
                return Rgb(0, 174, 255);    // Battle.net Light Blue: #00aeff

            if (launcher.Contains("GOG", StringComparison.OrdinalIgnoreCase))
                return Rgb(142, 68, 173);   // GOG Purple: #8e44ad

            return Rgb(65, 122, 155);       // Default Slate: #417a9b
        }
    }
}
