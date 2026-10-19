using System.Globalization;
using NonSteamLauncher.Sync.UI;
using Xunit;

namespace NonSteamLauncher.Tests;

public class LocalizationTests
{
    [Theory]
    [InlineData("es", "Cerrar", "JUEGOS AGREGADOS A STEAM:")]
    [InlineData("en", "Close", "GAMES ADDED TO STEAM:")]
    [InlineData("pt", "Fechar", "JOGOS ADICIONADOS À STEAM:")]
    [InlineData("fr", "Fermer", "JEUX AJOUTÉS À STEAM :")]
    [InlineData("de", "Schließen", "ZU STEAM HINZUGEFÜGTE SPIELE:")]
    public void Localization_ReturnsCorrectStringsForCulture(string cultureName, string expectedClose, string expectedCardHeader)
    {
        var prevCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo(cultureName);
            var strings = Localization.GetStrings();

            Assert.Equal(expectedClose, strings.ActionClose);
            Assert.Equal(expectedCardHeader, strings.CardHeaderAdded);
            Assert.False(string.IsNullOrEmpty(strings.WindowTitle));
            Assert.False(string.IsNullOrEmpty(strings.HeaderSuccess));
        }
        finally
        {
            CultureInfo.CurrentUICulture = prevCulture;
        }
    }
}
