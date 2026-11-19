using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using NonSteamLauncher.Sync.Steam;

namespace NonSteamLauncher.Sync.UI;

public partial class ArtworkPickerWindow : Window
{
    private static readonly ArtworkPieceType[] OrderedPieces =
    {
        ArtworkPieceType.Poster,
        ArtworkPieceType.Wide,
        ArtworkPieceType.Hero,
        ArtworkPieceType.Logo,
        ArtworkPieceType.Icon
    };

    private static readonly HttpClient PreviewHttpClient = CreatePreviewHttpClient();

    private readonly IReadOnlyList<GameArtworkOptions> _gameOptions;
    private readonly LocalizedStrings _strings;
    private readonly Dictionary<(int GameIdx, ArtworkPieceType Piece), int?> _selectedIndices = new();
    private readonly ConcurrentDictionary<string, BitmapSource> _imageCache = new(StringComparer.OrdinalIgnoreCase);

    private int _currentGameIndex;
    private ArtworkPieceType _currentPiece = ArtworkPieceType.Poster;
    private bool _isUpdatingUi;
    private CancellationTokenSource? _loadCts;

    // Input & Gamepad polling (XInput + WinMM for PlayStation/Nintendo/DirectInput)
    private ActiveInputMode _activeInputMode = ActiveInputMode.Keyboard;
    private DispatcherTimer? _gamepadTimer;
    private ushort _prevGamepadButtons;
    private bool _gamepadInitialized;
    private bool _gamepadAxisReleased = true;
    private int _gamepadDirCooldown;
    private bool _xinputFailed;
    private bool _winmmFailed;
    private int _cachedWinmmJoyId = -1;
    private Point _lastMousePos;
    private bool _hasMouseMovedOnce;

    public bool Confirmed { get; private set; }
    public int CurrentGameIndex => _currentGameIndex;
    public ArtworkPieceType CurrentPieceType => _currentPiece;
    public int CurrentOptionCount => GetCurrentCandidates().Count;

    public ArtworkPickerWindow(
        IReadOnlyList<GameArtworkOptions> gameOptions,
        LocalizedStrings strings,
        ActiveInputMode initialInputMode = ActiveInputMode.Keyboard)
    {
        InitializeComponent();

        _gameOptions = gameOptions ?? Array.Empty<GameArtworkOptions>();
        _strings = strings ?? Localization.GetStrings();
        _activeInputMode = initialInputMode;

        if (_activeInputMode == ActiveInputMode.Keyboard)
        {
            var detected = GamepadDetector.DetectConnectedGamepad();
            if (detected == GamepadType.PlayStation) _activeInputMode = ActiveInputMode.PlayStation;
            else if (detected == GamepadType.Nintendo) _activeInputMode = ActiveInputMode.Nintendo;
            else if (detected == GamepadType.Xbox) _activeInputMode = ActiveInputMode.Xbox;
        }

        // Pre-select the top-ranked option (#1) for every piece that has candidates
        for (int g = 0; g < _gameOptions.Count; g++)
        {
            foreach (var piece in OrderedPieces)
            {
                var list = _gameOptions[g].Pieces.TryGetValue(piece, out var cands) ? cands : null;
                _selectedIndices[(g, piece)] = (list is { Count: > 0 }) ? 0 : null;
            }
        }

        // Start on the first piece that actually has options for game 0
        if (_gameOptions.Count > 0)
        {
            foreach (var piece in OrderedPieces)
            {
                if (_gameOptions[0].Pieces.TryGetValue(piece, out var list) && list.Count > 0)
                {
                    _currentPiece = piece;
                    break;
                }
            }
        }

        ClampToWorkArea();
        ApplySystemFluentTheme();
        ApplyLocalization();
        UpdateInputModeVisuals();
        RenderCurrentView();

        SourceInitialized += (_, _) =>
        {
            ApplyDwmAttributes();
            AttachSystemChangeHook();
            StartGamepadPolling();
        };

        Closed += (_, _) =>
        {
            _loadCts?.Cancel();
            _loadCts?.Dispose();
            _gamepadTimer?.Stop();
        };

        PreviewMouseMove += (_, e) =>
        {
            var pos = e.GetPosition(this);
            if (!_hasMouseMovedOnce)
            {
                _hasMouseMovedOnce = true;
                _lastMousePos = pos;
                return;
            }
            if (Math.Abs(pos.X - _lastMousePos.X) > 6 || Math.Abs(pos.Y - _lastMousePos.Y) > 6)
            {
                _lastMousePos = pos;
                SetInputMode(ActiveInputMode.Keyboard);
            }
        };

        PreviewMouseDown += (_, _) => SetInputMode(ActiveInputMode.Keyboard);
        PreviewKeyDown += OnWindowPreviewKeyDown;
    }

