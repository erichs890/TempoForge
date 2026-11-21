using System.Text.RegularExpressions;
using TempoForge.App.Input;
using TempoForge.App.Localization;

namespace TempoForge.Tests;

public sealed class LocalizationTests
{
    [Fact]
    public void EveryTextExistsInBothLanguagesWithTheSamePlaceholders()
    {
        foreach (string key in Loc.Keys)
        {
            string pt = Loc.T(key, Loc.Portuguese), en = Loc.T(key, Loc.English);
            Assert.False(string.IsNullOrWhiteSpace(pt), key);
            Assert.False(string.IsNullOrWhiteSpace(en), key);
            Assert.Equal(Placeholders(pt), Placeholders(en));
        }
    }

    [Fact]
    public void UnknownKeyFallsBackToTheKeyItself() => Assert.Equal("NaoExiste", Loc.T("NaoExiste"));

    [Fact]
    public void ShortcutHelpHasNoMissingTexts()
    {
        Assert.All(Shortcuts.Help, entry =>
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.Keys));
            Assert.DoesNotMatch("^(Help|Key)[A-Z]", entry.Action);
        });
    }

    private static string[] Placeholders(string text) =>
        Regex.Matches(text, @"\{\d+\}").Select(m => m.Value).OrderBy(v => v).ToArray();
}
