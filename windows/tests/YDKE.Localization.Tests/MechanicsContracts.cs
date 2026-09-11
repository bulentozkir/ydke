using System.Text.RegularExpressions;

namespace YDKE_Windows;

internal static class MechanicsContracts
{
    public static void Verify(string sourceDirectory, IEnumerable<string> partials, Action<bool, string> check)
    {
        var source = SourceAudit.WithoutComments(string.Join("\n", partials));
        var engine = SourceAudit.WithoutComments(File.ReadAllText(Path.Combine(sourceDirectory, "GameEngine.cs")));

        // These read the actual game implementation but never compile or run its
        // UI, engine, storage or other test projects. A contract change requires
        // a deliberate translation review rather than silently stale help text.
        (string Name, string Pattern)[] sourceRules =
        [
            ("timer pauses on feedback/busy", @"if\s*\(!_gameTurn\.CanTick\(_gameEpoch,\s*!GameCanAnswer\(session\)\s*\|\|\s*_gameLocalBusy\)\)\s*return;"),
            ("feedback marks busy", @"_gameLocalBusy\s*=\s*true;\s*view\.Visibility\s*=\s*Visibility\.Collapsed"),
            ("feedback waits for Continue", @"var continued\s*=\s*await completion\.Task"),
            ("survival ends on first miss", @"if\s*\(game\.Id\s*==\s*""survival""\)\s*_activeGame\.Lives\s*=\s*1;"),
            ("six memory pairs", @"if\s*\(matches\s*==\s*6\)"),
            ("six word-guess attempts", @"if\s*\(attempts\s*>=\s*6\)"),
            ("three bosses", @"const int bossCount\s*=\s*3;"),
            ("five boss hit points", @"const int bossMaxHp\s*=\s*5;"),
            ("three boss mistakes", @"const int startHearts\s*=\s*3;"),
            ("five Cody clues", @"const int groupSize\s*=\s*5;[\s\S]*?pool\.Take\(groupSize\)"),
            ("three scramble attempts", @"if\s*\(wrong\s*>=\s*3\)"),
            ("six hangman misses", @"misses\s*>=\s*6"),
            ("six-wide matrix", @"const int width\s*=\s*6;"),
            ("4 by 4 bingo", @"Grid\.SetColumn\(button,\s*i\s*%\s*4\);\s*Grid\.SetRow\(button,\s*i\s*/\s*4\)"),
            ("bingo wrong choice ends board", @"var correct\s*=\s*index\s*==\s*target;[\s\S]*?if\s*\(!correct\)\s*\{\s*await ResolveGameAnswerAsync\(session,\s*false"),
            ("unique category words", @"_categoryFound\.Contains\(bare\)"),
            ("category answers limited to chosen pool", @"_categoryWords\.FirstOrDefault\(w\s*=>\s*GameEngine\.Bare\(w\)\s*==\s*bare\)"),
            ("crossword two answers", @"var entry\s*=\s*acrossSolved\s*\?\s*crossing\.Down\s*:\s*crossing\.Across;[\s\S]*?var correct\s*=\s*GameAnswer\(input\.Text\)\s*==\s*GameEngine\.Bare\(entry\);"),
            ("rack checks actual local words", @"GameEngine\.RackWord\(pool,\s*input\.Text,\s*rack"),
            ("clues cost points", @"Math\.Max\(20,\s*120\s*-\s*revealed\s*\*\s*20\)"),
            ("semantic and reading language restriction", "language is not \\(" + "\"en\" or \"de\" or \"fr\"" + "\\)"),
            ("daily language/level context", @"GameEngine\.DailyIndex\(pool\.Length,\s*StudyContext,\s*Today\)"),
            ("typing cloze rather than substitute quiz", @"GameAnswer\(input\.Text\)\s*==\s*GameEngine\.Bare\(picked\.Word\)"),
            ("reading feedback includes explanation", @"question\.Options\[question\.Correct\]\s*\+\s*""\\n""\s*\+\s*question\.Explanation"),
        ];
        foreach (var rule in sourceRules)
            check(Regex.IsMatch(source, rule.Pattern), "Mechanic changed; review translations: " + rule.Name);
        check(engine.Contains("!Closed && !busy", StringComparison.Ordinal), "Timer gate no longer excludes closed/busy turns.");
        check(engine.Contains("left[guess[i]]--", StringComparison.Ordinal), "Repeated-letter feedback no longer consumes letter counts.");
        check(engine.Contains("LearningEngine.CanBuildFromRack(bare, rack)", StringComparison.Ordinal), "Rack letter multiplicity validation changed.");
        check(engine.Contains("VocabularyEntry Across, VocabularyEntry Down", StringComparison.Ordinal), "Crossword is no longer a two-entry puzzle.");
        check(engine.Contains("Links(other, field).Contains(word", StringComparison.Ordinal) &&
            engine.Contains("!Links(other, opposite).Contains(word", StringComparison.Ordinal),
            "Reciprocal/non-conflicting semantic relationship validation changed.");

        // Semantic fragments independently reviewed per locale. The full English
        // instructions are additionally compared to real U-call fallback text by
        // Program. These fragments prevent a translated column keeping old rules
        // while its English column gets updated. They are not machine proof of
        // translation quality; intentionally concise wording still gets review.
        string[] languages = ["en", "tr", "de", "fr", "es", "pt", "nl"];
        foreach (var row in Contracts.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var cells = row.Split('|');
            check(cells.Length == 8, "Malformed mechanics translation contract: " + cells[0]);
            if (cells.Length != 8) continue;
            for (var i = 0; i < languages.Length; i++)
            {
                var text = Localizer.Get(languages[i], cells[0]);
                foreach (var fragment in cells[i + 1].Split('^'))
                    check(text.Contains(fragment, StringComparison.OrdinalIgnoreCase),
                        $"Stale {languages[i]}/{cells[0]}: missing reviewed mechanic '{fragment}'");
            }
        }

        foreach (var language in languages)
        {
            foreach (var key in new[] { "Games.Duration.Minute", "Games.Instructions.Speed", "Games.Instructions.Race" })
                check(Regex.Matches(Localizer.Get(language, key), @"\d+").Select(m => m.Value).SequenceEqual(new[] { "60" }),
                    $"{language}/{key}: must say 60 seconds, not an old duration");
            foreach (var key in new[] { "Games.Instructions.Guess", "Games.Instructions.Daily", "Games.Guess.Legend" })
                check(new[] { "✓", "~", "×" }.All(marker => Localizer.Get(language, key).Contains(marker, StringComparison.Ordinal)),
                    $"{language}/{key}: positional/absent letter markers changed");
            check(Regex.Matches(Localizer.Get(language, "Games.Duration.Short"), @"\d+").Select(m => m.Value).SequenceEqual(new[] { "2", "5" }),
                $"{language}: short rounds must remain a 2–5 minute estimate");
            check(Localizer.Get(language, "GameDescription.crossword").Contains('2'), $"{language}: crossword description must disclose two entries");
            check(Localizer.Get(language, "Games.Mode.practice") != Localizer.Get(language, "Games.Mode.timed"), $"{language}: practice and timed modes must differ");
        }
    }

