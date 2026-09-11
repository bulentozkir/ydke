using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace YDKE_Windows;

/// <summary>Parse the real native files, not copied/regex-selected pseudo callers.
/// All new Resolve callers must declare their attribution policy in this audit.</summary>
internal sealed class GameSourceContracts
{
    private readonly SyntaxNode[] _roots;
    public GameSourceContracts(string dataRoot)
    {
        var repo = Directory.GetParent(Path.TrimEndingDirectorySeparator(dataRoot))?.FullName ?? throw new DirectoryNotFoundException(dataRoot);
        _roots = new[] { "MainPage.xaml.cs", "MainPage.Games.cs" }.Select(file =>
        {
            var path = Path.Combine(repo, "windows", "src", "YDKE.Windows", file);
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(path), new CSharpParseOptions(LanguageVersion.Latest), path);
            var errors = tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
            Require(errors.Length == 0, string.Join("\n", errors.Select(e => e.ToString())));
            return tree.GetRoot();
        }).ToArray();
    }

    public MethodDeclarationSyntax Method(string name) => _roots.SelectMany(r => r.DescendantNodes().OfType<MethodDeclarationSyntax>())
        .Single(m => m.Identifier.ValueText == name);
    internal static string Compact(SyntaxNode node) => string.Concat(node.DescendantTokens().Select(t => t.Text));
    internal static string Called(InvocationExpressionSyntax call) => call.Expression switch
    {
        IdentifierNameSyntax id => id.Identifier.ValueText,
        MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
        _ => "",
    };
    private IEnumerable<InvocationExpressionSyntax> Calls(string name) => _roots.SelectMany(r => r.DescendantNodes().OfType<InvocationExpressionSyntax>()).Where(c => Called(c) == name);
    private static string Owner(SyntaxNode node) => node.Ancestors().OfType<MethodDeclarationSyntax>().First().Identifier.ValueText;
    internal static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public void ExplicitKeys()
    {
        var signature = Method("ResolveGameAnswerAsync").ParameterList.Parameters;
        Require(signature.Count == 6 && signature[2].Identifier.ValueText == "feedback" &&
            Compact(signature[4].Type!) == "IReadOnlyList<string>" && signature[4].Identifier.ValueText == "reviewedKeys" && signature[4].Default is null,
            "Resolve must require reviewedKeys separately from feedback; no optional/legacy overload.");
        var expected = new Dictionary<string, string[]>
        {
            ["RenderTypingChallenge"] = ["[entry.Key]"],
            ["RenderTrueFalseChallenge"] = ["[entry.Key]"],
            ["RenderChoiceChallenge"] = ["[entry.Key]"],
            ["RenderHangmanRound"] = ["[entry.Key]", "[entry.Key]"],
            ["RenderMemoryRound"] = ["reviewedKeys"],
            ["RenderWordGuessRound"] = ["[entry.Key]", "[entry.Key]"],
            ["RenderScrambleRound"] = ["[entry.Key]", "[entry.Key]"],
            ["RenderBossRushRound"] = ["[tested.Key]", "[tested.Key]"],
            ["RenderCodyCrossRound"] = ["[entries[row].Key]"],
            ["RenderAudioGame"] = ["[entry.Key]", "[entry.Key]"],
            ["RenderSentenceGame"] = ["[picked.Word.Key]"],
            ["RenderClozeGame"] = ["[picked.Word.Key]"],
            ["RenderCategoryGame"] = ["reviewedKeys"],
            ["RenderClueGame"] = ["[entry.Key]"],
            ["RenderRackGame"] = ["reviewedKeys"],
            ["RenderClassGame"] = ["[word.Key]"],
            ["RenderOddGame"] = ["[odd.Key]"],
            ["RenderBingoGame"] = ["[tested.Key]", "[tested.Key]"],
            ["RenderMatrixGame"] = ["[entry.Key]"],
            ["RenderCrosswordGame"] = ["[entry.Key]"],
            ["RenderLocalDataGame"] = ["[]", "reviewedKeys"],
        };
        var calls = Calls("ResolveGameAnswerAsync").ToArray();
        Require(calls.Length == expected.Values.Sum(v => v.Length), "Resolve call inventory changed; explicitly audit every new caller.");
        foreach (var call in calls)
            Require(call.ArgumentList.Arguments.Count is 5 or 6 && expected.ContainsKey(Owner(call)), "Missing explicit keys at " + call.GetLocation().GetLineSpan());
        foreach (var (method, keys) in expected)
        {
            var actual = calls.Where(c => Owner(c) == method).Select(c => Compact(c.ArgumentList.Arguments[4].Expression)).ToArray();
            Require(actual.SequenceEqual(keys), method + ": actual tested-key arguments changed.");
        }
        Console.WriteLine($"  Audited {calls.Length} real Resolve calls in {expected.Count} renderer methods (25 catalog games share renderers).");
    }

    public void NoDisplayOrLegacyAttribution()
    {
        foreach (var method in new[] { "ResolveGameAnswerAsync", "PersistGameAnswerAsync", "RecordGameSubAnswerAsync" })
        {
            var ids = Method(method).DescendantNodes().OfType<IdentifierNameSyntax>().Select(n => n.Identifier.ValueText).ToArray();
            Require(!ids.Contains("_words") && !ids.Contains("FirstOrDefault"), method + " must never recover keys by display lookup.");
        }
        Require(!Method("PersistGameAnswerAsync").DescendantNodes().OfType<IdentifierNameSyntax>().Any(n => n.Identifier.ValueText == "feedback"), "Feedback entered persistence.");
        foreach (var root in _roots)
        {
            Require(!root.DescendantNodes().OfType<IdentifierNameSyntax>().Any(n => n.Identifier.ValueText is
                "CorrectAnswers" or "WrongAnswers" or "DailyActivity" or "RegisterActivity" or "RegisterStudy" or "RegisterAnswerAsync"), "Legacy accounting remains in a new UI flow.");
            Require(!root.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(c => Compact(c.Expression) == "LearningEngine.Rate"), "Scored API must own scheduling too.");
        }
        var scored = Calls("RecordScoredAnswer").Single();
        Require(Owner(scored) == "PersistGameAnswerAsync" && Compact(scored) == "LearningEngine.RecordScoredAnswer(_progress,keys,correct,true,today)", "Games must record one objective attempt through the scored API.");
        var persist = Compact(Method("PersistGameAnswerAsync"));
        Require(persist.Contains("varkeys=reviewedKeys.ToArray();vartoday=Today;", StringComparison.Ordinal), "Freeze keys/day before an asynchronous retry.");
        Require(persist.Contains("save.SaveAsync(()=>MutateStudyAsync", StringComparison.Ordinal), "Scoring must use the study snapshot transaction.");
        var resolver = Compact(Method("ResolveGameAnswerAsync"));
        Require(resolver.Contains("if(!answerAlreadyRecorded&&!awaitPersistGameAnswerAsync", StringComparison.Ordinal), "Final board feedback must not rescore atomic answers.");
        Require(resolver.IndexOf("_gameTurn.TryClose", StringComparison.Ordinal) < resolver.IndexOf("awaitPersistGameAnswerAsync", StringComparison.Ordinal), "Close duplicate-submit gate before first await.");
    }

    public void BoardAtomicity()
    {
        var mutations = new Dictionary<string, string[]>
        {
            ["RenderMemoryRound"] = ["firstButton=null", "firstEntry=null", "matches++", "previous.IsEnabled=false"],
            ["RenderBossRushRound"] = ["answered=true", "bossHp--", "hearts--", "bossIndex++"],
            ["RenderCodyCrossRound"] = ["solved[row]=true", "activeRow++"],
            ["RenderBingoGame"] = ["found.Add(index)"],
            ["RenderCrosswordGame"] = ["acrossSolved=true"],
            ["RenderCategoryGame"] = ["_categoryFound.Add(bare)"],
            ["RenderWordGuessRound"] = ["attempts++", "board.Children.Add"],
            ["RenderScrambleRound"] = ["wrong++"],
        };
        foreach (var (name, writes) in mutations)
        {
            var method = Method(name);
            var sub = method.DescendantNodes().OfType<InvocationExpressionSyntax>().Single(c => Called(c) == "RecordGameSubAnswerAsync");
            Require(sub.ArgumentList.Arguments.Count == 4, name + ": atomic answer needs explicit keys.");
            var guard = sub.Ancestors().OfType<IfStatementSyntax>().First();
            Require(guard.Statement is ReturnStatementSyntax && Compact(guard.Condition).StartsWith("!awaitRecordGameSubAnswerAsync", StringComparison.Ordinal) &&
                Compact(guard.Condition).EndsWith("||!IsCurrentGameRound(session,epoch)", StringComparison.Ordinal), name + ": failure/stale continuation must return before advancing.");
            var tail = string.Concat(method.DescendantTokens().Where(t => t.SpanStart >= guard.Span.End).Select(t => t.Text));
            foreach (var write in writes) Require(tail.Contains(write, StringComparison.Ordinal), name + ": board mutation must follow accepted save: " + write);
            // These are assignment expressions, not the local variables' initializers.
            var before = string.Concat(method.DescendantNodes().OfType<ExpressionStatementSyntax>().Where(n => n.Span.End <= guard.SpanStart).Select(Compact));
            foreach (var write in writes) Require(!before.Contains(write, StringComparison.Ordinal), name + ": state changed before save: " + write);
            foreach (var final in method.DescendantNodes().OfType<InvocationExpressionSyntax>().Where(c => Called(c) == "ResolveGameAnswerAsync"))
                Require(final.ArgumentList.Arguments.Count == 6 && Compact(final.ArgumentList.Arguments[5]) == "answerAlreadyRecorded:true", name + ": round-final double accounting.");
        }
        Require(Compact(Method("RenderMemoryRound")).Contains("reviewedKeys=correct?[tile.Entry.Key]:[previousEntry.Key,tile.Entry.Key]", StringComparison.Ordinal), "Memory mismatch may only review the selected pair.");
        Require(Compact(Method("RenderBingoGame")).Contains("vartested=words[target]", StringComparison.Ordinal), "Bingo only tests the current definition.");
        Require(Compact(Method("RenderCrosswordGame")).Contains("varentry=acrossSolved?crossing.Down:crossing.Across", StringComparison.Ordinal), "Crossword submits one independent entry.");
        Require(Compact(Method("RenderBossRushRound")).Contains("awaitPlayDefeatAsync();if(!IsCurrentGameRound(session,epoch))return;", StringComparison.Ordinal), "A stale boss animation must not advance the gauntlet.");
    }

    public void KeyOrigins()
    {
        Require(Compact(Method("RenderSentenceGame")).Contains("(Word:w,Sentence:GameEngine.NativeText(w.Example))", StringComparison.Ordinal), "Sentence selection must retain its source word.");
        var local = Method("RenderLocalDataGame");
        var semantic = local.DescendantNodes().OfType<InvocationExpressionSyntax>().Single(c => Called(c) == "SemanticWordKeys");
        Require(Compact(semantic) == "GameEngine.SemanticWordKeys(pair,_words,language,level)", "Semantic attribution must use current vocabulary evidence.");
        var reading = Calls("ResolveGameAnswerAsync").Single(c => Owner(c) == "RenderLocalDataGame" && Compact(c.ArgumentList.Arguments[4].Expression) == "[]");
        Require(Compact(reading.ArgumentList.Arguments[2].Expression).Contains("question.Explanation", StringComparison.Ordinal), "Reading feedback can contain words without creating vocabulary credit.");
        foreach (var name in new[] { "RenderCategoryGame", "RenderRackGame" })
            Require(Compact(Method(name)).Contains("reviewedKeys=testedisnull?[]:[tested.Key]", StringComparison.Ordinal), "Unknown open-ended input must not invent keys: " + name);
    }

    public void CompletionAndStats()
    {
        var completion = Calls("RegisterGameCompleted").Single();
        Require(Owner(completion) == "CompleteGameAsync", "Only game completion may increment CompletedGames.");
        var transaction = completion.Ancestors().OfType<InvocationExpressionSyntax>().First(c => Called(c) == "MutateStudyAsync");
        Require(Compact(transaction).Contains("_progress.GameBestScores[key]=session.Score", StringComparison.Ordinal), "Completion and best score must share a rollback snapshot.");
        var body = Compact(Method("CompleteGameAsync"));
        Require(body.Contains("_gameCompletion.Committed", StringComparison.Ordinal) && body.Contains("completion.SaveAsync(()=>MutateStudyAsync", StringComparison.Ordinal), "Completion needs a persistent per-session commit guard.");
        Require(Compact(Method("StartGame")).Contains("_gameCompletion=newGameSaveGate()", StringComparison.Ordinal), "A new game needs a new completion gate.");
        Require(Compact(Method("RenderStats").Body!) == "{AddLearningStats();}", "Stats must delegate exactly once to the new metrics UI.");
        var profile = Compact(Method("RenderProfile"));
        Require(profile.Contains("MarkedKnownLabel", StringComparison.Ordinal) && !profile.Contains("T(\"Home.Known\")", StringComparison.Ordinal), "Profile must say marked known, not inferred mastery.");
    }

    public void ProgressAndPractice()
    {
        var render = Compact(Method("RenderGameRound"));
        Require(render.Contains("GameEngine.Progress(session,_gameMode)", StringComparison.Ordinal) && render.Contains("Maximum=progress.Maximum", StringComparison.Ordinal) &&
            render.Contains("progress.Visible?Visibility.Visible:Visibility.Collapsed", StringComparison.Ordinal), "Primary progress must reflect elapsed time or a real finite round limit.");
        Require(Compact(Method("StartGameTimer")).Contains("_gamePrimaryProgress.Value=GameEngine.Progress(session,_gameMode).Value", StringComparison.Ordinal), "The live timer must update PRIMARY progress too.");
        Require(!Compact(Method("GameHud")).Contains("session.IsTimed", StringComparison.Ordinal), "A timed HUD must remain timed when remaining seconds reach zero.");
        Require(Compact(Method("GameDuration")).Contains("GameEngine.Duration(game,_settings.UntimedPractice)", StringComparison.Ordinal), "Duration filters must reflect current practice settings.");
        Require(Compact(Method("AddGameFilters")).Contains("GameDuration(g)==_gameDurationFilter", StringComparison.Ordinal), "The catalog must filter by the mode-aware duration.");
        var instructions = Compact(Method("GameInstructions"));
        Require(instructions.Contains("_settings.UntimedPractice&&GameEngine.HasClock(game)?GamePracticeInstructions(game)", StringComparison.Ordinal), "Practice must branch before timed instructions.");
        foreach (var call in Method("GamePracticeInstructions").DescendantNodes().OfType<InvocationExpressionSyntax>().Where(c => Called(c) == "U"))
        {
            var english = (LiteralExpressionSyntax)call.ArgumentList.Arguments[1].Expression;
            Require(english.Token.ValueText.Contains("Unlimited time", StringComparison.Ordinal) && !english.Token.ValueText.Contains("60", StringComparison.Ordinal), "Practice instructions must explicitly promise unlimited TIME, never 60 seconds.");
        }
        Require(Compact(Method("GameBestLabel")).Contains("GameModeLabel(ScoreMode(game))", StringComparison.Ordinal), "Do not display raw score-mode identifiers.");
        var modeKeys = Method("GameModeLabel").DescendantNodes().OfType<InvocationExpressionSyntax>().Where(c => Called(c) == "U")
            .Select(c => ((LiteralExpressionSyntax)c.ArgumentList.Arguments[0].Expression).Token.ValueText).Order().ToArray();
        Require(modeKeys.SequenceEqual(new[] { "Games.Mode.practice", "Games.Mode.standard", "Games.Mode.timed" }), "Reuse the existing exact mode translation keys.");
    }
}