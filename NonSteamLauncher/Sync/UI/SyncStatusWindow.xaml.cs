using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using NonSteamLauncher.Sync.Steam;
using Path = System.IO.Path;

namespace NonSteamLauncher.Sync.UI;

public enum ActiveInputMode
{
    Keyboard,
    Xbox,
    PlayStation,
    Nintendo
}

public partial class SyncStatusWindow : Window
{
    private readonly SteamSyncResult _initialResult;
    private readonly Func<IReadOnlyList<InstalledGame>, SteamSyncResult>? _onSyncSelected;
    private readonly Func<SteamSyncResult>? _onSettingsSaved;
    private readonly string? _settingsFilePath;
    private readonly AppSettings _appSettings;
    private LocalizedStrings _strings;
    private SteamSyncResult _currentResult;

    // Input & Gamepad Tracking
    private ActiveInputMode _activeInputMode = ActiveInputMode.Keyboard;
    private DispatcherTimer? _gamepadTimer;
    private ushort _prevGamepadButtons;
    private bool _gamepadAxisReleased = true;
    private int _gamepadDirCooldown;
    private int _gamepadDetectCounter;
    private bool _initialButtonsCleared;
    private bool _xinputFailed;
    private bool _winmmFailed;
    private Point _lastMousePos;
    private bool _isUpdatingLanguage;
    private int _settingsFocusIndex;

    public ObservableCollection<GameCardItem> GameItems { get; } = new();

