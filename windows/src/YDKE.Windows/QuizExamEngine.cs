namespace YDKE_Windows;

/// <summary>Numbered, stable full-level quizzes. Uses the existing validated session
/// schema; choosing a tile never changes review/accuracy counters or another exam.</summary>
internal static class QuizExamEngine
{
    public const int QuestionsPerExam = 20;

    public static VocabularyEntry[] CreatePool(IEnumerable<VocabularyEntry> words, string language, string level) => words
        .Where(word => word.LanguageCode == language && word.Level == level)
        .DistinctBy(word => word.Key, StringComparer.Ordinal)
        .OrderBy(word => word.Word, StringComparer.OrdinalIgnoreCase)
        .ThenBy(word => word.Key, StringComparer.Ordinal)
        .ToArray();

    public static int ExamCount(int wordCount) => wordCount / QuestionsPerExam + (wordCount % QuestionsPerExam == 0 ? 0 : 1);

    public static string SessionKey(string context, int examIndex) =>
        $"{context}:quiz-exam-{examIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

    public static string OptionsKey(string context, int examIndex, int questionIndex) =>
        $"{SessionKey(context, examIndex)}-options-{questionIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

    public static VocabularyEntry[] ExamWords(IReadOnlyList<VocabularyEntry> pool, int examIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(examIndex);
        if (examIndex >= ExamCount(pool.Count)) throw new ArgumentOutOfRangeException(nameof(examIndex));
        return pool.Skip(examIndex * QuestionsPerExam).Take(QuestionsPerExam).ToArray();
    }

    public static bool TryGetExamIndex(IReadOnlyList<VocabularyEntry> pool, StudySessionState? session, out int examIndex)
    {
        examIndex = -1;
        if (session is null || session.WordKeys.Count == 0) return false;
        var start = -1;
        for (var index = 0; index < pool.Count; index++)
            if (pool[index].Key == session.WordKeys[0]) { start = index; break; }
        if (start < 0 || start % QuestionsPerExam != 0 ||
            session.WordKeys.Count != Math.Min(QuestionsPerExam, pool.Count - start)) return false;
        for (var index = 0; index < session.WordKeys.Count; index++)
            if (pool[start + index].Key != session.WordKeys[index]) return false;
        examIndex = start / QuestionsPerExam;
        return true;
    }

    /// <summary>Pure lookup. Only a complete, grid-aligned quiz with intact options
    /// can resume. An old eight-word prefix is NOT a twenty-word exam.</summary>
    public static StudySessionState? SavedExam(ProgressState progress, IReadOnlyList<VocabularyEntry> pool, int examIndex)
    {
        if (examIndex < 0 || examIndex >= ExamCount(pool.Count)) return null;
        var context = Context(pool);
        var key = SessionKey(context, examIndex);
        var named = progress.Sessions.GetValueOrDefault(key);
        if (named is not null) return Valid(named, key + "-options-") ? named : null;
        var legacy = progress.Sessions.GetValueOrDefault(context + ":quiz");
        return legacy is not null && Valid(legacy, context + ":quiz-options-") ? legacy : null;

        bool Valid(StudySessionState session, string optionPrefix) =>
            TryGetExamIndex(pool, session, out var savedIndex) && savedIndex == examIndex &&
            HasValidOptions(progress, pool, session, optionPrefix);
    }

