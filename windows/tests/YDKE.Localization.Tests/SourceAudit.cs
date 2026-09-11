using System.Text.RegularExpressions;

namespace YDKE_Windows;

internal sealed record LocalizationCall(string Helper, string Key, string? English, string File, int Line);

/// <summary>
/// Deliberately limited C# source audit, not a compiler. Regex locates real T/U
/// calls (including calls inside interpolated text); balanced delimiters read
/// their arguments. Unrecognised dynamic keys fail closed instead of disappearing
/// from the coverage count. No regex is allowed to extract keys from the resource
/// table and then pretend those are all the keys the UI needs.
/// </summary>
internal static class SourceAudit
{
    internal const string Literal = "\"(?:\\\\.|[^\"\\\\])*\"";

    public static string Unquote(string text) => Regex.Unescape(text[1..^1]);

    public static string WithoutComments(string source) => Regex.Replace(source,
        @"/\*[\s\S]*?\*/|(?m)^\s*//[^\r\n]*", match =>
            new string(match.Value.Select(c => c is '\r' or '\n' ? c : ' ').ToArray()));

    public static IEnumerable<LocalizationCall> Calls(string file, string source, IReadOnlyList<string> modes)
    {
        source = WithoutComments(source);
        foreach (Match call in Regex.Matches(source, @"\b(?<helper>[UT])\s*\("))
        {
            var args = Arguments(source, call.Index + call.Length);
            // The only non-call matches are the two known helper declarations.
            if (args[0] == "string key") continue;
            var line = source.AsSpan(0, call.Index).Count('\n') + 1;
            var helper = call.Groups["helper"].Value;
            var english = helper == "U" && args.Count > 1 && IsLiteral(args[1]) ? Unquote(args[1]) : null;
            if (IsLiteral(args[0]))
            {
                yield return new(helper, Unquote(args[0]), english, file, line);
                continue;
            }

            // Current T(group == GameGroup.Simple ? "..." : "...") calls.
            var conditional = Regex.Match(args[0],
                @"^[^""?:]+\?\s*(?<yes>" + Literal + @")\s*:\s*(?<no>" + Literal + @")$");
            if (conditional.Success)
            {
                yield return new(helper, Unquote(conditional.Groups["yes"].Value), null, file, line);
                yield return new(helper, Unquote(conditional.Groups["no"].Value), null, file, line);
                continue;
            }

            // Recognised score-mode expansions, should the UI start using them.
            if (Regex.IsMatch(args[0], "^\"Games\\.Mode\\.\"\\s*\\+\\s*(?:ScoreMode\\(game\\)|_gameMode|mode)$") ||
                Regex.IsMatch(args[0], "^\\$\"Games\\.Mode\\.\\{(?:ScoreMode\\(game\\)|_gameMode|mode)\\}\"$"))
            {
                foreach (var mode in modes) yield return new(helper, "Games.Mode." + mode, null, file, line);
                continue;
            }
            throw new InvalidOperationException($"Unaudited dynamic {helper} key at {file}:{line}: {args[0]}");
        }
    }

    public static bool IsLiteral(string value) => Regex.IsMatch(value, "^" + Literal + "$", RegexOptions.CultureInvariant);

    private static List<string> Arguments(string source, int start)
    {
        var values = new List<string>();
        var depth = 0;
        var quoted = false;
        var first = start;
        for (var i = start; i < source.Length; i++)
        {
            var c = source[i];
            if (quoted)
            {
                if (c == '\\') i++;
                else if (c == '"') quoted = false;
                continue;
            }
            if (c == '"') { quoted = true; continue; }
            if (c is '(' or '[' or '{') depth++;
            else if (c == ')' && depth == 0)
            {
                values.Add(source[first..i].Trim());
                return values;
            }
            else if (c is ')' or ']' or '}') depth--;
            else if (c == ',' && depth == 0)
            {
                values.Add(source[first..i].Trim());
                first = i + 1;
            }
        }
        throw new InvalidOperationException("Unterminated localization call");
    }

    public static string Expression(string source, string method)
    {
        source = WithoutComments(source);
        var match = Regex.Match(source, @"\b" + Regex.Escape(method) + @"\([^;{}]*?\)\s*=>\s*");
        if (!match.Success) throw new InvalidOperationException($"Cannot audit expression-bodied {method}");
        var start = match.Index + match.Length;
        var quoted = false;
        for (var i = start; i < source.Length; i++)
        {
            if (quoted)
            {
                if (source[i] == '\\') i++;
                else if (source[i] == '"') quoted = false;
            }
            else if (source[i] == '"') quoted = true;
            else if (source[i] == ';') return source[start..i];
        }
        throw new InvalidOperationException($"Unterminated expression-bodied {method}");
    }

    public static Dictionary<string, string> Instructions(string source)
    {
        var body = Expression(source, "GameInstructions");
        return Regex.Matches(body, @"(?<id>" + Literal + @"|_)\s*=>\s*U\(\s*(?<key>" + Literal + ")")
            .ToDictionary(m => m.Groups["id"].Value == "_" ? "_" : Unquote(m.Groups["id"].Value),
                m => Unquote(m.Groups["key"].Value), StringComparer.Ordinal);
    }

    public static string FindRoot(string? specified)
    {
        if (specified is not null)
        {
            var path = Path.GetFullPath(specified);
            if (File.Exists(Path.Combine(path, "windows", "src", "YDKE.Windows", "MainPage.Games.cs"))) return path;
            throw new DirectoryNotFoundException("Pass the repository root, not a dataset or profile folder.");
        }
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
                if (File.Exists(Path.Combine(directory.FullName, "windows", "src", "YDKE.Windows", "MainPage.Games.cs")))
                    return directory.FullName;
        throw new DirectoryNotFoundException("Cannot locate native source; pass the repository root.");
    }
}