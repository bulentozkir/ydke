using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Windows.System;

namespace YDKE_Windows;

public sealed partial class MainPage
{
    private bool GameMotionOff => _settings.ReduceMotion || !new Windows.UI.ViewManagement.UISettings().AnimationsEnabled;
    private FrameworkElement? _gameRoundView;
    private StackPanel? _gameFeedback;
    // Recomputed by every Render*Round call; the toolbar Hint button reads it live, so a
    // closure over a round-local variable (e.g. the current boss/row/clue index) always
    // reflects the CURRENT sub-question, never a stale one from an earlier round.
    private Func<string>? _gameHintProvider;
    private readonly GameTurnGate _gameTurn = new();
    private bool _gameRoundClosed => _gameTurn.Closed;
    private bool _gameLocalBusy;
    private int _gameEpoch;
    private GameSaveGate _gameCompletion = new();
    private string _gameScoreKey = "";
    private string _gameMode = "";
    private readonly HashSet<string> _categoryFound = [];
    private VocabularyEntry[] _categoryWords = [];
    private readonly Dictionary<string, ReadingPassage[]> _readingCache = [];
    private readonly Dictionary<string, SemanticPair[]> _semanticCache = [];
    private string _gameSkillFilter = "all";
    private string _gameDurationFilter = "all";

    private bool GameCanAnswer(GameSession session) => _activeGame == session && !_gameRoundClosed &&
        !_dialogOpen && !_studyBusy && !_navigationBusy && _resolvingGame != session && _completingGame != session;
    private string GameAnswer(string value) => LearningEngine.NormalizeAnswer(value, _settings.StudyLanguage);

    // Never reveals the thing being guessed/classified: only the WORD's own first letter
    // and length (plus an optional non-answer extra, e.g. its example sentence). Safe to
    // use even in classification games (word class/true-false/odd-one-out) where the
    // "answer" is the classification, not the word itself.
    private string WordHint(VocabularyEntry entry, string? extra = null)
    {
        var bare = GameEngine.Bare(entry);
        var head = bare.Length == 0 ? ""
            : $"{U("Games.Hint.StartsWith", "Starts with", "İle başlar")} ‘{char.ToUpperInvariant(bare[0])}’ · {bare.Length} {U("Games.Hint.Letters", "letters", "harf")}";
        return string.IsNullOrWhiteSpace(extra) ? head : (head.Length == 0 ? extra : $"{head} · {extra}");
    }
    private string FeedbackWithAnswer(string correctAnswer, string? yourAnswer = null, string? extra = null)
    {
        var lines = new List<string>();
        var correct = correctAnswer.Trim();
        if (correct.Length > 0)
            lines.Add($"{U("Games.Feedback.CorrectAnswer", "Correct answer", "Doğru yanıt")}: {correct}");
        var chosen = yourAnswer?.Trim();
        if (!string.IsNullOrWhiteSpace(chosen))
            lines.Add($"{U("Games.Feedback.YourAnswer", "Your answer", "Yanıtın")}: {chosen}");
        var detail = extra?.Trim();
        if (!string.IsNullOrWhiteSpace(detail)) lines.Add(detail);
        return lines.Count == 0 ? correctAnswer : string.Join('\n', lines);
    }

    private string FeedbackTrueFalse(VocabularyEntry entry, string shownDefinition, bool actual, bool selected)
    {
        var trueLabel = U("Games.True", "True", "Doğru");
        var falseLabel = U("Games.False", "False", "Yanlış");
        return string.Join('\n',
            $"{U("Games.Feedback.Statement", "Shown statement", "Gösterilen ifade")}: {entry.Word} — {shownDefinition}",
            $"{U("Games.Feedback.CorrectAnswer", "Correct answer", "Doğru yanıt")}: {(actual ? trueLabel : falseLabel)}",
            $"{U("Games.Feedback.YourAnswer", "Your answer", "Yanıtın")}: {(selected ? trueLabel : falseLabel)}");
    }

    private string ScoreMode(GameDefinition game) => _settings.UntimedPractice ? "practice" :
        game.Mechanic is GameMechanic.TimedChoice or GameMechanic.TimedTyping or GameMechanic.CategorySprint ? "timed" : "standard";
    private string GameModeLabel(string mode) => mode switch
    {
        "practice" => U("Games.Mode.practice", "Untimed practice", "Süresiz alıştırma"),
        "timed" => U("Games.Mode.timed", "Timed", "Süreli"),
        _ => U("Games.Mode.standard", "Standard", "Standart"),
    };
    private int GameBest(GameDefinition game) => _progress.GameBestScores.GetValueOrDefault(
        GameEngine.ScoreKey(_settings.StudyLanguage, _settings.Level, ScoreMode(game), game.Id));
    private string GameBestLabel(GameDefinition game)
    {
        var scoped = $"{T("Game.Best")}: {GameBest(game):N0} · {_settings.StudyLanguage}:{_settings.Level} · {GameModeLabel(ScoreMode(game))}";
        // Unscoped legacy values are retained but NEVER treated as a comparable competitive best.
        return _progress.GameBestScores.TryGetValue(game.Id, out var legacy)
            ? scoped + $" · {U("Games.LegacyBest", "Legacy (unscoped)", "Eski (kapsamsız)")}: {legacy:N0}" : scoped;
    }

    private UIElement BuildScopedGameStatistics()
    {
        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(Heading(U("Games.ScopedScores", "Game scores — current language, level and mode", "Oyun puanları — geçerli dil, seviye ve mod"), 20));
        var games = GameCatalog.All.OrderByDescending(GameBest)
            .Where(game => GameBest(game) > 0 || _progress.GameBestScores.ContainsKey(game.Id)).ToArray();
        var grid = new Grid { ColumnSpacing = 8, RowSpacing = 8 };
        var slots = new ContentControl[4];
        for (var index = 0; index < slots.Length; index++)
        {
            slots[index] = new ContentControl { HorizontalContentAlignment = HorizontalAlignment.Stretch };
            grid.Children.Add(slots[index]);
        }
        ConfigureResponsiveGrid(grid, 2, 260);
        var status = Body("");
        StudyLive(status, "stats.GameScoresPage");
        var previous = SecondaryButton(U("Stats.PreviousScores", "Previous scores", "Önceki puanlar"), "");
        FocusTarget(previous, "stats.PreviousScores");
        var next = SecondaryButton(U("Stats.NextScores", "Next scores", "Sonraki puanlar"), "");
        FocusTarget(next, "stats.NextScores");
        void Refresh()
        {
            var pages = Math.Max(1, (games.Length + 3) / 4);
            _statsGamePage = Math.Clamp(_statsGamePage, 0, pages - 1);
            var page = games.Skip(_statsGamePage * 4).Take(4).ToArray();
            for (var index = 0; index < slots.Length; index++)
            {
                slots[index].Content = index < page.Length ? Card(Body(page[index].Title + " · " + GameBestLabel(page[index])), 12) : null;
                slots[index].Visibility = index < page.Length ? Visibility.Visible : Visibility.Collapsed;
            }
            Announce(status, games.Length == 0
                ? U("Stats.NoGameScores", "No game scores yet", "Henüz oyun puanı yok")
                : string.Format(System.Globalization.CultureInfo.CurrentCulture,
                    U("Stats.ScoresPage", "Score page {0:N0} of {1:N0}", "Puan sayfası {0:N0} / {1:N0}"), _statsGamePage + 1, pages));
            previous.IsEnabled = _statsGamePage > 0;
            next.IsEnabled = _statsGamePage + 1 < pages;
        }
        previous.Click += (_, _) => { if (_statsGamePage > 0) { _statsGamePage--; Refresh(); } };
        next.Click += (_, _) => { if ((_statsGamePage + 1) * 4 < games.Length) { _statsGamePage++; Refresh(); } };
        var pager = new Grid { ColumnSpacing = 8, RowSpacing = 8, Children = { status, previous, next } };
        ConfigureResponsiveGrid(pager, 3, 150);
        panel.Children.Add(pager);
        panel.Children.Add(grid);
        Refresh();
        return panel;
    }

    private static string GameSkill(GameDefinition game) => game.Id switch
    {
        "dictation" or "listeningchoice" => "listening",
        "readingcomprehension" or "clozetest" or "sentencescramble" => "context",
        "hangman" or "scramble" or "wordguess" or "dailychallenge" or "scrabble" or "matrix" or "crossword" => "spelling",
        _ => "meaning",
    };
    private string SkillLabel(string skill) => skill switch
    {
        "listening" => U("Games.Skill.Listening", "Listening", "Dinleme"),
        "context" => U("Games.Skill.Context", "Reading & sentences", "Okuma ve cümleler"),
        "spelling" => U("Games.Skill.Spelling", "Spelling", "Yazım"),
        "meaning" => U("Games.Skill.Meaning", "Meaning & recall", "Anlam ve hatırlama"),
        _ => U("Games.Filter.All", "All", "Tümü"),
    };
    private string DurationLabel(string duration) => duration switch
    {
        "practice" => U("Games.Duration.Practice", "Unlimited time (practice)", "Sınırsız süre (alıştırma)"),
        "minute" => string.Format(CultureInfo.CurrentCulture,
            U("Games.Duration.Minute", "{0} active seconds", "{0} etkin saniye"), TimedSecondsSetting),
        "long" => U("Games.Duration.Long", "Extended puzzle (5+ min estimate)", "Uzun bulmaca (tahmini 5+ dk)"),
        "short" => U("Games.Duration.Short", "Short rounds (2–5 min estimate)", "Kısa turlar (tahmini 2–5 dk)"),
        _ => U("Games.Filter.All", "All", "Tümü"),
    };
    private string GameDuration(GameDefinition game) => GameEngine.Duration(game, _settings.UntimedPractice);

    private void AddGameFilters(GameGroup group, Grid results, Action? reflow = null)
    {
        var filters = new Grid { ColumnSpacing = 10 };
        var skill = new ComboBox { Header = U("Games.Filter.Skill", "Skill", "Beceri"), HorizontalAlignment = HorizontalAlignment.Stretch };
        var duration = new ComboBox { Header = U("Games.Filter.Duration", "Duration", "Süre"), HorizontalAlignment = HorizontalAlignment.Stretch };
        ConfigureReadingComboBox(skill);
        ConfigureReadingComboBox(duration);
        foreach (var value in new[] { "all", "listening", "context", "spelling", "meaning" }) skill.Items.Add(new ComboBoxItem { Content = SkillLabel(value), Tag = value });
        foreach (var value in new[] { "all", "minute", "practice", "short", "long" }) duration.Items.Add(new ComboBoxItem { Content = DurationLabel(value), Tag = value });
        skill.SelectedItem = skill.Items.OfType<ComboBoxItem>().First(i => (string)i.Tag == _gameSkillFilter);
        duration.SelectedItem = duration.Items.OfType<ComboBoxItem>().First(i => (string)i.Tag == _gameDurationFilter);
        filters.Children.Add(skill); filters.Children.Add(duration); ConfigureResponsiveGrid(filters, 2, 230);
        PageContent.Children.Add(filters);
        var count = Body("");
        StudyLive(count, "games.CatalogCount");
        PageContent.Children.Add(count);
        GameDefinition[] filtered = [];
        void Refresh()
        {
            results.Children.Clear();
            filtered = GameCatalog.Get(group).Where(g => (_gameSkillFilter == "all" || GameSkill(g) == _gameSkillFilter) &&
                (_gameDurationFilter == "all" || GameDuration(g) == _gameDurationFilter)).ToArray();
            foreach (var game in filtered) results.Children.Add(GameButton(game));
            reflow?.Invoke();
            Announce(count, string.Format(System.Globalization.CultureInfo.CurrentCulture,
                U("Games.CatalogCount", "{0:N0} games", "{0:N0} oyun"), filtered.Length));
        }
        skill.SelectionChanged += (_, _) => { _gameSkillFilter = (string)((ComboBoxItem)skill.SelectedItem).Tag; Refresh(); };
        duration.SelectionChanged += (_, _) => { _gameDurationFilter = (string)((ComboBoxItem)duration.SelectedItem).Tag; Refresh(); };
        Refresh();
    }

