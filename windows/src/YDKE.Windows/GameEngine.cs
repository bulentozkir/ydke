using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace YDKE_Windows;

/// <summary>UI-thread-owned turn arbitration: close before the first await; stale
/// callbacks cannot close or tick a newer turn. Busy includes saves, audio and feedback.</summary>
internal sealed class GameTurnGate
{
    public int Version { get; private set; }
    public bool Closed { get; private set; }
    public int Begin() { Version++; Closed = false; return Version; }
    public bool TryClose(int version)
    {
        if (version != Version || Closed) return false;
        Closed = true; return true;
    }
    public bool CanTick(int version, bool busy) => version == Version && !Closed && !busy;
}

/// <summary>One UI-owned save intent. The caller's snapshot transaction returns false
/// after rollback. Only a successful save commits the gate; retries and duplicate
/// callbacks never reapply a committed mutation. A stale continuation cannot advance UI.</summary>
internal sealed class GameSaveGate
{
    public bool Committed { get; private set; }
    private bool _saving;

    public async Task<bool> SaveAsync(Func<Task<bool>> saveSnapshot, Func<bool> isCurrent)
    {
        if (_saving || !isCurrent()) return false;
        if (Committed) return true;
        _saving = true;
        try
        {
            if (await saveSnapshot()) Committed = true;
            return Committed && isCurrent();
        }
        finally { _saving = false; }
    }
}

/// <summary>Pure offline rules. Never infers semantic relationships from category labels.</summary>
internal static class GameEngine
{
    public const int TimedSeconds = 60;

    public static bool HasClock(GameDefinition game) => game.Mechanic is
        GameMechanic.TimedChoice or GameMechanic.TimedTyping or GameMechanic.CategorySprint;

    public static string Duration(GameDefinition game, bool practice) => HasClock(game)
        ? practice ? "practice" : "minute"
        : game.Id is "bossrush" or "codycross" or "readingcomprehension" or "crossword" or "memory" or "bingo" ? "long" : "short";

    // Stable mode, NOT IsTimed (which becomes false at zero). Survival ends on a miss,
    // never at an arbitrary tenth round. Practice removes the clock, not round/life limits.
    public static int? RoundLimit(GameSession session, string mode) =>
        mode == "timed" || session.Game.Mechanic == GameMechanic.Survival ? null :
        session.Game.Id == "dailychallenge" ? 1 : session.MaxRounds;

    public static (int Maximum, int Value, bool Visible) Progress(GameSession session, string mode)
    {
        if (mode == "timed") return (TimedSeconds, TimedSeconds - Math.Clamp(session.SecondsRemaining, 0, TimedSeconds), true);
        return RoundLimit(session, mode) is { } rounds
            ? (rounds, Math.Clamp(session.Round - 1, 0, rounds), true) : (1, 0, false);
    }

    /// <summary>Relationship dataset terms are not vocabulary keys. Credit only ordinal
    /// exact headwords actually present in the current vocabulary context; no article,
    /// case, accent or feedback-text normalization and no fabricated fallback keys.</summary>
    public static IReadOnlyList<string> SemanticWordKeys(SemanticPair pair, IEnumerable<VocabularyEntry> words, string language, string level) =>
        words.Where(word => word.LanguageCode == language && word.Level == level &&
                (string.Equals(word.Word, pair.Word, StringComparison.Ordinal) || string.Equals(word.Word, pair.Related, StringComparison.Ordinal)))
            .Select(word => word.Key).Distinct(StringComparer.Ordinal).ToArray();

