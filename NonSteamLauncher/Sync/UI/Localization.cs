using System.Globalization;

namespace NonSteamLauncher.Sync.UI;

public class LocalizedStrings
{
    public string WindowTitle { get; init; } = "";
    public string HeaderSuccess { get; init; } = "";
    public string HeaderUpToDate { get; init; } = "";
    public string HeaderError { get; init; } = "";
    public string HeaderAvailableToAddFormat { get; init; } = "";
    public string CardHeaderAdded { get; init; } = "";
    public string CardHeaderCurrent { get; init; } = "";
    public string CardHeaderDetected { get; init; } = "";
    public string EmptyMessage { get; init; } = "";
    public string MoreGamesFormat { get; init; } = "";
    public string ActionClose { get; init; } = "";
    public string ActionAddSelectedFormat { get; init; } = "";
    public string ActionSelectAll { get; init; } = "";
    public string BadgeSynced { get; init; } = "";
    public string BadgeNotSynced { get; init; } = "";
    public string BadgeSelected { get; init; } = "";
    public string HintToggle { get; init; } = "";
    public string HintAdd { get; init; } = "";
    public string HintSelectAll { get; init; } = "";
    public string SettingsButton { get; init; } = "SteamGridDB";
    public string SettingsTitle { get; init; } = "";
    public string SettingsDownloadToggle { get; init; } = "";
    public string SettingsApiKeyLabel { get; init; } = "";
    public string SettingsApiKeyPlaceholder { get; init; } = "";
    public string SettingsPasteButton { get; init; } = "";
    public string SettingsClearButton { get; init; } = "";
    public string SettingsGetKeyButton { get; init; } = "";
    public string SettingsSaveBackButton { get; init; } = "";
    public string HeaderUpToDateSub { get; init; } = "All detected games are synced and ready to play in Steam.";
    public string HeaderPendingSub { get; init; } = "Select the games you want to add to your Steam library.";
    public string HeaderSuccessSub { get; init; } = "Shortcuts have been successfully saved to Steam.";
    public string SettingsSubtitle { get; init; } = "Configure automatic artwork, banner, and logo downloads.";
    public string TabLibrary { get; init; } = "Library";
    public string TabSettings { get; init; } = "SteamGridDB";
    public string SettingsDownloadToggleSub { get; init; } = "Fetch covers, heroes, logos, and icons from SteamGridDB for your games.";
    public string SettingsApiKeySub { get; init; } = "Enter your personal API key to enable automated downloads.";
    public string GamepadHintXbox { get; init; } = "(A) Toggle  (X) Sync  (Y) All  (B) Close  (LB/RB) Tabs";
    public string GamepadHintPlayStation { get; init; } = "(✕) Toggle  (▢) Sync  (△) All  (○) Close  (L1/R1) Tabs";
    public string KeyboardHintLegend { get; init; } = "[Tab] Tabs   [Space] Select   [Enter] Confirm   [Esc] Close";
    public string InputDeviceKeyboard { get; init; } = "Keyboard & Mouse";
    public string InputDeviceXbox { get; init; } = "Xbox Controller";
    public string InputDevicePlayStation { get; init; } = "PlayStation Controller";
    public string InputDeviceNintendo { get; init; } = "Nintendo Controller";
    public string ActionHintToggle { get; init; } = "Toggle";
    public string BadgeReadyToAdd { get; init; } = "Ready to add";
    public string TabArtwork { get; init; } = "Artwork";
    public string StatusInSteam { get; init; } = "In Steam";
    public string StatusReadyToSync { get; init; } = "Ready to sync";
    public string HeaderUpToDateCompact { get; init; } = "Your Steam library is up to date ({0} games synced)";
    public string HeaderPendingCompact { get; init; } = "{0} game(s) available to sync with Steam";
    public string LegendTabs { get; init; } = "Tabs";
    public string LegendToggle { get; init; } = "Select";
    public string LegendSync { get; init; } = "Sync";
    public string LegendClose { get; init; } = "Close";
    public string LegendAll { get; init; } = "All";
    public string KeyNameSpace { get; init; } = "Space";
    public string KeyNameTab { get; init; } = "Tab";
    public string KeyNameEnter { get; init; } = "Enter";
    public string KeyNameEsc { get; init; } = "Esc";
    public string SettingsLanguageLabel { get; init; } = "Language";
    public string ArtworkProviderTitle { get; init; } = "Artwork Downloads (SteamGridDB)";
    public string ArtworkProviderDesc { get; init; } = "Automatically download boxart covers, banners, logos, and icons for each game.";
}

