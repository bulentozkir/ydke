namespace YDKE_Windows;

internal static class Localizer
{
    private static IReadOnlyDictionary<string, string>? _englishDefaults;

    public static readonly IReadOnlyList<LanguageOption> UiLanguages =
    [
        new("tr", "Türkçe"),
        new("en", "English"),
        new("de", "Deutsch"),
        new("fr", "Français"),
        new("es", "Español"),
        new("pt", "Português"),
        new("nl", "Nederlands"),
    ];

    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Resources =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["tr"] = Text(
                ("Nav.Home", "Ana Sayfa"), ("Nav.Cards", "Kartlar"), ("Nav.Quiz", "Test"),
                ("Nav.Words", "Kelimeler"), ("Nav.Simple", "Basit Oyunlar"),
                ("Nav.Complex", "Karmaşık Oyunlar"), ("Nav.Stats", "İstatistikler"),
                ("Nav.Profile", "Profil"), ("Nav.Help", "Yardım"), ("Nav.About", "Hakkında"),
                ("Home.Title", "Bugün hangi dili güçlendireceksiniz?"),
                ("Home.Subtitle", "Kelimeler, kartlar ve 25 oyun cihazınızda. İnternet olmadan çalışmaya devam edin."),
                ("Home.Offline", "Çevrimdışı hazır"), ("Home.Continue", "Çalışmaya devam et"),
                ("Home.Known", "Öğrenilen"), ("Home.Favorites", "Favoriler"),
                ("Home.Games", "Oyun"), ("Home.Daily", "Bugünün önerisi"),
                ("Cards.Title", "Kelime Kartları"), ("Cards.Reveal", "Anlamı göster"),
                ("Cards.Listen", "Dinle"), ("Cards.Example", "Örnek"),
                ("Cards.Next", "Sonraki kart"), ("Cards.Known", "Öğrendim"),
                ("Cards.Favorite", "Favori"), ("Quiz.Title", "Hızlı Test"),
                ("Quiz.Question", "Doğru kelimeyi seçin"), ("Quiz.Next", "Sonraki soru"),
                ("Words.Title", "Kelime Kitaplığı"), ("Words.Search", "Kelime, anlam veya kategori ara"),
                ("Words.Empty", "Bu filtreyle eşleşen kelime bulunamadı."),
                ("Games.SimpleTitle", "Basit Oyunlar"),
                ("Games.SimpleSubtitle", "Hızlı öğrenme molaları için 11 sade ve akıcı oyun."),
                ("Games.ComplexTitle", "Karmaşık Oyunlar"),
                ("Games.ComplexSubtitle", "Strateji, bağlam ve daha derin düşünme isteyen 14 oyun."),
                ("Games.Start", "Oyunu başlat"), ("Games.Back", "Oyunlara dön"),
                ("Game.Score", "Puan"), ("Game.Streak", "Seri"), ("Game.Round", "Tur"),
                ("Game.Lives", "Can"), ("Game.Time", "Süre"), ("Game.Best", "En iyi"),
                ("Game.Finished", "Oyun tamamlandı"), ("Game.PlayAgain", "Tekrar oyna"),
                ("Game.TypeAnswer", "Cevabınızı yazın"), ("Game.Submit", "Cevapla"),
                ("Stats.Title", "İstatistikler"), ("Stats.Correct", "Doğru"),
                ("Stats.Wrong", "Yanlış"), ("Stats.Success", "Başarı"),
                ("Profile.Title", "Profil ve Ayarlar"),
                ("Profile.Subtitle", "Dil tercihleri ve ilerleme yalnızca bu cihazda saklanır."),
                ("Profile.UiLanguage", "Uygulama dili"),
                ("Profile.UiLanguageHint", "Menüler ve araç çubukları anında seçtiğiniz dile geçer."),
                ("Profile.StudyLanguage", "Öğrenme dili"),
                ("Profile.StudyLanguageHint", "Kartlarda, testlerde ve oyunlarda kullanılacak kelimeler."),
                ("Profile.Level", "Seviye"), ("Profile.Local", "Yerel profil"),
                ("Profile.QuickSettings", "Hızlı ayarlar"),
                ("Profile.QuickSettingsHint", "Dili ve seviyeyi üstteki çubuktan her ekranda değiştirebilirsiniz."),
                ("Profile.Appearance", "Görünüm"),
                ("Profile.AppearanceHint", "Renkleri ve yazı boyutunu gözünüz için rahat olacak şekilde ayarlayın."),
                ("Profile.FontSize", "Yazı boyutu"),
                ("Profile.FontSizeHint", "Tüm başlık, metin ve düğme yazılarını birlikte ölçekler."),
                ("Profile.AppColor", "Uygulama arka planı"),
                ("Profile.ButtonColor", "Düğme rengi"), ("Profile.BoxColor", "Kutu rengi"),
                ("Profile.ColorHint", "Metin ve simge rengi okunabilir kontrast için otomatik seçilir."),
                ("Profile.ApplyAppearance", "Görünümü uygula"),
                ("Profile.ResetAppearance", "Varsayılanlara dön"),
                ("Profile.LocalHint", "İnternet gerekmez. İlerleme bilgisayarınızda özel olarak saklanır."),
                ("Profile.Cloud", "İsteğe bağlı Google profili"),
                ("Profile.CloudHint", "Yalnızca isterseniz oturum açın ve ilerlemenizi bulutta saklayın."),
                ("Profile.SignIn", "Google ile bağlan"), ("Profile.Optional", "İsteğe bağlı"),
                ("Help.Title", "Yardım"),
                ("Help.Body", "Kartlardan bir çalışma dili ve seviye seçin. Kelimeyi düşünün, anlamı açın ve öğrendiğiniz kelimeleri işaretleyin. Basit ve Karmaşık Oyunlar araç çubuğunda ayrı tutulur. Tüm çalışma içeriği çevrimdışıdır."),
                ("About.Title", "YDKE Hakkında"),
                ("About.Body", "YDKE - Yabancı Dil Kelime Ezberleme; Windows 11 için C# ve WinUI ile geliştirilmiş yerel, çok dilli bir kelime çalışma uygulamasıdır. İngilizce, Almanca, Fransızca, İtalyanca, İspanyolca, Portekizce ve Hollandaca verileri cihazda çalışır."),
                ("Common.Language", "Dil"), ("Common.Level", "Seviye"),
                ("Common.Loading", "Yerel kelimeler hazırlanıyor…"), ("Common.Error", "Bir sorun oluştu"),
                ("Common.Correct", "Doğru"), ("Common.Wrong", "Yanlış")),
            ["en"] = CaptureEnglish(Text(
                ("Nav.Home", "Home"), ("Nav.Cards", "Cards"), ("Nav.Quiz", "Quiz"),
                ("Nav.Words", "Words"), ("Nav.Simple", "Simple Games"),
                ("Nav.Complex", "Complex Games"), ("Nav.Stats", "Statistics"),
                ("Nav.Profile", "Profile"), ("Nav.Help", "Help"), ("Nav.About", "About"),
                ("Home.Title", "Which language will you strengthen today?"),
                ("Home.Subtitle", "Words, cards, and 25 games are on your device. Keep learning without internet."),
                ("Home.Offline", "Ready offline"), ("Home.Continue", "Continue studying"),
                ("Home.Known", "Learned"), ("Home.Favorites", "Favorites"), ("Home.Games", "Games"),
                ("Home.Daily", "Today's suggestion"), ("Cards.Title", "Vocabulary Cards"),
                ("Cards.Reveal", "Reveal meaning"), ("Cards.Listen", "Listen"),
                ("Cards.Example", "Example"), ("Cards.Next", "Next card"),
                ("Cards.Known", "I know this"), ("Cards.Favorite", "Favorite"),
                ("Quiz.Title", "Quick Quiz"), ("Quiz.Question", "Choose the correct word"),
                ("Quiz.Next", "Next question"), ("Words.Title", "Word Library"),
                ("Words.Search", "Search word, meaning, or category"),
                ("Words.Empty", "No words match this filter."), ("Games.SimpleTitle", "Simple Games"),
                ("Games.SimpleSubtitle", "11 fluid games for quick learning breaks."),
                ("Games.ComplexTitle", "Complex Games"),
                ("Games.ComplexSubtitle", "14 games built around strategy, context, and deeper thinking."),
                ("Games.Start", "Start game"), ("Games.Back", "Back to games"),
                ("Game.Score", "Score"), ("Game.Streak", "Streak"), ("Game.Round", "Round"),
                ("Game.Lives", "Lives"), ("Game.Time", "Time"), ("Game.Best", "Best"),
                ("Game.Finished", "Game complete"), ("Game.PlayAgain", "Play again"),
                ("Game.TypeAnswer", "Type your answer"), ("Game.Submit", "Submit"),
                ("Stats.Title", "Statistics"), ("Stats.Correct", "Correct"),
                ("Stats.Wrong", "Wrong"), ("Stats.Success", "Success"),
                ("Profile.Title", "Profile & Settings"),
                ("Profile.Subtitle", "Language preferences and progress stay on this device."),
                ("Profile.UiLanguage", "App language"),
                ("Profile.UiLanguageHint", "Menus and toolbars switch immediately."),
                ("Profile.StudyLanguage", "Learning language"),
                ("Profile.StudyLanguageHint", "Vocabulary used by cards, quizzes, and games."),
                ("Profile.Level", "Level"), ("Profile.Local", "Local profile"),
                ("Profile.QuickSettings", "Quick settings"),
                ("Profile.QuickSettingsHint", "Change the language and level from the bar at the top on any screen."),
                ("Profile.Appearance", "Appearance"),
                ("Profile.AppearanceHint", "Adjust colors and text size for comfortable reading."),
                ("Profile.FontSize", "Text size"),
                ("Profile.FontSizeHint", "Scales headings, body text, and button labels together."),
                ("Profile.AppColor", "App background"),
                ("Profile.ButtonColor", "Button color"), ("Profile.BoxColor", "Box color"),
                ("Profile.ColorHint", "Text and icon colors are selected automatically for readable contrast."),
                ("Profile.ApplyAppearance", "Apply appearance"),
                ("Profile.ResetAppearance", "Restore defaults"),
                ("Profile.LocalHint", "No internet required. Progress is stored privately on your PC."),
                ("Profile.Cloud", "Optional Google profile"),
                ("Profile.CloudHint", "Sign in only if you want to store progress in the cloud."),
                ("Profile.SignIn", "Connect with Google"), ("Profile.Optional", "Optional"),
                ("Help.Title", "Help"),
                ("Help.Body", "Choose a learning language and level in Profile. Think of the word, reveal its meaning, and mark what you learn. Simple and Complex Games have separate toolbar destinations. All study content works offline."),
                ("About.Title", "About YDKE"),
                ("About.Body", "YDKE - Foreign Language Vocabulary Learning is a local multilingual study app built for Windows 11 with C# and WinUI. English, German, French, Italian, Spanish, Portuguese, and Dutch data run on your device."),
                ("Common.Language", "Language"), ("Common.Level", "Level"),
                ("Common.Loading", "Preparing local vocabulary…"), ("Common.Error", "Something went wrong"),
                ("Common.Correct", "Correct"), ("Common.Wrong", "Wrong"))),
            ["de"] = Overlay("en",
                ("Nav.Home", "Start"), ("Nav.Cards", "Karten"), ("Nav.Quiz", "Quiz"),
                ("Nav.Words", "Wörter"), ("Nav.Simple", "Einfache Spiele"),
                ("Nav.Complex", "Komplexe Spiele"), ("Nav.Stats", "Statistik"),
                ("Nav.Profile", "Profil"), ("Nav.Help", "Hilfe"), ("Nav.About", "Über"),
                ("Cards.Listen", "Anhören"), ("Cards.Example", "Beispiel"),
                ("Profile.Title", "Profil und Einstellungen"), ("Profile.UiLanguage", "App-Sprache"),
                ("Profile.StudyLanguage", "Lernsprache"), ("Profile.Level", "Niveau"),
                ("Help.Title", "Hilfe"), ("About.Title", "Über YDKE"),
                ("Games.SimpleTitle", "Einfache Spiele"), ("Games.ComplexTitle", "Komplexe Spiele")),
            ["fr"] = Overlay("en",
                ("Nav.Home", "Accueil"), ("Nav.Cards", "Cartes"), ("Nav.Quiz", "Quiz"),
                ("Nav.Words", "Mots"), ("Nav.Simple", "Jeux simples"),
                ("Nav.Complex", "Jeux complexes"), ("Nav.Stats", "Statistiques"),
                ("Nav.Profile", "Profil"), ("Nav.Help", "Aide"), ("Nav.About", "À propos"),
                ("Cards.Listen", "Écouter"), ("Cards.Example", "Exemple"),
                ("Profile.Title", "Profil et réglages"), ("Profile.UiLanguage", "Langue de l’application"),
                ("Profile.StudyLanguage", "Langue d’apprentissage"), ("Profile.Level", "Niveau"),
                ("Help.Title", "Aide"), ("About.Title", "À propos de YDKE"),
                ("Games.SimpleTitle", "Jeux simples"), ("Games.ComplexTitle", "Jeux complexes")),
            ["es"] = Overlay("en",
                ("Nav.Home", "Inicio"), ("Nav.Cards", "Tarjetas"), ("Nav.Quiz", "Prueba"),
                ("Nav.Words", "Palabras"), ("Nav.Simple", "Juegos simples"),
                ("Nav.Complex", "Juegos complejos"), ("Nav.Stats", "Estadísticas"),
                ("Nav.Profile", "Perfil"), ("Nav.Help", "Ayuda"), ("Nav.About", "Acerca de"),
                ("Cards.Listen", "Escuchar"), ("Cards.Example", "Ejemplo"),
                ("Profile.Title", "Perfil y ajustes"), ("Profile.UiLanguage", "Idioma de la aplicación"),
                ("Profile.StudyLanguage", "Idioma de aprendizaje"), ("Profile.Level", "Nivel"),
                ("Help.Title", "Ayuda"), ("About.Title", "Acerca de YDKE"),
                ("Games.SimpleTitle", "Juegos simples"), ("Games.ComplexTitle", "Juegos complejos")),
            ["pt"] = Overlay("en",
                ("Nav.Home", "Início"), ("Nav.Cards", "Cartões"), ("Nav.Quiz", "Teste"),
                ("Nav.Words", "Palavras"), ("Nav.Simple", "Jogos simples"),
                ("Nav.Complex", "Jogos complexos"), ("Nav.Stats", "Estatísticas"),
                ("Nav.Profile", "Perfil"), ("Nav.Help", "Ajuda"), ("Nav.About", "Sobre"),
                ("Cards.Listen", "Ouvir"), ("Cards.Example", "Exemplo"),
                ("Profile.Title", "Perfil e definições"), ("Profile.UiLanguage", "Idioma da aplicação"),
                ("Profile.StudyLanguage", "Idioma de aprendizagem"), ("Profile.Level", "Nível"),
                ("Help.Title", "Ajuda"), ("About.Title", "Sobre o YDKE"),
                ("Games.SimpleTitle", "Jogos simples"), ("Games.ComplexTitle", "Jogos complexos")),
            ["nl"] = Overlay("en",
                ("Nav.Home", "Start"), ("Nav.Cards", "Kaarten"), ("Nav.Quiz", "Toets"),
                ("Nav.Words", "Woorden"), ("Nav.Simple", "Eenvoudige spellen"),
                ("Nav.Complex", "Complexe spellen"), ("Nav.Stats", "Statistieken"),
                ("Nav.Profile", "Profiel"), ("Nav.Help", "Help"), ("Nav.About", "Over"),
                ("Cards.Listen", "Luisteren"), ("Cards.Example", "Voorbeeld"),
                ("Profile.Title", "Profiel en instellingen"), ("Profile.UiLanguage", "App-taal"),
                ("Profile.StudyLanguage", "Leertaal"), ("Profile.Level", "Niveau"),
                ("Help.Title", "Help"), ("About.Title", "Over YDKE"),
                ("Games.SimpleTitle", "Eenvoudige spellen"), ("Games.ComplexTitle", "Complexe spellen")),
        };

    public static string Get(string languageCode, string key) =>
        Resources.GetValueOrDefault(languageCode, Resources["tr"]).GetValueOrDefault(
            key,
            Resources["en"].GetValueOrDefault(key, key));

    private static IReadOnlyDictionary<string, string> Text(params (string Key, string Value)[] values) =>
        values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, string> CaptureEnglish(IReadOnlyDictionary<string, string> values)
    {
        _englishDefaults = values;
        return values;
    }

    private static IReadOnlyDictionary<string, string> Overlay(
        string baseLanguage,
        params (string Key, string Value)[] values)
    {
        var source = baseLanguage == "en"
            ? _englishDefaults ?? throw new InvalidOperationException("English resources must be initialized first.")
            : Resources[baseLanguage];
        var result = new Dictionary<string, string>(source, StringComparer.Ordinal);
        foreach (var (key, value) in values)
        {
            result[key] = value;
        }
        return result;
    }
}