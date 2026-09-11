using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

[assembly: InternalsVisibleTo("YDKE.Games.SourceProbe")]

namespace YDKE_Windows;

internal interface IGameSourceProbe
{
    ProgressState Progress { get; }
    GameSession Session { get; }
    int SaveAttempts { get; }
    int FailuresRemaining { get; set; }
    bool LeaveOnFeedback { get; set; }
    bool Active { get; }
    IReadOnlyList<string> Rollbacks { get; }
    void Start(string id = "speedround", string mode = "timed");
    Task Resolve(bool correct, string feedback, IReadOnlyList<string> keys, bool alreadyRecorded = false);
    Task<bool> SubAnswer(bool correct, IReadOnlyList<string> keys);
    Task Complete();
    void HoldNextSave();
    void ReleaseSave();
    void Leave();
    void DetachSource();
}

/// <summary>Compile the ACTUAL resolver/persistence/completion methods read from the
/// working tree against inert UI and in-memory snapshot seams. This deliberately has
/// no _words field: feedback lookup cannot be hidden by a friendly test copy.
/// No WinUI/AppStorage/profile, external files, delays or application launch.</summary>
internal static class GameSourceProbe
{
    public static Func<IGameSourceProbe> Compile(GameSourceContracts source)
    {
        var methods = new[] { "GameCanAnswer", "IsCurrentGameRound", "PersistGameAnswerAsync", "RecordGameSubAnswerAsync", "ResolveGameAnswerAsync", "CompleteGameAsync" };
        var code = Fixture.Replace("// ACTUAL_NATIVE_METHODS", string.Join("\n", methods.Select(name => source.Method(name).ToFullString())), StringComparison.Ordinal);
        var paths = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? throw new InvalidOperationException("Runtime reference list unavailable."))
            .Split(Path.PathSeparator).Append(typeof(GameEngine).Assembly.Location).Distinct(StringComparer.OrdinalIgnoreCase);
        var references = paths.Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("YDKE.Games.SourceProbe",
            [CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.Latest))], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        using var assemblyBytes = new MemoryStream();
        var emitted = compilation.Emit(assemblyBytes);
        GameSourceContracts.Require(emitted.Success, "Actual native scoring source no longer fits the non-UI contract:\n" +
            string.Join("\n", emitted.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        assemblyBytes.Position = 0;
        var type = AssemblyLoadContext.Default.LoadFromStream(assemblyBytes).GetType("YDKE_Windows.NativeGameProbe", throwOnError: true)!;
        return () => (IGameSourceProbe)Activator.CreateInstance(type)!;
    }

    private const string Fixture = """
        using System;
        using System.Collections.Generic;
        using System.IO;
        using System.Linq;
        using System.Text.Json;
        using System.Threading.Tasks;
        namespace YDKE_Windows;

        internal sealed class NativeGameProbe : IGameSourceProbe
        {
            private ProgressState _progress = new();
            private GameSession? _activeGame;
            private GameSession? _resolvingGame;
            private GameSession? _completingGame;
            private readonly GameTurnGate _gameTurn = new();
            private bool _gameRoundClosed => _gameTurn.Closed;
            private int _gameEpoch;
            private bool _studyBusy;
            private bool _gameLocalBusy;
            private bool _dialogOpen => false;
            private bool _navigationBusy => false;
            private TaskCompletionSource<bool>? _dialogClosed => null;
            private GameSaveGate _gameCompletion = new();
            private int _gameRoundPoints = 100;
            private string _gameMode = "timed";
            private string _gameScoreKey = "";
            private readonly HashSet<string> _categoryFound = [];
            private VocabularyEntry[] _categoryWords = [];
            private readonly Panel PageContent = new();
            private Control _source = new();
            private TaskCompletionSource<bool>? _heldSave;
            private readonly List<string> _rollbacks = [];
            private static DateOnly Today => new(2026, 9, 8);

            public NativeGameProbe() => Start();
            public ProgressState Progress => _progress;
            public GameSession Session { get; private set; } = null!;
            public int SaveAttempts { get; private set; }
            public int FailuresRemaining { get; set; }
            public bool LeaveOnFeedback { get; set; }
            public bool Active => _activeGame is not null;
            public IReadOnlyList<string> Rollbacks => _rollbacks;
            public void Start(string id = "speedround", string mode = "timed")
            {
                Session = new GameSession(GameCatalog.All.Single(g => g.Id == id));
                _activeGame = Session;
                _gameMode = mode;
                _gameScoreKey = GameEngine.ScoreKey("en", "A1", mode, id);
                _gameCompletion = new GameSaveGate();
                _resolvingGame = null;
                _completingGame = null;
                RenderGameRound(Session);
            }
            public Task Resolve(bool correct, string feedback, IReadOnlyList<string> keys, bool alreadyRecorded = false) =>
                ResolveGameAnswerAsync(Session, correct, feedback, _source, keys, alreadyRecorded);
            public Task<bool> SubAnswer(bool correct, IReadOnlyList<string> keys) => RecordGameSubAnswerAsync(Session, correct, _source, keys);
            public Task Complete() => CompleteGameAsync(Session);
            public void HoldNextSave() => _heldSave = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public void ReleaseSave()
            {
                var pending = _heldSave ?? throw new InvalidOperationException("No pending save.");
                _heldSave = null;
                pending.SetResult(true);
            }
            public void Leave() => _activeGame = null;
            public void DetachSource() => _source.IsLoaded = false;

            // The contract of MutateStudyAsync: one entire progress snapshot, rollback
            // on failure, no external storage. Faults occur AFTER the real mutation.
            private async Task<bool> MutateStudyAsync(Action mutation)
            {
                if (_studyBusy) return false;
                _studyBusy = true;
                var before = JsonSerializer.Serialize(_progress);
                SaveAttempts++;
                try
                {
                    mutation();
                    if (_heldSave is { } pending) await pending.Task;
                    if (FailuresRemaining > 0) { FailuresRemaining--; throw new IOException("Injected save failure"); }
                    return true;
                }
                catch
                {
                    _progress = JsonSerializer.Deserialize<ProgressState>(before)!;
                    _rollbacks.Add(JsonSerializer.Serialize(_progress));
                    return false;
                }
                finally { _studyBusy = false; }
            }
            private Task<bool> PauseGameFeedbackAsync(GameSession session, string message)
            {
                if (LeaveOnFeedback) Leave();
                _gameLocalBusy = false;
                return Task.FromResult(_activeGame == session);
            }
            private void RenderGameRound(GameSession session)
            {
                _source.IsLoaded = false;
                _source = new Control();
                _gameEpoch = _gameTurn.Begin();
                _gameLocalBusy = false;
            }
            private static bool IsWithin(Control source, Panel content) => source.IsLoaded;
            private static string T(string key) => key;
            private static string U(string key, string english, string turkish) => english;
            private static void AnimatePulse(Control source, bool correct) { }
            private static void StopGameTimer() { }
            private static Panel GameSceneContent() => new();
            private static object GameCaption(string text) => text;
            private static object GamePrompt(string text, int size) => text;
            private static string GameBestLabel(GameDefinition game) => game.Id;
            private static Button GameActionButton(string text, string glyph, GameDefinition game) => new();
            private void StartGame(GameDefinition game) => Start(game.Id, _gameMode);
            private static void AddGameScene(GameDefinition game, object content) { }
            private static void RenderGameSummary(GameSession session) { }

            // ACTUAL_NATIVE_METHODS
        }
        internal class Control
        {
            public bool IsLoaded { get; set; } = true;
            public bool IsEnabled { get; set; } = true;
        }
        internal sealed class Button : Control
        {
            public event EventHandler Click { add { } remove { } }
        }
        internal sealed class Panel
        {
            public List<object> Children { get; } = [];
        }
        """;
}