public static class Localization
{
    public static LocalizedStrings GetStrings(string? overrideLang = null)
    {
        string lang = !string.IsNullOrWhiteSpace(overrideLang)
            ? overrideLang.Trim().ToLowerInvariant()
            : CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();

        return lang switch
        {
            "es" => Spanish,
            "pt" => Portuguese,
            "fr" => French,
            "de" => German,
            "it" => Italian,
            "ru" => Russian,
            "ja" => Japanese,
            "zh" => Chinese,
            _ => English
        };
    }

    private static readonly LocalizedStrings Spanish = new()
    {
        WindowTitle = "Non-Steam Launcher",
        HeaderSuccess = "✔ Sincronización completada",
        HeaderUpToDate = "✔ Tu biblioteca de Steam ya está al día",
        HeaderError = "✖ Error en la sincronización con Steam",
        HeaderAvailableToAddFormat = "{0} juego(s) disponibles para agregar a Steam",
        CardHeaderAdded = "JUEGOS AGREGADOS A STEAM:",
        CardHeaderCurrent = "JUEGOS EN TU BIBLIOTECA:",
        CardHeaderDetected = "Juegos detectados en tu equipo",
        EmptyMessage = "No se detectaron juegos instalados en los launchers.",
        MoreGamesFormat = "... y {0} juego(s) más.",
        ActionClose = "Cerrar",
        ActionAddSelectedFormat = "Agregar ({0})",
        ActionSelectAll = "Seleccionar todos",
        BadgeSynced = "Sincronizado",
        BadgeNotSynced = "No sincronizado",
        BadgeSelected = "Seleccionado",
        HintToggle = "Marcar",
        HintAdd = "Agregar",
        HintSelectAll = "Todos",
        SettingsButton = "SteamGridDB",
        SettingsTitle = "Imágenes e iconos (SteamGridDB)",
        SettingsDownloadToggle = "Descargar portadas, banners, logos e iconos automáticamente",
        SettingsApiKeyLabel = "API Key de SteamGridDB",
        SettingsApiKeyPlaceholder = "Haz clic en Pegar o escribe tu API Key aquí...",
        SettingsPasteButton = "Pegar",
        SettingsClearButton = "Borrar",
        SettingsGetKeyButton = "Obtener API Key",
        SettingsSaveBackButton = "Guardar y volver",
        HeaderUpToDateSub = "Todos tus juegos están agregados y listos para jugar en Steam.",
        HeaderPendingSub = "Marca los juegos que desees agregar a tu biblioteca de Steam.",
        HeaderSuccessSub = "Los accesos directos han sido guardados correctamente en Steam.",
        SettingsSubtitle = "Configura la descarga automática de portadas, banners y logos.",
        TabLibrary = "Biblioteca",
        TabSettings = "SteamGridDB",
        SettingsDownloadToggleSub = "Obtener portadas, banners, logos e iconos de SteamGridDB para tus juegos.",
        SettingsApiKeySub = "Introduce tu clave de API personal para habilitar las descargas.",
        GamepadHintXbox = "(A) Marcar   (X) Sincronizar   (Y) Todos   (B) Cerrar   (LB/RB) Pestañas",
        GamepadHintPlayStation = "(✕) Marcar   (▢) Sincronizar   (△) Todos   (○) Cerrar   (L1/R1) Pestañas",
        KeyboardHintLegend = "[Tab] Pestañas   •   [Espacio] Marcar   •   [Enter] Sincronizar   •   [Esc] Cerrar",
        InputDeviceKeyboard = "Teclado y ratón",
        InputDeviceXbox = "Mando Xbox",
        InputDevicePlayStation = "Mando PlayStation",
        InputDeviceNintendo = "Mando Nintendo",
        ActionHintToggle = "Alternar",
        BadgeReadyToAdd = "Por agregar",
        TabArtwork = "Carátulas",
        StatusInSteam = "En Steam",
        StatusReadyToSync = "Por sincronizar",
        HeaderUpToDateCompact = "Tu biblioteca de Steam está al día ({0} juegos sincronizados)",
        HeaderPendingCompact = "{0} juego(s) disponibles para sincronizar con Steam",
        LegendTabs = "Pestañas",
        LegendToggle = "Marcar",
        LegendSync = "Sincronizar",
        LegendClose = "Cerrar",
        LegendAll = "Todos",
        KeyNameSpace = "Espacio",
        KeyNameTab = "Tab",
        KeyNameEnter = "Enter",
        KeyNameEsc = "Esc",
        SettingsLanguageLabel = "Idioma",
        ArtworkProviderTitle = "Descarga de carátulas e imágenes",
        ArtworkProviderDesc = "Configura SteamGridDB para descargar portadas verticales, banners, logos e iconos automáticamente."
    };