    // Storage's existing identifier contract does not allow colons in score keys.
    // This is a reversible encoding of language:level:mode:game, not a schema change.
    public static string ScoreKey(string language, string level, string mode, string id) => $"{language}_{level}_{mode}_{id}";
    public static string NativeText(string value) => value.Split([" - ", ";"], StringSplitOptions.None)[0].Trim();
    public static string Bare(VocabularyEntry word) => LearningEngine.NormalizeAnswer(word.Word, word.LanguageCode);
    public static string[] Tokens(string sentence) => Regex.Split(sentence.Trim(), @"\s+").Where(s => s.Length > 0).ToArray();
    public static string SentenceKey(string sentence) => string.Join(' ', Tokens(sentence)).Normalize(NormalizationForm.FormKC);
    public static bool UsableExample(string sentence) => sentence.Length is >= 12 and <= 240 && Tokens(sentence).Length >= 3 &&
        !sentence.Contains("No example", StringComparison.OrdinalIgnoreCase) &&
        !sentence.Contains("example sentence available", StringComparison.OrdinalIgnoreCase) &&
        !sentence.Contains("http", StringComparison.OrdinalIgnoreCase);
    public static string? Cloze(VocabularyEntry entry)
    {
        var sentence = NativeText(entry.Example);
        var target = Bare(entry);
        if (target.Length < 2 || !UsableExample(sentence)) return null;
        var pattern = @"(?<![\p{L}\p{M}\p{N}])" + Regex.Escape(target) + @"(?![\p{L}\p{M}\p{N}])";
        var masked = Regex.Replace(sentence, pattern, "_____", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return masked == sentence ? null : masked;
    }

    public static string? WordClass(string value) => value.Trim().ToLowerInvariant() switch
    {
        "noun" or "n" or "n." => "noun",
        "verb" or "v" or "v." => "verb",
        "adjective" or "adj" or "adj." => "adjective",
        "adverb" or "adv" or "adv." => "adverb",
        _ => null, // ambiguous/multiple/unknown classes are not scored as one class
    };

    public static IEnumerable<IGrouping<string, VocabularyEntry>> Categories(IEnumerable<VocabularyEntry> words, int minimum) =>
        words.Where(w => !string.IsNullOrWhiteSpace(w.Category) && !w.Category.Equals("General", StringComparison.OrdinalIgnoreCase))
            .DistinctBy(Bare).GroupBy(w => w.Category).Where(g => g.Count() >= minimum);

    public static VocabularyEntry? RackWord(IEnumerable<VocabularyEntry> words, string guess, string rack, ISet<string> found, string language)
    {
        var bare = LearningEngine.NormalizeAnswer(guess, language);
        if (found.Contains(bare) || !LearningEngine.CanBuildFromRack(bare, rack)) return null;
        return words.FirstOrDefault(w => Bare(w) == bare);
    }

    public sealed record Crossing(VocabularyEntry Across, VocabularyEntry Down, int AcrossIndex, int DownIndex);
    public static Crossing? FindCrossing(IEnumerable<VocabularyEntry> words)
    {
        var pool = words.Where(w => Bare(w).Length is >= 3 and <= 8 && Bare(w).All(char.IsLetter)).DistinctBy(Bare).Take(180).ToArray();
        foreach (var a in pool)
        foreach (var b in pool)
        {
            if (a == b) continue;
            var across = Bare(a); var down = Bare(b);
            for (var x = 0; x < across.Length; x++)
            for (var y = 0; y < down.Length; y++)
                if (across[x] == down[y]) return new(a, b, x, y);
        }
        return null;
    }

    public static int[] LetterFeedback(string guess, string target)
    {
        if (guess.Length != target.Length) throw new ArgumentException("Lengths differ.");
        var result = new int[guess.Length];
        var left = new Dictionary<char, int>();
        for (var i = 0; i < target.Length; i++)
            if (guess[i] == target[i]) result[i] = 2;
            else left[target[i]] = left.GetValueOrDefault(target[i]) + 1;
        for (var i = 0; i < guess.Length; i++)
            if (result[i] == 0 && left.GetValueOrDefault(guess[i]) > 0) { result[i] = 1; left[guess[i]]--; }
        return result;
    }

    public static bool StraightSelection(IReadOnlyList<int> cells, int width)
    {
        if (cells.Count < 2 || cells.Distinct().Count() != cells.Count) return false;
        var dx = cells[1] % width - cells[0] % width;
        var dy = cells[1] / width - cells[0] / width;
        if (Math.Abs(dx) > 1 || Math.Abs(dy) > 1 || (dx == 0 && dy == 0)) return false;
        return Enumerable.Range(1, cells.Count - 1).All(i =>
            cells[i] % width - cells[i - 1] % width == dx && cells[i] / width - cells[i - 1] / width == dy);
    }

    public static bool BingoLine(ISet<int> found) => Enumerable.Range(0, 4).Any(n =>
        Enumerable.Range(0, 4).All(i => found.Contains(n * 4 + i)) || Enumerable.Range(0, 4).All(i => found.Contains(i * 4 + n))) ||
        Enumerable.Range(0, 4).All(i => found.Contains(i * 5)) || Enumerable.Range(0, 4).All(i => found.Contains((i + 1) * 3));

    public static int DailyIndex(int count, string context, DateOnly day)
    {
        uint hash = 2166136261;
        foreach (var c in context + day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)) hash = unchecked((hash ^ c) * 16777619);
        return (int)(hash % (uint)count);
    }
}