    private string GamePracticeInstructions(GameDefinition game) => game.Id switch
    {
        "wordrace" => U("Games.Instructions.PracticeRace", "Untimed practice: choose the word matching each definition. Unlimited time; round and life limits still apply.", "Süresiz alıştırma: her tanıma uyan kelimeyi seçin. Süre sınırsızdır; tur ve can sınırları geçerlidir."),
        "categorysprint" => U("Games.Instructions.PracticeCategory", "Untimed practice: choose different words from one fixed category in this language and level. Unlimited time; each word scores once, and round and life limits still apply.", "Süresiz alıştırma: bu dil ve seviyede sabit bir kategoriden farklı kelimeleri seçin. Süre sınırsızdır; her kelime bir kez puanlanır, tur ve can sınırları geçerlidir."),
        _ => U("Games.Instructions.PracticeSpeed", "Untimed practice: choose the word matching each meaning. Unlimited time; round and life limits still apply.", "Süresiz alıştırma: her anlama uyan kelimeyi seçin. Süre sınırsızdır; tur ve can sınırları geçerlidir."),
    };

    private string GameInstructions(GameDefinition game) => _settings.UntimedPractice && GameEngine.HasClock(game)
        ? GamePracticeInstructions(game) : game.Id switch
    {
        "dictation" => U("Games.Instructions.Dictation", "Play the local voice, then choose the matching word. Replay as needed; the answer is hidden until feedback.", "Yerel sesi oynatıp eşleşen kelimeyi seçin. Gerektikçe tekrar dinleyin; yanıt geri bildirime kadar gizlidir."),
        "listeningchoice" => U("Games.Instructions.ListeningChoice", "Play the word and select its meaning. Replay is available; a matching installed voice is required.", "Kelimeyi dinleyip anlamını seçin. Tekrar dinlenebilir; uygun dilde yüklü ses gerekir."),
        "sentencescramble" => U("Games.Instructions.Sentence", "Select every token in the original example's order, including punctuation. Undo returns the last tile; submit when complete.", "Noktalama dahil tüm parçaları özgün örnekteki sıraya dizin. Geri al son taşı döndürür; bitince gönderin."),
        "clozetest" => U("Games.Instructions.Cloze", "Read the sentence and choose the missing word from four options. The blank can match an inflected form in the local example.", "Cümleyi okuyup dört seçenekten eksik kelimeyi seçin. Boşluk, yerel örnekte çekimli bir biçime de karşılık gelebilir."),
        "categorysprint" => U("Games.Instructions.Category", "Choose different words from one fixed dataset category. Each word counts once; only words in this language and level are accepted.", "Sabit bir veri kategorisinden farklı kelimeleri seçin. Her kelime bir kez sayılır; yalnızca bu dil ve seviyenin kelimeleri kabul edilir."),
        "cluedetective" => U("Games.Instructions.Clues", "Reveal category, class, length, initial and meaning one at a time, then choose the hidden word. Fewer clues earn more points.", "Kategori, tür, uzunluk, ilk harf ve anlam ipuçlarını sırayla açın, sonra gizli kelimeyi seçin. Daha az ipucu daha çok puan kazandırır."),
        "scrabble" => U("Games.Instructions.Rack", "Build one real local vocabulary word from the rack per round. Repeated letters require repeated tiles. This is a rack challenge, not a full Scrabble board.", "Her tur raftan bir gerçek yerel kelime üretin. Tekrarlanan harfler için birden fazla taş gerekir. Bu tam Scrabble tahtası değil, harf rafı alıştırmasıdır."),
        "crossword" => U("Games.Instructions.Crossword", "A compact two-entry crossword. Choose Across and Down; the shared square must match both answers.", "İki kelimelik küçük çapraz bulmaca. Yatay ve dikey yanıtı seçin; ortak kare iki yanıtta da aynı olmalıdır."),
        "wordclass" => U("Games.Instructions.Class", "Choose the part of speech recorded for this word and example. Ambiguous or missing metadata is excluded.", "Kelime ve örnek için kaydedilmiş sözcük türünü seçin. Belirsiz veya eksik tür verileri kullanılmaz."),
        "oddoneout" => U("Games.Instructions.Odd", "Three words share the displayed dataset category. Select the one recorded in another category; General is excluded.", "Üç kelime gösterilen veri kategorisindedir. Başka kategoride kaydedilmiş kelimeyi seçin; Genel kullanılmaz."),
        "bingo" => U("Games.Instructions.Bingo", "Match each definition on the 4×4 board. Complete a row, column or diagonal. Wrong selections end the board.", "Her tanımı 4×4 tahtada eşleştirin. Satır, sütun veya köşegeni tamamlayın. Yanlış seçim tahtayı bitirir."),
        "matrix" => U("Games.Instructions.Matrix", "Select the hidden word's letters in a straight line, then submit. Horizontal, vertical and diagonal lines are supported; no square can be reused.", "Gizli kelimenin harflerini düz bir çizgide seçip gönderin. Yatay, dikey ve çapraz çizgiler desteklenir; aynı kare tekrar kullanılamaz."),
        "wordmorph" => U("Games.Instructions.Semantic", "Classify the pair as synonyms or antonyms using reciprocal, non-conflicting relationships in the bundled dataset. Available for English, German and French only.", "Yerel veri kümesindeki karşılıklı ve çelişmeyen ilişkiye göre çifti eş veya zıt anlamlı olarak sınıflandırın. Yalnızca İngilizce, Almanca ve Fransızca kullanılabilir."),
        "readingcomprehension" => U("Games.Instructions.Reading", "Read the complete local passage using Previous/Next text pages, then answer. Feedback includes the source explanation. English, German and French only.", "Önceki/Sonraki metin sayfalarıyla yerel metni okuyup yanıtlayın. Geri bildirim kaynak açıklamasını içerir. Yalnızca İngilizce, Almanca ve Fransızca."),
        "dailychallenge" => U("Games.Instructions.Daily", "Today's language/level word is fixed. Choose one guess per turn for six tries. ✓ correct place, ~ present elsewhere, × absent. Repeated letters are counted individually.", "Günün kelimesi dil/seviye için sabittir. Altı deneme boyunca her tur bir tahmin seçin. ✓ doğru yerde, ~ başka yerde, × yok. Tekrarlanan harfler ayrı sayılır."),
        "wordguess" => U("Games.Instructions.Guess", "Choose one guess per turn for six tries. ✓ correct place, ~ present elsewhere, × absent. Repeated letters are counted individually.", "Altı deneme boyunca her tur bir tahmin seçin. ✓ doğru yerde, ~ başka yerde, × yok. Tekrarlanan harfler ayrı sayılır."),
        "survival" => U("Games.Instructions.Survival", "Choose the word matching the meaning. The first wrong answer ends the session.", "Anlama uyan kelimeyi seçin. İlk yanlış yanıt oturumu bitirir."),
        "hangman" => U("Games.Instructions.Hangman", "Select letters to uncover the word before six misses. Accented letters in the target are available on the keyboard.", "Altı hatadan önce harfleri seçerek kelimeyi açın. Hedefteki aksanlı harfler klavyede bulunur."),
        "memory" => U("Games.Instructions.Memory", "Turn over two cards at a time to match each word with its meaning. If they do not match, both cards flip back automatically.", "Kelimeyi anlamıyla eşleştirmek için iki kart açın. Eşleşmezse iki kart da kısa süre sonra otomatik kapanır."),
        "scramble" => U("Games.Instructions.Scramble", "Select letter tiles in order; select a filled slot to return its tile. A full word is checked automatically, with three attempts.", "Harf taşlarını sırayla seçin; dolu yuvayı seçerek taşı geri alın. Tam kelime otomatik kontrol edilir; üç deneme vardır."),
        "bossrush" => U("Games.Instructions.Boss", "Answer meanings to remove five hit points from each of three bosses. Three mistakes end the gauntlet; Continue follows each answer.", "Üç rakibin her birinden beş can azaltmak için anlamları yanıtlayın. Üç hata oyunu bitirir; her yanıttan sonra Devam gelir."),
        "codycross" => U("Games.Instructions.Cody", "Solve five vocabulary clues to reveal a bonus-letter code. Each correct answer opens one letter; incorrect attempts can be retried.", "Bonus harf kodunu açmak için beş kelime ipucunu çözün. Her doğru yanıt bir harf açar; yanlış yanıt tekrar denenebilir."),
        "truefalse" => U("Games.Instructions.TrueFalse", "Decide whether the displayed definition belongs to the word.", "Gösterilen tanımın kelimeye ait olup olmadığına karar verin."),
        "wordrace" => string.Format(CultureInfo.CurrentCulture,
            U("Games.Instructions.Race", "Choose the word matching each definition within {0} active seconds. Feedback pauses the clock.", "{0} etkin saniyede her tanıma uyan kelimeyi seçin. Geri bildirimde saat durur."),
            TimedSecondsSetting),
        _ => string.Format(CultureInfo.CurrentCulture,
            U("Games.Instructions.Speed", "Choose the word matching each meaning within {0} active seconds. Feedback pauses the clock.", "{0} etkin saniyede her anlama uyan kelimeyi seçin. Geri bildirimde saat durur."),
            TimedSecondsSetting),
    };

    private void GameUnavailable(GameSession session, string requirement)
    {
        if (_activeGame != session) return;
        StopGameTimer(); StopSpeechPlayback(); _gameTurn.TryClose(_gameEpoch); _activeGame = null;
        var panel = GameSceneContent();
        panel.Children.Add(GamePrompt(U("Games.Unavailable", "Unavailable for this selection", "Bu seçim için kullanılamıyor"), 24));
        panel.Children.Add(GameCaption(requirement));
        var back = GameActionButton(T("Games.Back"), "", session.Game);
        back.Click += (_, _) => RenderGameDetail(session.Game); panel.Children.Add(back);
        AddGameScene(session.Game, panel);
    }
    private string EligibleRequirement => U("Games.Require.Eligible", "This mechanic requires enough distinct eligible words/examples in the selected language and level. No substitute quiz is used.", "Bu oyun seçili dil ve seviyede yeterli sayıda farklı uygun kelime/örnek gerektirir. Yerine farklı bir test kullanılmaz.");

