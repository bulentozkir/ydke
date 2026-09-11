using System.Globalization;
using System.Text;

namespace YDKE_Windows;

/// <summary>Offline, deterministic study rules. Dates are supplied by the caller in local time.
/// Mutable models are UI-thread-owned: callers must not mutate them concurrently.</summary>
internal static class LearningEngine
{
    public const int MaxCounter = 1_000_000_000;
    public const int MaxIntervalDays = 36_500;
    public const int MaxCollectionCount = 100_000;
    public const int MaxSessionWords = 10_000;

    /// <summary>Updates only Reviews, not answer totals, KnownWords, or activity.
    /// Again: 1 day/reset repetitions/increment mistakes; Hard: 2 days initially then x1.2;
    /// Good: 3 days initially then x2; Easy: 7 days initially then x3 (rounded up).
    /// Same-day re-ratings use the existing interval without multiplying it again.
    /// New callers use RecordCardReview or RecordScoredAnswer for complete accounting.</summary>
    public static WordReview Rate(ProgressState progress, string key, RecallRating rating, DateOnly today)
    {
        ValidateProgress(progress);
        var review = PrepareReview(progress, key, rating, today);
        progress.Reviews[key] = review;
        return review;
    }

    private static WordReview PrepareReview(ProgressState progress, string key, RecallRating rating, DateOnly today)
    {
        ValidateWordKey(key);
        if (!Enum.IsDefined(rating)) throw new ArgumentOutOfRangeException(nameof(rating));
        if (today == DateOnly.MaxValue) throw new ArgumentOutOfRangeException(nameof(today), "No future review date is available.");
        var previous = progress.Reviews.GetValueOrDefault(key);
        if (previous?.LastReviewed > today)
            throw new ArgumentOutOfRangeException(nameof(today), "Cannot rate before the last review date.");
        if (previous is null && progress.Reviews.Count >= MaxCollectionCount)
            throw new InvalidDataException("Too many reviews.");

        var reviewed = previous?.LastReviewed is not null;
        var sameDay = previous?.LastReviewed == today;
        var interval = rating switch
        {
            RecallRating.Again => 1,
            RecallRating.Hard => !reviewed ? 2 : sameDay ? previous!.IntervalDays : (int)Math.Ceiling(previous!.IntervalDays * 1.2),
            RecallRating.Good => !reviewed ? 3 : sameDay ? previous!.IntervalDays : previous!.IntervalDays * 2,
            RecallRating.Easy => !reviewed ? 7 : sameDay ? previous!.IntervalDays : previous!.IntervalDays * 3,
            _ => throw new ArgumentOutOfRangeException(nameof(rating)),
        };
        interval = Math.Clamp(interval, 1, Math.Min(MaxIntervalDays, DateOnly.MaxValue.DayNumber - today.DayNumber));
        var review = new WordReview
        {
            LastReviewed = today,
            DueDate = today.AddDays(interval),
            IntervalDays = interval,
            Repetitions = rating == RecallRating.Again ? 0 : Math.Min(MaxCounter, (previous?.Repetitions ?? 0) + (sameDay ? 0 : 1)),
            Mistakes = Math.Min(MaxCounter, (previous?.Mistakes ?? 0) + (rating == RecallRating.Again ? 1 : 0)),
        };
        return review;
    }

    /// <summary>One explicit card self-rating: schedule + unique daily word + rating count +
    /// streak. Does NOT write objective answer totals, legacy activity, or KnownWords.
    /// Call once per accepted rating; UI must persist the entire progress snapshot.</summary>
    public static void RecordCardReview(ProgressState progress, string key, RecallRating rating, DateOnly today)
    {
        RecordReviews(progress, [key], rating, today);
        var name = rating.ToString();
        progress.RecallRatings[name] = Math.Min(MaxCounter, progress.RecallRatings.GetValueOrDefault(name) + 1);
    }

    /// <summary>One scored attempt, not one word: materialize/deduplicate ordinal keys and
    /// validate the ENTIRE batch before mutation. Rate each distinct key Good/Again, credit
    /// unique daily words, and increment Quiz or Game answers ONCE. Empty keys are valid
    /// for reading: count the objective answer and streak, but invent no vocabulary credit.
    /// Never writes RecallRatings, legacy counters/activity, KnownWords or CompletedGames.
    /// Caller owns duplicate-submit guards and saves progress atomically after the call.</summary>
    public static void RecordScoredAnswer(ProgressState progress, IEnumerable<string> wordKeys, bool correct, bool isGame, DateOnly today)
    {
        RecordReviews(progress, wordKeys, correct ? RecallRating.Good : RecallRating.Again, today);
        if (isGame)
        {
            if (correct) progress.GameCorrectAnswers = Math.Min(MaxCounter, progress.GameCorrectAnswers + 1);
            else progress.GameWrongAnswers = Math.Min(MaxCounter, progress.GameWrongAnswers + 1);
        }
        else
        {
            if (correct) progress.QuizCorrectAnswers = Math.Min(MaxCounter, progress.QuizCorrectAnswers + 1);
            else progress.QuizWrongAnswers = Math.Min(MaxCounter, progress.QuizWrongAnswers + 1);
        }
    }