    /// <summary>Prepare all questions/options before mutation. Migrates a valid old
    /// twenty-word exam on selection only; leaves unrelated/legacy history untouched.
    /// Restart is the whole island and retains retry attribution, never an eight-word subset.</summary>
    public static StudySessionState OpenExam(ProgressState progress, IReadOnlyList<VocabularyEntry> pool,
        int examIndex, Random random, bool restart = false)
    {
        ArgumentNullException.ThrowIfNull(random);
        LearningEngine.ValidateProgress(progress);
        var entries = ExamWords(pool, examIndex);
        var context = Context(pool);
        if (pool.Any(word => $"{word.LanguageCode}:{word.Level}" != context) ||
            pool.Select(word => word.Key).Distinct(StringComparer.Ordinal).Count() != pool.Count)
            throw new InvalidDataException("Quiz pool must have unique words from one language and level.");
        if (pool.Select(word => word.Word).Distinct(StringComparer.OrdinalIgnoreCase).Take(2).Count() < 2)
            throw new InvalidDataException("Quiz requires at least two different words.");

        var key = SessionKey(context, examIndex);
        var saved = restart ? null : SavedExam(progress, pool, examIndex);
        if (saved is not null && ReferenceEquals(saved, progress.Sessions.GetValueOrDefault(key))) return saved;
        var prepared = new Dictionary<string, StudySessionState>(StringComparer.Ordinal);
        StudySessionState session;
        if (saved is not null)
        {
            session = Copy(saved);
            for (var index = 0; index < entries.Length; index++)
                prepared[OptionsKey(context, examIndex, index)] = Copy(progress.Sessions[
                    context + ":quiz-options-" + index.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
        }
        else
        {
            session = LearningEngine.CreateSession(entries.Select(word => word.Key), isRetry: restart);
            for (var index = 0; index < entries.Length; index++)
            {
                var answer = entries[index];
                var choices = pool.Where(word => !string.Equals(word.Word, answer.Word, StringComparison.OrdinalIgnoreCase))
                    .DistinctBy(word => word.Word, StringComparer.OrdinalIgnoreCase).OrderBy(_ => random.Next()).Take(3)
                    .Append(answer).OrderBy(_ => random.Next()).Select(word => word.Key);
                prepared[OptionsKey(context, examIndex, index)] = LearningEngine.CreateSession(choices);
            }
        }
        prepared[key] = session;
        if (progress.Sessions.Count + prepared.Keys.Count(candidate => !progress.Sessions.ContainsKey(candidate)) > LearningEngine.MaxCollectionCount)
            throw new InvalidDataException("Too many saved sessions.");
        foreach (var stale in progress.Sessions.Keys.Where(candidate => candidate.StartsWith(key + "-options-", StringComparison.Ordinal)).ToArray())
            progress.Sessions.Remove(stale);
        foreach (var item in prepared) progress.Sessions[item.Key] = item.Value;
        return session;
    }

    private static string Context(IReadOnlyList<VocabularyEntry> pool) => $"{pool[0].LanguageCode}:{pool[0].Level}";

    private static StudySessionState Copy(StudySessionState session) => new()
    {
        WordKeys = [.. session.WordKeys], Index = session.Index, IsRetry = session.IsRetry,
        Answers = new Dictionary<string, bool>(session.Answers, StringComparer.Ordinal),
    };

    private static bool HasValidOptions(ProgressState progress, IReadOnlyList<VocabularyEntry> pool,
        StudySessionState session, string optionPrefix)
    {
        if (session.Index < 0 || session.Index > session.WordKeys.Count ||
            session.Answers.Keys.Any(key => !session.WordKeys.Contains(key, StringComparer.Ordinal))) return false;
        var words = pool.ToDictionary(word => word.Key, StringComparer.Ordinal);
        var expectedOptions = Math.Min(4, pool.Select(word => word.Word).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        if (expectedOptions < 2) return false;
        for (var index = 0; index < session.WordKeys.Count; index++)
        {
            var answerKey = session.WordKeys[index];
            var options = progress.Sessions.GetValueOrDefault(optionPrefix + index.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (options is null || options.WordKeys.Count != expectedOptions ||
                !options.WordKeys.Contains(answerKey, StringComparer.Ordinal) || options.WordKeys.Any(key => !words.ContainsKey(key)) ||
                options.WordKeys.Select(key => words[key].Word).Distinct(StringComparer.OrdinalIgnoreCase).Count() != options.WordKeys.Count)
                return false;
            if (session.Answers.TryGetValue(answerKey, out var correct))
            {
                if (options.Answers.Count != 1 || options.Index != options.WordKeys.Count) return false;
                var selected = options.Answers.Single();
                if (!options.WordKeys.Contains(selected.Key, StringComparer.Ordinal) || selected.Value != correct ||
                    (selected.Key == answerKey) != correct) return false;
            }
            else if (index < session.Index || options.Answers.Count != 0 || options.Index != 0) return false;
        }
        return true;
    }
}