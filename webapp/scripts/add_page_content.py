# -*- coding: utf-8 -*-
"""Add a static, crawlable <section class="seo-content"> to the app pages that
otherwise ship as an empty JS shell.

Rationale: a page that ships as an empty JS shell has nothing for Googlebot's
initial HTML pass to index. Every indexable page needs prose a reader (and a
crawler) can take in without executing the app. Each block is written
once, per page, and is genuinely useful to a learner -- not filler.

Idempotent: skips any file that already contains a .seo-content section.
"""

import os

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)

ANCHOR = "    </main>\n"


def block(label, heading, paras, tips, en, links):
    out = ['      <section class="seo-content" aria-label="%s">' % label]
    out.append('        <div class="seo-inner">')
    out.append("          <h2>%s</h2>" % heading)
    for p in paras:
        out.append("          <p>%s</p>" % p)
    if tips:
        out.append('          <ul class="seo-features">')
        for t in tips:
            out.append("            <li>%s</li>" % t)
        out.append("          </ul>")
    out.append('          <p class="seo-en" lang="en">%s</p>' % en)
    out.append('          <h3>İlgili sayfalar · Related pages</h3>')
    out.append('          <nav class="cert-links" aria-label="İlgili sayfalar">')
    for href, name, org in links:
        out.append('            <a class="cert-link" href="%s">' % href)
        out.append('              <span class="cert-name">%s</span>' % name)
        out.append('              <span class="cert-org">%s</span>' % org)
        out.append("            </a>")
    out.append("          </nav>")
    out.append("        </div>")
    out.append("      </section>\n")
    return "\n".join(out)


L = {
    "cards": ("index.html", "🃏 Kelime Kartları", "Flashcards · A1–C2"),
    "quiz": ("quiz.html", "📝 Quiz", "20 soruluk sınav · 20-question exam"),
    "games": ("games.html", "🎮 Oyunlar", "12 kelime oyunu · 12 games"),
    "words": ("wordlist.html", "📋 Kelime Listesi", "Tüm kelimeler · Full word list"),
    "morph": ("wordmorph.html", "🔄 Word Morph", "Eş &amp; zıt anlam · Synonyms &amp; antonyms"),
    "cloze": ("clozetest.html", "📝 Cloze Test", "Boşluk doldurma · Gap fill"),
    "read": ("readingcomprehension.html", "📖 Reading", "Okuduğunu anlama · Comprehension"),
    "listen": ("listening.html", "🎧 Dinleme", "Dinleme kaynakları · Listening"),
    "dict": ("dictation.html", "🎧 Dictation", "Dinle ve yaz · Listen and type"),
    "stats": ("stats.html", "📊 İstatistikler", "İlerlemeniz · Your progress"),
    "help": ("help.html", "❓ Yardım", "Kullanım kılavuzu · How to use"),
    "about": ("about.html", "ℹ️ Hakkında", "SSS · FAQ"),
}


def links(*keys):
    return [(L[k][0], L[k][1], L[k][2]) for k in keys]


PAGES = {}

PAGES["index.html"] = block(
    "Kelime kartları hakkında",
    "Kelime Kartları ile Yabancı Dil Kelime Ezberleme",
    [
        "<strong>Top Words</strong> kelime kartları (flashcards), İngilizce, "
        "Almanca, Fransızca, İtalyanca, İspanyolca ve Portekizce için "
        "<strong>A1'den C2'ye</strong> kadar en sık kullanılan kelimeleri "
        "ezberlemenizi sağlar. Her kartın ön yüzünde kelime, arka yüzünde ise "
        "<strong>Türkçe anlamı, sözcük türü (isim, fiil, sıfat…) ve gerçek bir "
        "örnek cümle</strong> bulunur. Örnek cümleler hem hedef dilde hem "
        "Türkçe verilir; böylece kelimeyi tek başına değil, "
        "<strong>bağlam içinde</strong> öğrenirsiniz.",
        "Üstteki menüden dili ve seviyeyi seçin, kartı çevirmek için üzerine "
        "tıklayın veya <strong>boşluk tuşuna</strong> basın; sonraki karta "
        "geçmek için <strong>sağ ok</strong> tuşunu kullanın. İngilizce için "
        "ayrıca <strong>Phrasal Verbs</strong> (öbek fiiller) ve "
        "<strong>TOEFL</strong> listeleri, Almanca için "
        "<strong>Partikelverben</strong> (ayrılabilen fiiller) listesi vardır. "
        "<strong>Mix</strong> seçeneği tüm seviyeleri birleştirir; "
        "<strong>Kategori</strong> seçicisi ise kelimeleri Spor, Yemek, Bilim, "
        "Teknoloji, Sağlık gibi konu alanlarına göre süzer.",
        "İlerlemeniz <strong>tarayıcınızda otomatik olarak</strong> saklanır — "
        "kayıt olmanıza gerek yoktur. Kaldığınız yerden devam edebilir, "
        "öğrendiğiniz kelimeleri işaretleyebilir ve zorlandıklarınızı tekrar "
        "çalışabilirsiniz.",
    ],
    [
        "🎯 <strong>Günde 20 kelime</strong> hedefleyin. Az ama her gün, çok ama "
        "seyrek çalışmaktan daha kalıcıdır.",
        "🗣️ <strong>🔊 Listen</strong> düğmesiyle telaffuzu dinleyin ve kelimeyi "
        "yüksek sesle tekrar edin.",
        "📝 Kartları bitirdikten sonra aynı seviyede <strong>Quiz</strong> çözün; "
        "hatırlama denemesi (retrieval practice) ezberi kalıcı hâle getirir.",
        "🔁 Yanlış bildiğiniz kelimeleri ertesi gün <strong>tekrar</strong> edin — "
        "aralıklı tekrar en etkili ezberleme yöntemidir.",
    ],
    "Study the most frequent A1–C2 words in English, German, French, Italian, "
    "Spanish and Portuguese with two-sided flashcards. Each card shows the word, "
    "its Turkish meaning, its part of speech and a real bilingual example "
    "sentence. Click the card or press Space to flip, and the right arrow key to "
    "advance. English also offers Phrasal Verbs and TOEFL lists; German offers "
    "separable verbs (Partikelverben). Progress is saved in your browser — no "
    "sign-up required.",
    links("quiz", "words", "games", "morph", "help"),
)