internal sealed record ReadingQuestion(string Prompt, string[] Options, int Correct, string Hint, string Explanation);
internal sealed record ReadingPassage(string Title, string Text, ReadingQuestion[] Questions);
internal sealed record SemanticPair(string Word, string Related, bool Antonym);

/// <summary>Reads the packaged data only, using a deliberately small non-executing JS literal parser.</summary>
internal static class LocalGameData
{
    public static ReadingPassage[] ReadPassages(string source, string level)
    {
        return LiteralArray(source).OfType<JsonObject>().Where(o => Text(o, "level") == level)
            .Select(o => new ReadingPassage(Text(o, "title"), Text(o, "text"),
                (o["questions"] as JsonArray ?? []).OfType<JsonObject>().Select(q => new ReadingQuestion(
                    Text(q, "q"), (q["options"] as JsonArray ?? []).Select(v => v?.GetValue<string>() ?? "").ToArray(),
                    q["correct"]?.GetValue<int>() ?? -1, Text(q, "hint"), Text(q, "explain")))
                .Where(q => q.Options.Length == 4 && q.Correct is >= 0 and < 4 && q.Options.All(s => !string.IsNullOrWhiteSpace(s)) &&
                    q.Options.Distinct().Count() == 4 && q.Prompt.Length > 0).ToArray()))
            .Where(p => p.Text.Length > 0 && p.Questions.Length > 0).ToArray();
    }

