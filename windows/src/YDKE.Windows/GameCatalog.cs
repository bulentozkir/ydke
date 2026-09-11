namespace YDKE_Windows;

internal static class GameCatalog
{
    public static readonly IReadOnlyList<GameDefinition> All =
    [
        Simple("hangman", "Hangman", "Kelimeyi harf harf tahmin edin.", "Guess the word one letter at a time.", "", GameMechanic.Hangman),
        Simple("scramble", "Word Scramble", "Karışık harfleri doğru sıraya dizin.", "Reorder shuffled letters into a word.", "", GameMechanic.LetterTiles),
        Simple("dictation", "Listening Dictation", "Kelimeyi dinleyin ve doğru yazın.", "Listen and type the word you hear.", "", GameMechanic.ListeningTyping),
        Simple("speedround", "Speed Round", "60 saniyede mümkün olduğunca çok doğru cevap verin.", "Answer as many questions as possible in 60 seconds.", "", GameMechanic.TimedChoice),
        Simple("survival", "Survival Streak", "İlk yanlışa kadar doğru serinizi büyütün.", "Build a streak until your first mistake.", "", GameMechanic.Survival),
        Simple("truefalse", "True or False", "Anlamın doğru olup olmadığına karar verin.", "Decide whether the meaning is correct.", "", GameMechanic.TrueFalse),
        Simple("wordclass", "Word Class", "Kelimenin türünü seçin.", "Classify the word as noun, verb, adjective, or adverb.", "", GameMechanic.Classification),
        Simple("memory", "Matching Pairs", "Kelime ve anlam kartlarını eşleştirin.", "Match vocabulary cards with their meanings.", "", GameMechanic.Memory),
        Simple("listeningchoice", "Listening Choice", "Kelimeyi dinleyip doğru anlamı seçin.", "Listen to a word and choose its meaning.", "", GameMechanic.ListeningChoice),
        Simple("oddoneout", "Odd One Out", "Dört kelimeden farklı olanı bulun.", "Find the unrelated word among four choices.", "", GameMechanic.OddOneOut),
        Simple("wordrace", "Word Race", "Anlamdan kelimeye zamana karşı yazın.", "Type the word from its meaning before time runs out.", "", GameMechanic.TimedTyping),

        Complex("clozetest", "Cloze Test", "Cümledeki boşluğu bağlama göre tamamlayın.", "Complete the sentence from context.", "", GameMechanic.MultipleChoice),
        Complex("sentencescramble", "Sentence Scramble", "Kelimeleri doğru cümle sırasına getirin.", "Rebuild a sentence in the correct order.", "", GameMechanic.WordSequence),
        Complex("readingcomprehension", "Reading Comprehension", "Metni okuyup anlama sorularını yanıtlayın.", "Read a passage and answer comprehension questions.", "", GameMechanic.Reading),
        Complex("wordmorph", "Word Morph", "Eş ve zıt anlam ilişkilerini keşfedin.", "Explore synonym and antonym relationships.", "", GameMechanic.SemanticLinks),
        Complex("bingo", "Word Bingo", "Tanımları 4×4 kelime kartında bulun.", "Match definitions on a 4×4 vocabulary board.", "", GameMechanic.Bingo),
        Complex("bossrush", "Boss Rush", "Soruları çözerek üç boss'u yenin.", "Defeat three bosses by answering vocabulary challenges.", "", GameMechanic.BossBattle),
        Complex("codycross", "CodyCross", "Beş kelime ipucunu çözerek bonus kodu çıkarın.", "Solve five vocabulary clues to reveal a bonus code.", "", GameMechanic.ClueWords),
        Complex("crossword", "Crossword", "İki kesişen kelimelik küçük çapraz bulmacayı tamamlayın.", "Complete a compact two-entry crossing puzzle.", "", GameMechanic.Crossword),
        Complex("dailychallenge", "Daily Challenge", "Günün kelimesini altı denemede bulun.", "Find today's word in six attempts.", "", GameMechanic.DailyGuess),
        Complex("scrabble", "Scrabble", "Harf rafından geçerli kelime üretin; tam tahta oyunu değildir.", "Build a valid word from a letter rack; not a full board game.", "", GameMechanic.Scrabble),
        Complex("wordguess", "Word Guess", "Renkli ipuçlarıyla hedef kelimeyi bulun.", "Find the target word using positional letter clues.", "", GameMechanic.DailyGuess),
        Complex("categorysprint", "Category Sprint", "60 saniyede bir kategoriden kelimeler üretin.", "Recall category words for 60 seconds.", "", GameMechanic.CategorySprint),
        Complex("cluedetective", "Clue Detective", "Açılan ipuçlarıyla gizli kelimeyi çözün.", "Deduce the hidden word from progressive clues.", "", GameMechanic.ProgressiveClues),
        Complex("matrix", "Word Matrix", "Harf ızgarasındaki gizli kelimeleri bulun.", "Find hidden vocabulary in a letter grid.", "", GameMechanic.LetterGrid),
    ];

    static GameCatalog()
    {
        if (All.Count != 25)
        {
            throw new InvalidOperationException($"YDKE must expose exactly 25 games; found {All.Count}.");
        }

        var duplicate = All.GroupBy(game => game.Id, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException($"Duplicate game id: {duplicate.Key}.");
        }

        if (All.Count(game => game.Group == GameGroup.Simple) != 11 ||
            All.Count(game => game.Group == GameGroup.Complex) != 14)
        {
            throw new InvalidOperationException("Game groups must contain 11 simple and 14 complex games.");
        }
    }

    public static IReadOnlyList<GameDefinition> Get(GameGroup group) =>
        All.Where(game => game.Group == group).ToArray();

    private static GameDefinition Simple(
        string id,
        string title,
        string descriptionTr,
        string descriptionEn,
        string glyph,
        GameMechanic mechanic) =>
        new(id, title, descriptionTr, descriptionEn, glyph, GameGroup.Simple, mechanic);

    private static GameDefinition Complex(
        string id,
        string title,
        string descriptionTr,
        string descriptionEn,
        string glyph,
        GameMechanic mechanic) =>
        new(id, title, descriptionTr, descriptionEn, glyph, GameGroup.Complex, mechanic);
}