    private static HttpClient CreatePreviewHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("NonSteamLauncher/1.9.0");
        return client;
    }

    public IReadOnlyList<GameArtworkSelection> GetSelections()
    {
        var list = new List<GameArtworkSelection>();
        for (int g = 0; g < _gameOptions.Count; g++)
        {
            var gameOpt = _gameOptions[g];
            var sel = new GameArtworkSelection
            {
                Game = gameOpt.Game,
                AppId = gameOpt.AppId
            };

            foreach (var piece in OrderedPieces)
            {
                if (_selectedIndices.TryGetValue((g, piece), out int? chosenIdx) &&
                    chosenIdx.HasValue &&
                    gameOpt.Pieces.TryGetValue(piece, out var candidates) &&
                    chosenIdx.Value >= 0 && chosenIdx.Value < candidates.Count)
                {
                    sel.SelectedPieces[piece] = candidates[chosenIdx.Value];
                }
                else
                {
                    sel.SelectedPieces[piece] = null;
                }
            }

            list.Add(sel);
        }

        return list;
    }

    public void SelectPiece(ArtworkPieceType pieceType)
    {
        _currentPiece = pieceType;
        RenderCurrentView();
    }

    public void SelectOptionIndex(int index)
    {
        var candidates = GetCurrentCandidates();
        if (index < 0 || index >= candidates.Count)
            return;

        _selectedIndices[(_currentGameIndex, _currentPiece)] = index;
        RenderCurrentView();
    }

    private void ClampToWorkArea()
    {
        var work = SystemParameters.WorkArea;
        if (work.Width > 0 && work.Height > 0)
        {
            MaxWidth = Math.Max(MinWidth, work.Width - 32);
            MaxHeight = Math.Max(MinHeight, work.Height - 32);
            Width = Math.Min(Width, MaxWidth);
            Height = Math.Min(Height, MaxHeight);
        }
    }

    private void ApplyLocalization()
    {
        Title = _strings.ArtworkPickerTitle;
        TxtTitle.Text = _strings.ArtworkPickerTitle;
        TxtSubtitle.Text = _strings.ArtworkPickerSubtitle;
        TxtTabPoster.Text = _strings.ArtworkPiecePoster;
        TxtTabWide.Text = _strings.ArtworkPieceWide;
        TxtTabHero.Text = _strings.ArtworkPieceHero;
        TxtTabLogo.Text = _strings.ArtworkPieceLogo;
        TxtTabIcon.Text = _strings.ArtworkPieceIcon;
        TxtSkipPiece.Text = _strings.ArtworkPickerSkipPiece;
        TxtNoOptions.Text = _strings.ArtworkPickerNoOptions;
        TxtSkipAllLabel.Text = _strings.ArtworkPickerSkipAllButton;
        TxtApplyLabel.Text = _strings.ArtworkPickerApplyButton;
    }

    private void SetInputMode(ActiveInputMode mode)
    {
        if (_activeInputMode == mode) return;
        _activeInputMode = mode;
        UpdateInputModeVisuals();
    }

    private void UpdateInputModeVisuals()
    {
        if (IconActiveDevice == null || TxtActiveDevice == null || TxtFooterHint == null)
            return;

        bool multiGame = _gameOptions.Count > 1;
        switch (_activeInputMode)
        {
            case ActiveInputMode.PlayStation:
                IconActiveDevice.Text = "\uE7FC";
                TxtActiveDevice.Text = _strings.InputDevicePlayStation;
                TxtApplyBadge.Text = "▢";
                TxtSkipAllBadge.Text = "○";
                TxtFooterHint.Text = multiGame
                    ? "Stick/D-Pad: elegir • L1/R1: pieza • L2/R2: juego • ✕: siguiente • △: omitir pieza • ▢: aplicar • ○: salir"
                    : "Stick/D-Pad: elegir • L1/R1: pieza • ✕: siguiente • △: omitir pieza • ▢: aplicar • ○: salir";
                break;

            case ActiveInputMode.Nintendo:
                IconActiveDevice.Text = "\uE7FC";
                TxtActiveDevice.Text = _strings.InputDeviceNintendo;
                TxtApplyBadge.Text = "Y";
                TxtSkipAllBadge.Text = "B";
                TxtFooterHint.Text = multiGame
                    ? "Stick/D-Pad: elegir • L/R: pieza • ZL/ZR: juego • A: siguiente • X: omitir pieza • Y: aplicar • B: salir"
                    : "Stick/D-Pad: elegir • L/R: pieza • A: siguiente • X: omitir pieza • Y: aplicar • B: salir";
                break;

            case ActiveInputMode.Xbox:
                IconActiveDevice.Text = "\uE7FC";
                TxtActiveDevice.Text = _strings.InputDeviceXbox;
                TxtApplyBadge.Text = "X";
                TxtSkipAllBadge.Text = "B";
                TxtFooterHint.Text = multiGame
                    ? "Stick/D-Pad: elegir • LB/RB: pieza • LT/RT: juego • A: siguiente • Y: omitir pieza • X: aplicar • B: salir"
                    : "Stick/D-Pad: elegir • LB/RB: pieza • A: siguiente • Y: omitir pieza • X: aplicar • B: salir";
                break;

            default:
                IconActiveDevice.Text = "\uE765";
                TxtActiveDevice.Text = _strings.InputDeviceKeyboard;
                TxtApplyBadge.Text = _strings.KeyNameEnter;
                TxtSkipAllBadge.Text = _strings.KeyNameEsc;
                TxtFooterHint.Text = _strings.ArtworkPickerHintLegend;
                break;
        }
    }

    private List<ArtworkCandidate> GetCurrentCandidates()
    {
        if (_currentGameIndex < 0 || _currentGameIndex >= _gameOptions.Count)
            return new List<ArtworkCandidate>();

        return _gameOptions[_currentGameIndex].Pieces.TryGetValue(_currentPiece, out var list)
            ? list
            : new List<ArtworkCandidate>();
    }

    private void RenderCurrentView()
    {
        if (_gameOptions.Count == 0)
        {
            PanelNoOptions.Visibility = Visibility.Visible;
            GridOptions.Visibility = Visibility.Collapsed;
            return;
        }

        _isUpdatingUi = true;
        try
        {
            var gameOpt = _gameOptions[_currentGameIndex];

            // Header & Stepper
            TxtGameTitle.Text = gameOpt.Game.Title;
            TxtGameLauncher.Text = gameOpt.Game.Launcher;

            if (_gameOptions.Count > 1)
            {
                PanelGameStepper.Visibility = Visibility.Visible;
                TxtGameProgress.Text = string.Format(_strings.ArtworkPickerGameProgressFormat, _currentGameIndex + 1, _gameOptions.Count);
                BtnPrevGame.IsEnabled = _currentGameIndex > 0;
                BtnNextGame.IsEnabled = _currentGameIndex < _gameOptions.Count - 1;
            }
            else
            {
                PanelGameStepper.Visibility = Visibility.Collapsed;
            }

            // Update Piece Tabs
            UpdatePieceTabVisual(ArtworkPieceType.Poster, TabPoster, IconTabPoster, CountTabPoster, gameOpt);
            UpdatePieceTabVisual(ArtworkPieceType.Wide, TabWide, IconTabWide, CountTabWide, gameOpt);
            UpdatePieceTabVisual(ArtworkPieceType.Hero, TabHero, IconTabHero, CountTabHero, gameOpt);
            UpdatePieceTabVisual(ArtworkPieceType.Logo, TabLogo, IconTabLogo, CountTabLogo, gameOpt);
            UpdatePieceTabVisual(ArtworkPieceType.Icon, TabIcon, IconTabIcon, CountTabIcon, gameOpt);

            var candidates = GetCurrentCandidates();
            int? selectedIdx = _selectedIndices.TryGetValue((_currentGameIndex, _currentPiece), out var s) ? s : null;

            ChkSkipPiece.IsEnabled = candidates.Count > 0;
            ChkSkipPiece.IsChecked = candidates.Count > 0 && !selectedIdx.HasValue;

            // Cancel previous image loads
            _loadCts?.Cancel();
            _loadCts?.Dispose();
            _loadCts = new CancellationTokenSource();
            var ct = _loadCts.Token;

            GridOptions.Children.Clear();

            if (candidates.Count == 0)
            {
                PanelNoOptions.Visibility = Visibility.Visible;
                GridOptions.Visibility = Visibility.Collapsed;
                return;
            }

            PanelNoOptions.Visibility = Visibility.Collapsed;
            GridOptions.Visibility = Visibility.Visible;

            // Optimal columns for aspect ratio: 4 columns for vertical Posters & square Icons; 2x2 for Wide, Hero, Logo
            GridOptions.Columns = (_currentPiece == ArtworkPieceType.Poster || _currentPiece == ArtworkPieceType.Icon)
                ? Math.Min(4, Math.Max(1, candidates.Count))
                : Math.Min(2, Math.Max(1, candidates.Count));

            for (int i = 0; i < candidates.Count; i++)
            {
                bool isSelected = selectedIdx.HasValue && selectedIdx.Value == i;
                var card = BuildCandidateCard(candidates[i], i, isSelected, ct);
                GridOptions.Children.Add(card);
            }
        }
        finally
        {
            _isUpdatingUi = false;
        }
    }

    private void UpdatePieceTabVisual(
        ArtworkPieceType piece,
        RadioButton tab,
        TextBlock icon,
        TextBlock countText,
        GameArtworkOptions gameOpt)
    {
        tab.IsChecked = (_currentPiece == piece);
        int count = gameOpt.Pieces.TryGetValue(piece, out var list) ? list.Count : 0;
        countText.Text = $"({count})";

        bool hasSelection = _selectedIndices.TryGetValue((_currentGameIndex, piece), out var sel) && sel.HasValue && count > 0;
        icon.Visibility = hasSelection ? Visibility.Visible : Visibility.Collapsed;
        tab.Opacity = count > 0 ? 1.0 : 0.55;
    }

    private Border BuildCandidateCard(ArtworkCandidate candidate, int index, bool isSelected, CancellationToken ct)
    {
        var accentBrush = (Brush)Resources["AccentBrush"];
        var cardBrush = (Brush)Resources["CardBrush"];
        var cardHoverBrush = (Brush)Resources["CardHoverBrush"];
        var strokeBrush = (Brush)Resources["CardStrokeBrush"];
        var layerBrush = (Brush)Resources["LayerBrush"];
        var textPrimary = (Brush)Resources["TextPrimaryBrush"];
        var textSecondary = (Brush)Resources["TextSecondaryBrush"];
        var textOnAccent = (Brush)Resources["TextOnAccentBrush"];

        var cardBorder = new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = cardBrush,
            BorderBrush = isSelected ? accentBrush : strokeBrush,
            BorderThickness = isSelected ? new Thickness(2) : new Thickness(1),
            Margin = new Thickness(6),
            Padding = new Thickness(8),
            Cursor = Cursors.Hand,
            SnapsToDevicePixels = true
        };

        cardBorder.MouseEnter += (_, _) =>
        {
            if (!isSelected)
                cardBorder.Background = cardHoverBrush;
        };
        cardBorder.MouseLeave += (_, _) =>
        {
            cardBorder.Background = cardBrush;
        };
        cardBorder.MouseLeftButtonDown += (_, _) =>
        {
            _selectedIndices[(_currentGameIndex, _currentPiece)] = index;
            RenderCurrentView();
        };

        var rootGrid = new Grid();
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Image preview stage (neutral dark surface for transparent Logos & Icons so white/black artwork are both visible)
        Brush stageBg = (_currentPiece == ArtworkPieceType.Logo || _currentPiece == ArtworkPieceType.Icon)
            ? new SolidColorBrush(Color.FromRgb(42, 46, 54))
            : layerBrush;

        var stageBorder = new Border
        {
            CornerRadius = new CornerRadius(6),
            Background = stageBg,
            ClipToBounds = true,
            Padding = (_currentPiece == ArtworkPieceType.Logo || _currentPiece == ArtworkPieceType.Icon)
                ? new Thickness(12)
                : new Thickness(0)
        };

        var stageGrid = new Grid();

        var loadingText = new TextBlock
        {
            Text = _strings.ArtworkPickerLoading,
            FontSize = 12,
            Foreground = textSecondary,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        stageGrid.Children.Add(loadingText);

        var img = new Image
        {
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
        stageGrid.Children.Add(img);

        // Selected badge in top-right corner
        if (isSelected)
        {
            var badge = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(12),
                Background = accentBrush,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(6)
            };
            badge.Child = new TextBlock
            {
                Text = "\uE73E",
                FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets, sans-serif"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = textOnAccent,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            stageGrid.Children.Add(badge);
        }

        stageBorder.Child = stageGrid;
        Grid.SetRow(stageBorder, 0);
        rootGrid.Children.Add(stageBorder);

        // Metadata footer (Caption 12px, Sentence case)
        var metaGrid = new Grid { Margin = new Thickness(2, 8, 2, 0) };
        metaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        metaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var leftMeta = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        leftMeta.Children.Add(new TextBlock
        {
            Text = $"#{index + 1}",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = isSelected ? accentBrush : textPrimary,
            Margin = new Thickness(0, 0, 6, 0)
        });

        string detailText = BuildCandidateDetailText(candidate);
        if (!string.IsNullOrEmpty(detailText))
        {
            leftMeta.Children.Add(new TextBlock
            {
                Text = detailText,
                FontSize = 11.5,
                Foreground = textSecondary,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
        }
        Grid.SetColumn(leftMeta, 0);
        metaGrid.Children.Add(leftMeta);

        if (candidate.Score > 0)
        {
            var scorePanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) };
            scorePanel.Children.Add(new TextBlock
            {
                Text = "\uE735",
                FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets, sans-serif"),
                FontSize = 10.5,
                Foreground = accentBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 3, 0)
            });
            scorePanel.Children.Add(new TextBlock
            {
                Text = candidate.Score.ToString(),
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = textSecondary,
                VerticalAlignment = VerticalAlignment.Center
            });
            Grid.SetColumn(scorePanel, 1);
            metaGrid.Children.Add(scorePanel);
        }

        Grid.SetRow(metaGrid, 1);
        rootGrid.Children.Add(metaGrid);

        cardBorder.Child = rootGrid;

        // Load preview image asynchronously
        string previewUrl = !string.IsNullOrWhiteSpace(candidate.ThumbUrl) ? candidate.ThumbUrl : candidate.Url;
        _ = LoadPreviewImageAsync(previewUrl, candidate.Url, img, loadingText, ct);

        return cardBorder;
    }

    private static string BuildCandidateDetailText(ArtworkCandidate candidate)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(candidate.DimensionsText))
            parts.Add(candidate.DimensionsText);
        if (!string.IsNullOrEmpty(candidate.Style))
            parts.Add(candidate.Style);
        else if (!string.IsNullOrEmpty(candidate.Source))
            parts.Add(candidate.Source);

        return string.Join(" • ", parts);
    }

    private async Task LoadPreviewImageAsync(string primaryUrl, string fallbackUrl, Image targetImage, TextBlock loadingLabel, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(primaryUrl))
        {
            loadingLabel.Visibility = Visibility.Collapsed;
            return;
        }

        if (_imageCache.TryGetValue(primaryUrl, out var cached))
        {
            targetImage.Source = cached;
            loadingLabel.Visibility = Visibility.Collapsed;
            return;
        }

        try
        {
            byte[]? bytes = await TryDownloadPreviewBytesAsync(primaryUrl, ct);
            if ((bytes == null || bytes.Length < 64) && !string.Equals(primaryUrl, fallbackUrl, StringComparison.OrdinalIgnoreCase))
            {
                bytes = await TryDownloadPreviewBytesAsync(fallbackUrl, ct);
            }

            if (ct.IsCancellationRequested || bytes == null || bytes.Length < 64)
            {
                if (!ct.IsCancellationRequested)
                {
                    await Dispatcher.InvokeAsync(() => loadingLabel.Visibility = Visibility.Collapsed);
                }
                return;
            }

            BitmapSource? bmp = await Task.Run(() =>
            {
                try
                {
                    using var ms = new MemoryStream(bytes);
                    var img = new BitmapImage();
                    img.BeginInit();
                    img.CacheOption = BitmapCacheOption.OnLoad;
                    img.DecodePixelWidth = 420;
                    img.StreamSource = ms;
                    img.EndInit();
                    img.Freeze();
                    return (BitmapSource)img;
                }
                catch
                {
                    return null;
                }
            }, ct);

            if (ct.IsCancellationRequested || bmp == null)
                return;

            _imageCache[primaryUrl] = bmp;

            await Dispatcher.InvokeAsync(() =>
            {
                if (!ct.IsCancellationRequested)
                {
                    targetImage.Source = bmp;
                    loadingLabel.Visibility = Visibility.Collapsed;
                }
            });
        }
        catch
        {
            if (!ct.IsCancellationRequested)
            {
                await Dispatcher.InvokeAsync(() => loadingLabel.Visibility = Visibility.Collapsed);
            }
        }
    }

    private static async Task<byte[]?> TryDownloadPreviewBytesAsync(string url, CancellationToken ct)
    {
        try
        {
            using var resp = await PreviewHttpClient.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode)
                return null;
            return await resp.Content.ReadAsByteArrayAsync(ct);
        }
        catch
        {
            return null;
        }
    }

    private void PieceTab_Checked(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingUi) return;
        if (sender is RadioButton rb && rb.Tag is string tag && Enum.TryParse<ArtworkPieceType>(tag, out var piece))
        {
            _currentPiece = piece;
            RenderCurrentView();
        }
    }

    private void ChkSkipPiece_Changed(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingUi) return;
        var candidates = GetCurrentCandidates();
        if (candidates.Count == 0) return;

        if (ChkSkipPiece.IsChecked == true)
        {
            _selectedIndices[(_currentGameIndex, _currentPiece)] = null;
        }
        else
        {
            _selectedIndices[(_currentGameIndex, _currentPiece)] = 0;
        }

        RenderCurrentView();
    }

    private void BtnPrevGame_Click(object sender, RoutedEventArgs e)
    {
        if (_currentGameIndex > 0)
        {
            _currentGameIndex--;
            RenderCurrentView();
        }
    }

    private void BtnNextGame_Click(object sender, RoutedEventArgs e)
    {
        if (_currentGameIndex < _gameOptions.Count - 1)
        {
            _currentGameIndex++;
            RenderCurrentView();
        }
    }

    private void BtnApply_Click(object sender, RoutedEventArgs e)
    {
        Confirmed = true;
        DialogResult = true;
        Close();
    }

    private void BtnSkipAll_Click(object sender, RoutedEventArgs e)
    {
        Confirmed = false;
        DialogResult = false;
        Close();
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        SetInputMode(ActiveInputMode.Keyboard);

        if (e.Key == Key.Escape)
        {
            BtnSkipAll_Click(this, new RoutedEventArgs());
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            BtnApply_Click(this, new RoutedEventArgs());
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Tab || e.Key == Key.E)
        {
            int dir = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift ? -1 : 1;
            StepPiece(dir);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Q)
        {
            StepPiece(-1);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.PageDown)
        {
            BtnNextGame_Click(this, new RoutedEventArgs());
            e.Handled = true;
            return;
        }

        if (e.Key == Key.PageUp)
        {
            BtnPrevGame_Click(this, new RoutedEventArgs());
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Up || e.Key == Key.Down)
        {
            MoveOptionSelection(e.Key);
            e.Handled = true;
        }
    }

    private void StepPiece(int delta)
    {
        int idx = Array.IndexOf(OrderedPieces, _currentPiece);
        int next = (idx + delta + OrderedPieces.Length) % OrderedPieces.Length;
        _currentPiece = OrderedPieces[next];
        RenderCurrentView();
    }

    private void MoveOptionSelection(Key key)
    {
        var candidates = GetCurrentCandidates();
        if (candidates.Count == 0) return;

        int cols = Math.Max(1, GridOptions.Columns);
        int cur = _selectedIndices.TryGetValue((_currentGameIndex, _currentPiece), out var sel) && sel.HasValue
            ? sel.Value
            : 0;

        int next = key switch
        {
            Key.Left => Math.Max(0, cur - 1),
            Key.Right => Math.Min(candidates.Count - 1, cur + 1),
            Key.Up => Math.Max(0, cur - cols),
            Key.Down => Math.Min(candidates.Count - 1, cur + cols),
            _ => cur
        };

        _selectedIndices[(_currentGameIndex, _currentPiece)] = next;
        RenderCurrentView();
    }

    // =====================================================================
    // Gamepad Polling (XInput 1.4 / 9.1.0 + WinMM PlayStation/Nintendo/DirectInput)
    // =====================================================================
    // Bitmask extensions for Triggers (LT/L2 = 0x0400, RT/R2 = 0x0800)
    private const ushort BTN_LT = 0x0400;
    private const ushort BTN_RT = 0x0800;

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

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct JOYCAPSW
    {
        public ushort wMid;
        public ushort wPid;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szPname;
        public uint wXmin;
        public uint wXmax;
        public uint wYmin;
        public uint wYmax;
        public uint wZmin;
        public uint wZmax;
        public uint wNumButtons;
        public uint wPeriodMin;
        public uint wPeriodMax;
        public uint wRmin;
        public uint wRmax;
        public uint wUmin;
        public uint wUmax;
        public uint wVmin;
        public uint wVmax;
        public uint wCaps;
        public uint wMaxAxes;
        public uint wNumAxes;
        public uint wMaxButtons;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szRegKey;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szOEMVxD;
    }

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    private static extern uint XInputGetState14(uint dwUserIndex, out XINPUT_STATE pState);

    [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")]
    private static extern uint XInputGetState910(uint dwUserIndex, out XINPUT_STATE pState);

    [DllImport("winmm.dll", EntryPoint = "joyGetDevCapsW", CharSet = CharSet.Unicode)]
    private static extern uint joyGetDevCapsW(uint uJoyID, ref JOYCAPSW pjc, uint cbjc);

    [DllImport("winmm.dll")]
    private static extern uint joyGetPosEx(uint uJoyID, ref JOYINFOEX pji);

    private void StartGamepadPolling()
    {
        _gamepadInitialized = false;
        _gamepadTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(33)
        };
        _gamepadTimer.Tick += GamepadTimer_Tick;
        _gamepadTimer.Start();
    }

    private void GamepadTimer_Tick(object? sender, EventArgs e)
    {
        if (_gamepadDirCooldown > 0)
            _gamepadDirCooldown--;

        if (!TryReadGamepadInput(out ushort buttons, out int dirX, out int dirY))
            return;

        // First poll seeds held buttons so buttons pressed in parent window don't fire here
        if (!_gamepadInitialized)
        {
            _prevGamepadButtons = buttons;
            _gamepadInitialized = true;
            return;
        }

        if (buttons != 0 || dirX != 0 || dirY != 0)
        {
            var family = GamepadDetector.DetectGamepadFamily();
            var targetMode = family == GamepadType.PlayStation
                ? ActiveInputMode.PlayStation
                : (family == GamepadType.Nintendo ? ActiveInputMode.Nintendo : ActiveInputMode.Xbox);

            SetInputMode(targetMode);
        }

        ushort newlyPressed = (ushort)(buttons & ~_prevGamepadButtons);
        _prevGamepadButtons = buttons;

        // LB / L1 -> Previous piece
        if ((newlyPressed & 0x0100) != 0)
        {
            StepPiece(-1);
            return;
        }
        // RB / R1 -> Next piece
        if ((newlyPressed & 0x0200) != 0)
        {
            StepPiece(1);
            return;
        }
        // LT / L2 -> Previous game
        if ((newlyPressed & BTN_LT) != 0)
        {
            BtnPrevGame_Click(this, new RoutedEventArgs());
            return;
        }
        // RT / R2 -> Next game
        if ((newlyPressed & BTN_RT) != 0)
        {
            BtnNextGame_Click(this, new RoutedEventArgs());
            return;
        }
        // A / Cross -> Confirm current piece & advance to next piece
        if ((newlyPressed & 0x1000) != 0)
        {
            var candidates = GetCurrentCandidates();
            if (candidates.Count > 0 &&
                (!_selectedIndices.TryGetValue((_currentGameIndex, _currentPiece), out var sel) || !sel.HasValue))
            {
                _selectedIndices[(_currentGameIndex, _currentPiece)] = 0;
                RenderCurrentView();
            }
            else
            {
                StepPiece(1);
            }
            return;
        }
        // X / Square / Start -> Apply artwork
        if ((newlyPressed & 0x4000) != 0 || (newlyPressed & 0x0010) != 0)
        {
            BtnApply_Click(this, new RoutedEventArgs());
            return;
        }
        // B / Circle / Back -> Skip & close
        if ((newlyPressed & 0x2000) != 0 || (newlyPressed & 0x0020) != 0)
        {
            BtnSkipAll_Click(this, new RoutedEventArgs());
            return;
        }
        // Y / Triangle -> Toggle skip this piece
        if ((newlyPressed & 0x8000) != 0)
        {
            if (ChkSkipPiece.IsEnabled)
            {
                ChkSkipPiece.IsChecked = !(ChkSkipPiece.IsChecked ?? false);
            }
            return;
        }

        if (dirX == 0 && dirY == 0)
        {
            _gamepadAxisReleased = true;
            _gamepadDirCooldown = 0;
        }
        else if (_gamepadAxisReleased || _gamepadDirCooldown == 0)
        {
            if (dirX < 0) MoveOptionSelection(Key.Left);
            else if (dirX > 0) MoveOptionSelection(Key.Right);
            else if (dirY < 0) MoveOptionSelection(Key.Up);
            else if (dirY > 0) MoveOptionSelection(Key.Down);

            _gamepadAxisReleased = false;
            _gamepadDirCooldown = 6;
        }
    }

    private bool TryReadGamepadInput(out ushort buttons, out int dirX, out int dirY)
    {
        buttons = 0;
        dirX = 0;
        dirY = 0;

        bool hasXInputDevice = false;
        ushort xinputButtons = 0;
        int xinputDirX = 0;
        int xinputDirY = 0;

        // 1. Try XInput (1.4 with fallback to 9.1.0)
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
                    if (state.Gamepad.bLeftTrigger > 120) b |= BTN_LT;
                    if (state.Gamepad.bRightTrigger > 120) b |= BTN_RT;

                    int dX = 0;
                    int dY = 0;

                    if ((b & 0x0001) != 0 || state.Gamepad.sThumbLY > 16000) dY = -1;
                    else if ((b & 0x0002) != 0 || state.Gamepad.sThumbLY < -16000) dY = 1;

                    if ((b & 0x0004) != 0 || state.Gamepad.sThumbLX < -16000) dX = -1;
                    else if ((b & 0x0008) != 0 || state.Gamepad.sThumbLX > 16000) dX = 1;

                    if (b != 0 || dX != 0 || dY != 0)
                    {
                        buttons = b;
                        dirX = dX;
                        dirY = dY;
                        return true;
                    }

                    hasXInputDevice = true;
                    xinputButtons = b;
                    xinputDirX = dX;
                    xinputDirY = dY;
                    break;
                }
            }
        }

        // 2. Fallback to WinMM (PlayStation DualSense/DualShock, Nintendo & DirectInput controllers)
        if (!_winmmFailed)
        {
            if (_cachedWinmmJoyId >= 0 && _cachedWinmmJoyId < 16)
            {
                if (TryPollWinmmDevice((uint)_cachedWinmmJoyId, out ushort b, out int dX, out int dY))
                {
                    buttons = b;
                    dirX = dX;
                    dirY = dY;
                    return true;
                }
                _cachedWinmmJoyId = -1;
            }

            for (uint j = 0; j < 16; j++)
            {
                if (TryPollWinmmDevice(j, out ushort b, out int dX, out int dY))
                {
                    _cachedWinmmJoyId = (int)j;
                    buttons = b;
                    dirX = dX;
                    dirY = dY;
                    return true;
                }
            }
        }

        if (hasXInputDevice)
        {
            buttons = xinputButtons;
            dirX = xinputDirX;
            dirY = xinputDirY;
            return true;
        }

        return false;
    }

    private bool TryPollWinmmDevice(uint j, out ushort buttons, out int dirX, out int dirY)
    {
        buttons = 0;
        dirX = 0;
        dirY = 0;

        try
        {
            var jc = new JOYCAPSW();
            if (joyGetDevCapsW(j, ref jc, (uint)Marshal.SizeOf(jc)) != 0)
                return false;

            if (jc.wNumAxes < 2)
                return false;

            var ji = new JOYINFOEX
            {
                dwSize = (uint)Marshal.SizeOf<JOYINFOEX>(),
                dwFlags = 0x000000FF // JOY_RETURNALL
            };

            if (joyGetPosEx(j, ref ji) == 0)
            {
                if (ji.dwXpos == 0 && ji.dwYpos == 0 && ji.dwZpos == 0 && ji.dwRpos == 0 && ji.dwPOV == 0 && ji.dwButtons == 0)
                    return false;

                bool isPlayStation = (jc.wMid == 0x054C) || (GamepadDetector.DetectGamepadFamily() == GamepadType.PlayStation);
                ushort mapped = 0;

                if (isPlayStation)
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

                // L1/R1 (LB/RB) and L2/R2 (LT/RT)
                if ((ji.dwButtons & 0x0010) != 0) mapped |= 0x0100;
                if ((ji.dwButtons & 0x0020) != 0) mapped |= 0x0200;
                if ((ji.dwButtons & 0x0040) != 0) mapped |= BTN_LT;
                if ((ji.dwButtons & 0x0080) != 0) mapped |= BTN_RT;

                // Share / Options
                if ((ji.dwButtons & 0x0100) != 0) mapped |= 0x0020;
                if ((ji.dwButtons & 0x0200) != 0) mapped |= 0x0010;

                buttons = mapped;

                // D-Pad via POV Hat
                if (ji.dwPOV != 0xFFFF && ji.dwPOV <= 36000)
                {
                    if (ji.dwPOV >= 31500 || ji.dwPOV <= 4500) dirY = -1;
                    else if (ji.dwPOV >= 13500 && ji.dwPOV <= 22500) dirY = 1;

                    if (ji.dwPOV >= 4500 && ji.dwPOV <= 13500) dirX = 1;
                    else if (ji.dwPOV >= 22500 && ji.dwPOV <= 31500) dirX = -1;
                }

                // Left Analog Stick
                if (dirY == 0)
                {
                    if (ji.dwYpos < 16000) dirY = -1;
                    else if (ji.dwYpos > 49500) dirY = 1;
                }
                if (dirX == 0)
                {
                    if (ji.dwXpos < 16000) dirX = -1;
                    else if (ji.dwXpos > 49500) dirX = 1;
                }

                return true;
            }
        }
        catch
        {
            _winmmFailed = true;
        }

        return false;
    }

    // =====================================================================
    // Microsoft Fluent 2 System-Adaptive Theme & DWM Mica Integration
    // =====================================================================
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWA_BORDER_COLOR = 34;
    private const int DWMWA_CAPTION_COLOR = 35;
    private const int DWMWA_TEXT_COLOR = 36;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
    private const int DWMWCP_ROUND = 2;
    private const int DWMSBT_MAINWINDOW = 2;

    private const uint SPI_GETHIGHCONTRAST = 0x0042;
    private const uint HCF_HIGHCONTRASTON = 0x00000001;
    private const int WM_SYSCOLORCHANGE = 0x0015;
    private const int WM_SETTINGCHANGE = 0x001A;
    private const int WM_THEMECHANGED = 0x031A;
    private const int WM_DWMCOLORIZATIONCOLORCHANGED = 0x0320;

    [StructLayout(LayoutKind.Sequential)]
    private struct HIGHCONTRAST
    {
        public uint cbSize;
        public uint dwFlags;
        public IntPtr lpszDefaultScheme;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("user32.dll")]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref HIGHCONTRAST pvParam, uint fWinIni);

    private void AttachSystemChangeHook()
    {
        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            source.AddHook(WndProc);
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg is WM_SETTINGCHANGE or WM_SYSCOLORCHANGE or WM_THEMECHANGED or WM_DWMCOLORIZATIONCOLORCHANGED)
        {
            ApplySystemFluentTheme();
            ApplyDwmAttributes();
            RenderCurrentView();
        }
        return IntPtr.Zero;
    }

    private static bool IsHighContrastEnabled()
    {
        try
        {
            var hc = new HIGHCONTRAST { cbSize = (uint)Marshal.SizeOf<HIGHCONTRAST>() };
            if (SystemParametersInfo(SPI_GETHIGHCONTRAST, hc.cbSize, ref hc, 0))
                return (hc.dwFlags & HCF_HIGHCONTRASTON) != 0;
        }
        catch { }
        return false;
    }

    private static bool QuerySystemLightModePreference()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key?.GetValue("AppsUseLightTheme") is int val)
                return val != 0;
        }
        catch { }
        return false;
    }

    private static Color QuerySystemAccentColor(bool isLight)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent");
            if (key?.GetValue("AccentPalette") is byte[] palette && palette.Length >= 32)
            {
                // AccentDark1 (index 4 -> byte 16) in Light Theme, AccentLight2 (index 1 -> byte 4) in Dark Theme
                int offset = isLight ? 16 : 4;
                return Color.FromRgb(palette[offset], palette[offset + 1], palette[offset + 2]);
            }
        }
        catch { }
        return isLight ? Color.FromRgb(0, 103, 192) : Color.FromRgb(96, 205, 255);
    }

    private static Color BlendColor(Color fg, Color bg, double fgWeight)
    {
        byte r = (byte)Math.Clamp(Math.Round(fg.R * fgWeight + bg.R * (1.0 - fgWeight)), 0, 255);
        byte g = (byte)Math.Clamp(Math.Round(fg.G * fgWeight + bg.G * (1.0 - fgWeight)), 0, 255);
        byte b = (byte)Math.Clamp(Math.Round(fg.B * fgWeight + bg.B * (1.0 - fgWeight)), 0, 255);
        return Color.FromRgb(r, g, b);
    }

    private static double GetRelativeLuminance(Color c) =>
        (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;

    private void ApplySystemFluentTheme()
    {
        bool isHighContrast = IsHighContrastEnabled();
        bool isLight = QuerySystemLightModePreference();

        if (isHighContrast)
        {
            Resources["BackgroundBrush"] = SystemColors.WindowBrush;
            Resources["FooterBrush"] = SystemColors.ControlBrush;
            Resources["LayerBrush"] = SystemColors.WindowBrush;
            Resources["CardBrush"] = SystemColors.WindowBrush;
            Resources["CardHoverBrush"] = SystemColors.ControlBrush;
            Resources["CardStrokeBrush"] = SystemColors.WindowFrameBrush;
            Resources["DividerBrush"] = SystemColors.WindowFrameBrush;
            Resources["TextPrimaryBrush"] = SystemColors.WindowTextBrush;
            Resources["TextSecondaryBrush"] = SystemColors.WindowTextBrush;
            Resources["TextTertiaryBrush"] = SystemColors.GrayTextBrush;
            Resources["AccentBrush"] = SystemColors.HighlightBrush;
            Resources["AccentHoverBrush"] = SystemColors.HighlightBrush;
            Resources["AccentPressedBrush"] = SystemColors.HighlightBrush;
            Resources["AccentTintBrush"] = SystemColors.ControlBrush;
            Resources["TextOnAccentBrush"] = SystemColors.HighlightTextBrush;
            return;
        }

        Color accentCol = QuerySystemAccentColor(isLight);

        // Exact Fluent 2 Semantic Tokens (AGENTS.md §1.1)
        Color bgBase = isLight ? Color.FromRgb(243, 243, 243) : Color.FromRgb(32, 32, 32);
        Color bgFooter = isLight ? Color.FromRgb(238, 238, 238) : Color.FromRgb(24, 24, 24);
        Color layerCol = isLight ? Color.FromRgb(249, 249, 249) : Color.FromRgb(39, 39, 39);
        Color cardCol = isLight ? Color.FromRgb(255, 255, 255) : Color.FromRgb(45, 45, 45);
        Color cardHover = isLight ? Color.FromRgb(246, 246, 246) : Color.FromRgb(54, 54, 54);
        Color cardStroke = isLight ? Color.FromRgb(229, 229, 229) : Color.FromRgb(58, 58, 58);
        Color dividerCol = isLight ? Color.FromRgb(229, 229, 229) : Color.FromRgb(48, 48, 48);
        Color textPrimary = isLight ? Color.FromRgb(26, 26, 26) : Color.FromRgb(255, 255, 255);
        Color textSecondary = isLight ? Color.FromRgb(92, 92, 92) : Color.FromRgb(197, 197, 197);
        Color textTertiary = Color.FromRgb(138, 138, 138);

        Color textOnAccent = GetRelativeLuminance(accentCol) > 0.55 ? Color.FromRgb(0, 0, 0) : Color.FromRgb(255, 255, 255);
        Color accentHover = BlendColor(accentCol, bgBase, 0.90);
        Color accentPressed = BlendColor(accentCol, bgBase, 0.80);
        Color accentTint = Color.FromArgb((byte)(isLight ? 28 : 42), accentCol.R, accentCol.G, accentCol.B);

        Resources["BackgroundBrush"] = new SolidColorBrush(bgBase);
        Resources["FooterBrush"] = new SolidColorBrush(bgFooter);
        Resources["LayerBrush"] = new SolidColorBrush(layerCol);
        Resources["CardBrush"] = new SolidColorBrush(cardCol);
        Resources["CardHoverBrush"] = new SolidColorBrush(cardHover);
        Resources["CardStrokeBrush"] = new SolidColorBrush(cardStroke);
        Resources["DividerBrush"] = new SolidColorBrush(dividerCol);
        Resources["TextPrimaryBrush"] = new SolidColorBrush(textPrimary);
        Resources["TextSecondaryBrush"] = new SolidColorBrush(textSecondary);
        Resources["TextTertiaryBrush"] = new SolidColorBrush(textTertiary);
        Resources["AccentBrush"] = new SolidColorBrush(accentCol);
        Resources["AccentHoverBrush"] = new SolidColorBrush(accentHover);
        Resources["AccentPressedBrush"] = new SolidColorBrush(accentPressed);
        Resources["AccentTintBrush"] = new SolidColorBrush(accentTint);
        Resources["TextOnAccentBrush"] = new SolidColorBrush(textOnAccent);
    }

    private void ApplyDwmAttributes()
    {
        try
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;

            bool isLight = QuerySystemLightModePreference();
            int darkMode = isLight ? 0 : 1;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

            int cornerPref = DWMWCP_ROUND;
            DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPref, sizeof(int));

            int backdrop = DWMSBT_MAINWINDOW;
            DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));

            int captionColor = isLight ? 0x00F3F3F3 : 0x00202020;
            DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref captionColor, sizeof(int));

            int textColor = isLight ? 0x001A1A1A : 0x00FFFFFF;
            DwmSetWindowAttribute(hwnd, DWMWA_TEXT_COLOR, ref textColor, sizeof(int));

            int borderColor = isLight ? 0x00E5E5E5 : 0x003A3A3A;
            DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref borderColor, sizeof(int));
        }
        catch { }
    }
}