    public static SemanticPair[] ReadRelationships(string source, string level)
    {
        var records = LiteralArray(source).OfType<JsonObject>().ToArray();
        var byWord = records.GroupBy(o => Text(o, "word"), StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        string[] Links(JsonObject o, string field) => Text(o, field).Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var result = new List<SemanticPair>();
        foreach (var o in records.Where(o => Text(o, "level") == level))
        foreach (var field in new[] { "synonyms", "antonyms" })
        foreach (var related in Links(o, field))
        {
            var word = Text(o, "word");
            var opposite = field == "synonyms" ? "antonyms" : "synonyms";
            // Require reciprocal, non-conflicting source evidence. No invented distractors.
            if (!word.Equals(related, StringComparison.OrdinalIgnoreCase) && byWord.TryGetValue(related, out var other) &&
                Links(other, field).Contains(word, StringComparer.OrdinalIgnoreCase) &&
                !Links(o, opposite).Contains(related, StringComparer.OrdinalIgnoreCase) &&
                !Links(other, opposite).Contains(word, StringComparer.OrdinalIgnoreCase))
                result.Add(new(word, related, field == "antonyms"));
        }
        return result.Distinct().ToArray();
    }

    private static string Text(JsonObject o, string field) => o[field]?.GetValue<string>() ?? "";
    internal static JsonArray LiteralArray(string source)
    {
        // The anchor is outside the licence/schema comments (which also contain '[').
        var assignment = Regex.Match(source, @"window\.[A-Z_]+\s*=\s*\[");
        if (!assignment.Success) throw new InvalidDataException("Missing local dataset assignment.");
        return (JsonArray)new LiteralReader(source, assignment.Index + assignment.Length - 1).Read();
    }

    private sealed class LiteralReader(string source, int position)
    {
        private int _position = position;
        private void Skip()
        {
            while (_position < source.Length)
            {
                if (char.IsWhiteSpace(source[_position])) { _position++; continue; }
                if (source.AsSpan(_position).StartsWith("//"))
                { while (_position < source.Length && source[_position] != '\n') _position++; continue; }
                if (source.AsSpan(_position).StartsWith("/*"))
                { var end = source.IndexOf("*/", _position + 2, StringComparison.Ordinal); if (end < 0) throw new InvalidDataException("Comment"); _position = end + 2; continue; }
                break;
            }
        }
        private bool Take(char c) { Skip(); if (_position < source.Length && source[_position] == c) { _position++; return true; } return false; }
        private void Require(char c) { if (!Take(c)) throw new InvalidDataException($"Expected {c} at {_position}."); }
        public JsonNode Read(int depth = 0)
        {
            if (depth > 16) throw new InvalidDataException("Dataset nesting.");
            Skip();
            if (Take('['))
            {
                var array = new JsonArray();
                while (!Take(']')) { array.Add(Read(depth + 1)); if (Take(']')) break; Require(','); }
                return array;
            }
            if (Take('{'))
            {
                var obj = new JsonObject();
                while (!Take('}'))
                {
                    Skip();
                    string key;
                    if (_position < source.Length && source[_position] is '\'' or '"') key = ReadString();
                    else { var start = _position; while (_position < source.Length && (char.IsAsciiLetterOrDigit(source[_position]) || source[_position] == '_')) _position++; key = source[start.._position]; }
                    if (key.Length == 0) throw new InvalidDataException("Object key.");
                    Require(':'); obj.Add(key, Read(depth + 1)); if (Take('}')) break; Require(',');
                }
                return obj;
            }
            if (_position < source.Length && source[_position] is '\'' or '"')
            {
                var text = ReadString();
                while (Take('+')) { Skip(); text += ReadString(); }
                return JsonValue.Create(text)!;
            }
            var numberStart = _position;
            while (_position < source.Length && (char.IsAsciiDigit(source[_position]) || source[_position] == '-')) _position++;
            if (int.TryParse(source.AsSpan(numberStart, _position - numberStart), out var number)) return JsonValue.Create(number)!;
            throw new InvalidDataException($"Non-literal dataset value at {_position}.");
        }
        private string ReadString()
        {
            if (_position >= source.Length || source[_position] is not ('\'' or '"')) throw new InvalidDataException("String expected.");
            var quote = source[_position++]; var result = new StringBuilder();
            while (_position < source.Length)
            {
                var c = source[_position++];
                if (c == quote) return result.ToString();
                if (c != '\\') { result.Append(c); continue; }
                if (_position >= source.Length) break;
                c = source[_position++];
                if (c == 'u')
                {
                    if (_position + 4 > source.Length || !ushort.TryParse(source.AsSpan(_position, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code)) throw new InvalidDataException("Unicode escape.");
                    result.Append((char)code); _position += 4;
                }
                else result.Append(c switch { 'n' => '\n', 'r' => '\r', 't' => '\t', '\\' => '\\', '\'' => '\'', '"' => '"', _ => throw new InvalidDataException("Unsupported escape.") });
            }
            throw new InvalidDataException("Unterminated string.");
        }
    }
}