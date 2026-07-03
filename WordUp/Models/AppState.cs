namespace WordUp.Models;

public sealed class AppState
{
    public UserProfile User { get; set; } = new();
    public List<Deck> Decks { get; set; } = [];
    public List<VocabularyWord> Words { get; set; } = [];
    public SettingsState Settings { get; set; } = new();
}
