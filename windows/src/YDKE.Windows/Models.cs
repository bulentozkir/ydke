namespace YDKE_Windows;

internal sealed record LanguageOption(string Code, string NativeName);

internal sealed record StudyLanguage(
    string Code,
    string NativeName,
    IReadOnlyDictionary<string, string> Files)
{
    public IReadOnlyList<string> Levels { get; } = Files.Keys.ToArray();
}

internal sealed record VocabularyEntry(
    string Word,
    string PartOfSpeech,
    string Level,
    string Category,
    string Definition,
    string Example,
    string LanguageCode)
{
    public string Key => $"{LanguageCode}:{Level}:{Word}";
}

internal enum GameGroup
{
    Simple,
    Complex,
}

internal enum GameMechanic
{
    Hangman,
    MultipleChoice,
    LetterTiles,
    Memory,
    TimedChoice,
    TimedTyping,
    WordSequence,
    ListeningTyping,
    TrueFalse,
    Survival,
    LetterGrid,
    Reading,
    SemanticLinks,
    OddOneOut,
    Classification,
    Bingo,
    BossBattle,
    ClueWords,
    Crossword,
    DailyGuess,
    ListeningChoice,
    Scrabble,
    CategorySprint,
    ProgressiveClues,
}

internal sealed record GameDefinition(
    string Id,
    string Title,
    string DescriptionTr,
    string DescriptionEn,
    string Glyph,
    GameGroup Group,
    GameMechanic Mechanic);

internal sealed class GameSession(GameDefinition game)
{
    public GameDefinition Game { get; } = game;

    public int Score { get; set; }

    public int Streak { get; set; }

    public int Lives { get; set; } = 3;

    public int Round { get; set; } = 1;

    public int MaxRounds { get; } = 10;

    public int SecondsRemaining { get; set; } = game.Mechanic is
        GameMechanic.TimedChoice or GameMechanic.TimedTyping or GameMechanic.CategorySprint ? 60 : 0;

    public bool IsTimed => SecondsRemaining > 0;
}

internal sealed class UserSettings
{
    public string UiLanguage { get; set; } = "tr";

    public string StudyLanguage { get; set; } = "en";

    public string Level { get; set; } = "A1";

    public double FontScale { get; set; } = 1;

    public string AppBackgroundColor { get; set; } = AppearancePalette.DefaultBackground;

    public string ButtonColor { get; set; } = AppearancePalette.DefaultButton;

    public string BoxColor { get; set; } = AppearancePalette.DefaultBox;
}

internal sealed class ProgressState
{
    public HashSet<string> KnownWords { get; set; } = [];

    public HashSet<string> FavoriteWords { get; set; } = [];

    public Dictionary<string, int> GameBestScores { get; set; } = [];

    public int CorrectAnswers { get; set; }

    public int WrongAnswers { get; set; }

    public int StudyStreak { get; set; }

    public DateOnly? LastStudyDate { get; set; }
}