    /// <summary>Call exactly once at a completed game's commit point, never on abandon or
    /// render. Only increments CompletedGames; caller supplies completion/idempotency guard.</summary>
    public static void RegisterGameCompleted(ProgressState progress)
    {
        ValidateProgress(progress);
        progress.CompletedGames = Math.Min(MaxCounter, progress.CompletedGames + 1);
    }

    private static void RecordReviews(ProgressState progress, IEnumerable<string> wordKeys, RecallRating rating, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(wordKeys);
        ValidateProgress(progress);
        // Bound even lazy input before computing or committing any reviews.
        var keys = wordKeys.Take(MaxCollectionCount + 1).ToArray();
        Require(keys.Length <= MaxCollectionCount, "Too many words in one attempt.");
        keys = keys.Distinct(StringComparer.Ordinal).ToArray();
        foreach (var key in keys) ValidateWordKey(key);
        Require(progress.Reviews.Count + keys.Count(key => !progress.Reviews.ContainsKey(key)) <= MaxCollectionCount, "Too many reviews.");
        var reviews = keys.Select(key => PrepareReview(progress, key, rating, today)).ToArray();
        var day = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var existing = progress.DailyReviewedWords.GetValueOrDefault(day);
        if (keys.Length > 0)
        {
            Require(existing is not null || progress.DailyReviewedWords.Count < MaxCollectionCount, "Too many review days.");
            Require((existing?.Count ?? 0) + keys.Count(key => existing?.Contains(key) != true) <= MaxCollectionCount, "Too many daily words.");
        }
        // All validation complete: nothing below can reject a later key after an earlier write.
        for (var index = 0; index < keys.Length; index++) progress.Reviews[keys[index]] = reviews[index];
        if (keys.Length > 0)
        {
            if (existing is null) progress.DailyReviewedWords[day] = existing = new HashSet<string>(StringComparer.Ordinal);
            existing.UnionWith(keys);
        }
        UpdateStreak(progress, today);
    }

    /// <summary>Due reviewed words remain due even when manually marked known. Only
    /// unreviewed manually known words are excluded from new-word selection.</summary>
    public static IReadOnlyList<VocabularyEntry> DueAndNew(IEnumerable<VocabularyEntry> words, ProgressState progress, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(words);
        var entries = words.Where(word => word is not null).DistinctBy(word => word.Key, StringComparer.Ordinal).ToArray();
        return DueWords(entries, progress, today).Concat(entries.Where(word =>
            !progress.KnownWords.Contains(word.Key) &&
            (!progress.Reviews.TryGetValue(word.Key, out var review) || review.LastReviewed is null))).ToArray();
    }

    /// <summary>Pure finite queue creation used for both normal and explicit retry sessions.
    /// No answer ever appends a word. Callers choose the subset and limit before creation.</summary>
    public static StudySessionState CreateSession(IEnumerable<string> wordKeys, bool isRetry = false)
    {
        ArgumentNullException.ThrowIfNull(wordKeys);
        var keys = wordKeys.Take(MaxSessionWords + 1).ToArray();
        Require(keys.Length <= MaxSessionWords, "Too many session words.");
        foreach (var key in keys) ValidateWordKey(key);
        var distinct = keys.Distinct(StringComparer.Ordinal).ToList();
        Require(distinct.Select(key => string.Join(":", key.Split(':').Take(2))).Distinct().Count() <= 1, "Cross-context session.");
        return new StudySessionState { WordKeys = distinct, IsRetry = isRetry };
    }

    public static IReadOnlyList<string> MissedKeys(StudySessionState session) =>
        session.WordKeys.Where(key => session.Answers.TryGetValue(key, out var correct) && !correct).Distinct(StringComparer.Ordinal).ToArray();

    public static int NextUnansweredIndex(StudySessionState session)
    {
        var index = session.WordKeys.FindIndex(key => !session.Answers.ContainsKey(key));
        return index < 0 ? session.WordKeys.Count : index;
    }