PAGES["quiz.html"] = block(
    "Quiz hakkında",
    "20 Soruluk Kelime Testi · YDS, YÖKDİL, TOEFL ve telc Hazırlığı",
    [
        "<strong>Quiz</strong> modu, seçtiğiniz dil ve seviyedeki kelimeleri "
        "<strong>20 soruluk sınavlar</strong> hâlinde ölçer. Her soruda bir "
        "kelime ve dört seçenek görürsünüz; doğru anlamı seçtiğinizde anında "
        "geri bildirim ve <strong>örnek cümle</strong> alırsınız. Sınav bittiğinde "
        "puanınızı, en iyi skorunuzu ve <strong>yanlış yaptığınız soruların "
        "tümünü</strong> tek tek inceleyebileceğiniz bir özet çıkar.",
        "<strong>Reverse mode</strong> (ters mod) düğmesini açarsanız soru yönü "
        "değişir: anlamı görüp <strong>kelimeyi</strong> seçersiniz. Bu, sınavda "
        "gerçekten ihtiyaç duyduğunuz <strong>aktif hatırlama</strong> becerisini "
        "geliştirir; kelimeyi tanımak ile hatırlamak farklı şeylerdir.",
        "Kelime havuzu seviyeye göre bölünür, böylece <strong>A1</strong> "
        "sınavında C2 kelimesiyle karşılaşmazsınız. İngilizce için "
        "<strong>YDS, YÖKDİL, ÜDS, UDSP ve TOEFL</strong>, Almanca için "
        "<strong>telc</strong> ve CEFR sınavlarına hazırlananlar bu modu "
        "doğrudan deneme sınavı gibi kullanabilir.",
    ],
    [
        "⏱️ Bir oturumda <strong>1–2 sınav</strong> çözün; yorulunca doğruluk "
        "oranınız düşer ve yanlışlarınız pekişir.",
        "🔄 Aynı sınavı <strong>Retry</strong> ile tekrar çözerek skorunuzu "
        "yükseltmeye çalışın.",
        "🔍 <strong>Review answers</strong> ekranını atlamayın — asıl öğrenme "
        "yanlışları incelerken gerçekleşir.",
        "🃏 Zorlandığınız seviyede önce <strong>Kelime Kartları</strong> ile "
        "çalışın, sonra sınava dönün.",
    ],
    "Quiz mode tests the level you selected in 20-question exams with four "
    "options per question, instant feedback and an example sentence. Turn on "
    "Reverse mode to see the meaning and pick the word — active recall, the skill "
    "real exams measure. Ideal for YDS, YÖKDİL, ÜDS, UDSP, TOEFL, telc and CEFR "
    "vocabulary preparation.",
    links("cards", "words", "games", "stats", "help"),
)