    private static readonly LocalizedStrings English = new()
    {
        WindowTitle = "Non-Steam Launcher",
        HeaderSuccess = "✔ Sync completed",
        HeaderUpToDate = "✔ Your Steam library is up to date",
        HeaderError = "✖ Steam sync failed",
        HeaderAvailableToAddFormat = "{0} game(s) available to add to Steam",
        CardHeaderAdded = "GAMES ADDED TO STEAM:",
        CardHeaderCurrent = "GAMES IN YOUR LIBRARY:",
        CardHeaderDetected = "Detected games on your PC",
        EmptyMessage = "No installed games detected across launchers.",
        MoreGamesFormat = "... and {0} more game(s).",
        ActionClose = "Close",
        ActionAddSelectedFormat = "Add ({0})",
        ActionSelectAll = "Select all",
        BadgeSynced = "Synced",
        BadgeNotSynced = "Not synced",
        BadgeSelected = "Selected",
        HintToggle = "Toggle",
        HintAdd = "Add",
        HintSelectAll = "All",
        SettingsButton = "SteamGridDB",
        SettingsTitle = "Artwork & Images",
        SettingsDownloadToggle = "Automatically download artwork",
        SettingsApiKeyLabel = "SteamGridDB API Key",
        SettingsApiKeyPlaceholder = "Click Paste or type your API Key here...",
        SettingsPasteButton = "Paste",
        SettingsClearButton = "Clear",
        SettingsGetKeyButton = "Get API Key",
        SettingsSaveBackButton = "Save & Back",
        HeaderUpToDateSub = "All detected games are synced and ready to play in Steam.",
        HeaderPendingSub = "Select the games you want to add to your Steam library.",
        HeaderSuccessSub = "Shortcuts have been successfully saved to Steam.",
        SettingsSubtitle = "Configure automatic artwork, banner, and logo downloads.",
        TabLibrary = "Library",
        TabSettings = "Artwork",
        SettingsDownloadToggleSub = "Fetch covers, heroes, logos, and icons from SteamGridDB for your games.",
        SettingsApiKeySub = "Enter your personal API key to enable automated downloads.",
        GamepadHintXbox = "(A) Select   (X) Sync   (Y) All   (B) Close   (LB/RB) Tabs",
        GamepadHintPlayStation = "(✕) Select   (▢) Sync   (△) All   (○) Close   (L1/R1) Tabs",
        KeyboardHintLegend = "[Tab] Tabs   •   [Space] Select   •   [Enter] Sync   •   [Esc] Close",
        InputDeviceKeyboard = "Keyboard & Mouse",
        InputDeviceXbox = "Xbox Controller",
        InputDevicePlayStation = "PlayStation Controller",
        InputDeviceNintendo = "Nintendo Controller",
        ActionHintToggle = "Select",
        BadgeReadyToAdd = "Ready to add",
        TabArtwork = "Artwork",
        StatusInSteam = "In Steam",
        StatusReadyToSync = "Ready to sync",
        HeaderUpToDateCompact = "Your Steam library is up to date ({0} games synced)",
        HeaderPendingCompact = "{0} game(s) available to sync with Steam",
        LegendTabs = "Tabs",
        LegendToggle = "Select",
        LegendSync = "Sync",
        LegendClose = "Close",
        LegendAll = "All",
        KeyNameSpace = "Space",
        KeyNameTab = "Tab",
        KeyNameEnter = "Enter",
        KeyNameEsc = "Esc",
        SettingsLanguageLabel = "Language",
        ArtworkProviderTitle = "Artwork & Images Downloads",
        ArtworkProviderDesc = "Configure SteamGridDB to automatically download vertical covers, banners, logos, and icons."
    };