    /// <summary>Quiz's accepted-response gate, shared by UI and regression tests. Repeated
    /// submissions/navigation are no-ops; explicit retry sessions reschedule and credit
    /// unique words/streak only, never inflate first-attempt quiz accuracy or card ratings.</summary>
    public static bool RecordQuizResponse(ProgressState progress, StudySessionState session, string key, bool correct, DateOnly today)
    {
        if (session.Index >= session.WordKeys.Count || session.Index < 0 || session.WordKeys[session.Index] != key || session.Answers.ContainsKey(key)) return false;
        if (session.IsRetry) RecordReviews(progress, [key], correct ? RecallRating.Good : RecallRating.Again, today);
        else RecordScoredAnswer(progress, [key], correct, false, today);
        session.Answers[key] = correct;
        return true;
    }

    /// <summary>Reviewed words due on/before today, oldest due first; ties use ordinal keys.
    /// Unseen vocabulary and placeholders with null LastReviewed are excluded.</summary>
    public static IReadOnlyList<VocabularyEntry> DueWords(
        IEnumerable<VocabularyEntry> words, ProgressState progress, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(words);
        ValidateProgress(progress);
        return words.Where(word => word is not null &&
                progress.Reviews.TryGetValue(word.Key, out var review) &&
                review.LastReviewed.HasValue && review.DueDate <= today)
            .DistinctBy(word => word.Key, StringComparer.Ordinal)
            .OrderBy(word => progress.Reviews[word.Key].DueDate)
            .ThenBy(word => word.Key, StringComparer.Ordinal).ToArray();
    }

    /// <summary>Legacy compatibility ONLY: new flows must not call this mixed counter.
    /// Counts an action every call, but advances the streak only once per day.
    /// A backwards clock records activity without rewinding LastStudyDate or the streak.</summary>
    public static void RegisterActivity(ProgressState progress, DateOnly today)
    {
        ValidateProgress(progress);
        var key = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (!progress.DailyActivity.ContainsKey(key) && progress.DailyActivity.Count >= MaxCollectionCount)
            throw new InvalidDataException("Too many activity days.");
        progress.DailyActivity[key] = Math.Min(MaxCounter, progress.DailyActivity.GetValueOrDefault(key) + 1);
        UpdateStreak(progress, today);
    }

    private static void UpdateStreak(ProgressState progress, DateOnly today)
    {
        if (progress.LastStudyDate >= today) return;
        progress.StudyStreak = progress.LastStudyDate?.DayNumber == today.DayNumber - 1
            ? Math.Min(DateOnly.MaxValue.DayNumber, progress.StudyStreak + 1) : 1;
        progress.LastStudyDate = today;
    }

    private static readonly Dictionary<string, string[]> Articles = new(StringComparer.Ordinal)
    {
        ["en"] = ["a", "an", "the"],
        ["de"] = ["der", "die", "das", "ein", "eine", "den", "dem", "des", "einen", "einem", "einer", "eines"],
        ["fr"] = ["le", "la", "les", "un", "une", "des", "du", "l'"],
        ["it"] = ["il", "lo", "la", "i", "gli", "le", "un", "uno", "una", "l'", "un'"],
        ["es"] = ["el", "la", "los", "las", "un", "una", "unos", "unas"],
        ["pt"] = ["o", "a", "os", "as", "um", "uma", "uns", "umas"],
        ["nl"] = ["de", "het", "een"],
    };

    /// <summary>NFKC, invariant lowercase, canonical apostrophes/dashes and whitespace;
    /// strips one leading article and outer punctuation, never strips accents.
    /// Supply languageCode to avoid cross-language article ambiguity; null uses all languages.</summary>
    public static string NormalizeAnswer(string? answer, string? languageCode = null)
    {
        var text = NormalizeText(answer).Trim(' ', '.', ',', '!', '?', ';', ':', '"', '\'', '(', ')', '[', ']', '«', '»', '“', '”');
        var articles = languageCode is null ? Articles.Values.SelectMany(value => value) :
            Articles.GetValueOrDefault(languageCode, []);
        foreach (var article in articles.Distinct(StringComparer.Ordinal).OrderByDescending(value => value.Length))
        {
            var prefix = article.EndsWith('\'') ? article : article + " ";
            if (text.StartsWith(prefix, StringComparison.Ordinal) && text.Length > prefix.Length)
                return text[prefix.Length..].Trim();
        }
        return text;
    }