PAGES["wordlist.html"] = block(
    "Kelime listesi hakkında",
    "Seviyeye Göre Tam Kelime Listesi · A1–C2",
    [
        "<strong>Kelime Listesi</strong>, seçtiğiniz dil ve seviyedeki "
        "<strong>bütün kelimeleri</strong> tek bir sayfada, anlamları ve örnek "
        "cümleleriyle birlikte gösterir. Kart kart ilerlemek yerine listenin "
        "tamamını taramak istediğinizde, sınav öncesi hızlı bir "
        "<strong>son tekrar</strong> yapmak istediğinizde veya belirli bir "
        "kelimeyi aradığınızda en pratik moddur.",
        "Listeyi <strong>arama kutusuyla</strong> filtreleyebilir, "
        "<strong>kategoriye göre</strong> (Spor, Yemek, Bilim, Teknoloji, Sağlık, "
        "İş Dünyası…) daraltabilir ve öğrendiğiniz kelimeleri işaretleyerek "
        "hangi kelimelerin kaldığını görebilirsiniz. Her satırda kelimenin "
        "<strong>sözcük türü</strong> ve <strong>CEFR seviyesi</strong> de yer alır.",
        "Listeler; İngilizce (A1–C2, Phrasal Verbs, TOEFL), Almanca (A1–C2, "
        "Partikelverben), Fransızca, İtalyanca, İspanyolca ve Portekizce (A1–C2) "
        "için ayrı ayrı hazırlanmıştır ve toplamda "
        "<strong>yüz binden fazla</strong> kelime–anlam–örnek üçlüsü içerir.",
    ],
    [
        "🖨️ Listeyi tarayıcınızın <strong>yazdır</strong> özelliğiyle kâğıda "
        "dökerek çevrimdışı çalışabilirsiniz.",
        "🔎 Aradığınız kelimeyi bulmak için önce <strong>arama kutusunu</strong> "
        "kullanın, sonra kategoriyle daraltın.",
        "✅ Bildiğiniz kelimeleri işaretleyin; kalanlara odaklanmak çalışma "
        "sürenizi belirgin şekilde kısaltır.",
    ],
    "The word list shows every word of the language and level you picked on a "
    "single page, with meanings and example sentences. Filter it with the search "
    "box or by topic category, and tick off the words you already know. Lists "
    "cover English (A1–C2, phrasal verbs, TOEFL), German (A1–C2, separable "
    "verbs), French, Italian, Spanish and Portuguese.",
    links("cards", "quiz", "morph", "stats", "about"),
)

PAGES["wordmorph.html"] = block(
    "Word Morph hakkında",
    "Word Morph · Eş Anlamlı ve Zıt Anlamlı Kelimeler",
    [
        "<strong>Word Morph</strong>, bir kelimenin <strong>eş anlamlılarını "
        "(synonyms)</strong> ve <strong>zıt anlamlılarını (antonyms)</strong> "
        "çalıştıran moddur. Sınavlarda en çok puan kaybettiren soru tiplerinden "
        "biri, “bu kelimenin yerine hangisi gelebilir?” sorusudur; Word Morph tam "
        "olarak bu beceriyi hedefler.",
        "Ekranda bir kelime ve anlamı görünür; sizden onun eş ya da zıt anlamlısını "
        "seçmeniz istenir. Doğru yanıtla birlikte kelimenin "
        "<strong>tüm eş ve zıt anlamlıları</strong> listelenir, böylece tek soruda "
        "birden fazla kelime öğrenirsiniz. Mod; <strong>İngilizce, Almanca ve "
        "Fransızca</strong> için hazırlanmıştır.",
        "Eş anlamlıları öğrenmek yalnızca test için değil, <strong>yazma ve "
        "konuşma</strong> için de önemlidir: aynı kelimeyi tekrarlamak yerine "
        "alternatifini kullanabilmek, dil seviyenizi B2'den C1'e taşıyan en "
        "belirgin farklardan biridir.",
    ],
    [
        "🧠 Eş anlamlıları <strong>tek tek değil öbek hâlinde</strong> ezberleyin; "
        "beyin ilişkili kelimeleri birlikte daha iyi saklar.",
        "⚖️ Zıt anlamlılar çoğu zaman daha kolay akılda kalır — bir kelimeyi "
        "unuttuğunuzda zıddından yola çıkın.",
        "✍️ Öğrendiğiniz eş anlamlıyı hemen bir cümlede kullanın; kullanılmayan "
        "kelime birkaç günde silinir.",
    ],
    "Word Morph drills synonyms and antonyms — the skill behind the “which word "
    "could replace this one?” question that costs most marks in vocabulary exams. "
    "Every correct answer also reveals the word's full synonym and antonym set, so "
    "you learn several words per question. Available for English, German and "
    "French.",
    links("cards", "quiz", "words", "games", "help"),
)

