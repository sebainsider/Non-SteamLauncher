using System.Globalization;

namespace NonSteamLauncher.Sync.UI;

public class LocalizedStrings
{
    public string WindowTitle { get; init; } = "";
    public string HeaderSuccess { get; init; } = "";
    public string HeaderUpToDate { get; init; } = "";
    public string HeaderError { get; init; } = "";
    public string CardHeaderAdded { get; init; } = "";
    public string CardHeaderCurrent { get; init; } = "";
    public string EmptyMessage { get; init; } = "";
    public string MoreGamesFormat { get; init; } = "";
    public string ActionClose { get; init; } = "";
}

public static class Localization
{
    public static LocalizedStrings GetStrings()
    {
        string lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
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
        WindowTitle = "Non-Steam Launcher — Sincronización",
        HeaderSuccess = "✔ Sincronización completada",
        HeaderUpToDate = "✔ Tu biblioteca de Steam ya está al día",
        HeaderError = "✖ Error en la sincronización con Steam",
        CardHeaderAdded = "JUEGOS AGREGADOS A STEAM:",
        CardHeaderCurrent = "JUEGOS EN TU BIBLIOTECA:",
        EmptyMessage = "No se detectaron juegos instalados en los launchers.",
        MoreGamesFormat = "... y {0} juego(s) más.",
        ActionClose = "Cerrar"
    };

    private static readonly LocalizedStrings English = new()
    {
        WindowTitle = "Non-Steam Launcher — Sync",
        HeaderSuccess = "✔ Sync completed",
        HeaderUpToDate = "✔ Your Steam library is up to date",
        HeaderError = "✖ Steam sync failed",
        CardHeaderAdded = "GAMES ADDED TO STEAM:",
        CardHeaderCurrent = "GAMES IN YOUR LIBRARY:",
        EmptyMessage = "No installed games detected across launchers.",
        MoreGamesFormat = "... and {0} more game(s).",
        ActionClose = "Close"
    };

    private static readonly LocalizedStrings Portuguese = new()
    {
        WindowTitle = "Non-Steam Launcher — Sincronização",
        HeaderSuccess = "✔ Sincronização concluída",
        HeaderUpToDate = "✔ Sua biblioteca Steam já está atualizada",
        HeaderError = "✖ Falha na sincronização com a Steam",
        CardHeaderAdded = "JOGOS ADICIONADOS À STEAM:",
        CardHeaderCurrent = "JOGOS NA SUA BIBLIOTECA:",
        EmptyMessage = "Nenhum jogo instalado detectado nos inicializadores.",
        MoreGamesFormat = "... e mais {0} jogo(s).",
        ActionClose = "Fechar"
    };

    private static readonly LocalizedStrings French = new()
    {
        WindowTitle = "Non-Steam Launcher — Synchronisation",
        HeaderSuccess = "✔ Synchronisation terminée",
        HeaderUpToDate = "✔ Votre bibliothèque Steam est à jour",
        HeaderError = "✖ Échec de la synchronisation Steam",
        CardHeaderAdded = "JEUX AJOUTÉS À STEAM :",
        CardHeaderCurrent = "JEUX DANS VOTRE BIBLIOTHÈQUE :",
        EmptyMessage = "Aucun jeu installé détecté dans les lanceurs.",
        MoreGamesFormat = "... et {0} autre(s) jeu(x).",
        ActionClose = "Fermer"
    };

    private static readonly LocalizedStrings German = new()
    {
        WindowTitle = "Non-Steam Launcher — Synchronisierung",
        HeaderSuccess = "✔ Synchronisierung abgeschlossen",
        HeaderUpToDate = "✔ Deine Steam-Bibliothek ist auf dem neuesten Stand",
        HeaderError = "✖ Steam-Synchronisierung fehlgeschlagen",
        CardHeaderAdded = "ZU STEAM HINZUGEFÜGTE SPIELE:",
        CardHeaderCurrent = "SPIELE IN DEINER BIBLIOTHEK:",
        EmptyMessage = "Keine installierten Spiele in den Launchern erkannt.",
        MoreGamesFormat = "... und {0} weitere(s) Spiel(e).",
        ActionClose = "Schließen"
    };

    private static readonly LocalizedStrings Italian = new()
    {
        WindowTitle = "Non-Steam Launcher — Sincronizzazione",
        HeaderSuccess = "✔ Sincronizzazione completata",
        HeaderUpToDate = "✔ La tua libreria di Steam è aggiornata",
        HeaderError = "✖ Sincronizzazione con Steam non riuscita",
        CardHeaderAdded = "GIOCHI AGGIUNTI A STEAM:",
        CardHeaderCurrent = "GIOCHI NELLA TUA LIBRERIA:",
        EmptyMessage = "Nessun gioco installato rilevato nei launcher.",
        MoreGamesFormat = "... e altri {0} giochi.",
        ActionClose = "Chiudi"
    };

    private static readonly LocalizedStrings Russian = new()
    {
        WindowTitle = "Non-Steam Launcher — Синхронизация",
        HeaderSuccess = "✔ Синхронизация завершена",
        HeaderUpToDate = "✔ Ваша библиотека Steam обновлена",
        HeaderError = "✖ Ошибка синхронизации со Steam",
        CardHeaderAdded = "ИГРЫ, ДОБАВЛЕННЫЕ В STEAM:",
        CardHeaderCurrent = "ИГРЫ В ВАШЕЙ БИБЛИОТЕКЕ:",
        EmptyMessage = "Установленные игры в лаунчерах не обнаружены.",
        MoreGamesFormat = "... и ещё {0} игр(ы).",
        ActionClose = "Закрыть"
    };

    private static readonly LocalizedStrings Japanese = new()
    {
        WindowTitle = "Non-Steam Launcher — 同期",
        HeaderSuccess = "✔ 同期完了",
        HeaderUpToDate = "✔ Steamライブラリは最新です",
        HeaderError = "✖ Steamの同期に失敗しました",
        CardHeaderAdded = "STEAMに追加されたゲーム:",
        CardHeaderCurrent = "ライブラリ内のゲーム:",
        EmptyMessage = "ランチャーにインストールされたゲームが見つかりません。",
        MoreGamesFormat = "...他{0}本のゲーム。",
        ActionClose = "閉じる"
    };

    private static readonly LocalizedStrings Chinese = new()
    {
        WindowTitle = "Non-Steam Launcher — 同步",
        HeaderSuccess = "✔ 同步完成",
        HeaderUpToDate = "✔ 您的 Steam 库已是最新状态",
        HeaderError = "✖ Steam 同步失败",
        CardHeaderAdded = "已添加到 STEAM 的游戏：",
        CardHeaderCurrent = "库中现有游戏：",
        EmptyMessage = "未在启动器中检测到已安装的游戏。",
        MoreGamesFormat = "...以及另外 {0} 款游戏。",
        ActionClose = "关闭"
    };
}