    /// <summary>Checks a candidate against a rack as Unicode text-element multisets.
    /// Spaces/punctuation consume no tiles. Accents and repeated tiles matter; no wildcards.
    /// Neither candidate nor rack has articles stripped (rack letters are literal).</summary>
    public static bool CanBuildFromRack(string? answer, string? rack)
    {
        var needed = Tiles(answer).ToArray();
        if (needed.Length == 0) return false;
        var available = Tiles(rack).GroupBy(tile => tile, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        foreach (var tile in needed)
        {
            if (!available.TryGetValue(tile, out var count) || count == 0) return false;
            available[tile] = count - 1;
        }
        return true;
    }

    private static IEnumerable<string> Tiles(string? value)
    {
        var elements = StringInfo.GetTextElementEnumerator(NormalizeText(value));
        while (elements.MoveNext())
        {
            var element = elements.GetTextElement();
            if (Rune.IsLetterOrDigit(Rune.GetRuneAt(element, 0))) yield return element;
        }
    }

    private static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        if (value.Length > 8192) throw new ArgumentException("Answer/rack is too long.", nameof(value));
        var builder = new StringBuilder();
        foreach (var rune in value.Normalize(NormalizationForm.FormKC).EnumerateRunes())
        {
            if (Rune.GetUnicodeCategory(rune) == UnicodeCategory.Format) continue;
            if (Rune.IsWhiteSpace(rune))
            {
                if (builder.Length > 0 && builder[^1] != ' ') builder.Append(' ');
            }
            else if (rune.Value is 0x2018 or 0x2019 or 0x02BC or 0xFF07) builder.Append('\'');
            else if (Rune.GetUnicodeCategory(rune) == UnicodeCategory.DashPunctuation) builder.Append('-');
            else builder.Append(Rune.ToLowerInvariant(rune).ToString());
        }
        return builder.ToString().Trim().Normalize(NormalizationForm.FormC);
    }