    // Columns: key | en | tr | de | fr | es | pt | nl. ^ joins required fragments.
    private const string Contracts = """
        Games.Duration.Minute|active|etkin|aktive|actives|activos|ativos|actieve
        Games.Duration.Long|estimate^5+|tahmini^5+|geschätzt^5+|environ^5^plus|unos^5^más|cerca de^5^mais|schatting^5+
        Games.Duration.Short|estimate|tahmini|geschätzt|environ|unos|cerca de|schatting
        Games.Instructions.Speed|Choose^60^pauses|seçin^60^durur|Wählen^60^pausieren|Choisissez^60^suspend|Elige^60^pausan|Escolha^60^pausa|Kies^60^pauzeert
        Games.Instructions.Race|Type^60^pauses|yazın^60^durur|Tippen^60^pausieren|Saisissez^60^suspend|Escribe^60^pausan|Escreva^60^pausa|Typ^60^pauzeert
        Games.ScopedScores|language^level^mode|dil^seviye^mod|Sprache^Niveau^Modus|langue^niveau^mode|idioma^nivel^modo|idioma^nível^modo|taal^niveau^modus
        Games.LegacyBest|Legacy^unscoped|Eski^kapsamsız|Altwert^ohne Zuordnung|Ancien^sans périmètre|anterior^sin ámbito|antigo^sem âmbito|Oude^zonder afbakening
        Games.FeedbackPolicy|Continue^pauses^separate|Devam^duraklatır^ayrıdır|Weiter^pausiert^getrennt|Continuer^suspend^séparés|Continuar^pausan^separadas|Continuar^pausa^separadas|Doorgaan^pauzeert^apart
        Games.Mode.practice|Untimed^practice|Süresiz^alıştırma|Übung^ohne Zeitlimit|Entraînement^sans chronomètre|Práctica^sin cronómetro|Prática^sem cronómetro|Oefenen^zonder timer
        GameDescription.speedround|without a timer|süresiz|ohne Zeitlimit|sans chronomètre|sin cronómetro|sem cronómetro|zonder timer
        GameDescription.wordrace|without a timer|süresiz|ohne Zeitlimit|sans chronomètre|sin cronómetro|sem cronómetro|zonder timer
        GameDescription.categorysprint|without a timer|süresiz|ohne Zeitlimit|sans chronomètre|sin cronómetro|sem cronómetro|zonder timer
        GameDescription.scrabble|one^not a full board game|bir^tam tahta oyunu değildir|ein Wort^kein vollständiges Brettspiel|un mot^pas un jeu de plateau complet|una palabra^no es un juego de tablero completo|uma palavra^não é um jogo de tabuleiro completo|één woord^geen volledig bordspel
        Games.Instructions.Crossword|two-entry^Across^Down^shared|İki kelimelik^Yatay^dikey^ortak|zwei Wörtern^waagerecht^senkrecht^gemeinsame|Deux mots^horizontalement^verticalement^commune|dos palabras^horizontal^vertical^compartida|duas entradas^horizontal^vertical^partilhada|twee woorden^horizontaal^verticaal^gedeelde
        Games.Instructions.Rack|one^Repeated^not a full|bir^Tekrarlanan^tam Scrabble tahtası değil|ein echtes Wort^Doppelte^Kein vollständiges|un mot^répétée^pas un plateau complet|una palabra^repetidas^No es un tablero completo|uma palavra^repetidas^Não é um tabuleiro completo|één echt^Herhaalde^geen volledig
        Games.Instructions.Bingo|4×4^row^column^diagonal^Wrong|4×4^Satır^sütun^köşegen^Yanlış|4×4^Reihe^Spalte^Diagonale^falsche|4×4^ligne^colonne^diagonale^mauvais|4×4^fila^columna^diagonal^errónea|4×4^linha^coluna^diagonal^errada|4×4^rij^kolom^diagonaal^foute
        Games.Instructions.Semantic|reciprocal^non-conflicting^English, German and French only|karşılıklı^çelişmeyen^Yalnızca İngilizce, Almanca ve Fransızca|wechselseitiger^widerspruchsfreier^Nur für Englisch, Deutsch und Französisch|réciproques^cohérentes^Anglais, allemand et français uniquement|recíprocas^coherentes^Solo inglés, alemán y francés|recíprocas^coerentes^Apenas inglês, alemão e francês|wederkerige^consistente^Alleen Engels, Duits en Frans
        Games.Instructions.Reading|complete^explanation^English, German and French only|metni^açıklamasını^Yalnızca İngilizce, Almanca ve Fransızca|ganzen^Quellerklärung^Nur Englisch, Deutsch und Französisch|tout^explication^Anglais, allemand et français uniquement|todo^explicación^Solo inglés, alemán y francés|todo^explicação^Apenas inglês, alemão e francês|hele^uitleg^Alleen Engels, Duits en Frans
        Games.Instructions.Category|fixed^once^language and level|Sabit^bir kez^dil ve seviye|festen^einmal^Sprache^Niveaus|même catégorie^une fois^langue^niveau|fija^una vez^idioma y nivel|fixa^uma vez^idioma e nível|vaste^één keer^taal^niveau
        Games.Instructions.Clues|one at a time^Fewer^more points|sırayla^Daha az^çok puan|einzeln^Weniger^mehr Punkte|un à un^Moins^plus de points|uno en uno^Menos^más puntos|um a um^Menos^mais pontos|één voor één^Minder^meer punten
        Games.Instructions.Memory|two cards^Mismatches^until Continue|iki kart^Eşleşmeyenler^Devam|zwei Karten^Fehlpaare^bis Weiter|deux cartes^mauvaises paires^jusqu’à Continuer|dos tarjetas^incorrectos^hasta Continuar|dois cartões^incorretos^até Continuar|twee kaarten^Foute paren^tot Doorgaan
        Games.Instructions.Survival|first wrong^ends|İlk yanlış^bitirir|erste falsche^beendet|première erreur^termine|primer error^termina|primeiro erro^termina|eerste foute^beëindigt
        Games.Instructions.Guess|six^Repeated letters^individually|Altı^Tekrarlanan harfler^ayrı|sechs^Doppelte Buchstaben^einzeln|six^lettres répétées^séparément|seis^letras repetidas^por separado|seis^Letras repetidas^separadamente|zes^Herhaalde letters^afzonderlijk
        Games.Instructions.Daily|language/level^fixed^six^Repeated letters|dil/seviye^sabittir^Altı^Tekrarlanan harfler|Sprache und Niveau^festgelegt^Sechs^Doppelte Buchstaben|langue et le niveau^fixe^Six^lettres répétées|idioma y nivel^fija^Seis^letras repetidas|idioma e nível^fixa^Seis^Letras repetidas|taal en niveau^vast^Zes^Herhaalde letters
        Games.Instructions.Boss|five hit points^three bosses^Three mistakes|beş can^Üç rakibin^Üç hata|fünf Lebenspunkte^drei Gegner^Drei Fehler|cinq points de vie^trois adversaires^Trois erreurs|cinco puntos de vida^tres jefes^Tres errores|cinco pontos de vida^três adversários^Três erros|vijf levenspunten^drie eindbazen^Drie fouten
        Games.Instructions.Cody|five^Each correct^retried|beş^Her doğru^tekrar denenebilir|fünf^Jede richtige^wiederholt|cinq^Chaque bonne^retentées|cinco^Cada acierto^reintentar|cinco^Cada acerto^novamente|vijf^Elk goed^opnieuw
        Games.Instructions.Scramble|automatically^three attempts|otomatik^üç deneme|automatisch^drei Versuchen|automatiquement^trois essais|automáticamente^tres intentos|automaticamente^três tentativas|automatisch^drie pogingen
        Games.Instructions.Hangman|six misses^Accented|Altı hata^aksanlı|sechsten Fehler^Akzentbuchstaben|six erreurs^accentuées|seis fallos^acentuadas|seis erros^acentuadas|zes missers^accenten
        Games.Instructions.Cloze|Type^exact occurrence^inflected-only|yazın^tam eşleşmeler^yalnızca çekimli|Tippen^exakten Vorkommen^gebeugten|Saisissez^occurrence exacte^formes fléchies|Escribe^coincidencias exactas^formas flexionadas|Escreva^ocorrências exatas^formas flexionadas|Typ^exacte voorkomens^verbogen vormen
        Games.Instructions.Matrix|straight line^no square can be reused|düz bir çizgi^aynı kare tekrar kullanılamaz|geraden Linie^kein Feld darf erneut|ligne droite^aucune case ne peut être réutilisée|línea recta^no puedes repetir casillas|linha reta^não pode repetir casas|rechte lijn^elk vak mag maar één keer
        Games.Require.Voice|matching^never substitutes|uygun^başka dilde ses kullanılmaz|gewählte Sprache/Region^keine andere Sprache|correspondant^Aucune autre langue|idioma y región^No se usa otro idioma|idioma e região^Não se usa outro idioma|gekozen taal en regio^nooit een andere taal
        Games.SaveRetry|not saved^retries^discards|kaydedilmedi^tekrar dener^atar|nicht gespeichert^erneut^verworfen|non enregistrée^retente^abandonne|no guardada^reintenta^descarta|não guardada^repete^descarta|niet opgeslagen^opnieuw^wist
        """;
}