    public SyncStatusWindow(
        SteamSyncResult result,
        Func<IReadOnlyList<InstalledGame>, SteamSyncResult>? onSyncSelected = null,
        Func<SteamSyncResult>? onSettingsSaved = null,
        string? settingsFilePath = null)
    {
        InitializeComponent();

        _initialResult = result;
        _currentResult = result;
        _onSyncSelected = onSyncSelected;
        _onSettingsSaved = onSettingsSaved;
        _settingsFilePath = settingsFilePath;
        _appSettings = AppSettings.Load(settingsFilePath);

        // Language defaults to Spanish ("es") unless explicitly set to English
        string lang = !string.IsNullOrWhiteSpace(_appSettings.Language) 
            ? _appSettings.Language 
            : "es";
        _strings = Localization.GetStrings(lang);

        ApplySystemFluentTheme();
        ApplyLocalization();
        LoadWindowIcon();
        PopulateView(result);

        // Bind Settings
        ChkDownloadArtwork.IsChecked = _appSettings.DownloadArtwork;
        TxtApiKey.Text = _appSettings.SteamGridDbApiKey ?? string.Empty;

        // Bind Language Selector
        _isUpdatingLanguage = true;
        ComboLanguage.SelectedIndex = (lang.ToLowerInvariant() == "en") ? 1 : 0;
        _isUpdatingLanguage = false;

        // Detect initial connected controller family
        var initialGamepad = GamepadDetector.DetectConnectedGamepad();
        if (initialGamepad == GamepadType.PlayStation) _activeInputMode = ActiveInputMode.PlayStation;
        else if (initialGamepad == GamepadType.Nintendo) _activeInputMode = ActiveInputMode.Nintendo;
        else if (initialGamepad == GamepadType.Xbox) _activeInputMode = ActiveInputMode.Xbox;
        else _activeInputMode = ActiveInputMode.Keyboard;

        UpdateInputModeVisuals();

        SourceInitialized += (s, e) =>
        {
            ApplyDwmAttributes();
            StartGamepadPolling();
        };

        Closed += (s, e) =>
        {
            _gamepadTimer?.Stop();
        };

        // Keyboard & Mouse Listeners to instantly switch to Keyboard Mode
        PreviewMouseMove += (s, e) =>
        {
            var pos = e.GetPosition(this);
            if (Math.Abs(pos.X - _lastMousePos.X) > 3 || Math.Abs(pos.Y - _lastMousePos.Y) > 3)
            {
                _lastMousePos = pos;
                SetInputMode(ActiveInputMode.Keyboard);
            }
        };

        PreviewKeyDown += (s, e) =>
        {
            SetInputMode(ActiveInputMode.Keyboard);

            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                BtnPrimaryAction_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (e.Key == Key.Space && PanelGames.Visibility == Visibility.Visible)
            {
                if (ListGames.SelectedItem is GameCardItem item && !item.IsSynced)
                {
                    item.IsSelected = !item.IsSelected;
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.A && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && PanelGames.Visibility == Visibility.Visible)
            {
                ToggleSelectAll();
                e.Handled = true;
            }
            else if (e.Key == Key.D1 || e.Key == Key.NumPad1 || e.Key == Key.Q)
            {
                SwitchTab(0);
                e.Handled = true;
            }
            else if (e.Key == Key.D2 || e.Key == Key.NumPad2 || e.Key == Key.E)
            {
                SwitchTab(1);
                e.Handled = true;
            }
        };

        ListGames.SelectionChanged += (s, e) =>
        {
            UpdateItemSelectionVisuals();
        };
    }

    public SteamSyncResult Result => _currentResult;

    private void SwitchTab(int tabIndex)
    {
        if (PanelGames == null || PanelSettings == null || TabGames == null || TabSettings == null) return;

        if (tabIndex == 0)
        {
            TabGames.IsChecked = true;
            PanelGames.Visibility = Visibility.Visible;
            PanelSettings.Visibility = Visibility.Collapsed;
        }
        else
        {
            TabSettings.IsChecked = true;
            PanelGames.Visibility = Visibility.Collapsed;
            PanelSettings.Visibility = Visibility.Visible;
            _settingsFocusIndex = 0;
            UpdateSettingsFocusVisuals();
        }
        UpdateFooterButtons();
    }

    private void SetInputMode(ActiveInputMode mode)
    {
        if (_activeInputMode == mode) return;
        _activeInputMode = mode;
        UpdateInputModeVisuals();
    }

    private void UpdateInputModeVisuals()
    {
        if (IconActiveDevice == null || BadgePrimaryAction == null || BadgeClose == null) return;

        // 1. Top-Right Device Indicator Pill
        switch (_activeInputMode)
        {
            case ActiveInputMode.Keyboard:
                IconActiveDevice.Text = "\uE765"; // Keyboard icon
                TxtActiveDevice.Text = _strings.InputDeviceKeyboard;
                break;
            case ActiveInputMode.PlayStation:
                IconActiveDevice.Text = "\uE7FC"; // Controller icon
                TxtActiveDevice.Text = _strings.InputDevicePlayStation;
                break;
            case ActiveInputMode.Nintendo:
                IconActiveDevice.Text = "\uE7FC";
                TxtActiveDevice.Text = _strings.InputDeviceNintendo;
                break;
            default: // Xbox
                IconActiveDevice.Text = "\uE7FC";
                TxtActiveDevice.Text = _strings.InputDeviceXbox;
                break;
        }

        // 2. Tab Navigation Badges (Show bumpers ONLY in gamepad mode)
        switch (_activeInputMode)
        {
            case ActiveInputMode.PlayStation:
                BadgeTabGames.Content = CreateBumperBadge("L1");
                BadgeTabSettings.Content = CreateBumperBadge("R1");
                BadgeTabGames.Visibility = Visibility.Visible;
                BadgeTabSettings.Visibility = Visibility.Visible;
                break;
            case ActiveInputMode.Nintendo:
                BadgeTabGames.Content = CreateBumperBadge("L");
                BadgeTabSettings.Content = CreateBumperBadge("R");
                BadgeTabGames.Visibility = Visibility.Visible;
                BadgeTabSettings.Visibility = Visibility.Visible;
                break;
            case ActiveInputMode.Xbox:
                BadgeTabGames.Content = CreateBumperBadge("LB");
                BadgeTabSettings.Content = CreateBumperBadge("RB");
                BadgeTabGames.Visibility = Visibility.Visible;
                BadgeTabSettings.Visibility = Visibility.Visible;
                break;
            default: // Keyboard (Clean, no floating keycaps on tabs)
                BadgeTabGames.Visibility = Visibility.Collapsed;
                BadgeTabSettings.Visibility = Visibility.Collapsed;
                break;
        }

        // 3. Footer Action Button Badges
        switch (_activeInputMode)
        {
            case ActiveInputMode.Keyboard:
                BadgePrimaryAction.Content = CreateKeycapBadge(_strings.KeyNameEnter);
                BadgeClose.Content = CreateKeycapBadge(_strings.KeyNameEsc);
                break;
            case ActiveInputMode.PlayStation:
                BadgePrimaryAction.Content = CreatePlayStationBadge("square");
                BadgeClose.Content = CreatePlayStationBadge("circle");
                break;
            case ActiveInputMode.Nintendo:
                BadgePrimaryAction.Content = CreateXboxBadge("Y", System.Windows.Media.Color.FromRgb(255, 185, 0));
                BadgeClose.Content = CreateXboxBadge("B", System.Windows.Media.Color.FromRgb(216, 59, 1));
                break;
            default: // Xbox
                BadgePrimaryAction.Content = CreateXboxBadge("X", System.Windows.Media.Color.FromRgb(0, 120, 215));
                BadgeClose.Content = CreateXboxBadge("B", System.Windows.Media.Color.FromRgb(216, 59, 1));
                break;
        }

        // 4. Footer Hint Legend
        BuildFooterLegend();

        // 5. Update items selection/focus visuals
        UpdateItemSelectionVisuals();
    }

    private void BuildFooterLegend()
    {
        if (PanelFooterLegend == null) return;
        PanelFooterLegend.Children.Clear();

        switch (_activeInputMode)
        {
            case ActiveInputMode.Keyboard:
                AddLegendItem(CreateKeycapBadge(_strings.KeyNameTab), _strings.LegendTabs);
                AddLegendSeparator();
                AddLegendItem(CreateKeycapBadge(_strings.KeyNameSpace), _strings.LegendToggle);
                AddLegendSeparator();
                AddLegendItem(CreateKeycapBadge(_strings.KeyNameEnter), _strings.LegendSync);
                AddLegendSeparator();
                AddLegendItem(CreateKeycapBadge(_strings.KeyNameEsc), _strings.LegendClose);
                break;
            case ActiveInputMode.PlayStation:
                AddLegendItem(CreateBumperBadge("L1/R1"), _strings.LegendTabs);
                AddLegendSeparator();
                AddLegendItem(CreatePlayStationBadge("cross"), _strings.LegendToggle);
                AddLegendSeparator();
                AddLegendItem(CreatePlayStationBadge("square"), _strings.LegendSync);
                AddLegendSeparator();
                AddLegendItem(CreatePlayStationBadge("circle"), _strings.LegendClose);
                break;
            default: // Xbox
                AddLegendItem(CreateBumperBadge("LB/RB"), _strings.LegendTabs);
                AddLegendSeparator();
                AddLegendItem(CreateXboxBadge("A", System.Windows.Media.Color.FromRgb(16, 124, 16)), _strings.LegendToggle);
                AddLegendSeparator();
                AddLegendItem(CreateXboxBadge("X", System.Windows.Media.Color.FromRgb(0, 120, 215)), _strings.LegendSync);
                AddLegendSeparator();
                AddLegendItem(CreateXboxBadge("B", System.Windows.Media.Color.FromRgb(216, 59, 1)), _strings.LegendClose);
                break;
        }
    }

    private void AddLegendItem(UIElement badge, string label)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        sp.Children.Add(badge);
        var txt = new TextBlock
        {
            Text = label,
            FontSize = 11.5,
            FontWeight = FontWeights.Normal,
            Foreground = (System.Windows.Media.Brush)Resources["TextSecondaryBrush"],
            VerticalAlignment = VerticalAlignment.Center
        };
        sp.Children.Add(txt);
        PanelFooterLegend.Children.Add(sp);
    }

    private void AddLegendSeparator()
    {
        var dot = new TextBlock
        {
            Text = "•",
            Margin = new Thickness(8, 0, 8, 0),
            FontSize = 11,
            Foreground = (System.Windows.Media.Brush)Resources["TextTertiaryBrush"],
            VerticalAlignment = VerticalAlignment.Center
        };
        PanelFooterLegend.Children.Add(dot);
    }

    // Dynamic Vector Glyph Builders
    private Border CreateKeycapBadge(string keyText)
    {
        var border = new Border
        {
            Height = 19,
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1),
            BorderBrush = (System.Windows.Media.Brush)Resources["KeycapBorderBrush"],
            Background = (System.Windows.Media.Brush)Resources["KeycapBgBrush"],
            Padding = new Thickness(5, 0, 5, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
            SnapsToDevicePixels = true
        };
        var text = new TextBlock
        {
            Text = keyText,
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            FontFamily = new FontFamily("Segoe UI Variable Text, Consolas, Segoe UI, sans-serif"),
            Foreground = (System.Windows.Media.Brush)Resources["KeycapFgBrush"],
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        border.Child = text;
        return border;
    }

    private Border CreateXboxBadge(string buttonText, System.Windows.Media.Color bgCol)
    {
        var border = new Border
        {
            Width = 19,
            Height = 19,
            CornerRadius = new CornerRadius(9.5),
            Background = new SolidColorBrush(bgCol),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
            SnapsToDevicePixels = true
        };
        var text = new TextBlock
        {
            Text = buttonText,
            FontSize = 10.5,
            FontWeight = FontWeights.Bold,
            Foreground = System.Windows.Media.Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, -1, 0, 0)
        };
        border.Child = text;
        return border;
    }

