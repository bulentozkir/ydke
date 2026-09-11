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

internal sealed class UserSettings : System.Text.Json.Serialization.IJsonOnDeserialized
{
    // Missing in legacy local files; unknown explicit versions are rejected.
    public int SchemaVersion { get; set; } = 1;

    public string UiLanguage { get; set; } = "tr";

    public string StudyLanguage { get; set; } = "en";

    public string Level { get; set; } = "A1";

    // Persisted per-language selection. Level is authoritative for StudyLanguage on load.
    public Dictionary<string, string> LastStudyLevels { get; set; } = [];

    void System.Text.Json.Serialization.IJsonOnDeserialized.OnDeserialized()
    {
        LearningEngine.ValidateSettings(this);
        LastStudyLevels[StudyLanguage] = Level;
    }

    public double FontScale { get; set; } = 1;

    public string AppBackgroundColor { get; set; } = AppearancePalette.DefaultBackground;

    public string ButtonColor { get; set; } = AppearancePalette.DefaultButton;

    public string BoxColor { get; set; } = AppearancePalette.DefaultBox;

    public int DailyGoal { get; set; } = 20;

    public bool ReduceMotion { get; set; }

    public bool UntimedPractice { get; set; }

    // Migration only. Storage always reads/writes this as false; no cloud service exists.
    public bool CloudConnected { get; set; }
}

internal sealed class ProgressState
{
    public int SchemaVersion { get; set; } = 1;

    // Explicit manual bookmarks (U), NOT inferred mastery. Rating never changes these.
    public HashSet<string> KnownWords { get; set; } = [];

    public HashSet<string> FavoriteWords { get; set; } = [];

    public Dictionary<string, int> GameBestScores { get; set; } = [];

    // Historical mixed counters: retain for compatibility; new flows must not write them.
    public int CorrectAnswers { get; set; }

    public int WrongAnswers { get; set; }

    public int QuizCorrectAnswers { get; set; }
    public int QuizWrongAnswers { get; set; }
    public int GameCorrectAnswers { get; set; }
    public int GameWrongAnswers { get; set; }
    public int CompletedGames { get; set; }

    // Exact RecallRating enum names, self-rated cards only; never objective accuracy.
    public Dictionary<string, int> RecallRatings { get; set; } = [];

    // ISO local day -> ordinal distinct vocabulary keys, across ALL languages/modes.
    // Repeated attempts count once toward the daily word goal. Reading has no word credit.
    public Dictionary<string, HashSet<string>> DailyReviewedWords { get; set; } = [];

    public int StudyStreak { get; set; }

    public DateOnly? LastStudyDate { get; set; }

    // Exact VocabularyEntry.Key (language:level:word); ordinal, accent-sensitive keys.
    public Dictionary<string, WordReview> Reviews { get; set; } = [];

    // Historical mixed activity only. New actions use DailyReviewedWords, not this field.
    public Dictionary<string, int> DailyActivity { get; set; } = [];

    // language:level -> VocabularyEntry.Key, never a fragile dataset array index.
    public Dictionary<string, string> CardPositions { get; set; } = [];

    // language:level:mode -> resumable session; mode is an ASCII identifier.
    public Dictionary<string, StudySessionState> Sessions { get; set; } = [];
}

internal enum RecallRating { Again, Hard, Good, Easy }

internal sealed class WordReview
{
    public DateOnly DueDate { get; set; }

    public int IntervalDays { get; set; }

    // Consecutive non-Again ratings, not the total number of study actions.
    public int Repetitions { get; set; }

    public int Mistakes { get; set; }

    // Null denotes an unreviewed placeholder, which is never returned by DueWords.
    public DateOnly? LastReviewed { get; set; }
}

internal sealed class StudySessionState
{
    // Explicit finite missed-item practice; quiz retry answers do not change first-attempt accuracy.
    public bool IsRetry { get; set; }

    public List<string> WordKeys { get; set; } = [];

    // Zero-based current item; Count means completed (including an empty session).
    public int Index { get; set; }

    // VocabularyEntry.Key -> correctness; keys must belong to WordKeys.
    public Dictionary<string, bool> Answers { get; set; } = [];
}