PAGES["clozetest.html"] = block(
    "Cloze test hakkında",
    "Cloze Test · Boşluk Doldurma Alıştırması",
    [
        "<strong>Cloze Test</strong> (boşluk doldurma), gerçek bir örnek cümleden "
        "hedef kelimenin çıkarılmasıyla oluşturulur; sizden boşluğa gelecek doğru "
        "kelimeyi seçmeniz istenir. Bu alıştırma, kelimeyi "
        "<strong>bağlam içinde</strong> tanıma becerinizi ölçer — YDS, YÖKDİL ve "
        "TOEFL gibi sınavlarda karşınıza çıkan soru tipinin birebir aynısıdır.",
        "Şıkların hepsi aynı seviye ve mümkün olduğunca aynı sözcük türünden "
        "seçilir; yani doğru yanıtı yalnızca <strong>anlam ve eşdizimlilik "
        "(collocation)</strong> ile bulabilirsiniz, biçimden tahmin edemezsiniz. "
        "Yanıtladığınızda cümlenin Türkçe çevirisi de gösterilir.",
        "Boşluk doldurma, kelime kartlarından bir adım ileri gitmenizi sağlar: "
        "kelimeyi tanımak yetmez, <strong>hangi cümlede kullanılacağını</strong> "
        "bilmeniz gerekir. Zorlanıyorsanız aynı seviyede önce kartlara dönün.",
    ],
    [
        "🔍 Cümlenin tamamını okuyun; ipucu genellikle boşluktan <strong>sonra</strong> "
        "gelir.",
        "🧩 Bilmediğiniz kelimeleri elemeyin, <strong>bildiklerinizi eleyin</strong> — "
        "eleme yöntemi doğruluk oranını yükseltir.",
        "📝 Yanlışlarınızı not alıp aynı kelimeyi <strong>Quiz</strong> modunda "
        "tekrar test edin.",
    ],
    "Cloze Test removes the target word from a real example sentence and asks you "
    "to pick the word that fills the gap. Distractors come from the same level and "
    "part of speech, so only meaning and collocation get you to the answer — "
    "exactly the question type used in YDS, YÖKDİL and TOEFL.",
    links("cards", "quiz", "read", "games", "help"),
)

PAGES["hangman.html"] = block(
    "Hangman hakkında",
    "Hangman · Adam Asmaca ile Kelime Çalışma",
    [
        "<strong>Hangman</strong> (adam asmaca), kelimenin Türkçe anlamını "
        "gördükten sonra kelimeyi <strong>harf harf</strong> tahmin ettiğiniz "
        "klasik oyundur. Yanlış her harf bir can götürür. Oyun, kelimenin "
        "<strong>yazımını</strong> (spelling) pekiştirmesi bakımından diğer "
        "modlardan ayrılır — çoktan seçmeli testlerin ölçemediği tek beceri budur.",
        "Almanca <strong>ä, ö, ü, ß</strong>; Fransızca <strong>é, è, ê, ç</strong>; "
        "İspanyolca <strong>ñ</strong> gibi özel harfler de klavyede yer alır, "
        "böylece kelimeyi gerçekte yazıldığı gibi öğrenirsiniz.",
        "Kelimeler seçtiğiniz dil ve seviyeden gelir; A1'de kısa ve sık kelimeler, "
        "C2'de uzun ve nadir kelimeler görürsünüz. Oyun bittiğinde kelimenin "
        "anlamı ve örnek cümlesi gösterilir.",
    ],
    [
        "🔤 Önce <strong>sesli harfleri</strong> deneyin (a, e, i, o, u) — hemen "
        "hemen her kelimede en az biri vardır.",
        "📏 Kelimenin <strong>uzunluğu</strong> güçlü bir ipucudur; anlamı okuyup "
        "uzunluğa uyan bir karşılık düşünün.",
        "🧩 Almanca uzun bileşik kelimelerde <strong>-ung, -heit, -keit</strong> "
        "gibi ekleri tahmin etmek çok işe yarar.",
    ],
    "Hangman shows you the Turkish meaning and asks you to guess the word letter "
    "by letter. Unlike multiple-choice modes it drills spelling — including "
    "German ä/ö/ü/ß, French é/è/ê/ç and Spanish ñ, which are all available on the "
    "on-screen keyboard.",
    links("games", "cards", "quiz", "words", "help"),
)