    private static readonly LocalizedStrings Portuguese = new()
    {
        WindowTitle = "Non-Steam Launcher",
        HeaderSuccess = "✔ Sincronização concluída",
        HeaderUpToDate = "✔ Sua biblioteca Steam já está atualizada",
        HeaderError = "✖ Falha na sincronização com a Steam",
        HeaderAvailableToAddFormat = "{0} jogo(s) disponíveis para adicionar à Steam",
        CardHeaderAdded = "JOGOS ADICIONADOS À STEAM:",
        CardHeaderCurrent = "JOGOS NA SUA BIBLIOTECA:",
        CardHeaderDetected = "Jogos detectados no seu PC",
        EmptyMessage = "Nenhum jogo instalado detectado nos inicializadores.",
        MoreGamesFormat = "... e mais {0} jogo(s).",
        ActionClose = "Fechar",
        ActionAddSelectedFormat = "Adicionar ({0})",
        ActionSelectAll = "Selecionar todos",
        BadgeSynced = "Sincronizado",
        BadgeNotSynced = "Não sincronizado",
        BadgeSelected = "Selecionado",
        HintToggle = "Marcar",
        HintAdd = "Adicionar",
        HintSelectAll = "Todos",
        SettingsButton = "SteamGridDB",
        SettingsTitle = "Imagens e ícones (SteamGridDB)",
        SettingsDownloadToggle = "Baixar capas, banners, logos e ícones automaticamente",
        SettingsApiKeyLabel = "Chave da API do SteamGridDB",
        SettingsApiKeyPlaceholder = "Clique em Colar ou digite sua API Key aqui...",
        SettingsPasteButton = "Colar",
        SettingsClearButton = "Limpar",
        SettingsGetKeyButton = "Obter API Key",
        SettingsSaveBackButton = "Salvar e voltar"
    };

    private static readonly LocalizedStrings French = new()
    {
        WindowTitle = "Non-Steam Launcher",
        HeaderSuccess = "✔ Synchronisation terminée",
        HeaderUpToDate = "✔ Votre bibliothèque Steam est à jour",
        HeaderError = "✖ Échec de la synchronisation Steam",
        HeaderAvailableToAddFormat = "{0} jeu(x) disponible(s) à ajouter à Steam",
        CardHeaderAdded = "JEUX AJOUTÉS À STEAM :",
        CardHeaderCurrent = "JEUX DANS VOTRE BIBLIOTHÈQUE :",
        CardHeaderDetected = "Jeux détectés sur votre PC",
        EmptyMessage = "Aucun jeu installé détecté dans les lanceurs.",
        MoreGamesFormat = "... et {0} autre(s) jeu(x).",
        ActionClose = "Fermer",
        ActionAddSelectedFormat = "Ajouter ({0})",
        ActionSelectAll = "Tout sélectionner",
        BadgeSynced = "Synchronisé",
        BadgeNotSynced = "Non synchronisé",
        BadgeSelected = "Sélectionné",
        HintToggle = "Cocher",
        HintAdd = "Ajouter",
        HintSelectAll = "Tous",
        SettingsButton = "SteamGridDB",
        SettingsTitle = "Images et icônes (SteamGridDB)",
        SettingsDownloadToggle = "Télécharger automatiquement les jaquettes, bannières, logos et icônes",
        SettingsApiKeyLabel = "Clé API SteamGridDB",
        SettingsApiKeyPlaceholder = "Cliquez sur Coller ou saisissez votre clé API ici...",
        SettingsPasteButton = "Coller",
        SettingsClearButton = "Effacer",
        SettingsGetKeyButton = "Obtenir une clé API",
        SettingsSaveBackButton = "Enregistrer"
    };