    // PlayStation DualSense: Pure Vector Monochrome (Square, Circle, Cross, Triangle)
    // Perfectly scaled (8x8 to 9x9), centered, and crisp!
    private Border CreatePlayStationBadge(string shapeType)
    {
        var border = new Border
        {
            Width = 19,
            Height = 19,
            CornerRadius = new CornerRadius(9.5),
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(36, 38, 44)),
            BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(68, 72, 84)),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
            SnapsToDevicePixels = true
        };

        UIElement shapeElement;
        switch (shapeType.ToLowerInvariant())
        {
            case "square":
                shapeElement = new System.Windows.Shapes.Rectangle
                {
                    Width = 8,
                    Height = 8,
                    Stroke = System.Windows.Media.Brushes.White,
                    StrokeThickness = 1.4,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                break;
            case "circle":
                shapeElement = new System.Windows.Shapes.Ellipse
                {
                    Width = 9,
                    Height = 9,
                    Stroke = System.Windows.Media.Brushes.White,
                    StrokeThickness = 1.4,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                break;
            case "cross":
                var crossPath = new System.Windows.Shapes.Path
                {
                    Width = 8,
                    Height = 8,
                    Data = Geometry.Parse("M 0,0 L 8,8 M 8,0 L 0,8"),
                    Stroke = System.Windows.Media.Brushes.White,
                    StrokeThickness = 1.4,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                shapeElement = crossPath;
                break;
            case "triangle":
                var triangle = new System.Windows.Shapes.Polygon
                {
                    Width = 9,
                    Height = 8,
                    Points = new PointCollection { new Point(4.5, 0), new Point(9, 8), new Point(0, 8) },
                    Stroke = System.Windows.Media.Brushes.White,
                    StrokeThickness = 1.4,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                shapeElement = triangle;
                break;
            default:
                shapeElement = new TextBlock
                {
                    Text = shapeType,
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    Foreground = System.Windows.Media.Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                break;
        }

        border.Child = shapeElement;
        return border;
    }

    private Border CreateBumperBadge(string bumperText)
    {
        var border = new Border
        {
            Height = 19,
            CornerRadius = new CornerRadius(4),
            Background = (System.Windows.Media.Brush)Resources["KeycapBgBrush"],
            BorderBrush = (System.Windows.Media.Brush)Resources["KeycapBorderBrush"],
            BorderThickness = new Thickness(1),
            Padding = new Thickness(5, 0, 5, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
            SnapsToDevicePixels = true
        };
        var text = new TextBlock
        {
            Text = bumperText,
            FontSize = 9.5,
            FontWeight = FontWeights.Bold,
            Foreground = (System.Windows.Media.Brush)Resources["KeycapFgBrush"],
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        border.Child = text;
        return border;
    }

    private void ApplyLocalization()
    {
        Title = _strings.WindowTitle;
        TxtTabGames.Text = _strings.TabLibrary;
        TxtTabSettings.Text = _strings.TabArtwork;
        TxtDownloadArtworkTitle.Text = _strings.SettingsDownloadToggle;
        TxtApiKeyTitle.Text = _strings.SettingsApiKeyLabel;
        BtnPasteKey.Content = _strings.SettingsPasteButton;
        BtnClearKey.Content = _strings.SettingsClearButton;
        BtnGetKey.Content = _strings.SettingsGetKeyButton;
        TxtLanguageLabel.Text = _strings.SettingsLanguageLabel;
        TxtClose.Text = _strings.ActionClose;
    }

    private void ApplySystemFluentTheme()
    {
        bool isLight = QuerySystemLightModePreference();
        System.Windows.Media.Color accentCol = QuerySystemAccentColor(isLight);

        // Harmonious, uniform Fluent 2 surface palette (No messy grey/white mix!)
        System.Windows.Media.Color bgBase = isLight 
            ? System.Windows.Media.Color.FromRgb(248, 248, 248) 
            : System.Windows.Media.Color.FromRgb(30, 30, 30);

        System.Windows.Media.Color bgFooter = isLight 
            ? System.Windows.Media.Color.FromRgb(242, 242, 242) 
            : System.Windows.Media.Color.FromRgb(24, 24, 24);

        System.Windows.Media.Color layerCol = isLight 
            ? System.Windows.Media.Color.FromRgb(255, 255, 255) 
            : System.Windows.Media.Color.FromRgb(37, 37, 37);

        System.Windows.Media.Color cardCol = isLight 
            ? System.Windows.Media.Color.FromRgb(255, 255, 255) 
            : System.Windows.Media.Color.FromRgb(43, 43, 43);

        System.Windows.Media.Color cardHover = isLight 
            ? System.Windows.Media.Color.FromRgb(240, 240, 240) 
            : System.Windows.Media.Color.FromRgb(51, 51, 51);

        System.Windows.Media.Color cardStroke = isLight 
            ? System.Windows.Media.Color.FromRgb(226, 226, 226) 
            : System.Windows.Media.Color.FromRgb(56, 56, 56);

        System.Windows.Media.Color textPrimary = isLight 
            ? System.Windows.Media.Color.FromRgb(28, 28, 28) 
            : System.Windows.Media.Color.FromRgb(250, 250, 250);

        System.Windows.Media.Color textSecondary = isLight 
            ? System.Windows.Media.Color.FromRgb(110, 110, 110) 
            : System.Windows.Media.Color.FromRgb(165, 165, 165);

        System.Windows.Media.Color textTertiary = isLight 
            ? System.Windows.Media.Color.FromRgb(150, 150, 150) 
            : System.Windows.Media.Color.FromRgb(125, 125, 125);

        System.Windows.Media.Color keycapBg = isLight 
            ? System.Windows.Media.Color.FromRgb(240, 240, 240) 
            : System.Windows.Media.Color.FromRgb(46, 46, 50);

        System.Windows.Media.Color keycapBorder = isLight 
            ? System.Windows.Media.Color.FromRgb(215, 215, 215) 
            : System.Windows.Media.Color.FromRgb(72, 72, 80);

        System.Windows.Media.Color textOnAccent = Colors.White;
        System.Windows.Media.Color accentHover = BlendColor(accentCol, bgBase, 0.90);
        System.Windows.Media.Color accentPressed = BlendColor(accentCol, bgBase, 0.80);
        System.Windows.Media.Color accentTint = System.Windows.Media.Color.FromArgb((byte)(isLight ? 25 : 40), accentCol.R, accentCol.G, accentCol.B);

        Resources["BackgroundBrush"] = new SolidColorBrush(bgBase);
        Resources["FooterBrush"] = new SolidColorBrush(bgFooter);
        Resources["LayerBrush"] = new SolidColorBrush(layerCol);
        Resources["CardBrush"] = new SolidColorBrush(cardCol);
        Resources["CardHoverBrush"] = new SolidColorBrush(cardHover);
        Resources["CardStrokeBrush"] = new SolidColorBrush(cardStroke);
        Resources["DividerBrush"] = new SolidColorBrush(cardStroke);
        Resources["TextPrimaryBrush"] = new SolidColorBrush(textPrimary);
        Resources["TextSecondaryBrush"] = new SolidColorBrush(textSecondary);
        Resources["TextTertiaryBrush"] = new SolidColorBrush(textTertiary);
        Resources["KeycapBgBrush"] = new SolidColorBrush(keycapBg);
        Resources["KeycapBorderBrush"] = new SolidColorBrush(keycapBorder);
        Resources["KeycapFgBrush"] = new SolidColorBrush(textPrimary);
        Resources["AccentBrush"] = new SolidColorBrush(accentCol);
        Resources["AccentHoverBrush"] = new SolidColorBrush(accentHover);
        Resources["AccentPressedBrush"] = new SolidColorBrush(accentPressed);
        Resources["AccentTintBrush"] = new SolidColorBrush(accentTint);
        Resources["TextOnAccentBrush"] = new SolidColorBrush(textOnAccent);
    }

    private void PopulateView(SteamSyncResult result)
    {
        bool isLight = QuerySystemLightModePreference();
        System.Windows.Media.Color accentCol = QuerySystemAccentColor(isLight);

        int unsyncedCount = result.PendingGames.Count;

        if (result.Success)
        {
            if (unsyncedCount > 0)
            {
                TxtInfoIcon.Text = "\uE895"; // Sync
                TxtInfoMessage.Text = string.Format(_strings.HeaderPendingCompact, unsyncedCount);

                System.Windows.Media.Color syncCol = isLight ? accentCol : System.Windows.Media.Color.FromRgb(100, 195, 255);
                Resources["InfoIconBrush"] = new SolidColorBrush(syncCol);
            }
            else
            {
                TxtInfoIcon.Text = "\uE73E"; // Checkmark
                TxtInfoMessage.Text = string.Format(_strings.HeaderUpToDateCompact, result.ExistingGames.Count + result.AddedGames.Count);

                System.Windows.Media.Color successCol = isLight ? System.Windows.Media.Color.FromRgb(15, 123, 15) : System.Windows.Media.Color.FromRgb(108, 203, 95);
                Resources["InfoIconBrush"] = new SolidColorBrush(successCol);
            }
        }
        else
        {
            TxtInfoIcon.Text = "\uE783"; // Error badge
            TxtInfoMessage.Text = result.ErrorMessage;

            System.Windows.Media.Color critCol = isLight ? System.Windows.Media.Color.FromRgb(196, 43, 28) : System.Windows.Media.Color.FromRgb(255, 153, 164);
            Resources["InfoIconBrush"] = new SolidColorBrush(critCol);
        }

        // Build Game Items
        GameItems.Clear();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Pending games first
        foreach (var g in result.PendingGames)
        {
            string key = GameDetector.NormalizeTitle(g.Title);
            if (string.IsNullOrEmpty(key)) key = g.Title;
            if (seen.Add(key))
            {
                GameItems.Add(CreateGameCardItem(g, isSynced: false, isLight));
            }
        }

        // 2. Added games
        foreach (var g in result.AddedGames)
        {
            string key = GameDetector.NormalizeTitle(g.Title);
            if (string.IsNullOrEmpty(key)) key = g.Title;
            if (seen.Add(key))
            {
                GameItems.Add(CreateGameCardItem(g, isSynced: true, isLight));
            }
        }

        // 3. Existing games
        foreach (var g in result.ExistingGames)
        {
            string key = GameDetector.NormalizeTitle(g.Title);
            if (string.IsNullOrEmpty(key)) key = g.Title;
            if (seen.Add(key))
            {
                GameItems.Add(CreateGameCardItem(g, isSynced: true, isLight));
            }
        }

        TxtTabGamesCount.Text = GameItems.Count.ToString();
        ListGames.ItemsSource = GameItems;
        if (GameItems.Count > 0 && ListGames.SelectedIndex < 0)
        {
            ListGames.SelectedIndex = 0;
        }

        UpdateFooterButtons();
        UpdateItemSelectionVisuals();
    }

    private void UpdateFooterButtons()
    {
        if (BtnPrimaryAction == null || BtnClose == null || PanelSettings == null || TxtPrimaryAction == null || TxtClose == null) return;

        int unsyncedCount = GameItems.Count(g => !g.IsSynced);
        int selectedCount = GameItems.Count(g => !g.IsSynced && g.IsSelected);

        if (PanelSettings.Visibility == Visibility.Visible)
        {
            BtnPrimaryAction.Visibility = Visibility.Visible;
            TxtPrimaryAction.Text = _strings.SettingsSaveBackButton;
            TxtClose.Text = _strings.ActionClose;
        }
        else if (unsyncedCount > 0)
        {
            BtnPrimaryAction.Visibility = Visibility.Visible;
            TxtPrimaryAction.Text = selectedCount > 0
                ? string.Format(_strings.ActionAddSelectedFormat, selectedCount)
                : _strings.ActionSelectAll;
            TxtClose.Text = _strings.ActionClose;
        }
        else
        {
            BtnPrimaryAction.Visibility = Visibility.Collapsed;
            TxtClose.Text = _strings.ActionClose;
        }
    }

    private GameCardItem CreateGameCardItem(InstalledGame game, bool isSynced, bool isLight)
    {
        var brandCol = GetLauncherColor(game.Launcher, isLight);
        System.Windows.Media.Brush brandBrush = new SolidColorBrush(brandCol);
        System.Windows.Media.Brush iconBgBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(25, brandCol.R, brandCol.G, brandCol.B));
        
        ImageSource? iconSource = LoadGameIconSource(game);
        bool hasCustomIcon = (iconSource != null);

        var item = new GameCardItem
        {
            Game = game,
            Title = game.Title,
            LauncherName = game.Launcher,
            BrandBrush = brandBrush,
            IconBgBrush = iconBgBrush,
            IconSource = iconSource,
            HasCustomIcon = hasCustomIcon,
            IsSynced = isSynced,
            IsSelected = false,
            RowBorderBrush = System.Windows.Media.Brushes.Transparent,
            RowBorderThickness = new Thickness(1),
            RowBgBrush = (System.Windows.Media.Brush)Resources["LayerBrush"]
        };

        UpdateGameCardStatusVisuals(item, isLight);
        item.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(GameCardItem.IsSelected))
            {
                UpdateGameCardStatusVisuals(item, isLight);
                UpdateFooterButtons();
            }
        };

        return item;
    }

    private void UpdateGameCardStatusVisuals(GameCardItem item, bool isLight)
    {
        if (item.IsSynced)
        {
            item.StatusGlyph = "\uE73E"; // Subtle checkmark
            item.StatusText = _strings.StatusInSteam;
            
            System.Windows.Media.Color successCol = isLight 
                ? System.Windows.Media.Color.FromRgb(20, 130, 20) 
                : System.Windows.Media.Color.FromRgb(110, 205, 110);
            item.StatusBrush = new SolidColorBrush(successCol);
        }
        else if (item.IsSelected)
        {
            item.StatusGlyph = "\uF127";
            item.StatusText = _strings.BadgeSelected;

            System.Windows.Media.Color highContrastBlue = isLight 
                ? System.Windows.Media.Color.FromRgb(0, 100, 205) 
                : System.Windows.Media.Color.FromRgb(120, 205, 255);
            item.StatusBrush = new SolidColorBrush(highContrastBlue);
        }
        else
        {
            item.StatusGlyph = "";
            item.StatusText = _strings.StatusReadyToSync;
            item.StatusBrush = (System.Windows.Media.Brush)Resources["TextSecondaryBrush"];
        }
    }

    private void UpdateItemSelectionVisuals()
    {
        if (ListGames == null) return;
        int selectedIndex = ListGames.SelectedIndex;

        for (int i = 0; i < GameItems.Count; i++)
        {
            var item = GameItems[i];
            bool isFocused = (i == selectedIndex);

            // Distinct Focus Outline (for Gamepad and Keyboard navigation)
            if (isFocused)
            {
                item.RowBorderBrush = (System.Windows.Media.Brush)Resources["AccentBrush"];
                item.RowBorderThickness = new Thickness(1.5);
                item.RowBgBrush = (System.Windows.Media.Brush)Resources["CardHoverBrush"];
            }
            else
            {
                item.RowBorderBrush = System.Windows.Media.Brushes.Transparent;
                item.RowBorderThickness = new Thickness(1);
                item.RowBgBrush = (System.Windows.Media.Brush)Resources["LayerBrush"];
            }
        }
    }

    private void UpdateSettingsFocusVisuals()
    {
        if (BorderSettingToggle == null || BorderSettingApiKey == null || BorderSettingLanguage == null) return;

        // Reset all settings borders
        var defaultBorder = (System.Windows.Media.Brush)Resources["CardStrokeBrush"];
        var accentBorder = (System.Windows.Media.Brush)Resources["AccentBrush"];

        BorderSettingToggle.BorderBrush = (_settingsFocusIndex == 0) ? accentBorder : defaultBorder;
        BorderSettingToggle.BorderThickness = (_settingsFocusIndex == 0) ? new Thickness(1.5) : new Thickness(1);

        BorderSettingApiKey.BorderBrush = (_settingsFocusIndex >= 1 && _settingsFocusIndex <= 4) ? accentBorder : defaultBorder;
        BorderSettingApiKey.BorderThickness = (_settingsFocusIndex >= 1 && _settingsFocusIndex <= 4) ? new Thickness(1.5) : new Thickness(1);

        BorderSettingLanguage.BorderBrush = (_settingsFocusIndex == 5) ? accentBorder : defaultBorder;
        BorderSettingLanguage.BorderThickness = (_settingsFocusIndex == 5) ? new Thickness(1.5) : new Thickness(1);
    }

    private void ToggleSelectAll()
    {
        int unsynced = GameItems.Count(g => !g.IsSynced);
        if (unsynced > 0)
        {
            bool shouldSelectAll = GameItems.Count(g => !g.IsSynced && g.IsSelected) < unsynced;
            foreach (var g in GameItems.Where(g => !g.IsSynced))
            {
                g.IsSelected = shouldSelectAll;
            }
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr ExtractAssociatedIconW(IntPtr hInst, string lpIconPath, ref ushort lpiIcon);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private ImageSource? LoadGameIconSource(InstalledGame game)
    {
        try
        {
            // 1. Direct IconPath if exists and is .ico or .png
            if (!string.IsNullOrWhiteSpace(game.IconPath) && File.Exists(game.IconPath))
            {
                string ext = Path.GetExtension(game.IconPath).ToLowerInvariant();
                if (ext == ".ico" || ext == ".png")
                {
                    var uri = new Uri(Path.GetFullPath(game.IconPath));
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = uri;
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
                }
            }

            // 2. Search for dedicated game icon in InstallDirectory (e.g. goggame-*.ico, game.ico, icon.ico)
            if (!string.IsNullOrWhiteSpace(game.InstallDirectory) && Directory.Exists(game.InstallDirectory))
            {
                var icoFiles = Directory.GetFiles(game.InstallDirectory, "*.ico", SearchOption.TopDirectoryOnly);
                var bestIco = icoFiles.FirstOrDefault(f => Path.GetFileName(f).Contains("goggame", StringComparison.OrdinalIgnoreCase))
                           ?? icoFiles.FirstOrDefault(f => !Path.GetFileName(f).Contains("uninstall", StringComparison.OrdinalIgnoreCase));
                if (bestIco != null)
                {
                    var uri = new Uri(Path.GetFullPath(bestIco));
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = uri;
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
                }

                // Check subfolder bin\x64 for games like The Witcher 3 or Cyberpunk
                string bin64 = Path.Combine(game.InstallDirectory, "bin", "x64");
                if (Directory.Exists(bin64))
                {
                    var subExes = Directory.GetFiles(bin64, "*.exe", SearchOption.TopDirectoryOnly);
                    var mainSubExe = subExes.FirstOrDefault(e => !e.Contains("crash", StringComparison.OrdinalIgnoreCase));
                    if (mainSubExe != null)
                    {
                        var extracted = TryExtractIcon(mainSubExe);
                        if (extracted != null) return extracted;
                    }
                }
            }

            // 3. Extract icon from ExecutablePath
            if (!string.IsNullOrWhiteSpace(game.ExecutablePath) && File.Exists(game.ExecutablePath))
            {
                var extracted = TryExtractIcon(game.ExecutablePath);
                if (extracted != null) return extracted;
            }
        }
        catch { }

        // Return null so the sleek vector placeholder is displayed!
        return null;
    }

    private static ImageSource? TryExtractIcon(string exePath)
    {
        try
        {
            ushort idx = 0;
            IntPtr hIcon = ExtractAssociatedIconW(IntPtr.Zero, exePath, ref idx);
            if (hIcon != IntPtr.Zero)
            {
                try
                {
                    var bmp = Imaging.CreateBitmapSourceFromHIcon(hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    bmp.Freeze();
                    return bmp;
                }
                finally
                {
                    DestroyIcon(hIcon);
                }
            }
        }
        catch { }
        return null;
    }

    private void LoadWindowIcon()
    {
        try
        {
            string baseDir = AppContext.BaseDirectory;
            string[] candidatePaths =
            {
                Path.Combine(baseDir, "Assets", "Icons", "NonSteamLauncher.ico"),
                Path.Combine(baseDir, "Assets", "Icons", "NonSteamLauncher-B.ico"),
                Path.Combine(baseDir, "icon.ico"),
                Path.Combine(baseDir, "..", "..", "..", "Assets", "Icons", "NonSteamLauncher.ico"),
                Path.Combine(baseDir, "..", "..", "..", "Assets", "Icons", "NonSteamLauncher-B.ico"),
                Path.Combine(baseDir, "..", "..", "..", "icon.ico"),
                Path.Combine(baseDir, "..", "..", "..", "..", "icons", "Non-Steam-Launcher.ico")
            };

            foreach (var path in candidatePaths)
            {
                if (File.Exists(path))
                {
                    var uri = new Uri(Path.GetFullPath(path));
                    Icon = BitmapFrame.Create(uri);
                    return;
                }
            }
        }
        catch { }
    }

    private void TabGames_Checked(object sender, RoutedEventArgs e)
    {
        SwitchTab(0);
    }

    private void TabSettings_Checked(object sender, RoutedEventArgs e)
    {
        SwitchTab(1);
    }

    private void ComboLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingLanguage) return;

        if (ComboLanguage.SelectedItem is ComboBoxItem item && item.Tag is string langCode)
        {
            _appSettings.Language = langCode;
            SaveSettings();

            _strings = Localization.GetStrings(langCode);
            ApplyLocalization();
            UpdateInputModeVisuals();
            PopulateView(_currentResult);
        }
    }

    private void BtnPasteKey_Click(object sender, RoutedEventArgs e)
    {
        if (Clipboard.ContainsText())
        {
            TxtApiKey.Text = Clipboard.GetText().Trim();
            SaveSettings();
        }
    }

    private void BtnClearKey_Click(object sender, RoutedEventArgs e)
    {
        TxtApiKey.Text = string.Empty;
        SaveSettings();
    }

    private void BtnGetKey_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://www.steamgriddb.com/profile/preferences/api",
                UseShellExecute = true
            });
        }
        catch { }
    }

    private void SaveSettings()
    {
        _appSettings.DownloadArtwork = ChkDownloadArtwork.IsChecked ?? true;
        _appSettings.SteamGridDbApiKey = TxtApiKey.Text.Trim();
        _appSettings.Save(_settingsFilePath);
    }

    private void BtnPrimaryAction_Click(object sender, RoutedEventArgs e)
    {
        if (PanelSettings.Visibility == Visibility.Visible)
        {
            SaveSettings();
            SwitchTab(0);
            return;
        }

        int unsyncedCount = GameItems.Count(g => !g.IsSynced);
        int selectedCount = GameItems.Count(g => !g.IsSynced && g.IsSelected);

        if (selectedCount == 0 && unsyncedCount > 0)
        {
            // Select all
            foreach (var item in GameItems.Where(g => !g.IsSynced))
            {
                item.IsSelected = true;
            }
            return;
        }

        if (_onSyncSelected != null)
        {
            var toSync = GameItems.Where(g => !g.IsSynced && g.IsSelected).Select(g => g.Game).ToList();
            _currentResult = _onSyncSelected(toSync);
            PopulateView(_currentResult);
        }
        else
        {
            foreach (var item in GameItems.Where(g => !g.IsSynced && g.IsSelected))
            {
                item.IsSynced = true;
                item.IsSelected = false;
            }
            PopulateView(_currentResult);
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        SaveSettings();
        Close();
    }

    // =====================================================================
    // Gamepad / Joystick Polling & Real-Time Navigation
    // =====================================================================
    private void StartGamepadPolling()
    {
        _gamepadTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(33) // ~30 FPS
        };
        _gamepadTimer.Tick += GamepadTimer_Tick;
        _gamepadTimer.Start();
    }

    private void GamepadTimer_Tick(object? sender, EventArgs e)
    {
        if (_gamepadDirCooldown > 0)
            _gamepadDirCooldown--;

        _gamepadDetectCounter++;
        if (_gamepadDetectCounter >= 30)
        {
            _gamepadDetectCounter = 0;
            var connected = GamepadDetector.DetectConnectedGamepad();
            if (connected != GamepadType.Keyboard && _activeInputMode == ActiveInputMode.Keyboard)
            {
                // Controller plugged in, but keep keyboard until controller is touched
            }
        }

        if (!TryReadGamepadInput(out ushort buttons, out int dirX, out int dirY))
            return;

        // If any button or joystick axis is moved, immediately switch to Gamepad mode
        if (buttons != 0 || dirX != 0 || dirY != 0)
        {
            var family = GamepadDetector.DetectGamepadFamily();
            var targetMode = family == GamepadType.PlayStation 
                ? ActiveInputMode.PlayStation 
                : (family == GamepadType.Nintendo ? ActiveInputMode.Nintendo : ActiveInputMode.Xbox);

            if (_activeInputMode != targetMode)
            {
                SetInputMode(targetMode);
            }
        }

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

        // 1. Tab Switching via Bumpers (LB / L1 = Tab 0, RB / R1 = Tab 1)
        if ((newlyPressed & 0x0100) != 0) // LB / L1
        {
            SwitchTab(0);
            return;
        }
        if ((newlyPressed & 0x0200) != 0) // RB / R1
        {
            SwitchTab(1);
            return;
        }

        // 2. Navigation in Library vs Artwork Tabs
        if (dirY == 0 && dirX == 0)
        {
            _gamepadAxisReleased = true;
            _gamepadDirCooldown = 0;
        }
        else if (_gamepadAxisReleased || _gamepadDirCooldown == 0)
        {
            if (PanelGames.Visibility == Visibility.Visible && GameItems.Count > 0)
            {
                if (dirY != 0)
                {
                    int current = ListGames.SelectedIndex;
                    int next = Math.Clamp(current + dirY, 0, GameItems.Count - 1);

                    if (next != current)
                    {
                        ListGames.SelectedIndex = next;
                        ListGames.ScrollIntoView(ListGames.SelectedItem);
                    }
                }
            }
            else if (PanelSettings.Visibility == Visibility.Visible)
            {
                // Artwork Tab Joystick Navigation!
                if (dirY != 0)
                {
                    _settingsFocusIndex = Math.Clamp(_settingsFocusIndex + dirY, 0, 5);
                    UpdateSettingsFocusVisuals();
                }
                else if (dirX != 0 && _settingsFocusIndex >= 2 && _settingsFocusIndex <= 4)
                {
                    _settingsFocusIndex = Math.Clamp(_settingsFocusIndex + dirX, 2, 4);
                    UpdateSettingsFocusVisuals();
                }
            }

            _gamepadAxisReleased = false;
            _gamepadDirCooldown = 5;
        }

        // 3. Action Buttons (A / Cross, B / Circle, X / Square, Y / Triangle)
        // A (0x1000) = Toggle selection or activate setting control
        if ((newlyPressed & 0x1000) != 0)
        {
            if (PanelGames.Visibility == Visibility.Visible && ListGames.SelectedItem is GameCardItem item && !item.IsSynced)
            {
                item.IsSelected = !item.IsSelected;
            }
            else if (PanelSettings.Visibility == Visibility.Visible)
            {
                // Handle settings item activation
                if (_settingsFocusIndex == 0)
                {
                    ChkDownloadArtwork.IsChecked = !ChkDownloadArtwork.IsChecked;
                    SaveSettings();
                }
                else if (_settingsFocusIndex == 2)
                {
                    BtnPasteKey_Click(this, new RoutedEventArgs());
                }
                else if (_settingsFocusIndex == 3)
                {
                    BtnClearKey_Click(this, new RoutedEventArgs());
                }
                else if (_settingsFocusIndex == 4)
                {
                    BtnGetKey_Click(this, new RoutedEventArgs());
                }
                else if (_settingsFocusIndex == 5)
                {
                    ComboLanguage.SelectedIndex = (ComboLanguage.SelectedIndex == 0) ? 1 : 0;
                }
            }
            return;
        }

        // B (0x2000) = Close / Back
        if ((newlyPressed & 0x2000) != 0 || (newlyPressed & 0x0020) != 0)
        {
            if (PanelSettings.Visibility == Visibility.Visible)
            {
                SwitchTab(0);
            }
            else
            {
                Close();
            }
            return;
        }

        // X (0x4000) = Primary Action (Add / Sync)
        if ((newlyPressed & 0x4000) != 0 || (newlyPressed & 0x0010) != 0)
        {
            BtnPrimaryAction_Click(this, new RoutedEventArgs());
            return;
        }

        // Y (0x8000) = Select All / Deselect All
        if ((newlyPressed & 0x8000) != 0 && PanelGames.Visibility == Visibility.Visible)
        {
            ToggleSelectAll();
            return;
        }
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

    private bool TryReadGamepadInput(out ushort buttons, out int dirX, out int dirY)
    {
        buttons = 0;
        dirX = 0;
        dirY = 0;

        // 1. Try XInput
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
                catch
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

                if (err == 0)
                {
                    ushort b = state.Gamepad.wButtons;
                    buttons = b;

                    if ((b & 0x0001) != 0 || state.Gamepad.sThumbLY > 16000) dirY = -1;
                    else if ((b & 0x0002) != 0 || state.Gamepad.sThumbLY < -16000) dirY = 1;

                    if ((b & 0x0004) != 0 || state.Gamepad.sThumbLX < -16000) dirX = -1;
                    else if ((b & 0x0008) != 0 || state.Gamepad.sThumbLX > 16000) dirX = 1;

                    return true;
                }
            }
        }

        // 2. Fallback to WinMM (PlayStation DualSense/DualShock & DirectInput)
        if (!_winmmFailed)
        {
            for (uint j = 0; j < 4; j++)
            {
                try
                {
                    var ji = new JOYINFOEX
                    {
                        dwSize = (uint)Marshal.SizeOf<JOYINFOEX>(),
                        dwFlags = 0x000000FF // JOY_RETURNALL
                    };

                    if (joyGetPosEx(j, ref ji) == 0)
                    {
                        ushort mapped = 0;
                        if (_activeInputMode == ActiveInputMode.PlayStation)
                        {
                            if ((ji.dwButtons & 0x0002) != 0) mapped |= 0x1000; // Cross -> A
                            if ((ji.dwButtons & 0x0004) != 0) mapped |= 0x2000; // Circle -> B
                            if ((ji.dwButtons & 0x0001) != 0) mapped |= 0x4000; // Square -> X
                            if ((ji.dwButtons & 0x0008) != 0) mapped |= 0x8000; // Triangle -> Y
                        }
                        else
                        {
                            if ((ji.dwButtons & 0x0001) != 0) mapped |= 0x1000;
                            if ((ji.dwButtons & 0x0002) != 0) mapped |= 0x2000;
                            if ((ji.dwButtons & 0x0004) != 0) mapped |= 0x4000;
                            if ((ji.dwButtons & 0x0008) != 0) mapped |= 0x8000;
                        }

                        // L1 and R1 Bumpers (Button 5 = L1, Button 6 = R1)
                        if ((ji.dwButtons & 0x0010) != 0) mapped |= 0x0100; // L1 / LB
                        if ((ji.dwButtons & 0x0020) != 0) mapped |= 0x0200; // R1 / RB

                        // Share / Options
                        if ((ji.dwButtons & 0x0100) != 0) mapped |= 0x0020; // Back / Share
                        if ((ji.dwButtons & 0x0200) != 0) mapped |= 0x0010; // Start / Options

                        buttons = mapped;

                        if (ji.dwPOV != 0xFFFF && ji.dwPOV <= 36000)
                        {
                            if (ji.dwPOV >= 31500 || ji.dwPOV <= 4500) dirY = -1;
                            else if (ji.dwPOV >= 13500 && ji.dwPOV <= 22500) dirY = 1;

                            if (ji.dwPOV >= 4500 && ji.dwPOV <= 13500) dirX = 1;
                            else if (ji.dwPOV >= 22500 && ji.dwPOV <= 31500) dirX = -1;
                        }

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

    private void ApplyDwmAttributes()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;

        bool isLight = QuerySystemLightModePreference();
        int useDarkMode = isLight ? 0 : 1;
        DwmSetWindowAttribute(hwnd, 20 /* DWMWA_USE_IMMERSIVE_DARK_MODE */, ref useDarkMode, sizeof(int));

        int cornerPref = 2 /* DWMWCP_ROUND (8px) */;
        DwmSetWindowAttribute(hwnd, 33 /* DWMWA_WINDOW_CORNER_PREFERENCE */, ref cornerPref, sizeof(int));

        int backdropMica = 2 /* DWMSBT_MAINWINDOW (Mica) */;
        int hr = DwmSetWindowAttribute(hwnd, 38 /* DWMWA_SYSTEMBACKDROP_TYPE */, ref backdropMica, sizeof(int));
        if (hr != 0)
        {
            // Fallback for Windows 11 Build 22000
            int micaTrue = 1;
            DwmSetWindowAttribute(hwnd, 1029 /* DWMWA_MICA_EFFECT */, ref micaTrue, sizeof(int));
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private static bool QuerySystemLightModePreference()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key?.GetValue("AppsUseLightTheme") is int val)
            {
                return val != 0;
            }
        }
        catch { }
        return true;
    }

    private static System.Windows.Media.Color QuerySystemAccentColor(bool isLight)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent");
            if (key?.GetValue("AccentPalette") is byte[] palette && palette.Length >= 24)
            {
                int offset = isLight ? 4 * 4 : 1 * 4;
                return System.Windows.Media.Color.FromRgb(palette[offset], palette[offset + 1], palette[offset + 2]);
            }
        }
        catch { }
        return System.Windows.Media.Color.FromRgb(0, 103, 192);
    }

    private static System.Windows.Media.Color BlendColor(System.Windows.Media.Color fg, System.Windows.Media.Color bg, double weight)
    {
        byte r = (byte)Math.Clamp(Math.Round(fg.R * weight + bg.R * (1.0 - weight)), 0, 255);
        byte g = (byte)Math.Clamp(Math.Round(fg.G * weight + bg.G * (1.0 - weight)), 0, 255);
        byte b = (byte)Math.Clamp(Math.Round(fg.B * weight + bg.B * (1.0 - weight)), 0, 255);
        return System.Windows.Media.Color.FromRgb(r, g, b);
    }

    private static System.Windows.Media.Color GetLauncherColor(string launcher, bool isLight) => launcher.ToLowerInvariant() switch
    {
        "epic games" or "epic" => System.Windows.Media.Color.FromRgb(0, 120, 242),
        "ea app" or "ea" or "origin" => System.Windows.Media.Color.FromRgb(255, 71, 71),
        "ubisoft connect" or "ubisoft" or "uplay" => System.Windows.Media.Color.FromRgb(0, 176, 244),
        "gog galaxy" or "gog" => isLight ? System.Windows.Media.Color.FromRgb(139, 61, 255) : System.Windows.Media.Color.FromRgb(180, 115, 255),
        "battle.net" or "battlenet" or "blizzard" => System.Windows.Media.Color.FromRgb(0, 174, 255),
        "xbox" or "microsoft store" or "game pass" => System.Windows.Media.Color.FromRgb(16, 124, 16),
        _ => isLight ? System.Windows.Media.Color.FromRgb(100, 100, 100) : System.Windows.Media.Color.FromRgb(160, 160, 160)
    };
}