PAGES["scramble.html"] = block(
    "Word scramble hakkında",
    "Word Scramble · Karışık Harfleri Sıraya Dizin",
    [
        "<strong>Word Scramble</strong>, harfleri karıştırılmış bir kelimeyi doğru "
        "sıraya dizmenizi ister. Anlamı ipucu olarak verilir. Hangman gibi bu oyun "
        "da <strong>yazım</strong> odaklıdır, ama farklı bir bilişsel süreci "
        "çalıştırır: harfleri tek tek tahmin etmek yerine, kelimenin "
        "<strong>bütün görüntüsünü</strong> zihninizde canlandırmanız gerekir.",
        "Bu, özellikle <strong>okurken tanıdığınız ama yazamadığınız</strong> "
        "kelimeler için etkilidir — pasif kelime dağarcığınızı aktif hâle getirir. "
        "İngilizce'de <strong>-ough, -tion, -ious</strong>, Almanca'da "
        "<strong>sch-, -ung</strong>, Fransızca'da <strong>-eau, -ment</strong> "
        "gibi yaygın harf gruplarını tanımayı da öğretir.",
    ],
    [
        "🔡 Önce tanıdık <strong>harf gruplarını</strong> bir araya getirin "
        "(th, ch, sch, ph, tion…), kalan harfleri sonra yerleştirin.",
        "💡 Takıldığınızda <strong>anlamı</strong> tekrar okuyun; çoğu zaman "
        "kelimenin ilk harfini hatırlatır.",
        "⏱️ Süre baskısı istiyorsanız <strong>Speed Round</strong> oyununu deneyin.",
    ],
    "Word Scramble gives you the letters of a word in random order plus its "
    "meaning, and asks you to rebuild it. It turns passive vocabulary — words you "
    "recognise while reading but cannot write — into active vocabulary, and "
    "teaches common letter clusters such as -tion, sch- and -eau.",
    links("games", "cards", "quiz", "words", "help"),
)

PAGES["memory.html"] = block(
    "Matching pairs hakkında",
    "Matching Pairs · Kelime ve Anlamını Eşleştirin",
    [
        "<strong>Matching Pairs</strong> (hafıza kartları), kapalı kartları "
        "çevirerek her kelimeyi kendi anlamıyla eşleştirdiğiniz oyundur. Aynı anda "
        "hem <strong>kelime–anlam bağını</strong> hem de görsel hafızanızı "
        "çalıştırır.",
        "Oyun, kelime ezberlemenin en sıkıcı kısmını — ilk tanışma aşamasını — "
        "eğlenceli hâle getirdiği için özellikle <strong>yeni bir seviyeye "
        "başlarken</strong> idealdir. Bir turda karşınıza az sayıda kelime çıkar; "
        "bu kelimeleri birkaç kez görmek zorunda kaldığınız için "
        "<strong>tekrar</strong> kendiliğinden gerçekleşir.",
        "Hamle sayınız ve süreniz kaydedilir; aynı seti daha az hamlede bitirmeye "
        "çalışmak, kelimeleri ne kadar iyi tuttuğunuzun basit ama gerçekçi bir "
        "ölçüsüdür.",
    ],
    [
        "🧠 Kart yerlerini ezberlemek yerine <strong>kelimeyi okuyun</strong> — "
        "amaç oyunu kazanmak değil, kelimeyi öğrenmek.",
        "🆕 Yeni bir seviyeye <strong>bu oyunla başlayın</strong>, sonra kartlara "
        "ve quize geçin.",
        "🔁 Aynı seti iki kez oynayın; ikinci turda fark ettiğiniz hız artışı "
        "öğrenmenin kanıtıdır.",
    ],
    "Matching Pairs is a memory game where you flip cards to pair each word with "
    "its meaning. It makes the hardest part of vocabulary learning — the very "
    "first encounter — enjoyable, and builds in repetition automatically. Ideal "
    "when you start a new level.",
    links("games", "cards", "quiz", "words", "help"),
)

PAGES["speedround.html"] = block(
    "Speed round hakkında",
    "Speed Round · 60 Saniyede Kaç Doğru?",
    [
        "<strong>Speed Round</strong>, 60 saniye içinde olabildiğince çok kelimeyi "
        "doğru yanıtlamaya çalıştığınız hızlı bir moddur. Süre baskısı, "
        "düşünerek değil <strong>otomatik olarak</strong> hatırlamanızı zorlar — "
        "bir kelimeyi gerçekten bildiğinizin en iyi göstergesi budur.",
        "Sınavda her soruya ayıracak sınırlı vaktiniz olduğu için, "
        "<strong>hatırlama hızı</strong> en az doğruluk kadar önemlidir. Aynı "
        "seviyeyi birkaç gün üst üste oynayıp skorunuzun yükselişini izlemek, "
        "ilerlemenizi ölçmenin somut bir yoludur.",
    ],
    [
        "⚡ Emin olamadığınız soruda <strong>duraksamayın</strong>; geçin ve "
        "bildiklerinizden puan toplayın.",
        "📈 Skorunuzu <strong>gün gün</strong> karşılaştırın — asıl ölçüt tek bir "
        "oyun değil, eğilimdir.",
        "🎯 Yeni öğrendiğiniz kelimelerde önce <strong>kartlar</strong> ve "
        "<strong>Quiz</strong>, hız için en son bu mod.",
    ],
    "Speed Round gives you 60 seconds to answer as many words as you can. Time "
    "pressure forces automatic recall rather than deliberate reasoning — the best "
    "evidence that you truly know a word, and the skill a timed exam actually "
    "measures.",
    links("games", "cards", "quiz", "stats", "help"),
)