    private static readonly LocalizedStrings German = new()
    {
        WindowTitle = "Non-Steam Launcher",
        HeaderSuccess = "✔ Synchronisierung abgeschlossen",
        HeaderUpToDate = "✔ Deine Steam-Bibliothek ist auf dem neuesten Stand",
        HeaderError = "✖ Steam-Synchronisierung fehlgeschlagen",
        HeaderAvailableToAddFormat = "{0} Spiel(e) zum Hinzufügen zu Steam verfügbar",
        CardHeaderAdded = "ZU STEAM HINZUGEFÜGTE SPIELE:",
        CardHeaderCurrent = "SPIELE IN DEINER BIBLIOTHEK:",
        CardHeaderDetected = "Erkannte Spiele auf deinem PC",
        EmptyMessage = "Keine installierten Spiele in den Launchern erkannt.",
        MoreGamesFormat = "... und {0} weitere(s) Spiel(e).",
        ActionClose = "Schließen",
        ActionAddSelectedFormat = "Hinzufügen ({0})",
        ActionSelectAll = "Alle auswählen",
        BadgeSynced = "Synchronisiert",
        BadgeNotSynced = "Nicht synchronisiert",
        BadgeSelected = "Ausgewählt",
        HintToggle = "Wählen",
        HintAdd = "Hinzufügen",
        HintSelectAll = "Alle",
        SettingsButton = "SteamGridDB",
        SettingsTitle = "Bilder & Icons (SteamGridDB)",
        SettingsDownloadToggle = "Cover, Banner, Logos und Icons automatisch herunterladen",
        SettingsApiKeyLabel = "SteamGridDB API-Schlüssel",
        SettingsApiKeyPlaceholder = "Auf Einfügen klicken oder API-Schlüssel eingeben...",
        SettingsPasteButton = "Einfügen",
        SettingsClearButton = "Löschen",
        SettingsGetKeyButton = "API-Schlüssel abrufen",
        SettingsSaveBackButton = "Speichern"
    };

    private static readonly LocalizedStrings Italian = new()
    {
        WindowTitle = "Non-Steam Launcher",
        HeaderSuccess = "✔ Sincronizzazione completata",
        HeaderUpToDate = "✔ La tua libreria di Steam è aggiornata",
        HeaderError = "✖ Sincronizzazione con Steam non riuscita",
        HeaderAvailableToAddFormat = "{0} giochi disponibili da aggiungere a Steam",
        CardHeaderAdded = "GIOCHI AGGIUNTI A STEAM:",
        CardHeaderCurrent = "GIOCHI NELLA TUA LIBRERIA:",
        CardHeaderDetected = "Giochi rilevati sul tuo PC",
        EmptyMessage = "Nessun gioco installato rilevato nei launcher.",
        MoreGamesFormat = "... e altri {0} giochi.",
        ActionClose = "Chiudi",
        ActionAddSelectedFormat = "Aggiungi ({0})",
        ActionSelectAll = "Seleziona tutti",
        BadgeSynced = "Sincronizzato",
        BadgeNotSynced = "Non sincronizzato",
        BadgeSelected = "Selezionato",
        HintToggle = "Seleziona",
        HintAdd = "Aggiungi",
        HintSelectAll = "Tutti",
        SettingsButton = "SteamGridDB",
        SettingsTitle = "Immagini e icone (SteamGridDB)",
        SettingsDownloadToggle = "Scarica automaticamente copertine, banner, loghi e icone",
        SettingsApiKeyLabel = "Chiave API SteamGridDB",
        SettingsApiKeyPlaceholder = "Fai clic su Incolla o inserisci la chiave API...",
        SettingsPasteButton = "Incolla",
        SettingsClearButton = "Cancella",
        SettingsGetKeyButton = "Ottieni chiave API",
        SettingsSaveBackButton = "Salva e torna"
    };

    private static readonly LocalizedStrings Russian = new()
    {
        WindowTitle = "Non-Steam Launcher",
        HeaderSuccess = "✔ Синхронизация завершена",
        HeaderUpToDate = "✔ Ваша библиотека Steam обновлена",
        HeaderError = "✖ Ошибка синхронизации со Steam",
        HeaderAvailableToAddFormat = "Доступно игр для добавления в Steam: {0}",
        CardHeaderAdded = "ИГРЫ, ДОБАВЛЕННЫЕ В STEAM:",
        CardHeaderCurrent = "ИГРЫ В ВАШЕЙ БИБЛИОТЕКЕ:",
        CardHeaderDetected = "Обнаруженные игры на ПК",
        EmptyMessage = "Установленные игры в лаунчерах не обнаружены.",
        MoreGamesFormat = "... и ещё {0} игр(ы).",
        ActionClose = "Закрыть",
        ActionAddSelectedFormat = "Добавить ({0})",
        ActionSelectAll = "Выбрать все",
        BadgeSynced = "Синхронизировано",
        BadgeNotSynced = "Не синхронизировано",
        BadgeSelected = "Выбрано",
        HintToggle = "Выбрать",
        HintAdd = "Добавить",
        HintSelectAll = "Все",
        SettingsButton = "SteamGridDB",
        SettingsTitle = "Обложки и иконки (SteamGridDB)",
        SettingsDownloadToggle = "Автоматически загружать обложки, фоны, логотипы и иконки",
        SettingsApiKeyLabel = "API-ключ SteamGridDB",
        SettingsApiKeyPlaceholder = "Нажмите Вставить или введите API-ключ...",
        SettingsPasteButton = "Вставить",
        SettingsClearButton = "Очистить",
        SettingsGetKeyButton = "Получить ключ API",
        SettingsSaveBackButton = "Сохранить"
    };