public class GameCardItem : INotifyPropertyChanged
{
    public required InstalledGame Game { get; init; }
    public required string Title { get; init; }
    public required string LauncherName { get; init; }
    public required System.Windows.Media.Brush BrandBrush { get; init; }
    public required System.Windows.Media.Brush IconBgBrush { get; init; }
    public ImageSource? IconSource { get; init; }
    public bool HasCustomIcon { get; init; }

    public Visibility IconVisibility => HasCustomIcon ? Visibility.Visible : Visibility.Collapsed;
    public Visibility PlaceholderVisibility => HasCustomIcon ? Visibility.Collapsed : Visibility.Visible;

    private bool _isSynced;
    public bool IsSynced
    {
        get => _isSynced;
        set
        {
            _isSynced = value;
            OnPropertyChanged(nameof(IsSynced));
            OnPropertyChanged(nameof(CheckBoxVisibility));
        }
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            OnPropertyChanged(nameof(IsSelected));
        }
    }

    public Visibility CheckBoxVisibility => IsSynced ? Visibility.Collapsed : Visibility.Visible;

    private string _statusGlyph = "";
    public string StatusGlyph
    {
        get => _statusGlyph;
        set { _statusGlyph = value; OnPropertyChanged(nameof(StatusGlyph)); }
    }

    private string _statusText = "";
    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(nameof(StatusText)); }
    }

    private System.Windows.Media.Brush _statusBrush = System.Windows.Media.Brushes.Gray;
    public System.Windows.Media.Brush StatusBrush
    {
        get => _statusBrush;
        set { _statusBrush = value; OnPropertyChanged(nameof(StatusBrush)); }
    }

    private System.Windows.Media.Brush _rowBgBrush = System.Windows.Media.Brushes.Transparent;
    public System.Windows.Media.Brush RowBgBrush
    {
        get => _rowBgBrush;
        set { _rowBgBrush = value; OnPropertyChanged(nameof(RowBgBrush)); }
    }

    private System.Windows.Media.Brush _rowBorderBrush = System.Windows.Media.Brushes.Transparent;
    public System.Windows.Media.Brush RowBorderBrush
    {
        get => _rowBorderBrush;
        set { _rowBorderBrush = value; OnPropertyChanged(nameof(RowBorderBrush)); }
    }

    private Thickness _rowBorderThickness = new Thickness(1);
    public Thickness RowBorderThickness
    {
        get => _rowBorderThickness;
        set { _rowBorderThickness = value; OnPropertyChanged(nameof(RowBorderThickness)); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string prop) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
}