PAGES["wordrace.html"] = block(
    "Word race hakkında",
    "Word Race · Anlamdan Kelimeyi Yazın",
    [
        "<strong>Word Race</strong>, size anlamı verir ve süre dolmadan "
        "<strong>kelimeyi yazmanızı</strong> ister. Şık yoktur, ipucu yoktur — bu "
        "yüzden uygulamadaki en zor ve en öğretici modlardan biridir.",
        "Çoktan seçmeli sorularda doğru yanıtı <strong>tanıyarak</strong> "
        "bulabilirsiniz; burada ise kelimeyi sıfırdan <strong>üretmeniz</strong> "
        "gerekir. Bu beceri doğrudan <strong>yazma ve konuşma</strong> "
        "performansınıza yansır, çünkü gerçek iletişimde de şık verilmez.",
        "Yazım hataları da sayıldığı için oyun aynı anda imlâyı pekiştirir. "
        "Süre bittiğinde doğru kelime ve örnek cümlesi gösterilir.",
    ],
    [
        "⌨️ Yazarken <strong>hız değil doğruluk</strong> hedefleyin; yanlış yazılan "
        "kelime sayılmaz.",
        "🧠 Aklınıza gelmiyorsa kelimenin <strong>ilk harfini</strong> düşünmeye "
        "çalışın — genellikle gerisi gelir.",
        "📚 Bu modda çok zorlanıyorsanız o seviye sizin için henüz erken; bir alt "
        "seviyeye dönün.",
    ],
    "Word Race shows the meaning and asks you to type the word before time runs "
    "out — no options, no hints. Producing a word from scratch is much harder than "
    "recognising it, and it is exactly the skill that carries over into writing "
    "and speaking.",
    links("games", "cards", "quiz", "words", "help"),
)

PAGES["sentencescramble.html"] = block(
    "Sentence scramble hakkında",
    "Sentence Scramble · Cümleyi Doğru Sıraya Dizin",
    [
        "<strong>Sentence Scramble</strong>, gerçek bir örnek cümlenin "
        "kelimelerini karıştırır ve sizden cümleyi <strong>doğru sıraya "
        "dizmenizi</strong> ister. Bu oyun kelime değil, <strong>söz dizimi "
        "(sentence structure)</strong> çalıştırır.",
        "Almanca'da fiilin <strong>ikinci sırada</strong> veya yan cümlede "
        "<strong>sonda</strong> olması, ayrılabilen fiillerin parçasının cümle "
        "sonuna gitmesi; Fransızca'da sıfatın isimden <strong>sonra</strong> "
        "gelmesi; İngilizce'de sıfat sıralaması gibi kuralları, kural ezberleyerek "
        "değil <strong>doğru cümleyi kurarak</strong> öğrenirsiniz.",
        "Cümleler kelime listelerindeki gerçek örneklerden alınır, uydurulmaz; "
        "yani öğrendiğiniz her yapı gerçekten kullanılan bir yapıdır.",
    ],
    [
        "🔎 Önce <strong>özneyi ve fiili</strong> bulun, cümleyi onların etrafına "
        "kurun.",
        "🇩🇪 Almanca'da <strong>çekimli fiil</strong> ana cümlede daima ikinci "
        "öğedir — buradan başlayın.",
        "📖 Doğru cevabı gördükten sonra cümleyi <strong>bir kez de yüksek "
        "sesle</strong> okuyun.",
    ],
    "Sentence Scramble shuffles the words of a real example sentence and asks you "
    "to restore the correct order. It drills syntax rather than vocabulary — "
    "German verb-second and separable verbs, French adjective placement, English "
    "adjective order — by building correct sentences instead of memorising rules.",
    links("games", "cloze", "read", "cards", "help"),
)