    private static readonly LocalizedStrings Japanese = new()
    {
        WindowTitle = "Non-Steam Launcher",
        HeaderSuccess = "✔ 同期完了",
        HeaderUpToDate = "✔ Steamライブラリは最新です",
        HeaderError = "✖ Steamの同期に失敗しました",
        HeaderAvailableToAddFormat = "Steamに追加可能なゲームが {0} 本あります",
        CardHeaderAdded = "STEAMに追加されたゲーム:",
        CardHeaderCurrent = "ライブラリ内のゲーム:",
        CardHeaderDetected = "検出されたゲーム",
        EmptyMessage = "ランチャーにインストールされたゲームが見つかりません。",
        MoreGamesFormat = "...他{0}本のゲーム。",
        ActionClose = "閉じる",
        ActionAddSelectedFormat = "追加 ({0})",
        ActionSelectAll = "すべて選択",
        BadgeSynced = "同期済み",
        BadgeNotSynced = "未同期",
        BadgeSelected = "選択中",
        HintToggle = "選択",
        HintAdd = "追加",
        HintSelectAll = "すべて",
        SettingsButton = "SteamGridDB",
        SettingsTitle = "画像とアイコン設定 (SteamGridDB)",
        SettingsDownloadToggle = "カバー、ヒーロー画像、ロゴ、アイコンを自動取得する",
        SettingsApiKeyLabel = "SteamGridDB APIキー",
        SettingsApiKeyPlaceholder = "貼り付けをクリックするかAPIキーを入力...",
        SettingsPasteButton = "貼り付け",
        SettingsClearButton = "クリア",
        SettingsGetKeyButton = "APIキーを取得",
        SettingsSaveBackButton = "保存して戻る"
    };

    private static readonly LocalizedStrings Chinese = new()
    {
        WindowTitle = "Non-Steam Launcher",
        HeaderSuccess = "✔ 同步完成",
        HeaderUpToDate = "✔ 您的 Steam 库已是最新状态",
        HeaderError = "✖ Steam 同步失败",
        HeaderAvailableToAddFormat = "有 {0} 款游戏可添加到 Steam",
        CardHeaderAdded = "已添加到 STEAM 的游戏：",
        CardHeaderCurrent = "库中现有游戏：",
        CardHeaderDetected = "检测到的游戏",
        EmptyMessage = "未在启动器中检测到已安装的游戏。",
        MoreGamesFormat = "...以及另外 {0} 款游戏。",
        ActionClose = "关闭",
        ActionAddSelectedFormat = "添加 ({0})",
        ActionSelectAll = "全选",
        BadgeSynced = "已同步",
        BadgeNotSynced = "未同步",
        BadgeSelected = "已选择",
        HintToggle = "勾选",
        HintAdd = "添加",
        HintSelectAll = "全选",
        SettingsButton = "SteamGridDB",
        SettingsTitle = "封面与图标设置 (SteamGridDB)",
        SettingsDownloadToggle = "自动下载游戏封面、背景、徽标和图标",
        SettingsApiKeyLabel = "SteamGridDB API 密钥",
        SettingsApiKeyPlaceholder = "点击粘贴或在此输入您的 API 密钥...",
        SettingsPasteButton = "粘贴",
        SettingsClearButton = "清除",
        SettingsGetKeyButton = "获取 API 密钥",
        SettingsSaveBackButton = "保存并返回"
    };
}