    private static void Live(TextBlock text) => AutomationProperties.SetLiveSetting(text, AutomationLiveSetting.Polite);
    private static void Announce(TextBlock text, string message)
    {
        text.Text = message;
        (FrameworkElementAutomationPeer.FromElement(text) ?? FrameworkElementAutomationPeer.CreatePeerForElement(text))?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    private bool IsCurrentGameRound(GameSession session, int epoch) => _activeGame == session && epoch == _gameEpoch;

    // Feedback is deliberately NOT an argument. The renderer supplies the exact tested
    // keys before any await. Every retry uses the same answer/day and the study snapshot.
    private async Task<bool> PersistGameAnswerAsync(GameSession session, int epoch, bool correct, IReadOnlyList<string> reviewedKeys)
    {
        var keys = reviewedKeys.ToArray();
        var today = Today;
        var save = new GameSaveGate();
        while (IsCurrentGameRound(session, epoch))
        {
            _gameLocalBusy = true;
            if (await save.SaveAsync(() => MutateStudyAsync(() =>
                LearningEngine.RecordScoredAnswer(_progress, keys, correct, true, today)),
                () => IsCurrentGameRound(session, epoch))) return true;
            if (!IsCurrentGameRound(session, epoch) || !await PauseGameFeedbackAsync(session,
                U("Games.SaveRetry", "Answer not saved. Continue retries the local save; leaving discards this answer.", "Yanıt kaydedilmedi. Devam yerel kaydı tekrar dener; çıkmak bu yanıtı atar."))) return false;
        }
        return false;
    }

    // Atomic board attempts (pair, clue, boss question, word guess). Call BEFORE changing
    // solved/matched/HP/attempt state, and advance that state only on true. The final
    // Resolve call must explicitly pass answerAlreadyRecorded: true to avoid double credit.
    private async Task<bool> RecordGameSubAnswerAsync(GameSession session, bool correct, Control source, IReadOnlyList<string> reviewedKeys)
    {
        if (!source.IsLoaded || !IsWithin(source, PageContent) || !GameCanAnswer(session) || _gameLocalBusy) return false;
        var epoch = _gameEpoch;
        _gameLocalBusy = true;
        try { return await PersistGameAnswerAsync(session, epoch, correct, reviewedKeys); }
        finally { if (IsCurrentGameRound(session, epoch)) _gameLocalBusy = false; }
    }

    private async Task<bool> PauseGameFeedbackAsync(GameSession session, string message)
    {
        if (_activeGame != session || _gameFeedback is null || _gameRoundView is null) return false;
        var epoch = _gameEpoch;
        var view = _gameRoundView; var feedback = _gameFeedback;
        _gameLocalBusy = true;
        view.Visibility = Visibility.Collapsed;
        SyncGameHud(session);
        message = FriendlyGameFeedback(message);
        feedback.Children.Clear();
        var text = GameFeedbackText(feedback, message); Live(text);
        message = string.Join('\n', message.Split('\n')
            .Where(line => !line.StartsWith("@gameKey:", StringComparison.Ordinal)));
        var next = GameActionButton(U("Kids.Games.Next", "Next", "Sonraki"), "\uE72A", session.Game);
        next.HorizontalAlignment = HorizontalAlignment.Stretch;
        next.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        AutomationProperties.SetAutomationId(next, "game.Continue");
        AutomationProperties.SetAcceleratorKey(next, "Enter");
        var continueHelp = U("Games.ContinueHelp", "Continue to the next round.", "Sonraki tura devam et.");
        AutomationProperties.SetHelpText(next, continueHelp);
        ToolTipService.SetToolTip(next, continueHelp);
        var completion = new TaskCompletionSource<bool>();
        var announced = false;
        void AnnounceOnce()
        {
            if (announced) return;
            announced = true;
            Announce(text, message);
        }
        next.Click += (_, _) => { if (!IsCurrentGameRound(session, epoch)) return; next.IsEnabled = false; completion.TrySetResult(true); };
        next.Loaded += (_, _) =>
        {
            if (!IsCurrentGameRound(session, epoch) || !next.IsLoaded) return;
            next.Focus(FocusState.Programmatic);
            AnnounceOnce();
        };
        feedback.Unloaded += OnUnloadedFeedback;
        void OnUnloadedFeedback(object sender, RoutedEventArgs e) => completion.TrySetResult(false);
        feedback.Children.Add(next);
        if (next.IsLoaded)
        {
            next.Focus(FocusState.Programmatic);
            AnnounceOnce();
        }
        var continued = await completion.Task;
        feedback.Unloaded -= OnUnloadedFeedback;
        if (_activeGame != session || epoch != _gameEpoch) return false;
        feedback.Children.Clear(); view.Visibility = Visibility.Visible; _gameLocalBusy = false;
        return continued;
    }

    private void GameSceneKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Handled || e.KeyStatus.WasKeyDown || _activeGame is null || _dialogOpen || _studyBusy || _navigationBusy) return;
        foreach (var modifier in new[] { VirtualKey.Control, VirtualKey.Menu, VirtualKey.Shift })
            if (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(modifier).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)) return;
        var editing = false;
        for (var focus = FocusManager.GetFocusedElement(XamlRoot) as DependencyObject; focus is not null; focus = VisualTreeHelper.GetParent(focus))
        {
            if (focus is ComboBox or Slider or NumberBox or AutoSuggestBox or PasswordBox or RichEditBox) return;
            if (focus is TextBox) editing = true;
            if (ReferenceEquals(focus, sender)) break;
        }
        var allButtons = GameDescendants((DependencyObject)sender).OfType<Button>().ToArray();
        var buttons = allButtons.Where(b => GameElementAvailable(b)).ToArray();
        Button? target = null;
        if (e.Key == VirtualKey.Enter)
        {
            target = buttons.FirstOrDefault(b => AutomationProperties.GetAutomationId(b) == "game.Continue") ??
                buttons.FirstOrDefault(b => AutomationProperties.GetName(b) == T("Game.Submit"));
            // A disabled, incomplete Submit still explains Enter; never invoke it or
            // steal number keys from an editor. Disabled audio/hidden rounds stay inert.
            if (target is null && GameCanAnswer(_activeGame) && !_gameLocalBusy)
            {
                var pending = allButtons.FirstOrDefault(b => b.Tag is GameSubmitState &&
                    GameElementAvailable(VisualTreeHelper.GetParent(b)));
                if (pending?.Tag is GameSubmitState state)
                {
                    state.Refresh();
                    if (pending.IsEnabled) target = pending;
                    else { state.Validate(); e.Handled = true; return; }
                }
            }
        }
        else if (!editing && !_gameLocalBusy && !_gameRoundClosed)
        {
            if (e.Key >= VirtualKey.A && e.Key <= VirtualKey.Z)
                target = buttons.FirstOrDefault(b => b.Tag is string tag && tag == $"game-letter-{(char)e.Key}");
            var n = (int)e.Key - (int)VirtualKey.Number1;
            if (n is < 0 or > 3) n = (int)e.Key - (int)VirtualKey.NumberPad1;
            if (n is >= 0 and <= 3) target = buttons.FirstOrDefault(b => b.Tag is string tag && tag == $"game-choice-{n}");
        }
        if (target is null) return;
        e.Handled = true;
        ((IInvokeProvider)new ButtonAutomationPeer(target).GetPattern(PatternInterface.Invoke)).Invoke();
    }
    private static IEnumerable<DependencyObject> GameDescendants(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is UIElement { Visibility: Visibility.Collapsed }) continue;
            yield return child;
            foreach (var nested in GameDescendants(child)) yield return nested;
        }
    }

    private TextBox GameInput(StackPanel panel) =>
        GameInput(panel, U("Games.Input.Answer", "Your answer", "Yanıtınız"));

    private TextBox GameInput(StackPanel panel, string label, string? help = null)
    {
        help ??= U("Kids.Input.Help", "Choose an answer. Click Check answer.", "Bir yanıt seç. Yanıtı kontrol et'e tıkla.");
        var header = GameCaption(label); header.TextAlignment = TextAlignment.Left;
        var guidance = GameCaption(help); guidance.TextAlignment = TextAlignment.Left;
        var input = new TextBox
        {
            Header = header, Description = guidance, PlaceholderText = T("Game.TypeAnswer"),
            FontSize = ReadingSize(20), MinHeight = 52, MinWidth = 48, MaxLength = 512, HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        AutomationProperties.SetName(input, label);
        AutomationProperties.SetHelpText(input, help);
        AutomationProperties.SetAutomationId(input, "game.Answer");
        panel.Children.Add(input); return input;
    }

    private static bool GameElementAvailable(DependencyObject? element)
    {
        if (element is null) return false;
        for (var current = element; current is not null; current = VisualTreeHelper.GetParent(current))
            if (current is Control { IsEnabled: false } or UIElement { Visibility: Visibility.Collapsed }) return false;
        return true;
    }

    // Walk the scene's own containers, not control templates: this also works BEFORE
    // Loaded, and includes the disabled Down box so it can be observed when unlocked.
    private static IEnumerable<TextBox> GameAnswerInputs(DependencyObject parent)
    {
        if (parent is TextBox input) { yield return input; yield break; }
        IEnumerable<DependencyObject> children = parent switch
        {
            Panel panel => panel.Children.Cast<DependencyObject>(),
            Border { Child: { } child } => [child],
            ContentControl { Content: DependencyObject content } => [content],
            _ => [],
        };
        foreach (var child in children)
            foreach (var nested in GameAnswerInputs(child)) yield return nested;
    }

    private bool HasGameAnswer(TextBox input)
    {
        if (string.IsNullOrWhiteSpace(input.Text)) return false;
        try { return GameAnswer(input.Text).Any(char.IsLetterOrDigit); }
        catch (ArgumentException) { return false; } // Malformed pasted Unicode is not a scored attempt.
    }

    private sealed record GameSubmitState(Action Refresh, Func<bool> Validate);

    private static void UpdateGameSubmitState(Button button)
    {
        if (button.Tag is GameSubmitState state) state.Refresh();
    }

    private Button SubmitGame(StackPanel panel, GameSession session, Func<Button, Task> submit, Func<bool>? canSubmit = null)
    {
        var button = GameActionButton(T("Game.Submit"), "", session.Game);
        var inputs = GameAnswerInputs(panel).ToArray();
        var validation = GameCaption(""); validation.TextAlignment = TextAlignment.Left;
        validation.Visibility = Visibility.Collapsed; Live(validation);
        AutomationProperties.SetAutomationId(validation, "game.Validation");
        AutomationProperties.SetAutomationId(button, "game.Submit");
        AutomationProperties.SetAcceleratorKey(button, "Enter");
        AutomationProperties.SetHelpText(button, inputs.Length == 0
            ? U("Games.Input.SelectionRequired", "Complete the selection shown above before submitting.", "Göndermeden önce yukarıdaki seçimi tamamlayın.")
            : U("Kids.Input.Help", "Choose an answer. Click Check answer.", "Bir yanıt seç. Yanıtı kontrol et'e tıkla."));

        TextBox[] ActiveInputs() => inputs.Where(input => !input.IsReadOnly && GameElementAvailable(input)).ToArray();
        bool CanSubmit()
        {
            var active = ActiveInputs();
            return GameElementAvailable(panel) && (inputs.Length == 0 || active.Length > 0) &&
                active.All(HasGameAnswer) && (canSubmit?.Invoke() ?? true);
        }
        void Refresh()
        {
            // Input validity, NOT GameCanAnswer: a new round can be built while the
            // previous async resolver is still unwinding. Execution gates stay below.
            button.IsEnabled = CanSubmit();
            if (button.IsEnabled) { validation.Text = ""; validation.Visibility = Visibility.Collapsed; }
        }
        bool Validate()
        {
            if (CanSubmit()) { validation.Visibility = Visibility.Collapsed; return true; }
            validation.Visibility = Visibility.Visible;
            Announce(validation, inputs.Length == 0
                ? U("Games.Input.SelectionRequired", "Complete the selection shown above before submitting.", "Göndermeden önce yukarıdaki seçimi tamamlayın.")
                : U("Kids.Input.Required", "Choose your answer first.", "Önce yanıtını seç."));
            ActiveInputs().FirstOrDefault(input => !HasGameAnswer(input))?.Focus(FocusState.Programmatic);
            return false;
        }
        button.Tag = new GameSubmitState(Refresh, Validate);
        foreach (var input in inputs)
        {
            input.TextChanged += (_, _) => Refresh();
            input.IsEnabledChanged += (_, _) => Refresh();
            input.RegisterPropertyChangedCallback(UIElement.VisibilityProperty, (_, _) => Refresh());
            input.RegisterPropertyChangedCallback(TextBox.IsReadOnlyProperty, (_, _) => Refresh());
        }
        button.Loaded += (_, _) => Refresh();
        button.Click += async (_, _) =>
        {
            if (!button.IsLoaded || !IsWithin(button, PageContent) || !GameElementAvailable(panel) || !GameCanAnswer(session) || _gameLocalBusy) return;
            if (!Validate()) { Refresh(); return; }
            await submit(button);
        };
        panel.Children.Add(validation); panel.Children.Add(button); Refresh(); return button;
    }
    private void GameChoices(StackPanel panel, GameSession session, IReadOnlyList<string> labels, Func<int, Button, Task> answer)
    {
        var grid = new Grid { ColumnSpacing = 8, RowSpacing = 8 };
        for (var i = 0; i < labels.Count; i++)
        {
            var index = i;
            var button = GameChoiceButton($"{i + 1} · {labels[i]}", session.Game); button.Tag = $"game-choice-{i}";
            AutomationProperties.SetAutomationId(button, $"game.Choice.{i + 1}");
            if (i < 4)
            {
                var shortcut = string.Format(U("Games.Choice.Shortcut", "Press {0} to choose this answer when not typing.", "Metin yazmıyorken bu yanıtı seçmek için {0} tuşuna basın."), i + 1);
                AutomationProperties.SetHelpText(button, shortcut);
                AutomationProperties.SetAcceleratorKey(button, (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
                ToolTipService.SetToolTip(button, shortcut);
            }
            button.Click += async (_, _) => { if (GameCanAnswer(session) && !_gameLocalBusy && GameElementAvailable(button)) await answer(index, button); };
            grid.Children.Add(button);
        }
        ConfigureResponsiveGrid(grid, 2, 220); panel.Children.Add(grid);
    }

    private void RenderAudioGame(GameSession session)
    {
        var voice = StudyVoiceFor(_settings.StudyLanguage);
        if (voice is null) { GameUnavailable(session, U("Games.Require.Voice", "Install a Windows 11 speech voice matching the selected language/region. Then open Help > Backups & privacy > Listen and troubleshooting and follow the setup steps.", "Seçili dil/bölgeye uygun bir Windows 11 konuşma sesi yükleyin. Ardından Yardım > Yedekler ve gizlilik > Dinle düğmesi ve sorun giderme bölümünü açıp kurulum adımlarını izleyin.")); return; }
        var pool = _words.DistinctBy(w => LocalizedPart(w.Definition))
            .Where(w => !string.IsNullOrWhiteSpace(w.Definition) && LocalizedPart(w.Definition).Length <= 96)
            .OrderBy(_ => _random.Next()).Take(4).ToArray();
        if (pool.Length < 4) { GameUnavailable(session, EligibleRequirement); return; }
        var entry = pool[0]; var panel = GameSceneContent();
        _gameHintProvider = () => WordHint(entry);
        AddGameQuestion(panel, GameTask(session.Game), () => WordExample(entry));
        var replay = GameActionButton(U("Games.Audio.Replay", "Play / replay audio", "Sesi oynat / tekrar dinle"), "", session.Game);
        var played = false; var epoch = _gameEpoch;
        var status = GameCaption(U("Games.Audio.PlayFirst", "Play the audio to unlock the answer.", "Yanıtlamak için önce sesi oynatın.")); Live(status);
        var audioControls = new Grid { ColumnSpacing = 8, RowSpacing = 8, Children = { replay, status } };
        ConfigureResponsiveGrid(audioControls, 2, 200);
        panel.Children.Add(audioControls);
        var answers = GameSceneContent();
        var answersHost = new ContentControl { Content = answers, IsEnabled = false, HorizontalContentAlignment = HorizontalAlignment.Stretch };
        replay.Click += async (_, _) =>
        {
            if (!GameCanAnswer(session) || _gameLocalBusy) return;
            _gameLocalBusy = true;
            answersHost.IsEnabled = false;
            // Dedicated player: never shares the study player's fallback or async state.
            replay.IsEnabled = false;
            try
            {
                using var synth = new Windows.Media.SpeechSynthesis.SpeechSynthesizer { Voice = voice };
                using var stream = await synth.SynthesizeTextToStreamAsync(entry.Word);
                if (_activeGame != session || epoch != _gameEpoch) return;
                using var source = Windows.Media.Core.MediaSource.CreateFromStream(stream, stream.ContentType);
                using var player = new Windows.Media.Playback.MediaPlayer();
                player.CommandManager.IsEnabled = false; player.Source = source;
                var ended = new TaskCompletionSource<bool>();
                player.MediaEnded += (_, _) => ended.TrySetResult(true);
                player.MediaFailed += (_, _) => ended.TrySetResult(false);
                panel.Unloaded += Stop;
                void Stop(object sender, RoutedEventArgs e) => ended.TrySetResult(false);
                player.Play();
                bool success;
                try { success = await ended.Task.WaitAsync(TimeSpan.FromSeconds(30)); }
                finally { panel.Unloaded -= Stop; }
                player.Pause(); player.Source = null;
                if (_activeGame != session || epoch != _gameEpoch) return;
                played |= success;
                Announce(status, success ? U("Games.Audio.Ready", "Audio played. Answer or replay.", "Ses oynatıldı. Yanıtlayın veya tekrar dinleyin.") : U("Games.Audio.Failed", "Audio could not be played. Replay to try again; no answer was scored.", "Ses oynatılamadı. Tekrar deneyin; yanıt puanlanmadı."));
            }
            catch (Exception) { if (_activeGame == session && epoch == _gameEpoch) Announce(status, U("Games.Audio.Failed", "Audio could not be played. Replay to try again; no answer was scored.", "Ses oynatılamadı. Tekrar deneyin; yanıt puanlanmadı.")); }
            finally
            {
                if (_activeGame == session && epoch == _gameEpoch)
                {
                    replay.IsEnabled = true;
                    answersHost.IsEnabled = played;
                    _gameLocalBusy = false;
                }
            }
        };
        if (session.Game.Id == "dictation")
        {
            var heardWords = pool.OrderBy(_ => _random.Next()).ToArray();
            GameChoices(answers, session, heardWords.Select(word => word.Word).ToArray(), async (index, button) =>
            {
                if (played) await ResolveGameAnswerAsync(session, heardWords[index] == entry,
                    FeedbackWithAnswer(entry.Word, heardWords[index].Word), button, [entry.Key]);
            });
        }
        else
        {
            var choices = pool.OrderBy(_ => _random.Next()).ToArray();
            GameChoices(answers, session, choices.Select(w => LocalizedPart(w.Definition)).ToArray(), async (i, button) =>
            {
                if (played) await ResolveGameAnswerAsync(session, choices[i] == entry,
                    FeedbackWithAnswer(entry.Word + " — " + MeaningWithTurkish(entry.Definition), MeaningWithTurkish(choices[i].Definition)), button, [entry.Key]);
            });
        }
        panel.Children.Add(answersHost);
        AddGameScene(session.Game, panel);
    }

    private void RenderSentenceGame(GameSession session)
    {
        // Keep the vocabulary identity alongside its example, never recover it from
        // the assembled/translated feedback. Require that the tested word occurs in it.
        var sentences = _words.Select(w => (Word: w, Sentence: GameEngine.NativeText(w.Example)))
            .Where(p => GameEngine.UsableExample(p.Sentence) && p.Sentence.Length <= 180 &&
                GameEngine.Tokens(p.Sentence).Length is >= 3 and <= 12 && GameEngine.Cloze(p.Word) is not null)
            .DistinctBy(p => p.Sentence).ToArray();
        if (sentences.Length == 0) { GameUnavailable(session, EligibleRequirement); return; }
        var picked = sentences[_random.Next(sentences.Length)];
        var sentence = picked.Sentence;
        var tokens = GameEngine.Tokens(sentence).OrderBy(_ => _random.Next()).ToArray();
        if (GameEngine.SentenceKey(string.Join(' ', tokens)) == GameEngine.SentenceKey(sentence)) tokens = tokens.Skip(1).Append(tokens[0]).ToArray();
        // No single "word" to hint on an order puzzle -- a non-revealing token count instead.
        _gameHintProvider = () => $"{tokens.Length} {U("Games.Hint.Words", "words", "kelime")}";
        var selected = new List<int>(); var panel = GameSceneContent();
        AddGameQuestion(panel, GameTask(session.Game), () => WordExample(picked.Word));
        var assembled = GamePrompt("…", 22); Live(assembled); panel.Children.Add(assembled);
        var count = GameCaption(""); Live(count); panel.Children.Add(count);
        AutomationProperties.SetAutomationId(count, "game.SelectionCount");
        var tiles = new Grid { ColumnSpacing = 5, RowSpacing = 5 }; var buttons = new List<Button>();
        var undo = GameActionButton(U("Games.UndoTile", "Undo last tile", "Son taşı geri al"), "", session.Game);
        undo.IsEnabled = false;
        Button? submit = null;
        void RefreshSelection()
        {
            Announce(assembled, selected.Count == 0
                ? U("Games.Sentence.Empty", "Select tiles to build your sentence.", "Cümlenizi oluşturmak için parçaları seçin.")
                : string.Join(' ', selected.Select(n => tokens[n])));
            Announce(count, $"{U("Games.Sentence.Selected", "Tiles selected", "Seçilen parçalar")}: {selected.Count} / {tokens.Length}");
            undo.IsEnabled = selected.Count > 0;
            if (submit is not null) UpdateGameSubmitState(submit);
        }
        for (var i = 0; i < tokens.Length; i++)
        {
            var index = i; var button = GameChoiceButton(tokens[i], session.Game); button.MinHeight = 48; button.Padding = new Thickness(8);
            SetGameChoiceMetadata(button, $"game.Tile.{index + 1}");
            AutomationProperties.SetName(button, $"{index + 1}: {tokens[i]}");
            button.Click += (_, _) =>
            {
                if (!GameCanAnswer(session) || _gameLocalBusy || selected.Contains(index)) return;
                selected.Add(index); button.IsEnabled = false; RefreshSelection();
            };
            buttons.Add(button); tiles.Children.Add(button);
        }
        ConfigureResponsiveGrid(tiles, 4, 95); panel.Children.Add(tiles);
        undo.Click += (_, _) =>
        {
            if (!GameCanAnswer(session) || _gameLocalBusy || selected.Count == 0) return;
            buttons[selected[^1]].IsEnabled = true; selected.RemoveAt(selected.Count - 1); RefreshSelection();
        };
        panel.Children.Add(undo);
        submit = SubmitGame(panel, session, b => ResolveGameAnswerAsync(session, GameEngine.SentenceKey(assembled.Text) == GameEngine.SentenceKey(sentence),
            FeedbackWithAnswer(sentence, assembled.Text), b, [picked.Word.Key]),
            () => selected.Count == tokens.Length);
        panel.Children.Remove(undo);
        panel.Children.Remove(submit);
        var actions = new Grid { ColumnSpacing = 8, RowSpacing = 8, Children = { undo, submit } };
        ConfigureResponsiveGrid(actions, 2, 180);
        panel.Children.Add(actions);
        RefreshSelection();
        AddGameScene(session.Game, panel);
    }

    private sealed record ClozeCandidate(VocabularyEntry Word, string BaseWord, string Sentence, int Start, int End)
    {
        public string MaskedSentence => Sentence[..Start] + "_______" + Sentence[End..];
    }

    private sealed record ClozeOption(string BaseWord, bool IsCorrect);

    private static string ClozeBaseWord(VocabularyEntry entry)
    {
        var raw = entry.Word.Trim();
        if (raw.StartsWith("sich ", StringComparison.OrdinalIgnoreCase)) raw = raw[5..].TrimStart();
        return LearningEngine.NormalizeAnswer(raw, entry.LanguageCode);
    }

    private static string ClozeFold(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var text = value.Replace("ß", "ss", StringComparison.Ordinal).Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(text.Length);
        foreach (var character in text)
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(char.ToLowerInvariant(character));
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static int ClozeCommonPrefixLength(string left, string right)
    {
        var size = Math.Min(left.Length, right.Length);
        var index = 0;
        while (index < size && left[index] == right[index]) index++;
        return index;
    }

    private static (int Start, int End)? ClozeSpan(string sentence, string baseWord)
    {
        if (string.IsNullOrWhiteSpace(sentence) || string.IsNullOrWhiteSpace(baseWord) || baseWord.Contains(' ')) return null;
        var target = ClozeFold(baseWord);
        if (target.Length < 2) return null;
        (int Start, int End)? best = null;
        var bestScore = 0d;
        foreach (Match token in Regex.Matches(sentence, @"[\p{L}\p{M}]+"))
        {
            if (token.Length == 0) continue;
            var folded = ClozeFold(token.Value);
            if (folded == target) return (token.Index, token.Index + token.Length);
            if (target.Length < 5) continue;
            var common = ClozeCommonPrefixLength(folded, target);
            var need = Math.Max(5, (int)Math.Ceiling(target.Length * 0.65));
            var lengthGap = Math.Abs(folded.Length - target.Length);
            if (common < need || lengthGap > 5) continue;
            var score = (double)common / target.Length;
            if (score <= bestScore) continue;
            bestScore = score;
            best = (token.Index, token.Index + token.Length);
        }
        return bestScore >= 0.6 ? best : null;
    }

    private ClozeCandidate[] BuildClozePool()
    {
        var pool = new List<ClozeCandidate>();
        foreach (var entry in _words)
        {
            var sentence = GameEngine.NativeText(entry.Example).Trim();
            if (!GameEngine.UsableExample(sentence)) continue;
            var baseWord = ClozeBaseWord(entry);
            if (baseWord.Length < 2 || baseWord.Contains(' ')) continue;
            var span = ClozeSpan(sentence, baseWord);
            if (span is null) continue;
            pool.Add(new ClozeCandidate(entry, baseWord, sentence, span.Value.Start, span.Value.End));
        }
        return pool.ToArray();
    }

    private ClozeOption[]? BuildClozeOptions(ClozeCandidate picked, IReadOnlyList<ClozeCandidate> pool)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal) { ClozeFold(picked.BaseWord) };
        var candidates = pool.Where(candidate => candidate.Word.Key != picked.Word.Key)
            .Where(candidate => seen.Add(ClozeFold(candidate.BaseWord))).ToArray();
        var samePartOfSpeech = candidates.Where(candidate =>
            string.Equals(candidate.Word.PartOfSpeech, picked.Word.PartOfSpeech, StringComparison.OrdinalIgnoreCase)).ToArray();
        var source = samePartOfSpeech.Length >= 3 ? samePartOfSpeech : candidates;
        var distractors = source.OrderBy(_ => _random.Next()).Take(3).Select(candidate =>
            new ClozeOption(candidate.BaseWord, false)).ToArray();
        if (distractors.Length < 3) return null;
        return distractors.Append(new ClozeOption(picked.BaseWord, true)).OrderBy(_ => _random.Next()).ToArray();
    }

    private void RenderClozeGame(GameSession session)
    {
        var pool = BuildClozePool();
        if (pool.Length < 4) { GameUnavailable(session, EligibleRequirement); return; }
        var picked = pool[_random.Next(pool.Length)];
        var options = BuildClozeOptions(picked, pool);
        if (options is null) { GameUnavailable(session, EligibleRequirement); return; }

        var panel = GameSceneContent();
        _gameHintProvider = () => LocalizedPart(picked.Word.Definition);
        AddGameQuestion(panel, picked.MaskedSentence, () => WordExample(picked.Word));
        if (!string.IsNullOrWhiteSpace(picked.Word.PartOfSpeech))
            panel.Children.Add(GameCaption(picked.Word.PartOfSpeech.ToUpperInvariant()));

        var choicesGrid = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
        var letters = new[] { "A", "B", "C", "D" };
        for (var index = 0; index < options.Length; index++)
        {
            var option = options[index];
            var button = GameChoiceButton($"{letters[index]}   {option.BaseWord}", session.Game);
            button.Tag = $"game-choice-{index}";
            SetGameChoiceMetadata(button, $"game.Choice.{index + 1}", (index + 1).ToString(CultureInfo.InvariantCulture));
            button.Click += async (_, _) =>
            {
                await ResolveGameAnswerAsync(session, option.IsCorrect,
                    FeedbackWithAnswer(picked.BaseWord, option.BaseWord, picked.Sentence), button, [picked.Word.Key]);
            };
            choicesGrid.Children.Add(button);
        }
        ConfigureResponsiveGrid(choicesGrid, 2, 220);
        panel.Children.Add(choicesGrid);
        AddGameScene(session.Game, panel);
    }

    private void RenderCategoryGame(GameSession session)
    {
        var epoch = _gameEpoch;
        if (_categoryWords.Length == 0)
        {
            var groups = GameEngine.Categories(_words, 3).ToArray();
            if (groups.Length == 0) { GameUnavailable(session, EligibleRequirement); return; }
            _categoryWords = groups[_random.Next(groups.Length)].ToArray();
        }
        var remaining = _categoryWords.Where(word => !_categoryFound.Contains(GameEngine.Bare(word))).ToArray();
        if (remaining.Length == 0) { GameUnavailable(session, EligibleRequirement); return; }
        var entry = remaining[_random.Next(remaining.Length)];
        var bare = GameEngine.Bare(entry);
        var distractors = _words.Where(word =>
                !string.Equals(word.Category, entry.Category, StringComparison.OrdinalIgnoreCase) &&
                GameEngine.Bare(word) != bare)
            .DistinctBy(GameEngine.Bare)
            .OrderBy(_ => _random.Next())
            .Take(3)
            .ToArray();
        if (distractors.Length < 3) { GameUnavailable(session, EligibleRequirement); return; }
        var choices = distractors.Append(entry).OrderBy(_ => _random.Next()).ToArray();
        _gameHintProvider = () => WordHint(entry, entry.Category);
        var panel = GameSceneContent();
        AddGameQuestion(panel, $"{entry.Category}\n{LocalizedPart(entry.Definition)}", () => WordExample(entry));
        var count = GameCaption($"{_categoryFound.Count} / {_categoryWords.Length}");
        Live(count);
        panel.Children.Add(count);
        panel.Children.Add(GameCaption(U("Games.Category.Unique", "Choose a new word in this category. The bulb shows one example word.", "Bu kategoriden yeni bir kelime seçin. Ampul bir örnek kelime gösterir.")));
        GameChoices(panel, session, choices.Select(word => word.Word).ToArray(), async (index, button) =>
        {
            var selected = choices[index];
            var correct = selected.Key == entry.Key;
            IReadOnlyList<string> reviewedKeys = [selected.Key];
            if (!await RecordGameSubAnswerAsync(session, correct, button, reviewedKeys) || !IsCurrentGameRound(session, epoch)) return;
            if (correct)
            {
                _categoryFound.Add(bare);
                Announce(count, $"{_categoryFound.Count} / {_categoryWords.Length}");
            }
            await ResolveGameAnswerAsync(session, correct, FeedbackWithAnswer(entry.Word, selected.Word), button, reviewedKeys, answerAlreadyRecorded: true);
        });
        AddGameScene(session.Game, panel);
    }

    private int _gameRoundPoints = 100;
    private void RenderClueGame(GameSession session)
    {
        var entry = _words.Where(w => GameEngine.Bare(w).Length >= 2 && !string.IsNullOrWhiteSpace(w.Definition)).OrderBy(_ => _random.Next()).FirstOrDefault();
        if (entry is null) { GameUnavailable(session, EligibleRequirement); return; }
        var bare = GameEngine.Bare(entry); var panel = GameSceneContent();
        var choices = _words.Where(word => word.Key != entry.Key && GameEngine.Bare(word) != bare)
            .DistinctBy(GameEngine.Bare)
            .OrderBy(_ => _random.Next())
            .Take(3)
            .Append(entry)
            .OrderBy(_ => _random.Next())
            .ToArray();
        if (choices.Length < 4) { GameUnavailable(session, EligibleRequirement); return; }
        _gameHintProvider = () => WordHint(entry);
        _gameRoundPoints = 120;
        string[] clues = [entry.Category, entry.PartOfSpeech, $"{bare.Length}", bare[..1], LocalizedPart(entry.Definition)];
        var revealed = 0; var caption = AddGameQuestion(panel, GameTask(session.Game), () => WordExample(entry));
        var reveal = GameActionButton(U("Games.Clue.Reveal", "Reveal next clue (costs points)", "Sonraki ipucu (puan azaltır)"), "", session.Game);
        reveal.Click += (_, _) =>
        {
            if (!GameCanAnswer(session) || revealed >= clues.Length) return;
            revealed++; _gameRoundPoints = Math.Max(20, 120 - revealed * 20);
            Announce(caption, string.Join(" · ", clues.Take(revealed))); reveal.IsEnabled = revealed < clues.Length;
        };
        panel.Children.Add(reveal);
        GameChoices(panel, session, choices.Select(word => word.Word).ToArray(), (index, button) =>
            ResolveGameAnswerAsync(session, choices[index].Key == entry.Key,
                FeedbackWithAnswer(entry.Word, choices[index].Word), button, [entry.Key]));
        AddGameScene(session.Game, panel);
    }

    private void RenderRackGame(GameSession session)
    {
        var pool = _words.Where(w => GameEngine.Bare(w).Length is >= 3 and <= 9 && GameEngine.Bare(w).All(char.IsLetter)).ToArray();
        if (pool.Length == 0) { GameUnavailable(session, EligibleRequirement); return; }
        var seed = pool[_random.Next(pool.Length)]; var rack = new string(GameEngine.Bare(seed).OrderBy(_ => _random.Next()).ToArray());
        _gameHintProvider = () => WordHint(seed);
        var panel = GameSceneContent();
        panel.Spacing = 6;
        AddGameQuestion(panel, GameTask(session.Game), () => WordExample(seed));
        panel.Children.Add(GamePrompt(string.Join(' ', rack.ToUpperInvariant().ToCharArray()), 30));
        var selected = new List<int>();
        var assembled = GamePrompt("", 24);
        Live(assembled);
        panel.Children.Add(assembled);
        var count = GameCaption("");
        Live(count);
        panel.Children.Add(count);
        var tiles = new Grid { ColumnSpacing = 5, RowSpacing = 4 };
        var tileButtons = new List<Button>();
        var undo = GameActionButton(U("Games.UndoTile", "Undo last tile", "Son taşı geri al"), "", session.Game);
        var clear = GameActionButton(U("Games.ClearSelection", "Clear selection", "Seçimi temizle"), "", session.Game);
        undo.IsEnabled = false;
        clear.IsEnabled = false;
        Button? submit = null;

        void RefreshSelection()
        {
            var attempt = new string(selected.Select(index => char.ToUpperInvariant(rack[index])).ToArray());
            Announce(assembled, selected.Count == 0
                ? U("Games.Input.RackWord", "Word built from these tiles", "Bu harflerden oluşturulan kelime")
                : attempt);
            Announce(count, $"{U("Games.Sentence.Selected", "Tiles selected", "Seçilen parçalar")}: {selected.Count} / {rack.Length}");
            for (var i = 0; i < tileButtons.Count; i++) tileButtons[i].IsEnabled = !selected.Contains(i);
            undo.IsEnabled = selected.Count > 0;
            clear.IsEnabled = selected.Count > 0;
            if (submit is not null) UpdateGameSubmitState(submit);
        }

        for (var i = 0; i < rack.Length; i++)
        {
            var index = i;
            var button = GameChoiceButton(char.ToUpperInvariant(rack[index]).ToString(), session.Game);
            button.MinHeight = 44;
            button.MinWidth = 44;
            button.Padding = new Thickness(6);
            button.FontSize = ReadingSize(18);
            button.HorizontalContentAlignment = HorizontalAlignment.Center;
            SetGameChoiceMetadata(button, $"game.Tile.{index + 1}");
            button.Click += (_, _) =>
            {
                if (!GameCanAnswer(session) || _gameLocalBusy || selected.Contains(index)) return;
                selected.Add(index);
                RefreshSelection();
            };
            tileButtons.Add(button);
            tiles.Children.Add(button);
        }
        ConfigureResponsiveGrid(tiles, Math.Min(6, rack.Length), 48);
        panel.Children.Add(tiles);

        undo.Click += (_, _) =>
        {
            if (!GameCanAnswer(session) || _gameLocalBusy || selected.Count == 0) return;
            selected.RemoveAt(selected.Count - 1);
            RefreshSelection();
        };
        clear.Click += (_, _) =>
        {
            if (!GameCanAnswer(session) || _gameLocalBusy || selected.Count == 0) return;
            selected.Clear();
            RefreshSelection();
        };

        panel.Children.Add(undo);
        panel.Children.Add(clear);
        submit = SubmitGame(panel, session, button =>
        {
            var attempt = new string(selected.Select(index => rack[index]).ToArray());
            var word = GameEngine.RackWord(pool, attempt, rack, new HashSet<string>(), _settings.StudyLanguage);
            var tested = word ?? _words.FirstOrDefault(w => GameEngine.Bare(w) == GameAnswer(attempt));
            IReadOnlyList<string> reviewedKeys = tested is null ? [] : [tested.Key];
            _gameRoundPoints = word is null ? 0 : GameEngine.Bare(word).Length * 20;
            return ResolveGameAnswerAsync(session, word is not null,
                FeedbackWithAnswer(word?.Word ?? seed.Word, attempt), button, reviewedKeys);
        }, () => selected.Count >= 3);

        void CompactAction(Button button)
        {
            button.MinHeight = 48;
            button.Padding = new Thickness(12, 8, 12, 8);
            button.FontSize = ReadingSize(18);
        }

        CompactAction(undo);
        CompactAction(clear);
        CompactAction(submit);

        panel.Children.Remove(undo);
        panel.Children.Remove(clear);
        panel.Children.Remove(submit);
        var actions = new Grid { ColumnSpacing = 8, RowSpacing = 2, Children = { undo, clear, submit } };
        ConfigureResponsiveGrid(actions, 3, 140);
        panel.Children.Add(actions);
        RefreshSelection();
        AddGameScene(session.Game, panel);
    }

    private void RenderClassGame(GameSession session)
    {
        var pool = _words.Where(w => GameEngine.WordClass(w.PartOfSpeech) is not null).ToArray();
        if (pool.Length == 0) { GameUnavailable(session, EligibleRequirement); return; }
        var word = pool[_random.Next(pool.Length)]; var panel = GameSceneContent();
        // The word is already fully visible and the answer IS its class -- a hint would either be
        // redundant (letters/length) or leak the answer (the class itself). Left unset.
        AddGameQuestion(panel, $"{word.Word}\n{GameEngine.NativeText(word.Example)}", () => WordExample(word));
        string[] ids = ["noun", "verb", "adjective", "adverb"];
        string[] labels = [U("Games.Class.Noun", "Noun", "İsim"), U("Games.Class.Verb", "Verb", "Fiil"), U("Games.Class.Adjective", "Adjective", "Sıfat"), U("Games.Class.Adverb", "Adverb", "Zarf")];
        var correct = Array.IndexOf(ids, GameEngine.WordClass(word.PartOfSpeech));
        GameChoices(panel, session, labels, (i, b) => ResolveGameAnswerAsync(session, i == correct,
            FeedbackWithAnswer(word.Word + " — " + labels[correct], labels[i]), b, [word.Key]));
        AddGameScene(session.Game, panel);
    }

    private void RenderOddGame(GameSession session)
    {
        var groups = GameEngine.Categories(_words, 3).OrderBy(_ => _random.Next()).ToArray();
        if (groups.Length < 2) { GameUnavailable(session, EligibleRequirement); return; }
        var category = groups[0]; var odd = groups[1].OrderBy(_ => _random.Next()).First();
        var choices = category.OrderBy(_ => _random.Next()).Take(3).Append(odd).OrderBy(_ => _random.Next()).ToArray();
        // The answer is WHICH choice doesn't belong -- hinting the category or the odd word leaks it. Left unset.
        var panel = GameSceneContent(); AddGameQuestion(panel, category.Key, () => category.FirstOrDefault() is { } first ? WordExample(first) : null); panel.Children.Add(GameCaption(GameInstructions(session.Game)));
        GameChoices(panel, session, choices.Select(w => w.Word).ToArray(), (i, b) => ResolveGameAnswerAsync(session, choices[i] == odd,
            FeedbackWithAnswer(odd.Word + " — " + odd.Category, choices[i].Word), b, [odd.Key]));
        AddGameScene(session.Game, panel);
    }

    private void RenderBingoGame(GameSession session)
    {
        var words = _words.DistinctBy(GameEngine.Bare).DistinctBy(w => LocalizedPart(w.Definition)).Where(w => w.Word.Length <= 22 && LocalizedPart(w.Definition).Length is > 0 and <= 220).OrderBy(_ => _random.Next()).Take(16).ToArray();
        if (words.Length < 16) { GameUnavailable(session, EligibleRequirement); return; }
        var epoch = _gameEpoch;
        var found = new HashSet<int>(); var target = _random.Next(16); var panel = GameSceneContent();
        var clue = AddGameQuestion(panel, LocalizedPart(words[target].Definition), () => WordExample(words[target]));
        _gameHintProvider = () => WordHint(words[target]);
        var board = new Grid { ColumnSpacing = 5, RowSpacing = 5 };
        for (var i = 0; i < 16; i++)
        {
            var index = i; var button = GameChoiceButton(words[i].Word, session.Game); button.MinHeight = 48; button.Padding = new Thickness(8); button.FontSize = ReadingSize(18);
            SetGameChoiceMetadata(button, $"game.Cell.{index + 1}");
            AutomationProperties.SetName(button, $"{index + 1}: {words[i].Word}");
            button.Click += async (_, _) =>
            {
                if (!GameCanAnswer(session) || _gameLocalBusy || found.Contains(index)) return;
                var tested = words[target];
                var correct = index == target;
                if (!await RecordGameSubAnswerAsync(session, correct, button, [tested.Key]) || !IsCurrentGameRound(session, epoch)) return;
                if (!correct)
                {
                    await ResolveGameAnswerAsync(session, false, FeedbackWithAnswer(tested.Word, words[index].Word), button, [tested.Key], answerAlreadyRecorded: true);
                    return;
                }
                found.Add(index); button.Content = ButtonContent("✓ " + words[index].Word, "\uE73E"); button.IsEnabled = false;
                if (GameEngine.BingoLine(found)) { await ResolveGameAnswerAsync(session, true, U("Games.Bingo.Line", "Completed a line", "Bir çizgi tamamlandı"), button, [tested.Key], answerAlreadyRecorded: true); return; }
                var remaining = Enumerable.Range(0, 16).Where(n => !found.Contains(n)).ToArray(); target = remaining[_random.Next(remaining.Length)];
                Announce(clue, LocalizedPart(words[target].Definition));
                _gameHintProvider = () => WordHint(words[target]); // target changed -- refresh so the hint never goes stale
            };
            Grid.SetColumn(button, i % 4); Grid.SetRow(button, i / 4); board.Children.Add(button);
        }
        for (var i = 0; i < 4; i++) { board.ColumnDefinitions.Add(new()); board.RowDefinitions.Add(new() { Height = GridLength.Auto }); }
        panel.Children.Add(board); AddGameScene(session.Game, panel);
    }

    private void RenderMatrixGame(GameSession session)
    {
        const int width = 6;
        var pool = _words.Where(w => GameEngine.Bare(w).Length is >= 3 and <= width && GameEngine.Bare(w).All(char.IsLetter)).ToArray();
        if (pool.Length == 0) { GameUnavailable(session, EligibleRequirement); return; }
        var entry = pool[_random.Next(pool.Length)]; var target = GameEngine.Bare(entry).ToUpperInvariant();
        _gameHintProvider = () => WordHint(entry);
        var alphabet = string.Concat(pool.Select(GameEngine.Bare)).ToUpperInvariant();
        var letters = Enumerable.Range(0, width * width).Select(_ => alphabet[_random.Next(alphabet.Length)]).ToArray();
        var directions = new[] { (1, 0), (0, 1), (1, 1), (-1, 0), (0, -1), (-1, -1) };
        var (dx, dy) = directions[_random.Next(directions.Length)];
        var starts = Enumerable.Range(0, width * width).Where(n =>
            n % width + dx * (target.Length - 1) is >= 0 and < width &&
            n / width + dy * (target.Length - 1) is >= 0 and < width).ToArray();
        var start = starts[_random.Next(starts.Length)];
        var x = start % width; var y = start / width;
        for (var i = 0; i < target.Length; i++) letters[(y + dy * i) * width + x + dx * i] = target[i];
        var selected = new List<int>(); var panel = GameSceneContent(); AddGameQuestion(panel, LocalizedPart(entry.Definition), () => WordExample(entry));
        var selection = GameCaption(""); Live(selection);
        var count = GameCaption(""); Live(count);
        AutomationProperties.SetAutomationId(count, "game.SelectionCount");
        var board = new Grid { ColumnSpacing = 3, RowSpacing = 3 }; var buttons = new List<Button>();
        var selectionHelp = U("Games.Matrix.SelectionHelp", "Select adjacent letters in one straight line. Select a chosen square to undo from there, or clear to restart.", "Bitişik harfleri tek bir düz çizgide seçin. O noktadan geri almak için seçili bir kareye basın veya seçimi temizleyip baştan başlayın.");
        AutomationProperties.SetHelpText(board, selectionHelp);
        var status = new Grid { ColumnSpacing = 8, RowSpacing = 8, Children = { selection, count } };
        ConfigureResponsiveGrid(status, 2, 150);
        panel.Children.Add(status);
        Button? submit = null;
        bool CanExtend(int index) => selected.Count < target.Length && !selected.Contains(index) &&
            (selected.Count == 0 || GameEngine.StraightSelection(selected.Append(index).ToArray(), width));
        void RefreshSelection()
        {
            Announce(selection, new string(selected.Select(n => letters[n]).ToArray()));
            Announce(count, $"{U("Games.Matrix.Selected", "Letters selected", "Seçilen harfler")}: {selected.Count} / {target.Length}");
            for (var i = 0; i < buttons.Count; i++)
            {
                var chosen = selected.Contains(i);
                buttons[i].IsEnabled = chosen || CanExtend(i);
                buttons[i].BorderThickness = new Thickness(chosen ? 3 : 1);
                AutomationProperties.SetItemStatus(buttons[i], chosen
                    ? U("Games.Matrix.CellSelected", "Selected — activate to undo from here", "Seçildi — bu noktadan geri almak için etkinleştirin") : "");
            }
            if (submit is not null) UpdateGameSubmitState(submit);
        }
        for (var i = 0; i < letters.Length; i++)
        {
            var index = i; var button = GameChoiceButton(letters[i].ToString(), session.Game); button.Padding = new Thickness(6); button.MinHeight = 48;
            SetGameChoiceMetadata(button, $"game.Cell.{index + 1}");
            button.HorizontalContentAlignment = HorizontalAlignment.Center; button.VerticalContentAlignment = VerticalAlignment.Center;
            button.BorderBrush = button.Foreground;
            if (button.Content is TextBlock label) label.TextAlignment = TextAlignment.Center;
            AutomationProperties.SetName(button, $"{i / width + 1}, {i % width + 1}: {letters[i]}");
            AutomationProperties.SetHelpText(button, selectionHelp);
            button.Click += (_, _) =>
            {
                if (!GameCanAnswer(session) || _gameLocalBusy) return;
                var previous = selected.IndexOf(index);
                if (previous >= 0) selected.RemoveRange(previous, selected.Count - previous);
                else
                {
                    if (!CanExtend(index)) return;
                    selected.Add(index);
                }
                RefreshSelection();
            };
            Grid.SetColumn(button, i % width); Grid.SetRow(button, i / width); board.Children.Add(button); buttons.Add(button);
        }
        for (var i = 0; i < width; i++) { board.ColumnDefinitions.Add(new()); board.RowDefinitions.Add(new() { Height = GridLength.Auto }); }
        var visual = board; board.Width = 308;
        var clear = GameActionButton(U("Games.ClearSelection", "Clear selection", "Seçimi temizle"), "", session.Game);
        clear.Click += (_, _) => { if (!GameCanAnswer(session) || _gameLocalBusy) return; selected.Clear(); RefreshSelection(); };
        panel.Children.Add(clear);
        submit = SubmitGame(panel, session, b => ResolveGameAnswerAsync(session, GameEngine.StraightSelection(selected, width) && selection.Text == target,
            FeedbackWithAnswer(entry.Word, selection.Text), b, [entry.Key]),
            () => selected.Count == target.Length && GameEngine.StraightSelection(selected, width));
        RefreshSelection();
        AddGameScene(session.Game, panel, visual);
    }

    private void RenderCrosswordGame(GameSession session)
    {
        VocabularyEntry[] CompactCrosswordPool(int maxWordLength, int maxDefinitionLength) => _words.Where(word =>
            {
                var bare = GameEngine.Bare(word);
                var definition = LocalizedPart(word.Definition).Trim();
                return bare.Length >= 3 && bare.Length <= maxWordLength &&
                    definition.Length > 0 && definition.Length <= maxDefinitionLength;
            })
            .DistinctBy(word => GameEngine.Bare(word))
            .DistinctBy(word => LocalizedPart(word.Definition))
            .ToArray();

        var crossing =
            GameEngine.FindCrossing(CompactCrosswordPool(8, 90).OrderBy(_ => _random.Next())) ??
            GameEngine.FindCrossing(CompactCrosswordPool(10, 120).OrderBy(_ => _random.Next())) ??
            GameEngine.FindCrossing(_words.OrderBy(_ => _random.Next()));
        if (crossing is null) { GameUnavailable(session, EligibleRequirement); return; }
        var epoch = _gameEpoch;
        var a = GameEngine.Bare(crossing.Across); var d = GameEngine.Bare(crossing.Down); var panel = GameSceneContent();
        panel.Spacing = 8;
        var board = new Grid { ColumnSpacing = 2, RowSpacing = 2 }; var cells = new Dictionary<(int X, int Y), TextBlock>();
        var cellSize = Math.Max(34, ReadingSize(18) * 1.4 + 4);
        board.MinWidth = a.Length * cellSize + (a.Length - 1) * board.ColumnSpacing;
        for (var i = 0; i < a.Length; i++) board.ColumnDefinitions.Add(new() { Width = new GridLength(cellSize) });
        for (var i = 0; i < d.Length; i++) board.RowDefinitions.Add(new() { Height = new GridLength(cellSize) });
        void Cell(int x, int y)
        {
            if (cells.ContainsKey((x, y))) return;
            var cellBackground = Windows.UI.Color.FromArgb(255, 30, 41, 59);
            var cellForeground = AppearancePalette.EnsureTextContrast(cellBackground, Microsoft.UI.Colors.White);
            var text = GamePrompt("·", 16);
            text.Foreground = new SolidColorBrush(cellForeground);
            cells[(x, y)] = text;
            var box = new Border
            {
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(AppearancePalette.EnsureBoundaryContrast(cellBackground, Windows.UI.Color.FromArgb(255, 148, 163, 184))),
                Background = new SolidColorBrush(cellBackground),
                Child = text,
            };
            Grid.SetColumn(box, x); Grid.SetRow(box, y); board.Children.Add(box);
        }
        for (var i = 0; i < a.Length; i++) Cell(i, crossing.DownIndex);
        for (var i = 0; i < d.Length; i++) Cell(crossing.AcrossIndex, i);
        var acrossLabel = U("Games.Crossword.Across", "Across", "Yatay"); var downLabel = U("Games.Crossword.Down", "Down", "Dikey");
        var entryOrder = U("Games.Crossword.EntryOrder", "Across first, then Down. Each answer is scored separately.", "Önce yatay, sonra dikey. Her yanıt ayrı puanlanır.");
        AutomationProperties.SetHelpText(board, entryOrder);
        var acrossSummary = GameCaption(""); Live(acrossSummary); acrossSummary.Visibility = Visibility.Collapsed; panel.Children.Add(acrossSummary);
        AddGameQuestion(panel, acrossLabel + ": " + LocalizedPart(crossing.Across.Definition), () => WordExample(crossing.Across));
        var acrossCard = panel.Children[panel.Children.Count - 1];
        AddGameQuestion(panel, downLabel + ": " + LocalizedPart(crossing.Down.Definition), () => WordExample(crossing.Down), "Down");
        var downCard = panel.Children[panel.Children.Count - 1];
        downCard.Visibility = Visibility.Collapsed;
        var acrossChoicesHost = new ContentControl { HorizontalContentAlignment = HorizontalAlignment.Stretch };
        var downChoicesHost = new ContentControl
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Visibility = Visibility.Collapsed,
        };
        panel.Children.Add(acrossChoicesHost);
        panel.Children.Add(downChoicesHost);
        var acrossSolved = false;
        var acrossAnswer = "";
        var downAnswer = "";
        _gameHintProvider = () => WordHint(crossing.Across);

        void Update()
        {
            var av = acrossAnswer;
            var dv = downAnswer;
            foreach (var ((x, y), text) in cells)
            {
                var ac = y == crossing.DownIndex && x < av.Length ? av[x].ToString() : "";
                var dc = x == crossing.AcrossIndex && y < dv.Length ? dv[y].ToString() : "";
                text.Text = ac.Length > 0 && dc.Length > 0 && ac != dc ? "≠" : ac.Length > 0 ? ac : dc.Length > 0 ? dc : "·";
            }
        }

        VocabularyEntry[] BuildChoices(VocabularyEntry answer, int length)
        {
            var distractors = _words.Where(word => word.Key != answer.Key && GameEngine.Bare(word).Length == length)
                .DistinctBy(GameEngine.Bare)
                .OrderBy(_ => _random.Next())
                .Take(3)
                .ToArray();
            if (distractors.Length < 3) return [];
            return distractors.Append(answer).OrderBy(_ => _random.Next()).ToArray();
        }

        void CompactChoiceHost(StackPanel host)
        {
            if (host.Children.OfType<Grid>().FirstOrDefault() is not Grid grid) return;
            grid.ColumnSpacing = 6;
            grid.RowSpacing = 6;
            foreach (var button in grid.Children.OfType<Button>())
            {
                button.MinHeight = 44;
                button.Padding = new Thickness(10, 6, 10, 6);
                button.FontSize = ReadingSize(18);
            }
        }

        void RenderAcrossChoices()
        {
            var options = BuildChoices(crossing.Across, a.Length);
            if (options.Length < 4) { GameUnavailable(session, EligibleRequirement); return; }
            var answers = GameSceneContent();
            GameChoices(answers, session, options.Select(option => option.Word).ToArray(), async (index, button) =>
            {
                var selected = options[index];
                var correct = selected.Key == crossing.Across.Key;
                IReadOnlyList<string> reviewedKeys = [crossing.Across.Key];
                if (!await RecordGameSubAnswerAsync(session, correct, button, reviewedKeys) || !IsCurrentGameRound(session, epoch)) return;
                if (!correct)
                {
                    ShakeElement(button);
                    if (!await PauseGameFeedbackAsync(session, U("Games.TryAgain", "Not yet — try again", "Henüz değil — tekrar deneyin") + "\n" + selected.Word) || !IsCurrentGameRound(session, epoch)) return;
                    return;
                }
                acrossSolved = true;
                acrossAnswer = a.ToUpperInvariant();
                Update();
                acrossCard.Visibility = Visibility.Collapsed;
                acrossChoicesHost.Visibility = Visibility.Collapsed;
                downCard.Visibility = Visibility.Visible;
                downChoicesHost.Visibility = Visibility.Visible;
                acrossSummary.Visibility = Visibility.Visible;
                Announce(acrossSummary, $"✓ {acrossLabel}: {crossing.Across.Word}");
                _gameHintProvider = () => WordHint(crossing.Down);
                if (!await PauseGameFeedbackAsync(session, T("Common.Correct") + "\n" + crossing.Across.Word) || !IsCurrentGameRound(session, epoch)) return;
            });
            CompactChoiceHost(answers);
            acrossChoicesHost.Content = answers;
        }

        void RenderDownChoices()
        {
            var options = BuildChoices(crossing.Down, d.Length);
            if (options.Length < 4) { GameUnavailable(session, EligibleRequirement); return; }
            var answers = GameSceneContent();
            GameChoices(answers, session, options.Select(option => option.Word).ToArray(), async (index, button) =>
            {
                if (!acrossSolved) return;
                var selected = options[index];
                var correct = selected.Key == crossing.Down.Key;
                IReadOnlyList<string> reviewedKeys = [crossing.Down.Key];
                if (!await RecordGameSubAnswerAsync(session, correct, button, reviewedKeys) || !IsCurrentGameRound(session, epoch)) return;
                if (!correct)
                {
                    ShakeElement(button);
                    if (!await PauseGameFeedbackAsync(session, U("Games.TryAgain", "Not yet — try again", "Henüz değil — tekrar deneyin") + "\n" + selected.Word) || !IsCurrentGameRound(session, epoch)) return;
                    return;
                }
                downAnswer = d.ToUpperInvariant();
                Update();
                await ResolveGameAnswerAsync(session, true,
                    acrossLabel + ": " + crossing.Across.Word + " · " + downLabel + ": " + crossing.Down.Word,
                    button, reviewedKeys, answerAlreadyRecorded: true);
            });
            CompactChoiceHost(answers);
            downChoicesHost.Content = answers;
        }

        Update();
        RenderAcrossChoices();
        RenderDownChoices();
        AddGameScene(session.Game, panel, board);
    }

    private async void RenderLocalDataGame(GameSession session)
    {
        var language = _settings.StudyLanguage; var level = _settings.Level; var epoch = _gameEpoch;
        var reading = session.Game.Id == "readingcomprehension";
        if (language is not ("en" or "de" or "fr"))
        { GameUnavailable(session, U("Games.Require.LocalData", "This game needs a bundled, validated reading or relationship dataset for this language/level. Available languages: English, German, French.", "Bu oyun bu dil/seviye için yerel, doğrulanmış okuma veya ilişki verisi gerektirir. Kullanılabilir diller: İngilizce, Almanca, Fransızca.")); return; }
        _gameLocalBusy = true;
        try
        {
            var key = language + ":" + level;
            if (reading && !_readingCache.ContainsKey(key))
            {
                var file = language == "en" ? "readingcompencefr.js" : language == "de" ? "readingcompde.js" : "readingcompfr.js";
                var source = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Data", file));
                _readingCache[key] = await Task.Run(() => LocalGameData.ReadPassages(source, level));
            }
            else if (!reading && !_semanticCache.ContainsKey(key))
            {
                var file = language == "en" ? "synanten.js" : language == "de" ? "synantde.js" : "synantfr.js";
                var source = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Data", file));
                _semanticCache[key] = await Task.Run(() => LocalGameData.ReadRelationships(source, level));
            }
            if (_activeGame != session || epoch != _gameEpoch) return;
            _gameLocalBusy = false;
            if (reading)
            {
                var passages = _readingCache[key];
                if (passages.Length == 0) { GameUnavailable(session, U("Games.Require.Reading", "No valid local passage and questions exist for this language/level.", "Bu dil/seviye için geçerli yerel metin ve sorular bulunmuyor.")); return; }
                // Passage + question, no single vocabulary word target -- hint left unset.
                var passage = passages[((session.Round - 1) / 5) % passages.Length];
                var question = passage.Questions[(session.Round - 1) % passage.Questions.Length];
                var panel = GameSceneContent();
                // Explicit text pages, never clipping/truncating the passage or adding a scroll region.
                var pages = new List<string>(); var current = "";
                foreach (var token in GameEngine.Tokens(passage.Text))
                { if (current.Length + token.Length > 120 && current.Length > 0) { pages.Add(current); current = ""; } current += (current.Length > 0 ? " " : "") + token; }
                if (current.Length > 0) pages.Add(current);
                var pageStatus = GameCaption(""); Live(pageStatus);
                AutomationProperties.SetAutomationId(pageStatus, "game.Reading.Page");
                var titleRow = new Grid { ColumnSpacing = 8, RowSpacing = 8, Children = { GameCaption(passage.Title), pageStatus } };
                ConfigureResponsiveGrid(titleRow, 2, 180);
                panel.Children.Add(titleRow);
                var noTargetWord = U("Games.Example.NoTargetWord", "This reading round does not test one vocabulary word.", "Bu okuma turunda tek bir kelime sorulmaz.");
                var page = 0; var text = AddGameQuestion(panel, pages[0], () => new GameQuestionExample(noTargetWord, passage.Text));
                var navigation = new Grid { ColumnSpacing = 6 };
                var previous = GameActionButton(U("Games.Reading.Previous", "Previous text page", "Önceki metin sayfası"), "", session.Game);
                var next = GameActionButton(U("Games.Reading.Next", "Next text page", "Sonraki metin sayfası"), "", session.Game);
                void Refresh()
                {
                    Announce(pageStatus, $"{U("Games.Reading.Page", "Text page", "Metin sayfası")} {page + 1} / {pages.Count}");
                    Announce(text, pages[page]); previous.IsEnabled = page > 0; next.IsEnabled = page + 1 < pages.Count;
                }
                previous.Click += (_, _) => { if (!GameCanAnswer(session) || page == 0) return; page--; Refresh(); };
                next.Click += (_, _) => { if (!GameCanAnswer(session) || page + 1 == pages.Count) return; page++; Refresh(); };
                navigation.Children.Add(previous); navigation.Children.Add(next); ConfigureResponsiveGrid(navigation, 2, 150); panel.Children.Add(navigation); Refresh();
                var questionPanel = GameSceneContent(); questionPanel.Visibility = Visibility.Collapsed;
                AddGameQuestion(questionPanel, question.Prompt, () => new GameQuestionExample(question.Prompt, question.Explanation), "Answer");
                GameChoices(questionPanel, session, question.Options, (i, b) => ResolveGameAnswerAsync(session, i == question.Correct, question.Options[question.Correct] + "\n" + question.Explanation, b, []));
                var answerQuestion = GameActionButton(U("Games.Reading.Answer", "Answer question", "Soruyu yanıtla"), "", session.Game);
                answerQuestion.Click += (_, _) => { if (!GameCanAnswer(session) || _gameLocalBusy) return; panel.Visibility = Visibility.Collapsed; questionPanel.Visibility = Visibility.Visible; };
                panel.Children.Add(answerQuestion);
                var reread = GameActionButton(U("Games.Reading.Reread", "Read text again", "Metni tekrar oku"), "", session.Game);
                reread.Click += (_, _) => { if (!GameCanAnswer(session) || _gameLocalBusy) return; questionPanel.Visibility = Visibility.Collapsed; panel.Visibility = Visibility.Visible; };
                questionPanel.Children.Add(reread);
                var content = GameSceneContent(); content.Children.Add(panel); content.Children.Add(questionPanel);
                AddGameScene(session.Game, content);
            }
            else
            {
                var pairs = _semanticCache[key];
                if (pairs.Length == 0) { GameUnavailable(session, U("Games.Require.Relationships", "No reciprocal, non-conflicting synonym/antonym pairs exist for this language/level. Category labels are not semantic evidence.", "Bu dil/seviye için karşılıklı, çelişmeyen eş/zıt anlamlı çiftler yok. Kategori etiketleri anlamsal kanıt değildir.")); return; }
                // Both words already visible; the hidden part is the relationship classification itself -- hint left unset.
                var pair = pairs[_random.Next(pairs.Length)]; var panel = GameSceneContent();
                var reviewedKeys = GameEngine.SemanticWordKeys(pair, _words, language, level);
                VocabularyEntry? SemanticWord(string term) => _words.FirstOrDefault(word =>
                    string.Equals(word.Word, term, StringComparison.Ordinal) ||
                    string.Equals(GameEngine.Bare(word), term, StringComparison.OrdinalIgnoreCase));
                string DefinitionFor(string term)
                {
                    var entry = SemanticWord(term);
                    return entry is null ? $"{term}: {U("Games.Example.DefinitionUnavailable", "No definition is available for this round.", "Bu tur için tanım yok.")}"
                        : $"{entry.Word}: {MeaningWithTurkish(entry.Definition)}";
                }
                string ExampleFor(string term)
                {
                    var entry = SemanticWord(term);
                    return entry is null ? $"{term}: {U("Games.Example.Unavailable", "No example is available for this round.", "Bu tur için örnek yok.")}"
                        : $"{entry.Word}: {ExampleWithTurkish(entry.Example)}";
                }
                AddGameQuestion(panel, pair.Word + " ↔ " + pair.Related,
                    () => new GameQuestionExample(
                        $"{DefinitionFor(pair.Word)}\n{DefinitionFor(pair.Related)}",
                        $"{ExampleFor(pair.Word)}\n{ExampleFor(pair.Related)}"));
                string[] labels = [U("Games.Semantic.Synonym", "Synonyms", "Eş anlamlı"), U("Games.Semantic.Antonym", "Antonyms", "Zıt anlamlı")];
                GameChoices(panel, session, labels, (i, b) => ResolveGameAnswerAsync(session, (i == 1) == pair.Antonym, pair.Word + " ↔ " + pair.Related + " — " + labels[pair.Antonym ? 1 : 0], b, reviewedKeys));
                AddGameScene(session.Game, panel);
            }
        }
        catch (Exception ex) { if (_activeGame == session && epoch == _gameEpoch) GameUnavailable(session, EligibleRequirement + "\n" + ex.Message); }
        finally { if (_activeGame == session && epoch == _gameEpoch) _gameLocalBusy = false; }
    }
}