PAGES["dictation.html"] = block(
    "Dictation hakkında",
    "Listening Dictation · Dinleyin ve Yazın",
    [
        "<strong>Listening Dictation</strong>, kelimeyi <strong>duyup doğru "
        "yazmanızı</strong> ister. Ses, tarayıcınızın kendi konuşma sentezi "
        "(text-to-speech) özelliğiyle üretilir ve istediğiniz kadar "
        "tekrar dinleyebilirsiniz.",
        "Bu mod, kelime çalışmasının en çok ihmal edilen tarafını kapatır: "
        "<strong>ses ile yazım arasındaki bağ</strong>. Bir kelimeyi okuduğunuzda "
        "tanıyor ama konuşmada duyduğunuzda yakalayamıyorsanız eksik olan tam "
        "olarak budur. İngilizce'de yazıldığı gibi okunmayan kelimeler "
        "(<em>thorough, colonel, queue</em>) ve Fransızca'da okunmayan son harfler "
        "için özellikle değerlidir.",
        "Sesin çıkmaması durumunda cihazınızın sesinin açık olduğundan ve "
        "tarayıcınızın konuşma sentezini desteklediğinden emin olun; Chrome, Edge "
        "ve Safari bu özelliği destekler.",
    ],
    [
        "🎧 Kelimeyi <strong>iki kez</strong> dinleyin: ilkinde bütününü, "
        "ikincisinde hecelerini.",
        "✍️ Duyduğunuzu <strong>önce zihninizde heceleyin</strong>, sonra yazın.",
        "📻 Daha uzun dinleme pratiği için <strong>Dinleme Kaynakları</strong> "
        "sayfasındaki dış kaynaklara göz atın.",
    ],
    "Listening Dictation plays a word with your browser's speech synthesis and "
    "asks you to type it. It closes the gap between how a word sounds and how it "
    "is spelled — the reason you may recognise a word on paper but miss it in "
    "speech. Especially useful for irregular English spellings and French silent "
    "endings.",
    links("games", "listen", "cards", "quiz", "help"),
)

PAGES["truefalse.html"] = block(
    "True or false hakkında",
    "True or False · Anlam Doğru mu Yanlış mı?",
    [
        "<strong>True or False</strong>, bir kelime ve bir anlam gösterir; "
        "eşleşmenin doğru olup olmadığına <strong>hızlıca</strong> karar "
        "vermeniz gerekir. İki seçenek olduğu için tempo yüksektir ve kısa sürede "
        "çok sayıda kelime tekrarlanır.",
        "Yanlış eşleşmeler rastgele değil, <strong>benzer</strong> kelimelerden "
        "üretilir. Bu yüzden oyun, birbirine karıştırdığınız kelime çiftlerini "
        "(<em>affect / effect</em>, <em>bekommen / werden</em>, "
        "<em>poisson / poison</em>) ortaya çıkarmakta çok etkilidir.",
        "Hızlı bir <strong>ısınma</strong> veya günün sonunda kısa bir "
        "<strong>tekrar</strong> turu için idealdir.",
    ],
    [
        "⚡ Uzun düşünmeyin; ilk sezginiz genellikle doğrudur, tereddüt "
        "bilmediğinizin işaretidir.",
        "❌ Yanlış yaptığınız çiftleri not alın — asıl çalışmanız gereken kelimeler "
        "onlar.",
        "🔥 Hata yapmadan ne kadar gidebildiğinizi görmek için "
        "<strong>Survival Streak</strong> oyununu deneyin.",
    ],
    "True or False shows a word and a meaning and asks you to judge the pairing "
    "fast. Wrong pairings are built from similar words, so the game is very good "
    "at exposing the pairs you confuse — affect/effect, bekommen/werden, "
    "poisson/poison. A perfect warm-up or end-of-day review.",
    links("games", "cards", "quiz", "words", "help"),
)

PAGES["survival.html"] = block(
    "Survival streak hakkında",
    "Survival Streak · İlk Yanlışa Kadar",
    [
        "<strong>Survival Streak</strong>'te tek bir kural vardır: "
        "<strong>ilk yanlışta oyun biter</strong>. Amacınız üst üste kaç doğru "
        "yapabildiğinizi görmek ve kendi rekorunuzu kırmaktır.",
        "Hata payının olmaması, diğer modlarda fark etmediğiniz bir şeyi görünür "
        "kılar: <strong>tahmin ederek</strong> ilerlediğiniz kelimeler. "
        "Dört şıklı bir testte %25 şansla doğruyu tutturmak mümkündür, ama uzun "
        "bir seride şans işe yaramaz — seri uzunluğunuz gerçek bilginizin dürüst "
        "bir ölçüsüdür.",
        "Serinizi ve en iyi rekorunuzu kaydeder; ilerledikçe rekorunuzun "
        "yükselmesi, seviyenin artık size uygun olduğunun işaretidir.",
    ],
    [
        "🧘 Acele etmeyin — burada <strong>süre değil doğruluk</strong> önemlidir.",
        "📉 Seriniz hep kısa kalıyorsa bir <strong>alt seviyeye</strong> geçin; "
        "temeli sağlamlaştırmak daha hızlı ilerletir.",
        "🏆 Rekorunuzu <strong>haftalık</strong> takip edin.",
    ],
    "In Survival Streak the first mistake ends the run, so your streak length is "
    "an honest measure of what you actually know — guessing works in a "
    "four-option quiz but never survives a long streak. Beat your own record.",
    links("games", "cards", "quiz", "stats", "help"),
)