    /// <summary>Strict non-mutating validation, shared by storage and all study mutations.
    /// Missing fields deserialize to defaults; explicit null collections are invalid.</summary>
    public static void ValidateProgress(ProgressState progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        Require(progress.SchemaVersion == 1, "Unsupported progress schema version.");
        CheckCount(progress.KnownWords, nameof(progress.KnownWords));
        CheckCount(progress.FavoriteWords, nameof(progress.FavoriteWords));
        CheckCount(progress.GameBestScores, nameof(progress.GameBestScores));
        CheckCount(progress.Reviews, nameof(progress.Reviews));
        CheckCount(progress.DailyActivity, nameof(progress.DailyActivity));
        CheckCount(progress.CardPositions, nameof(progress.CardPositions));
        CheckCount(progress.Sessions, nameof(progress.Sessions));
        CheckCount(progress.RecallRatings, nameof(progress.RecallRatings), 4);
        CheckCount(progress.DailyReviewedWords, nameof(progress.DailyReviewedWords));
        Counter(progress.CorrectAnswers); Counter(progress.WrongAnswers);
        Counter(progress.QuizCorrectAnswers); Counter(progress.QuizWrongAnswers);
        Counter(progress.GameCorrectAnswers); Counter(progress.GameWrongAnswers); Counter(progress.CompletedGames);
        foreach (var (name, count) in progress.RecallRatings)
        {
            Require(Enum.GetNames<RecallRating>().Contains(name, StringComparer.Ordinal), "Invalid recall rating name.");
            Counter(count);
        }
        foreach (var (day, words) in progress.DailyReviewedWords)
        {
            Require(DateOnly.TryParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _), "Invalid reviewed-word day.");
            CheckCount(words, "Daily reviewed words");
            foreach (var word in words) ValidateWordKey(word);
        }
        Require(progress.StudyStreak >= 0 && progress.StudyStreak <= DateOnly.MaxValue.DayNumber, "Invalid study streak.");
        Require(progress.LastStudyDate.HasValue || progress.StudyStreak == 0, "Streak requires LastStudyDate.");
        foreach (var key in progress.KnownWords) ValidateWordKey(key);
        foreach (var key in progress.FavoriteWords) ValidateWordKey(key);
        foreach (var (key, score) in progress.GameBestScores)
        {
            Require(Identifier(key), "Invalid game key."); Counter(score);
        }
        foreach (var (key, review) in progress.Reviews)
        {
            ValidateWordKey(key);
            Require(review is not null, "Null review.");
            if (review is null) continue;
            Counter(review.Repetitions); Counter(review.Mistakes);
            Require(review.IntervalDays is >= 0 and <= MaxIntervalDays, "Invalid review interval.");
            if (review.LastReviewed is { } last)
                Require(review.IntervalDays > 0 && review.DueDate.DayNumber - last.DayNumber == review.IntervalDays, "Review dates do not match interval.");
            else
                Require(review.IntervalDays == 0 && review.Repetitions == 0 && review.Mistakes == 0, "Unreviewed placeholder has review history.");
        }
        foreach (var (date, count) in progress.DailyActivity)
        {
            Require(DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _), "Invalid activity date key.");
            Require(count > 0, "Activity count must be positive."); Counter(count);
        }
        foreach (var (context, word) in progress.CardPositions)
        {
            ValidateContext(context); ValidateWordKey(word);
            Require(word.StartsWith(context + ":", StringComparison.Ordinal), "Card position belongs to another context.");
        }
        foreach (var (key, session) in progress.Sessions)
        {
            Require(key is not null, "Null session key.");
            var parts = key!.Split(':');
            Require(parts.Length == 3 && Identifier(parts[2]), "Invalid session key.");
            var context = parts[0] + ":" + parts[1];
            ValidateContext(context);
            Require(session is not null, "Null session.");
            if (session is null) continue;
            CheckCount(session.WordKeys, "Session words", MaxSessionWords);
            CheckCount(session.Answers, "Session answers", MaxSessionWords);
            Require(session.Index >= 0 && session.Index <= session.WordKeys.Count, "Invalid session index.");
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var word in session.WordKeys)
            {
                ValidateWordKey(word);
                Require(word.StartsWith(context + ":", StringComparison.Ordinal) && keys.Add(word), "Duplicate or cross-context session word.");
            }
            foreach (var word in session.Answers.Keys)
                Require(keys.Contains(word), "Session answer is not in its word list.");
        }
    }

    public static void ValidateSettings(UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Require(settings.SchemaVersion == 1, "Unsupported settings schema version.");
        Require(settings.UiLanguage is "tr" or "en" or "de" or "fr" or "es" or "pt" or "nl", "Invalid UI language.");
        ValidateContext(settings.StudyLanguage + ":" + settings.Level);
        CheckCount(settings.LastStudyLevels, nameof(settings.LastStudyLevels), 7);
        foreach (var (language, level) in settings.LastStudyLevels) ValidateContext(language + ":" + level);
        Require(double.IsFinite(settings.FontScale) && settings.FontScale is >= 0.85 and <= 1.40, "Invalid font scale.");
        Require(settings.DailyGoal is >= 1 and <= 10_000, "Daily goal must be between 1 and 10000.");
        foreach (var color in new[] { settings.AppBackgroundColor, settings.ButtonColor, settings.BoxColor })
            Require(color is { Length: 7 } && color[0] == '#' && color.Skip(1).All(char.IsAsciiHexDigit), "Color must be #RRGGBB.");
    }

    /// <summary>One UI settings mutation (caller saves once and rolls back on failure).
    /// Persist outgoing AND incoming levels together; no session-history heuristics.</summary>
    public static void ChangeStudySelection(UserSettings settings, string language, string? level = null)
    {
        ValidateSettings(settings);
        var selectedLevel = level ?? (language == settings.StudyLanguage ? settings.Level : settings.LastStudyLevels.GetValueOrDefault(language, "A1"));
        ValidateContext(language + ":" + selectedLevel);
        settings.LastStudyLevels[settings.StudyLanguage] = settings.Level;
        settings.LastStudyLevels[language] = selectedLevel;
        settings.StudyLanguage = language;
        settings.Level = selectedLevel;
    }

    private static void ValidateContext(string context)
    {
        var parts = context.Split(':');
        Require(parts.Length == 2 && parts[0] is "en" or "de" or "fr" or "it" or "es" or "pt" or "nl" &&
            parts[1] is "A1" or "A2" or "B1" or "B2" or "C1" or "C2", "Invalid language:level context.");
    }

    internal static void ValidateWordKey(string key)
    {
        Require(!string.IsNullOrWhiteSpace(key) && key.Length <= 1024 && !key.Any(char.IsControl), "Invalid word key.");
        var parts = key.Split(':', 3);
        Require(parts.Length == 3 && !string.IsNullOrWhiteSpace(parts[2]), "Word key must be language:level:word.");
        ValidateContext(parts[0] + ":" + parts[1]);
        try { _ = key.Normalize(); }
        catch (ArgumentException ex) { throw new InvalidDataException("Invalid Unicode word key.", ex); }
    }

    private static bool Identifier(string? value) => value is { Length: > 0 and <= 64 } &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

    private static void CheckCount<T>(ICollection<T>? collection, string name, int maximum = MaxCollectionCount) =>
        Require(collection is not null && collection.Count <= maximum, $"Invalid {name} collection.");

    private static void Counter(int value) => Require(value is >= 0 and <= MaxCounter, "Counter is out of bounds.");

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}