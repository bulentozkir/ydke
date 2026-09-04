using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace YDKE_Windows;

internal sealed class VocabularyRepository
{
    public static readonly IReadOnlyList<StudyLanguage> Languages =
    [
        Create("en", "English", "words{0}.js"),
        Create("de", "Deutsch", "words{0}gode.js"),
        Create("fr", "Français", "words{0}fr.js"),
        Create("it", "Italiano", "words{0}it.js"),
        Create("es", "Español", "words{0}es.js"),
        Create("pt", "Português", "words{0}pt.js"),
        Create("nl", "Nederlands", "words{0}nl.js"),
    ];

    private readonly ConcurrentDictionary<string, IReadOnlyList<VocabularyEntry>> _cache = new();

    public async Task<IReadOnlyList<VocabularyEntry>> LoadAsync(
        string languageCode,
        string level,
        CancellationToken cancellationToken = default)
    {
        var key = $"{languageCode}:{level}";
        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var language = Languages.FirstOrDefault(item => item.Code == languageCode) ?? Languages[0];
        var effectiveLevel = language.Files.ContainsKey(level) ? level : language.Levels[0];
        var path = Path.Combine(AppContext.BaseDirectory, "Data", language.Files[effectiveLevel]);
        var source = await File.ReadAllTextAsync(path, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        var entries = await Task.Run(
            () => JavaScriptVocabularyParser.Parse(source, language.Code, cancellationToken),
            cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        if (entries.Count == 0)
        {
            throw new InvalidDataException($"No vocabulary records found in {path}.");
        }

        _cache[key] = entries;
        return entries;
    }

    private static StudyLanguage Create(string code, string name, string pattern)
    {
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var level in new[] { "A1", "A2", "B1", "B2", "C1", "C2" })
        {
            files[level] = string.Format(CultureInfo.InvariantCulture, pattern, level.ToLowerInvariant());
        }
        return new StudyLanguage(code, name, files);
    }
}

internal static class JavaScriptVocabularyParser
{
    private static readonly HashSet<string> Fields =
        ["word", "pos", "level", "category", "definition", "example"];

    public static IReadOnlyList<VocabularyEntry> Parse(
        string source,
        string languageCode,
        CancellationToken cancellationToken = default)
    {
        var arrayStart = source.IndexOf('[', StringComparison.Ordinal);
        if (arrayStart < 0)
        {
            return [];
        }

        var result = new List<VocabularyEntry>();
        foreach (var objectSource in ReadObjects(source, arrayStart, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fields = ReadFields(objectSource);
            if (!fields.TryGetValue("word", out var word) || string.IsNullOrWhiteSpace(word))
            {
                continue;
            }

            result.Add(new VocabularyEntry(
                word,
                fields.GetValueOrDefault("pos", ""),
                fields.GetValueOrDefault("level", ""),
                fields.GetValueOrDefault("category", "General"),
                fields.GetValueOrDefault("definition", ""),
                fields.GetValueOrDefault("example", ""),
                languageCode));
        }

        return result;
    }

    private static IEnumerable<string> ReadObjects(
        string source,
        int start,
        CancellationToken cancellationToken)
    {
        var objectStart = -1;
        var depth = 0;
        var quote = '\0';
        var escaped = false;

        for (var index = start; index < source.Length; index++)
        {
            if ((index & 0x3FFF) == 0) cancellationToken.ThrowIfCancellationRequested();
            var current = source[index];
            if (quote != '\0')
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (current == '\\')
                {
                    escaped = true;
                }
                else if (current == quote)
                {
                    quote = '\0';
                }
                continue;
            }

            if (current is '\'' or '"' or '`')
            {
                quote = current;
                continue;
            }

            if (current == '{')
            {
                if (depth++ == 0)
                {
                    objectStart = index;
                }
            }
            else if (current == '}' && depth > 0 && --depth == 0 && objectStart >= 0)
            {
                yield return source[objectStart..(index + 1)];
                objectStart = -1;
            }
            else if (current == ']' && depth == 0)
            {
                yield break;
            }
        }
    }

    private static Dictionary<string, string> ReadFields(string source)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var index = 1;

        while (index < source.Length - 1)
        {
            SkipSeparators(source, ref index);
            var nameStart = index;
            while (index < source.Length && (char.IsLetterOrDigit(source[index]) || source[index] == '_'))
            {
                index++;
            }

            if (nameStart == index)
            {
                index++;
                continue;
            }

            var name = source[nameStart..index];
            SkipWhitespace(source, ref index);
            if (index >= source.Length || source[index++] != ':')
            {
                continue;
            }

            SkipWhitespace(source, ref index);
            if (index >= source.Length || source[index] is not ('\'' or '"' or '`'))
            {
                SkipValue(source, ref index);
                continue;
            }

            var value = ReadString(source, ref index);
            if (Fields.Contains(name))
            {
                result[name] = value;
            }
        }

        return result;
    }

    private static string ReadString(string source, ref int index)
    {
        var quote = source[index++];
        var builder = new StringBuilder();

        while (index < source.Length)
        {
            var current = source[index++];
            if (current == quote)
            {
                break;
            }

            if (current != '\\' || index >= source.Length)
            {
                builder.Append(current);
                continue;
            }

            var escaped = source[index++];
            builder.Append(escaped switch
            {
                'n' => '\n',
                'r' => '\r',
                't' => '\t',
                'b' => '\b',
                'f' => '\f',
                'u' when index + 4 <= source.Length &&
                    int.TryParse(source.AsSpan(index, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code) =>
                    ReadUnicode(code, ref index),
                _ => escaped,
            });
        }

        return builder.ToString();
    }

    private static char ReadUnicode(int code, ref int index)
    {
        index += 4;
        return (char)code;
    }

    private static void SkipValue(string source, ref int index)
    {
        var depth = 0;
        while (index < source.Length)
        {
            var current = source[index];
            if (depth == 0 && current is ',' or '}')
            {
                return;
            }
            if (current is '[' or '{' or '(') depth++;
            if (current is ']' or '}' or ')') depth--;
            index++;
        }
    }

    private static void SkipSeparators(string source, ref int index)
    {
        while (index < source.Length && (char.IsWhiteSpace(source[index]) || source[index] == ',')) index++;
    }

    private static void SkipWhitespace(string source, ref int index)
    {
        while (index < source.Length && char.IsWhiteSpace(source[index])) index++;
    }
}