PAGES["matrix.html"] = block(
    "Word matrix hakkında",
    "Word Matrix · Harf Izgarasında Kelime Avı",
    [
        "<strong>Word Matrix</strong>, harflerden oluşan bir ızgarada gizlenmiş "
        "kelimeleri bulmanızı ister. Kelimeler yatay, dikey ve çapraz olarak "
        "yerleştirilir; bulmanız gereken kelimelerin <strong>anlamları</strong> "
        "ipucu olarak verilir.",
        "Bu oyun, kelimenin <strong>görsel şeklini</strong> tanıma becerisini "
        "geliştirir — hızlı okuma sırasında kelimeleri harf harf değil bir bütün "
        "olarak algıladığınız için, okuma hızınıza doğrudan katkı sağlar.",
        "Diğer modlara göre daha sakin ve süre baskısızdır; uzun bir çalışma "
        "seansının sonunda <strong>zihin yormadan tekrar</strong> yapmak için "
        "uygundur.",
    ],
    [
        "👀 Izgarayı <strong>satır satır</strong> tarayın, rastgele bakmayın.",
        "🔤 Aradığınız kelimenin <strong>ilk harfini</strong> gözünüzde tutup "
        "sadece o harfi arayın.",
        "↗️ <strong>Çapraz</strong> yerleşimleri en sona bırakın; genellikle en zor "
        "olanlar onlardır.",
    ],
    "Word Matrix hides words horizontally, vertically and diagonally in a letter "
    "grid, with their meanings as clues. It trains whole-word visual recognition, "
    "which feeds directly into reading speed — and it is a calm, "
    "no-time-pressure way to revise at the end of a study session.",
    links("games", "cards", "words", "read", "help"),
)

PAGES["readingcomprehension.html"] = block(
    "Okuduğunu anlama hakkında",
    "Reading Comprehension · Okuduğunu Anlama Alıştırmaları",
    [
        "<strong>Reading Comprehension</strong> bölümünde seviyenize uygun kısa bir "
        "metin okur, ardından metinle ilgili <strong>çoktan seçmeli soruları</strong> "
        "yanıtlarsınız. Sorular ana fikri, ayrıntıları, çıkarımı ve kelimenin "
        "metindeki anlamını ölçer — <strong>YDS, YÖKDİL ve TOEFL</strong> okuma "
        "bölümlerinin ölçtüğü becerilerin aynısı.",
        "Metinler <strong>A1'den C2'ye</strong> kadar seviyelendirilmiştir: A1'de "
        "kısa ve somut, C2'de uzun ve soyut metinler gelir. Her sorunun ardından "
        "<strong>doğru yanıtın neden doğru olduğu</strong> açıklanır, böylece "
        "sadece puan almakla kalmaz, soru mantığını da öğrenirsiniz.",
        "Okuma, kelime dağarcığını genişletmenin en doğal yoludur: kelimeyi "
        "listede değil, <strong>gerçek bir metnin içinde</strong> gördüğünüzde "
        "çok daha kalıcı öğrenirsiniz. Bölüm; İngilizce, Almanca ve Fransızca "
        "için metin içerir.",
    ],
    [
        "📖 Önce <strong>soruları</strong> okuyun, sonra metne geçin — ne "
        "aradığınızı bilmek okuma süresini kısaltır.",
        "🧭 Ana fikir sorularında yanıt genellikle <strong>ilk ve son "
        "paragraftadır</strong>.",
        "🚫 Bilmediğiniz her kelimeyi sözlükten aramayın; önce "
        "<strong>bağlamdan tahmin</strong> etmeye çalışın.",
        "📝 Metinde geçen yeni kelimeleri <strong>Kelime Listesi</strong>nde arayıp "
        "kartlara ekleyin.",
    ],
    "Read a short level-appropriate text, then answer multiple-choice questions on "
    "main idea, detail, inference and vocabulary in context — the same skills the "
    "YDS, YÖKDİL and TOEFL reading sections measure. Each answer comes with an "
    "explanation. Texts are graded A1–C2 and available in English, German and "
    "French.",
    links("cloze", "cards", "quiz", "words", "listen"),
)


def main():
    changed, skipped, missing = [], [], []
    for name, html in sorted(PAGES.items()):
        path = os.path.join(ROOT, name)
        with open(path, encoding="utf-8") as fh:
            src = fh.read()
        if 'class="seo-content"' in src:
            skipped.append(name)
            continue
        if src.count(ANCHOR) != 1:
            missing.append(name)
            continue
        with open(path, "w", encoding="utf-8", newline="") as fh:
            fh.write(src.replace(ANCHOR, html + ANCHOR))
        changed.append(name)
    print("changed   %2d: %s" % (len(changed), ", ".join(changed)))
    print("skipped   %2d: %s" % (len(skipped), ", ".join(skipped)))
    print("NO ANCHOR %2d: %s" % (len(missing), ", ".join(missing)))


if __name__ == "__main__":
    main()
