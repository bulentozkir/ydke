// Reading Comprehension passages -- German track (CEFR levels A1-C2).
// All passages and questions below are ORIGINAL content written for this
// app -- they are NOT copied or adapted from any third-party site.
// (lingua.com / german.net, both operated by Weblayout s.r.o., were
// consulted only for structural inspiration -- their Terms & Conditions
// explicitly prohibit copying, redistributing, or publishing their
// content: "Digital distribution or dissemination is not permitted.
// Publication is not permitted." -- so their actual texts were
// intentionally NOT used as a source here.)
// Schema: { level, title, text, questions: [ { q, options: [4], correct,
// hint, explain } ] }. `correct` is the 0-based index into `options` of
// the right answer. `hint`/`explain` are shown via the Hint/Explain
// buttons in the game UI. All text (passage, questions, hint, explain) is
// in German, matching the passage's own language.
// Used ONLY by the Reading Comprehension game (readingcomprehension.js),
// selected automatically when currentLang === "de".

window.READING_PASSAGES_DE = [
  // ===================== A1 =====================
  {
    level: "A1",
    title: "Meine Familie",
    text:
      "Ich heiße Lena. Ich bin zehn Jahre alt. Ich habe eine Mutter, einen Vater und einen Bruder. " +
      "Mein Bruder heißt Max. Wir wohnen in Berlin. Ich habe auch einen Hund. Der Hund heißt Bello.",
    questions: [
      { q: "Wie heißt das Mädchen?", options: ["Lena", "Max", "Bello", "Mutter"], correct: 0, hint: "Schau auf den ersten Satz.", explain: "Der Text sagt: \"Ich heiße Lena.\"" },
      { q: "Wie alt ist Lena?", options: ["Acht", "Neun", "Zehn", "Elf"], correct: 2, hint: "Schau auf den zweiten Satz.", explain: "Der Text sagt: \"Ich bin zehn Jahre alt.\"" },
      { q: "Wie heißt der Bruder?", options: ["Bello", "Max", "Lena", "Vater"], correct: 1, hint: "Schau auf den Satz über den Bruder.", explain: "Der Text sagt: \"Mein Bruder heißt Max.\"" },
      { q: "Wo wohnt die Familie?", options: ["In München", "In Berlin", "In Hamburg", "In Köln"], correct: 1, hint: "Schau auf den Satz mit dem Stadtnamen.", explain: "Der Text sagt: \"Wir wohnen in Berlin.\"" },
      { q: "Was für ein Tier hat Lena?", options: ["Eine Katze", "Einen Vogel", "Einen Hund", "Ein Pferd"], correct: 2, hint: "Schau auf den letzten Satz.", explain: "Der Text sagt: \"Ich habe auch einen Hund.\"" },
      { q: "Wie viele Brüder hat Lena?", options: ["Keinen", "Einen", "Zwei", "Drei"], correct: 1, hint: "Schau auf den dritten Satz.", explain: "Der Text sagt, Lena hat einen Bruder namens Max." },
      { q: "Wie heißt der Hund?", options: ["Max", "Bello", "Lena", "Rex"], correct: 1, hint: "Schau auf den letzten Satz.", explain: "Der Text sagt, der Hund heißt Bello." },
      { q: "Wer ist Max?", options: ["Lenas Vater", "Lenas Bruder", "Lenas Hund", "Lenas Freund"], correct: 1, hint: "Schau auf den Satz über den Bruder.", explain: "Der Text sagt, Max ist Lenas Bruder." },
      { q: "Hat Lena ein Haustier?", options: ["Nein", "Ja, einen Hund", "Ja, eine Katze", "Ja, einen Vogel"], correct: 1, hint: "Schau auf den letzten Satz.", explain: "Der Text sagt, Lena hat auch einen Hund." },
      { q: "Wen erwähnt Lena in ihrer Familie?", options: ["Nur die Mutter", "Mutter, Vater und Bruder", "Zwei Schwestern", "Die Großeltern"], correct: 1, hint: "Schau auf den dritten Satz.", explain: "Der Text nennt eine Mutter, einen Vater und einen Bruder." },
    ],
  },
  {
    level: "A1",
    title: "Mein Tag",
    text:
      "Ich stehe um sieben Uhr auf. Ich esse Brot zum Frühstück. Dann gehe ich zur Schule. " +
      "Die Schule beginnt um acht Uhr. Nach der Schule spiele ich Fußball. Am Abend lese ich ein Buch.",
    questions: [
      { q: "Um wie viel Uhr steht die Person auf?", options: ["Sechs Uhr", "Sieben Uhr", "Acht Uhr", "Neun Uhr"], correct: 1, hint: "Schau auf den ersten Satz.", explain: "Der Text sagt: \"Ich stehe um sieben Uhr auf.\"" },
      { q: "Was isst die Person zum Frühstück?", options: ["Brot", "Ei", "Obst", "Käse"], correct: 0, hint: "Schau auf den zweiten Satz.", explain: "Der Text sagt: \"Ich esse Brot zum Frühstück.\"" },
      { q: "Um wie viel Uhr beginnt die Schule?", options: ["Sieben Uhr", "Acht Uhr", "Neun Uhr", "Zehn Uhr"], correct: 1, hint: "Schau auf den Satz über die Schule.", explain: "Der Text sagt: \"Die Schule beginnt um acht Uhr.\"" },
      { q: "Was macht die Person nach der Schule?", options: ["Sie schläft", "Sie spielt Fußball", "Sie kocht", "Sie schwimmt"], correct: 1, hint: "Schau auf den Satz nach der Schule.", explain: "Der Text sagt: \"Nach der Schule spiele ich Fußball.\"" },
      { q: "Was macht die Person am Abend?", options: ["Sie liest ein Buch", "Sie sieht fern", "Sie geht aus", "Sie ruft an"], correct: 0, hint: "Schau auf den letzten Satz.", explain: "Der Text sagt: \"Am Abend lese ich ein Buch.\"" },
      { q: "Was macht die Person nach dem Aufstehen zuerst?", options: ["Sie geht zur Schule", "Sie isst Frühstück", "Sie spielt Fußball", "Sie liest"], correct: 1, hint: "Schau auf den zweiten Satz.", explain: "Nach dem Aufstehen isst die Person Brot zum Frühstück." },
      { q: "Wohin geht die Person nach dem Frühstück?", options: ["Zur Arbeit", "Zur Schule", "Zum Park", "Nach Hause"], correct: 1, hint: "Schau auf den dritten Satz.", explain: "Der Text sagt: Dann gehe ich zur Schule." },
      { q: "Welchen Sport spielt die Person?", options: ["Tennis", "Fußball", "Basketball", "Schwimmen"], correct: 1, hint: "Schau auf den Satz nach der Schule.", explain: "Der Text sagt, sie spielt Fußball." },
      { q: "Wann liest die Person ein Buch?", options: ["Am Morgen", "Am Mittag", "Am Abend", "In der Schule"], correct: 2, hint: "Schau auf den letzten Satz.", explain: "Der Text sagt: Am Abend lese ich ein Buch." },
      { q: "Um wie viel Uhr beginnt der Unterricht?", options: ["Um sieben Uhr", "Um acht Uhr", "Um neun Uhr", "Um zehn Uhr"], correct: 1, hint: "Schau auf den Satz über die Schule.", explain: "Die Schule beginnt um acht Uhr." },
    ],
  },

  // ===================== A2 =====================
  {
    level: "A2",
    title: "Einkaufen im Supermarkt",
    text:
      "Am Samstag gehe ich mit meiner Mutter einkaufen. Wir brauchen Brot, Milch und Äpfel. Der Supermarkt " +
      "ist nicht weit von unserem Haus. Zuerst suchen wir das Brot. Dann finden wir die Milch im Kühlregal. " +
      "Zum Schluss kaufen wir frische Äpfel. An der Kasse bezahlen wir mit Karte.",
    questions: [
      { q: "Wann geht die Person einkaufen?", options: ["Am Montag", "Am Samstag", "Am Sonntag", "Am Freitag"], correct: 1, hint: "Schau auf den ersten Satz.", explain: "Der Text sagt: \"Am Samstag gehe ich mit meiner Mutter einkaufen.\"" },
      { q: "Was brauchen sie nicht?", options: ["Brot", "Milch", "Äpfel", "Käse"], correct: 3, hint: "Schau auf die Liste im zweiten Satz.", explain: "Der Text nennt nur \"Brot, Milch und Äpfel\" -- Käse wird nicht erwähnt." },
      { q: "Wie weit ist der Supermarkt?", options: ["Sehr weit", "Nicht weit", "In einer anderen Stadt", "Unbekannt"], correct: 1, hint: "Schau auf den Satz über den Supermarkt.", explain: "Der Text sagt: \"Der Supermarkt ist nicht weit von unserem Haus.\"" },
      { q: "Wo finden sie die Milch?", options: ["Im Kühlregal", "Neben dem Brot", "An der Kasse", "Draußen"], correct: 0, hint: "Schau auf den Satz über die Milch.", explain: "Der Text sagt: \"Dann finden wir die Milch im Kühlregal.\"" },
      { q: "Wie bezahlen sie?", options: ["Mit Bargeld", "Mit Karte", "Mit einem Gutschein", "Sie bezahlen nicht"], correct: 1, hint: "Schau auf den letzten Satz.", explain: "Der Text sagt: \"An der Kasse bezahlen wir mit Karte.\"" },
      { q: "Mit wem geht die Person einkaufen?", options: ["Mit dem Vater", "Mit der Mutter", "Mit einem Freund", "Allein"], correct: 1, hint: "Schau auf den ersten Satz.", explain: "Der Text sagt, die Person geht mit ihrer Mutter einkaufen." },
      { q: "Was suchen sie zuerst?", options: ["Die Milch", "Das Brot", "Die Äpfel", "Den Käse"], correct: 1, hint: "Schau auf den Satz mit 'Zuerst'.", explain: "Der Text sagt: Zuerst suchen wir das Brot." },
      { q: "Was kaufen sie zum Schluss?", options: ["Brot", "Milch", "Frische Äpfel", "Käse"], correct: 2, hint: "Schau auf den Satz mit 'Zum Schluss'.", explain: "Der Text sagt: Zum Schluss kaufen wir frische Äpfel." },
      { q: "Wie viele Dinge stehen auf der Einkaufsliste?", options: ["Zwei", "Drei", "Vier", "Fünf"], correct: 1, hint: "Schau auf den zweiten Satz: Brot, Milch und Äpfel.", explain: "Der Text nennt drei Dinge: Brot, Milch und Äpfel." },
      { q: "Warum ist der Einkauf einfach?", options: ["Der Supermarkt ist nicht weit", "Sie haben viel Geld", "Die Mutter kennt den Weg", "Es gibt keine anderen Kunden"], correct: 0, hint: "Schau auf den Satz über den Supermarkt.", explain: "Der Supermarkt ist nicht weit von ihrem Haus." },
    ],
  },
  {
    level: "A2",
    title: "Ein Wochenende am See",
    text:
      "Letztes Wochenende bin ich mit meinen Freunden an einen See gefahren. Das Wetter war sonnig und warm. " +
      "Wir haben ein Picknick gemacht und viel gelacht. Am Nachmittag sind wir geschwommen. Ein Freund hat " +
      "sogar Fische gefangen. Am Abend haben wir ein Lagerfeuer gemacht. Es war ein wunderschöner Tag.",
    questions: [
      { q: "Wann ist die Person zum See gefahren?", options: ["Letzten Sommer", "Letztes Wochenende", "Gestern", "Letztes Jahr"], correct: 1, hint: "Schau auf den ersten Satz.", explain: "Der Text sagt: \"Letztes Wochenende bin ich mit meinen Freunden an einen See gefahren.\"" },
      { q: "Wie war das Wetter?", options: ["Kalt und regnerisch", "Sonnig und warm", "Windig", "Neblig"], correct: 1, hint: "Schau auf den zweiten Satz.", explain: "Der Text sagt: \"Das Wetter war sonnig und warm.\"" },
      { q: "Was haben sie gemacht, bevor sie geschwommen sind?", options: ["Ein Picknick", "Ein Lagerfeuer", "Eine Wanderung", "Ein Spiel"], correct: 0, hint: "Schau auf den Satz vor dem Schwimmen.", explain: "Der Text sagt, sie \"haben ein Picknick gemacht und viel gelacht\", bevor sie schwimmen gingen." },
      { q: "Was hat ein Freund gefangen?", options: ["Einen Vogel", "Fische", "Ein Kaninchen", "Nichts"], correct: 1, hint: "Schau auf den Satz über den Freund.", explain: "Der Text sagt: \"Ein Freund hat sogar Fische gefangen.\"" },
      { q: "Was haben sie am Abend gemacht?", options: ["Sie sind nach Hause gefahren", "Ein Lagerfeuer gemacht", "Wieder geschwommen", "Im Zelt geschlafen"], correct: 1, hint: "Schau auf den Satz über den Abend.", explain: "Der Text sagt: \"Am Abend haben wir ein Lagerfeuer gemacht.\"" },
      { q: "Mit wem ist die Person zum See gefahren?", options: ["Mit der Familie", "Mit ihren Freunden", "Allein", "Mit Kollegen"], correct: 1, hint: "Schau auf den ersten Satz.", explain: "Der Text sagt, die Person fuhr mit ihren Freunden." },
      { q: "Wann sind sie geschwommen?", options: ["Am Morgen", "Am Nachmittag", "Am Abend", "In der Nacht"], correct: 1, hint: "Schau auf den Satz über das Schwimmen.", explain: "Der Text sagt: Am Nachmittag sind wir geschwommen." },
      { q: "Was haben sie beim Picknick gemacht?", options: ["Viel gelacht", "Gestritten", "Geschlafen", "Gearbeitet"], correct: 0, hint: "Schau auf den Satz über das Picknick.", explain: "Der Text sagt, sie haben ein Picknick gemacht und viel gelacht." },
      { q: "Wie beschreibt die Person den Tag am Ende?", options: ["Als langweilig", "Als wunderschön", "Als anstrengend", "Als kalt"], correct: 1, hint: "Schau auf den letzten Satz.", explain: "Der Text sagt: Es war ein wunderschöner Tag." },
      { q: "Was hat ein Freund am See getan?", options: ["Ein Buch gelesen", "Fische gefangen", "Ein Zelt gebaut", "Musik gemacht"], correct: 1, hint: "Schau auf den Satz über den Freund.", explain: "Der Text sagt, ein Freund hat Fische gefangen." },
    ],
  },

  // ===================== B1 =====================
  {
    level: "B1",
    title: "Meine Stadt",
    text:
      "Ich wohne seit fünf Jahren in Leipzig. Die Stadt ist bekannt für ihre Musik und ihre Universität. " +
      "Es gibt viele Parks, in denen man spazieren gehen kann. Im Stadtzentrum gibt es zahlreiche Cafés und " +
      "Restaurants. Am Wochenende besuche ich oft den Markt, wo man frisches Obst und Gemüse kaufen kann. " +
      "Was mir am besten gefällt, ist die freundliche Atmosphäre der Stadt.",
    questions: [
      { q: "Wie lange wohnt die Person schon in Leipzig?", options: ["Zwei Jahre", "Drei Jahre", "Fünf Jahre", "Zehn Jahre"], correct: 2, hint: "Schau auf den ersten Satz.", explain: "Der Text sagt: \"Ich wohne seit fünf Jahren in Leipzig.\"" },
      { q: "Wofür ist Leipzig bekannt?", options: ["Für seinen Strand", "Für seine Musik und Universität", "Für seine Berge", "Für seine Industrie"], correct: 1, hint: "Schau auf den zweiten Satz.", explain: "Der Text sagt, die Stadt sei \"bekannt für ihre Musik und ihre Universität.\"" },
      { q: "Was kann man in den Parks machen?", options: ["Schwimmen", "Einkaufen", "Spazieren gehen", "Autofahren"], correct: 2, hint: "Schau auf den Satz über die Parks.", explain: "Der Text sagt, es gebe Parks, \"in denen man spazieren gehen kann.\"" },
      { q: "Was kauft die Person auf dem Markt?", options: ["Kleidung", "Frisches Obst und Gemüse", "Bücher", "Möbel"], correct: 1, hint: "Schau auf den Satz über das Wochenende.", explain: "Der Text sagt, man könne dort \"frisches Obst und Gemüse kaufen.\"" },
      { q: "Was gefällt der Person am besten an der Stadt?", options: ["Die Cafés", "Der Markt", "Die freundliche Atmosphäre", "Die Universität"], correct: 2, hint: "Schau auf den letzten Satz.", explain: "Der Text sagt: \"Was mir am besten gefällt, ist die freundliche Atmosphäre der Stadt.\"" },
      { q: "In welcher Stadt wohnt die Person?", options: ["Leipzig", "Berlin", "Dresden", "München"], correct: 0, hint: "Schau auf den ersten Satz.", explain: "Der Text sagt, die Person wohnt in Leipzig." },
      { q: "Was gibt es im Stadtzentrum?", options: ["Nur Parks", "Zahlreiche Cafés und Restaurants", "Einen Flughafen", "Große Fabriken"], correct: 1, hint: "Schau auf den Satz über das Stadtzentrum.", explain: "Der Text sagt, im Stadtzentrum gibt es zahlreiche Cafés und Restaurants." },
      { q: "Wann besucht die Person den Markt?", options: ["Am Wochenende", "Jeden Morgen", "Nie", "Am Montag"], correct: 0, hint: "Schau auf den Satz über den Markt.", explain: "Der Text sagt: Am Wochenende besuche ich oft den Markt." },
      { q: "Was kann man in den Parks von Leipzig tun?", options: ["Nur arbeiten", "Spazieren gehen", "Ski fahren", "Am Strand liegen"], correct: 1, hint: "Schau auf den Satz über die Parks.", explain: "Der Text sagt, man kann in den Parks spazieren gehen." },
      { q: "Wie ist die Atmosphäre der Stadt laut Text?", options: ["Unfreundlich", "Freundlich", "Laut", "Kalt"], correct: 1, hint: "Schau auf den letzten Satz.", explain: "Der Text nennt die freundliche Atmosphäre der Stadt." },
    ],
  },
  {
    level: "B1",
    title: "Gesunde Ernährung",
    text:
      "Immer mehr Menschen achten heutzutage auf eine gesunde Ernährung. Obst und Gemüse sollten täglich auf " +
      "dem Speiseplan stehen, da sie wichtige Vitamine liefern. Auch ausreichend Wasser zu trinken ist wichtig " +
      "für den Körper. Viele Experten empfehlen, weniger Zucker und Fett zu essen. Regelmäßige Mahlzeiten " +
      "helfen außerdem, Heißhunger zu vermeiden. Wer sich gesund ernährt, fühlt sich oft energiegeladener und " +
      "konzentrierter.",
    questions: [
      { q: "Worauf achten immer mehr Menschen?", options: ["Auf ihre Kleidung", "Auf eine gesunde Ernährung", "Auf ihr Geld", "Auf ihre Freizeit"], correct: 1, hint: "Schau auf den ersten Satz.", explain: "Der Text sagt: \"Immer mehr Menschen achten heutzutage auf eine gesunde Ernährung.\"" },
      { q: "Warum sollten Obst und Gemüse täglich gegessen werden?", options: ["Sie sind billig", "Sie liefern wichtige Vitamine", "Sie schmecken süß", "Sie sind leicht zu kochen"], correct: 1, hint: "Schau auf den zweiten Satz.", explain: "Der Text sagt, sie \"liefern wichtige Vitamine.\"" },
      { q: "Was ist laut Text auch wichtig für den Körper?", options: ["Genug schlafen", "Ausreichend Wasser trinken", "Viel Sport treiben", "Wenig arbeiten"], correct: 1, hint: "Schau auf den Satz über Wasser.", explain: "Der Text sagt: \"Auch ausreichend Wasser zu trinken ist wichtig für den Körper.\"" },
      { q: "Was empfehlen viele Experten?", options: ["Mehr Zucker zu essen", "Weniger Zucker und Fett zu essen", "Nur einmal am Tag zu essen", "Kein Gemüse zu essen"], correct: 1, hint: "Schau auf den Satz über Experten.", explain: "Der Text sagt, Experten empfehlen, \"weniger Zucker und Fett zu essen.\"" },
      { q: "Wie fühlen sich Menschen, die sich gesund ernähren, laut Text?", options: ["Müde und schwach", "Energiegeladener und konzentrierter", "Gestresster", "Hungriger"], correct: 1, hint: "Schau auf den letzten Satz.", explain: "Der Text sagt, man \"fühlt sich oft energiegeladener und konzentrierter.\"" },
      { q: "Wie oft sollten Obst und Gemüse gegessen werden?", options: ["Einmal pro Woche", "Täglich", "Nur im Sommer", "Selten"], correct: 1, hint: "Schau auf den zweiten Satz.", explain: "Der Text sagt, Obst und Gemüse sollten täglich auf dem Speiseplan stehen." },
      { q: "Wogegen helfen regelmäßige Mahlzeiten?", options: ["Gegen Müdigkeit", "Gegen Heißhunger", "Gegen Stress", "Gegen Krankheit"], correct: 1, hint: "Schau auf den Satz über Mahlzeiten.", explain: "Der Text sagt, regelmäßige Mahlzeiten helfen, Heißhunger zu vermeiden." },
      { q: "Was liefern Obst und Gemüse laut Text?", options: ["Fett", "Wichtige Vitamine", "Zucker", "Salz"], correct: 1, hint: "Schau auf den zweiten Satz.", explain: "Der Text sagt, sie liefern wichtige Vitamine." },
      { q: "Was sollte man laut Experten weniger essen?", options: ["Obst und Gemüse", "Zucker und Fett", "Brot und Reis", "Fisch und Fleisch"], correct: 1, hint: "Schau auf den Satz über Experten.", explain: "Experten empfehlen, weniger Zucker und Fett zu essen." },
      { q: "Was ist neben dem Essen noch wichtig für den Körper?", options: ["Genug Wasser trinken", "Viel schlafen", "Wenig reden", "Schnell essen"], correct: 0, hint: "Schau auf den Satz über Wasser.", explain: "Der Text sagt, ausreichend Wasser zu trinken ist wichtig." },
    ],
  },

  // ===================== B2 =====================
  {
    level: "B2",
    title: "Die Digitalisierung der Arbeitswelt",
    text:
      "Die Digitalisierung verändert die Arbeitswelt in den letzten Jahren grundlegend. Immer mehr Unternehmen " +
      "setzen auf digitale Werkzeuge, um Arbeitsprozesse effizienter zu gestalten. Homeoffice ist für viele " +
      "Angestellte mittlerweile zur Normalität geworden, was mehr Flexibilität, aber auch neue Herausforderungen " +
      "mit sich bringt. Kritiker weisen darauf hin, dass die ständige Erreichbarkeit über digitale Geräte die " +
      "Grenze zwischen Arbeit und Freizeit verschwimmen lässt. Dennoch sehen viele Experten in der " +
      "Digitalisierung eine große Chance, um Innovation und Produktivität zu fördern. Wie sich die Arbeitswelt " +
      "langfristig entwickeln wird, bleibt jedoch abzuwarten.",
    questions: [
      { q: "Was verändert die Arbeitswelt laut Text grundlegend?", options: ["Der Klimawandel", "Die Digitalisierung", "Die Globalisierung", "Die Inflation"], correct: 1, hint: "Schau auf den ersten Satz.", explain: "Der Text sagt: \"Die Digitalisierung verändert die Arbeitswelt...grundlegend.\"" },
      { q: "Wozu setzen Unternehmen digitale Werkzeuge ein?", options: ["Um Kosten zu verstecken", "Um Arbeitsprozesse effizienter zu gestalten", "Um weniger Mitarbeiter einzustellen", "Um Steuern zu sparen"], correct: 1, hint: "Schau auf den zweiten Satz.", explain: "Der Text sagt, sie nutzen digitale Werkzeuge, \"um Arbeitsprozesse effizienter zu gestalten.\"" },
      { q: "Was bringt Homeoffice laut Text mit sich?", options: ["Nur Vorteile", "Mehr Flexibilität, aber auch neue Herausforderungen", "Nur Nachteile", "Weniger Arbeit"], correct: 1, hint: "Schau auf den Satz über Homeoffice.", explain: "Der Text sagt, es bringe \"mehr Flexibilität, aber auch neue Herausforderungen mit sich.\"" },
      { q: "Worauf weisen Kritiker hin?", options: ["Dass digitale Geräte zu teuer sind", "Dass die Grenze zwischen Arbeit und Freizeit verschwimmt", "Dass Homeoffice verboten werden sollte", "Dass Unternehmen zu langsam digitalisieren"], correct: 1, hint: "Schau auf den Satz über Kritiker.", explain: "Der Text sagt, ständige Erreichbarkeit \"lässt die Grenze zwischen Arbeit und Freizeit verschwimmen.\"" },
      { q: "Wie bewerten viele Experten die Digitalisierung laut Text?", options: ["Als Bedrohung", "Als große Chance für Innovation und Produktivität", "Als unwichtig", "Als vorübergehenden Trend"], correct: 1, hint: "Schau auf den Satz vor dem letzten Satz.", explain: "Der Text sagt, Experten sehen darin \"eine große Chance, um Innovation und Produktivität zu fördern.\"" },
      { q: "Was ist für viele Angestellte zur Normalität geworden?", options: ["Nachtarbeit", "Homeoffice", "Geschäftsreisen", "Überstunden"], correct: 1, hint: "Schau auf den Satz über Angestellte.", explain: "Der Text sagt, Homeoffice ist für viele zur Normalität geworden." },
      { q: "Was lässt die ständige Erreichbarkeit laut Kritikern verschwimmen?", options: ["Die Grenze zwischen Arbeit und Freizeit", "Die Löhne", "Die Arbeitszeiten der Firma", "Die Technik"], correct: 0, hint: "Schau auf den Satz über Kritiker.", explain: "Der Text sagt, die Grenze zwischen Arbeit und Freizeit verschwimmt." },
      { q: "Was möchten Unternehmen mit digitalen Werkzeugen erreichen?", options: ["Weniger Gewinn", "Effizientere Arbeitsprozesse", "Mehr Papier", "Längere Pausen"], correct: 1, hint: "Schau auf den zweiten Satz.", explain: "Sie wollen Arbeitsprozesse effizienter gestalten." },
      { q: "Wie sicher ist die langfristige Entwicklung der Arbeitswelt laut Text?", options: ["Ganz sicher", "Sie bleibt abzuwarten", "Sie ist bereits abgeschlossen", "Sie wird sich nicht ändern"], correct: 1, hint: "Schau auf den letzten Satz.", explain: "Der Text sagt, wie sich die Arbeitswelt entwickelt, bleibt abzuwarten." },
      { q: "Welche Chance sehen Experten in der Digitalisierung?", options: ["Weniger Arbeit", "Innovation und Produktivität zu fördern", "Höhere Steuern", "Weniger Technik"], correct: 1, hint: "Schau auf den vorletzten Satz.", explain: "Experten sehen eine Chance, Innovation und Produktivität zu fördern." },
    ],
  },
  {
    level: "B2",
    title: "Erneuerbare Energien in Deutschland",
    text:
      "Deutschland hat sich zum Ziel gesetzt, seinen Energieverbrauch zunehmend aus erneuerbaren Quellen zu " +
      "decken. Wind- und Solarenergie spielen dabei eine zentrale Rolle, da sie im Gegensatz zu fossilen " +
      "Brennstoffen keine schädlichen Treibhausgase ausstoßen. Der Ausbau von Windparks stößt jedoch nicht " +
      "überall auf Zustimmung, da manche Anwohner den Anblick der Anlagen sowie die Geräuschentwicklung als " +
      "störend empfinden. Zusätzlich stellt die Speicherung von überschüssiger Energie weiterhin eine " +
      "technische Herausforderung dar. Trotz dieser Hürden gilt die Energiewende als notwendiger Schritt, um " +
      "die Klimaziele des Landes zu erreichen.",
    questions: [
      { q: "Was hat sich Deutschland laut Text zum Ziel gesetzt?", options: ["Weniger Energie zu verbrauchen", "Den Energieverbrauch zunehmend aus erneuerbaren Quellen zu decken", "Mehr Kohle zu nutzen", "Energie zu exportieren"], correct: 1, hint: "Schau auf den ersten Satz.", explain: "Der Text sagt, Deutschland wolle den Verbrauch \"zunehmend aus erneuerbaren Quellen zu decken.\"" },
      { q: "Warum spielen Wind- und Solarenergie eine zentrale Rolle?", options: ["Sie sind billiger als Kohle", "Sie stoßen keine schädlichen Treibhausgase aus", "Sie brauchen keine Technik", "Sie sind einfacher zu bauen"], correct: 1, hint: "Schau auf den zweiten Satz.", explain: "Der Text sagt, sie \"stoßen keine schädlichen Treibhausgase aus.\"" },
      { q: "Warum stößt der Ausbau von Windparks nicht überall auf Zustimmung?", options: ["Er ist zu teuer", "Manche Anwohner stört der Anblick und die Geräusche", "Er ist gesetzlich verboten", "Es gibt zu wenig Wind"], correct: 1, hint: "Schau auf den Satz über Windparks.", explain: "Der Text sagt, manche Anwohner empfinden \"den Anblick der Anlagen sowie die Geräuschentwicklung als störend.\"" },
      { q: "Welche technische Herausforderung wird im Text genannt?", options: ["Der Transport von Kohle", "Die Speicherung von überschüssiger Energie", "Der Bau neuer Straßen", "Die Ausbildung von Fachkräften"], correct: 1, hint: "Schau auf den Satz über Speicherung.", explain: "Der Text sagt, \"die Speicherung von überschüssiger Energie\" stelle eine Herausforderung dar." },
      { q: "Wie wird die Energiewende im letzten Satz beschrieben?", options: ["Als unnötig", "Als notwendiger Schritt für die Klimaziele", "Als gescheitert", "Als zu teuer"], correct: 1, hint: "Schau auf den letzten Satz.", explain: "Der Text sagt, sie gelte \"als notwendiger Schritt, um die Klimaziele des Landes zu erreichen.\"" },
      { q: "Welche zwei Energiearten spielen eine zentrale Rolle?", options: ["Kohle und Öl", "Wind- und Solarenergie", "Gas und Atomkraft", "Holz und Torf"], correct: 1, hint: "Schau auf den zweiten Satz.", explain: "Der Text nennt Wind- und Solarenergie." },
      { q: "Was empfinden manche Anwohner als störend?", options: ["Die niedrigen Preise", "Den Anblick und die Geräusche der Anlagen", "Die saubere Luft", "Die neuen Arbeitsplätze"], correct: 1, hint: "Schau auf den Satz über Windparks.", explain: "Manche Anwohner empfinden Anblick und Geräusche als störend." },
      { q: "Womit werden erneuerbare Energien im Text verglichen?", options: ["Mit fossilen Brennstoffen", "Mit Wasserkraft", "Mit Kernkraft", "Mit Biogas"], correct: 0, hint: "Schau auf den zweiten Satz.", explain: "Der Text vergleicht sie mit fossilen Brennstoffen." },
      { q: "Was ist weiterhin technisch schwierig?", options: ["Der Bau von Straßen", "Die Speicherung überschüssiger Energie", "Die Ausbildung von Ärzten", "Der Transport von Wasser"], correct: 1, hint: "Schau auf den Satz über Speicherung.", explain: "Die Speicherung überschüssiger Energie ist eine technische Herausforderung." },
      { q: "Wofür gilt die Energiewende als notwendig?", options: ["Um die Klimaziele zu erreichen", "Um Energie zu exportieren", "Um Kohle zu fördern", "Um Steuern zu senken"], correct: 0, hint: "Schau auf den letzten Satz.", explain: "Die Energiewende gilt als notwendiger Schritt für die Klimaziele." },
    ],
  },

  // ===================== C1 =====================
  {
    level: "C1",
    title: "Die Bedeutung der Pressefreiheit",
    text:
      "Die Pressefreiheit gilt als eines der fundamentalen Prinzipien einer demokratischen Gesellschaft, da " +
      "sie es Journalisten ermöglicht, Missstände aufzudecken und die Öffentlichkeit unabhängig zu " +
      "informieren. In vielen Ländern wird dieses Recht jedoch zunehmend eingeschränkt, sei es durch " +
      "staatliche Zensur, wirtschaftlichen Druck auf Medienunternehmen oder gezielte Einschüchterung " +
      "einzelner Journalisten. Organisationen wie Reporter ohne Grenzen dokumentieren solche Verstöße und " +
      "setzen sich weltweit für den Schutz von Medienschaffenden ein. Kritiker warnen zudem davor, dass die " +
      "zunehmende Konzentration von Medienbesitz in den Händen weniger Konzerne die Meinungsvielfalt " +
      "gefährden könnte. Eine freie und pluralistische Presse bleibt daher unverzichtbar, um Machtmissbrauch " +
      "zu verhindern und den öffentlichen Diskurs lebendig zu halten.",
    questions: [
      { q: "Warum gilt die Pressefreiheit als fundamentales Prinzip?", options: ["Weil sie Journalisten reich macht", "Weil sie es ermöglicht, Missstände aufzudecken und unabhängig zu informieren", "Weil sie von der Regierung kontrolliert wird", "Weil sie Werbeeinnahmen sichert"], correct: 1, hint: "Schau auf den ersten Satz.", explain: "Der Text sagt, sie ermögliche es, \"Missstände aufzudecken und die Öffentlichkeit unabhängig zu informieren.\"" },
      { q: "Wodurch wird die Pressefreiheit laut Text eingeschränkt?", options: ["Nur durch Zensur", "Durch Zensur, wirtschaftlichen Druck und Einschüchterung", "Nur durch wirtschaftlichen Druck", "Durch fehlende Leser"], correct: 1, hint: "Schau auf den zweiten Satz.", explain: "Der Text nennt \"staatliche Zensur, wirtschaftlichen Druck...oder gezielte Einschüchterung.\"" },
      { q: "Was macht die Organisation Reporter ohne Grenzen?", options: ["Sie zensiert Medien", "Sie dokumentiert Verstöße und schützt Medienschaffende", "Sie besitzt Zeitungen", "Sie bildet Journalisten aus"], correct: 1, hint: "Schau auf den Satz über Reporter ohne Grenzen.", explain: "Der Text sagt, sie \"dokumentieren solche Verstöße und setzen sich...für den Schutz von Medienschaffenden ein.\"" },
      { q: "Wovor warnen Kritiker laut Text?", options: ["Vor zu vielen Zeitungen", "Vor der Konzentration von Medienbesitz in wenigen Händen", "Vor zu freier Presse", "Vor zu billigen Nachrichten"], correct: 1, hint: "Schau auf den Satz über Kritiker.", explain: "Der Text sagt, die Konzentration von Medienbesitz \"in den Händen weniger Konzerne die Meinungsvielfalt gefährden könnte.\"" },
      { q: "Was bleibt laut dem letzten Satz unverzichtbar?", options: ["Staatliche Kontrolle der Presse", "Eine freie und pluralistische Presse", "Werbung in Zeitungen", "Zensur im Internet"], correct: 1, hint: "Schau auf den letzten Satz.", explain: "Der Text sagt: \"Eine freie und pluralistische Presse bleibt daher unverzichtbar.\"" },
      { q: "Was ermöglicht die Pressefreiheit den Journalisten?", options: ["Reich zu werden", "Missstände aufzudecken und unabhängig zu informieren", "Die Regierung zu kontrollieren", "Werbung zu verkaufen"], correct: 1, hint: "Schau auf den ersten Satz.", explain: "Sie ermöglicht es, Missstände aufzudecken und unabhängig zu informieren." },
      { q: "Nenne eine Form der Einschränkung der Pressefreiheit.", options: ["Kostenlose Zeitungen", "Staatliche Zensur", "Mehr Leser", "Neue Drucktechnik"], correct: 1, hint: "Schau auf den zweiten Satz.", explain: "Der Text nennt staatliche Zensur als eine Form." },
      { q: "Wofür setzt sich Reporter ohne Grenzen ein?", options: ["Für den Schutz von Medienschaffenden", "Für höhere Preise", "Für weniger Zeitungen", "Für staatliche Kontrolle"], correct: 0, hint: "Schau auf den Satz über die Organisation.", explain: "Die Organisation setzt sich für den Schutz von Medienschaffenden ein." },
      { q: "Was könnte die Konzentration von Medienbesitz gefährden?", options: ["Die Meinungsvielfalt", "Die Druckqualität", "Die Lesegeschwindigkeit", "Die Werbung"], correct: 0, hint: "Schau auf den Satz über Kritiker.", explain: "Sie könnte die Meinungsvielfalt gefährden." },
      { q: "Was soll eine freie Presse verhindern?", options: ["Machtmissbrauch", "Wirtschaftswachstum", "Bildung", "Tourismus"], correct: 0, hint: "Schau auf den letzten Satz.", explain: "Eine freie Presse soll Machtmissbrauch verhindern." },
    ],
  },
  {
    level: "C1",
    title: "Künstliche Intelligenz und Arbeitsmarkt",
    text:
      "Die rasante Entwicklung künstlicher Intelligenz wirft grundlegende Fragen über die Zukunft des " +
      "Arbeitsmarktes auf. Befürworter argumentieren, dass KI-Systeme repetitive und gefährliche Tätigkeiten " +
      "übernehmen können, wodurch Menschen mehr Zeit für kreative und zwischenmenschliche Aufgaben gewinnen. " +
      "Gleichzeitig befürchten Kritiker, dass ganze Berufsfelder, insbesondere in der Verwaltung und " +
      "Produktion, in absehbarer Zeit überflüssig werden könnten. Ökonomen betonen, dass historische " +
      "Erfahrungen mit technologischem Wandel zwar häufig zu Verschiebungen, aber selten zu dauerhaft " +
      "höherer Arbeitslosigkeit geführt haben. Dennoch erfordert der Übergang erhebliche Investitionen in " +
      "Weiterbildung, damit Arbeitnehmer neue Kompetenzen erwerben können. Wie sich diese Entwicklung " +
      "letztlich auf die Gesellschaft auswirken wird, hängt maßgeblich von politischen und wirtschaftlichen " +
      "Weichenstellungen ab.",
    questions: [
      { q: "Welche Fragen wirft die Entwicklung künstlicher Intelligenz laut Text auf?", options: ["Fragen über die Zukunft des Arbeitsmarktes", "Fragen über das Wetter", "Fragen über die Raumfahrt", "Fragen über die Landwirtschaft"], correct: 0, hint: "Schau auf den ersten Satz.", explain: "Der Text sagt, die Entwicklung \"wirft grundlegende Fragen über die Zukunft des Arbeitsmarktes auf.\"" },
      { q: "Was argumentieren Befürworter der KI laut Text?", options: ["Dass KI Menschen ersetzen sollte", "Dass KI repetitive und gefährliche Tätigkeiten übernehmen kann", "Dass KI Arbeitsplätze vernichtet", "Dass KI zu teuer ist"], correct: 1, hint: "Schau auf den Satz über Befürworter.", explain: "Der Text sagt, KI könne \"repetitive und gefährliche Tätigkeiten übernehmen.\"" },
      { q: "Was befürchten Kritiker laut Text?", options: ["Dass KI zu langsam ist", "Dass ganze Berufsfelder überflüssig werden könnten", "Dass KI nie richtig funktionieren wird", "Dass Löhne steigen werden"], correct: 1, hint: "Schau auf den Satz über Kritiker.", explain: "Der Text sagt, Kritiker befürchten, Berufsfelder \"könnten...überflüssig werden.\"" },
      { q: "Was zeigen historische Erfahrungen laut den Ökonomen im Text?", options: ["Technologischer Wandel führt selten zu dauerhaft höherer Arbeitslosigkeit", "Technologischer Wandel führt immer zu Massenarbeitslosigkeit", "Technologie hat noch nie den Arbeitsmarkt verändert", "Ökonomen sind sich nicht einig"], correct: 0, hint: "Schau auf den Satz über Ökonomen.", explain: "Der Text sagt, Wandel habe \"selten zu dauerhaft höherer Arbeitslosigkeit geführt.\"" },
      { q: "Was erfordert der Übergang laut Text?", options: ["Weniger Regulierung", "Erhebliche Investitionen in Weiterbildung", "Höhere Steuern", "Weniger Technologie"], correct: 1, hint: "Schau auf den vorletzten Satz.", explain: "Der Text sagt, der Übergang erfordere \"erhebliche Investitionen in Weiterbildung.\"" },
      { q: "Welche Tätigkeiten könnte KI laut Befürwortern übernehmen?", options: ["Nur kreative Arbeit", "Repetitive und gefährliche Tätigkeiten", "Alle menschlichen Aufgaben", "Keine Aufgaben"], correct: 1, hint: "Schau auf den Satz über Befürworter.", explain: "KI könnte repetitive und gefährliche Tätigkeiten übernehmen." },
      { q: "Welche Berufsfelder könnten laut Kritikern überflüssig werden?", options: ["Kunst und Musik", "Verwaltung und Produktion", "Sport und Freizeit", "Landwirtschaft"], correct: 1, hint: "Schau auf den Satz über Kritiker.", explain: "Der Text nennt Verwaltung und Produktion." },
      { q: "Was gewinnen Menschen laut Befürwortern durch KI?", options: ["Mehr Zeit für kreative und zwischenmenschliche Aufgaben", "Mehr Geld", "Weniger Urlaub", "Weniger Verantwortung"], correct: 0, hint: "Schau auf den Satz über Befürworter.", explain: "Menschen gewinnen mehr Zeit für kreative und zwischenmenschliche Aufgaben." },
      { q: "Wovon hängt die Auswirkung der KI laut Text maßgeblich ab?", options: ["Vom Wetter", "Von politischen und wirtschaftlichen Weichenstellungen", "Vom Zufall", "Von der Bevölkerungszahl"], correct: 1, hint: "Schau auf den letzten Satz.", explain: "Es hängt von politischen und wirtschaftlichen Weichenstellungen ab." },
      { q: "Was ist für den Übergang laut Text nötig?", options: ["Weniger Bildung", "Investitionen in Weiterbildung", "Höhere Löhne", "Mehr Maschinen"], correct: 1, hint: "Schau auf den vorletzten Satz.", explain: "Der Übergang erfordert Investitionen in Weiterbildung." },
    ],
  },

  // ===================== C2 =====================
  {
    level: "C2",
    title: "Die Krise der repräsentativen Demokratie",
    text:
      "In zahlreichen westlichen Demokratien lässt sich seit geraumer Zeit ein wachsendes Misstrauen " +
      "gegenüber etablierten politischen Institutionen beobachten, das sich unter anderem in sinkender " +
      "Wahlbeteiligung und dem Erstarken populistischer Bewegungen manifestiert. Politikwissenschaftler " +
      "führen dieses Phänomen auf ein Bündel miteinander verwobener Ursachen zurück: die wachsende Kluft " +
      "zwischen wirtschaftlichen Gewinnern und Verlierern der Globalisierung, das Gefühl mangelnder " +
      "politischer Teilhabe sowie eine zunehmend fragmentierte mediale Öffentlichkeit, in der gemeinsame " +
      "Faktengrundlagen zu erodieren scheinen. Einige Theoretiker plädieren daher für eine Erweiterung " +
      "repräsentativer Verfahren um deliberative Elemente, etwa Bürgerräte, um das Vertrauen in " +
      "demokratische Prozesse wiederherzustellen. Andere hingegen warnen davor, dass eine Überfrachtung des " +
      "politischen Systems mit immer neuen Partizipationsformen zu Entscheidungslähmung führen könnte, ohne " +
      "die zugrunde liegenden Legitimationsprobleme tatsächlich zu lösen. Die Frage, wie sich " +
      "Reaktionsfähigkeit und Stabilität demokratischer Systeme miteinander vereinbaren lassen, bleibt " +
      "folglich eine der zentralen Herausforderungen der Gegenwart.",
    questions: [
      { q: "Was lässt sich laut Text in zahlreichen westlichen Demokratien beobachten?", options: ["Steigendes Vertrauen in Institutionen", "Wachsendes Misstrauen gegenüber etablierten politischen Institutionen", "Ein Rückgang populistischer Bewegungen", "Eine Zunahme der Wahlbeteiligung"], correct: 1, hint: "Schau auf den ersten Satz.", explain: "Der Text spricht von einem \"wachsenden Misstrauen gegenüber etablierten politischen Institutionen.\"" },
      { q: "Welche Ursache nennen Politikwissenschaftler NICHT für dieses Phänomen?", options: ["Die Kluft zwischen Globalisierungsgewinnern und -verlierern", "Das Gefühl mangelnder politischer Teilhabe", "Eine fragmentierte mediale Öffentlichkeit", "Ein Mangel an politischen Parteien"], correct: 3, hint: "Schau auf die Liste der Ursachen im zweiten Satz.", explain: "Der Text nennt drei Ursachen, aber \"ein Mangel an politischen Parteien\" wird nicht erwähnt." },
      { q: "Wofür plädieren manche Theoretiker laut Text?", options: ["Für die Abschaffung von Wahlen", "Für eine Erweiterung repräsentativer Verfahren um deliberative Elemente", "Für weniger Bürgerbeteiligung", "Für mehr Zensur der Medien"], correct: 1, hint: "Schau auf den Satz über Theoretiker.", explain: "Der Text sagt, sie plädieren für \"eine Erweiterung repräsentativer Verfahren um deliberative Elemente, etwa Bürgerräte.\"" },
      { q: "Wovor warnen andere Theoretiker laut Text?", options: ["Vor zu wenig Partizipation", "Vor Entscheidungslähmung durch zu viele neue Partizipationsformen", "Vor zu stabilen Regierungen", "Vor sinkenden Wahlkosten"], correct: 1, hint: "Schau auf den Satz, der mit 'Andere hingegen' beginnt.", explain: "Der Text sagt, eine Überfrachtung könnte \"zu Entscheidungslähmung führen.\"" },
      { q: "Was bleibt laut dem letzten Satz eine zentrale Herausforderung?", options: ["Die Vereinbarkeit von Reaktionsfähigkeit und Stabilität demokratischer Systeme", "Die Abschaffung der Demokratie", "Die Finanzierung von Wahlkämpfen", "Die Digitalisierung der Verwaltung"], correct: 0, hint: "Schau auf den letzten Satz.", explain: "Der Text sagt, dies bleibe \"eine der zentralen Herausforderungen der Gegenwart.\"" },
      { q: "Worin manifestiert sich das Misstrauen unter anderem?", options: ["In steigender Wahlbeteiligung", "In sinkender Wahlbeteiligung und Populismus", "In mehr Parteien", "In höheren Steuern"], correct: 1, hint: "Schau auf den ersten Satz.", explain: "Es manifestiert sich in sinkender Wahlbeteiligung und dem Erstarken populistischer Bewegungen." },
      { q: "Was ist ein Beispiel für ein deliberatives Element?", options: ["Bürgerräte", "Wahlen abschaffen", "Zensur", "Mehr Werbung"], correct: 0, hint: "Schau auf den Satz über Theoretiker.", explain: "Der Text nennt Bürgerräte als Beispiel." },
      { q: "Was könnte laut manchen Theoretikern zu Entscheidungslähmung führen?", options: ["Zu wenige Wahlen", "Eine Überfrachtung mit neuen Partizipationsformen", "Weniger Bürgerbeteiligung", "Zu stabile Regierungen"], correct: 1, hint: "Schau auf den Satz mit 'Andere hingegen'.", explain: "Eine Überfrachtung des Systems könnte zu Entscheidungslähmung führen." },
      { q: "Was scheint in der fragmentierten Öffentlichkeit zu erodieren?", options: ["Gemeinsame Faktengrundlagen", "Die Wirtschaft", "Die Bevölkerung", "Die Technik"], correct: 0, hint: "Schau auf den Satz über die mediale Öffentlichkeit.", explain: "Gemeinsame Faktengrundlagen scheinen zu erodieren." },
      { q: "Worauf führen Politikwissenschaftler das Phänomen zurück?", options: ["Auf eine einzige Ursache", "Auf ein Bündel verwobener Ursachen", "Auf das Wetter", "Auf Zufall"], correct: 1, hint: "Schau auf den zweiten Satz.", explain: "Sie führen es auf ein Bündel miteinander verwobener Ursachen zurück." },
    ],
  },
  {
    level: "C2",
    title: "Sprachwandel und Sprachpurismus",
    text:
      "Sprachen befinden sich in einem fortwährenden Wandel, der sich in Lautverschiebungen, semantischen " +
      "Bedeutungsverschiebungen sowie der kontinuierlichen Aufnahme von Lehnwörtern aus anderen Sprachen " +
      "manifestiert. Sprachpuristische Strömungen, die eine vermeintlich reine oder ursprüngliche Form einer " +
      "Sprache bewahren wollen, existieren in nahezu jeder Sprachgemeinschaft und richten sich häufig gegen " +
      "Anglizismen oder andere als fremd empfundene Einflüsse. Linguisten weisen jedoch darauf hin, dass " +
      "dieser Wandel keineswegs ein Zeichen von Verfall darstellt, sondern vielmehr ein universelles, seit " +
      "Jahrtausenden zu beobachtendes Merkmal lebendiger Sprachsysteme ist. Tatsächlich lässt sich kaum eine " +
      "historische Sprachstufe identifizieren, die nicht selbst das Ergebnis vorangegangener Entlehnungs- " +
      "und Wandelprozesse gewesen wäre. Die Ablehnung von Sprachwandel beruht daher häufiger auf " +
      "soziokulturellen Ängsten vor Identitätsverlust als auf tatsächlich nachweisbaren sprachlichen " +
      "Qualitätsverlusten. Sprachwissenschaftler plädieren folglich meist für eine deskriptive statt einer " +
      "präskriptiven Herangehensweise an sprachliche Normen.",
    questions: [
      { q: "Worin manifestiert sich der fortwährende Sprachwandel laut Text?", options: ["Nur in neuen Wörtern", "In Lautverschiebungen, Bedeutungsverschiebungen und Lehnwörtern", "Nur in der Grammatik", "Nur in der Aussprache"], correct: 1, hint: "Schau auf den ersten Satz.", explain: "Der Text nennt \"Lautverschiebungen, semantische Bedeutungsverschiebungen sowie...Lehnwörter.\"" },
      { q: "Wogegen richten sich sprachpuristische Strömungen häufig?", options: ["Gegen alte Wörter", "Gegen Anglizismen oder andere fremde Einflüsse", "Gegen die Grammatik", "Gegen Dialekte"], correct: 1, hint: "Schau auf den zweiten Satz.", explain: "Der Text sagt, sie richten sich \"häufig gegen Anglizismen oder andere als fremd empfundene Einflüsse.\"" },
      { q: "Wie bewerten Linguisten den Sprachwandel laut Text?", options: ["Als Zeichen von Verfall", "Als universelles Merkmal lebendiger Sprachsysteme", "Als Fehler der jüngeren Generation", "Als vermeidbares Problem"], correct: 1, hint: "Schau auf den Satz, der mit 'Linguisten' beginnt.", explain: "Der Text sagt, es sei \"ein universelles...Merkmal lebendiger Sprachsysteme.\"" },
      { q: "Worauf beruht die Ablehnung von Sprachwandel laut Text meistens?", options: ["Auf nachweisbaren Qualitätsverlusten", "Auf soziokulturellen Ängsten vor Identitätsverlust", "Auf wissenschaftlichen Studien", "Auf wirtschaftlichen Gründen"], correct: 1, hint: "Schau auf den vorletzten Satz.", explain: "Der Text sagt, sie beruhe \"häufiger auf soziokulturellen Ängsten vor Identitätsverlust.\"" },
      { q: "Wofür plädieren Sprachwissenschaftler laut dem letzten Satz?", options: ["Für eine präskriptive Herangehensweise", "Für eine deskriptive statt präskriptive Herangehensweise", "Für die Abschaffung von Sprachregeln", "Für mehr Anglizismen"], correct: 1, hint: "Schau auf den letzten Satz.", explain: "Der Text sagt, sie plädieren \"für eine deskriptive statt einer präskriptiven Herangehensweise.\"" },
      { q: "Was wollen sprachpuristische Strömungen bewahren?", options: ["Eine vermeintlich reine Form der Sprache", "Neue Wörter", "Fremde Einflüsse", "Dialekte"], correct: 0, hint: "Schau auf den zweiten Satz.", explain: "Sie wollen eine vermeintlich reine oder ursprüngliche Form bewahren." },
      { q: "Stellt Sprachwandel laut Linguisten einen Verfall dar?", options: ["Ja, immer", "Nein, keineswegs", "Nur bei jungen Menschen", "Nur in der Schrift"], correct: 1, hint: "Schau auf den Satz über Linguisten.", explain: "Linguisten sagen, der Wandel stellt keineswegs einen Verfall dar." },
      { q: "Was ist fast jede historische Sprachstufe laut Text?", options: ["Das Ergebnis früherer Entlehnungs- und Wandelprozesse", "Völlig unverändert", "Eine reine Erfindung", "Ohne Lehnwörter"], correct: 0, hint: "Schau auf den Satz über historische Sprachstufen.", explain: "Kaum eine Sprachstufe ist nicht selbst Ergebnis früherer Prozesse." },
      { q: "Wo existieren sprachpuristische Strömungen laut Text?", options: ["Nur in Deutschland", "In nahezu jeder Sprachgemeinschaft", "Nur in Europa", "Nirgendwo mehr"], correct: 1, hint: "Schau auf den zweiten Satz.", explain: "Sie existieren in nahezu jeder Sprachgemeinschaft." },
      { q: "Welche Herangehensweise bevorzugen Sprachwissenschaftler?", options: ["Eine präskriptive", "Eine deskriptive", "Gar keine", "Eine strenge"], correct: 1, hint: "Schau auf den letzten Satz.", explain: "Sie bevorzugen eine deskriptive statt einer präskriptiven Herangehensweise." },
    ],
  },
  {
    level: "A1",
    title: "Mein Haustier",
    text:
      "Ich habe eine Katze. Sie heißt Minka. Minka ist schwarz und weiß. Sie ist drei Jahre alt. Jeden Morgen gibt mir Minka einen Kuss. Sie schläft gern auf dem Sofa. Am Abend spielt sie mit einem kleinen Ball. Ich liebe meine Katze sehr.",
    questions: [
      { q: "Was für ein Haustier hat der Autor?", options: ["Einen Hund", "Eine Katze", "Einen Vogel", "Ein Kaninchen"], correct: 1, hint: "Sieh dir den ersten Satz an.", explain: "Im Text steht, dass der Autor eine Katze hat." },
      { q: "Wie heißt die Katze?", options: ["Mimi", "Minka", "Molly", "Lucky"], correct: 1, hint: "Sieh dir den zweiten Satz an.", explain: "Die Katze heißt Minka." },
      { q: "Welche Farben hat Minka?", options: ["Nur braun", "Schwarz und weiß", "Grau", "Orange"], correct: 1, hint: "Sieh dir den dritten Satz an.", explain: "Minka ist schwarz und weiß." },
      { q: "Wie alt ist Minka?", options: ["Ein Jahr", "Drei Jahre", "Fünf Jahre", "Zehn Jahre"], correct: 1, hint: "Sieh dir den Satz über das Alter an.", explain: "Minka ist drei Jahre alt." },
      { q: "Was macht Minka jeden Morgen?", options: ["Sie schläft", "Sie gibt einen Kuss", "Sie isst", "Sie läuft weg"], correct: 1, hint: "Sieh dir den Satz über den Morgen an.", explain: "Jeden Morgen gibt Minka einen Kuss." },
      { q: "Wo schläft Minka gern?", options: ["Im Bett", "Auf dem Sofa", "Im Garten", "Auf dem Stuhl"], correct: 1, hint: "Sieh dir den Satz über das Schlafen an.", explain: "Minka schläft gern auf dem Sofa." },
      { q: "Womit spielt Minka am Abend?", options: ["Mit einer Maus", "Mit einem kleinen Ball", "Mit einem Seil", "Mit Papier"], correct: 1, hint: "Sieh dir den Satz über den Abend an.", explain: "Am Abend spielt sie mit einem kleinen Ball." },
      { q: "Wann spielt Minka mit dem Ball?", options: ["Am Morgen", "Am Mittag", "Am Abend", "In der Nacht"], correct: 2, hint: "Sieh dir den Satz über den Ball an.", explain: "Sie spielt am Abend mit dem Ball." },
      { q: "Wie findet der Autor die Katze?", options: ["Er mag sie nicht", "Er liebt sie sehr", "Er hat Angst", "Er ignoriert sie"], correct: 1, hint: "Sieh dir den letzten Satz an.", explain: "Der Autor liebt seine Katze sehr." },
      { q: "Hat der Autor ein Haustier?", options: ["Nein", "Ja, eine Katze", "Zwei Hunde", "Einen Fisch"], correct: 1, hint: "Sieh dir den ersten Satz an.", explain: "Der Autor hat eine Katze als Haustier." },
    ],
  },
  {
    level: "A1",
    title: "Im Café",
    text:
      "Am Samstag gehe ich gern ins Café. Das Café ist klein und gemütlich. Ich bestelle immer einen Kaffee und ein Stück Kuchen. Der Kuchen schmeckt sehr gut. Meine Freundin trinkt einen Tee. Wir sitzen am Fenster und reden viel. Manchmal lesen wir auch ein Buch. Das Café ist mein Lieblingsort.",
    questions: [
      { q: "Wann geht der Autor gern ins Café?", options: ["Am Montag", "Am Samstag", "Am Sonntag", "Am Freitag"], correct: 1, hint: "Sieh dir den ersten Satz an.", explain: "Der Autor geht am Samstag gern ins Café." },
      { q: "Wie ist das Café?", options: ["Groß und laut", "Klein und gemütlich", "Alt und kalt", "Neu und teuer"], correct: 1, hint: "Sieh dir den zweiten Satz an.", explain: "Das Café ist klein und gemütlich." },
      { q: "Was bestellt der Autor immer?", options: ["Nur Wasser", "Einen Kaffee und ein Stück Kuchen", "Eine Suppe", "Einen Saft"], correct: 1, hint: "Sieh dir den dritten Satz an.", explain: "Der Autor bestellt immer einen Kaffee und ein Stück Kuchen." },
      { q: "Wie schmeckt der Kuchen?", options: ["Sehr gut", "Schlecht", "Zu süß", "Salzig"], correct: 0, hint: "Sieh dir den Satz über den Kuchen an.", explain: "Der Kuchen schmeckt sehr gut." },
      { q: "Was trinkt die Freundin?", options: ["Einen Kaffee", "Einen Tee", "Eine Limonade", "Milch"], correct: 1, hint: "Sieh dir den Satz über die Freundin an.", explain: "Die Freundin trinkt einen Tee." },
      { q: "Wo sitzen sie?", options: ["An der Tür", "Am Fenster", "In der Ecke", "Draußen"], correct: 1, hint: "Sieh dir den Satz über das Sitzen an.", explain: "Sie sitzen am Fenster." },
      { q: "Was machen sie am Fenster?", options: ["Sie schlafen", "Sie reden viel", "Sie arbeiten", "Sie singen"], correct: 1, hint: "Sieh dir den Satz über das Fenster an.", explain: "Sie reden viel." },
      { q: "Was machen sie manchmal auch?", options: ["Sie tanzen", "Sie lesen ein Buch", "Sie kochen", "Sie malen"], correct: 1, hint: "Sieh dir den vorletzten Satz an.", explain: "Manchmal lesen sie auch ein Buch." },
      { q: "Was ist das Café für den Autor?", options: ["Ein langweiliger Ort", "Sein Lieblingsort", "Ein teurer Ort", "Ein neuer Ort"], correct: 1, hint: "Sieh dir den letzten Satz an.", explain: "Das Café ist der Lieblingsort des Autors." },
      { q: "Trinkt der Autor Kaffee?", options: ["Nein", "Ja", "Nur Tee", "Nur Wasser"], correct: 1, hint: "Sieh dir den dritten Satz an.", explain: "Der Autor bestellt immer einen Kaffee." },
    ],
  },
  {
    level: "A2",
    title: "Ein Ausflug in die Berge",
    text:
      "Letztes Wochenende machte ich mit meinen Freunden einen Ausflug in die Berge. Wir fuhren früh am Morgen mit dem Zug los. Das Wetter war sonnig und warm. Wir wanderten drei Stunden bis zu einem kleinen See. Am See machten wir ein Picknick und aßen belegte Brote. Später sahen wir sogar einige Ziegen. Am Abend waren wir müde, aber sehr glücklich. Es war ein toller Tag in der Natur.",
    questions: [
      { q: "Wohin ging der Ausflug?", options: ["Ans Meer", "In die Berge", "In die Stadt", "In den Wald"], correct: 1, hint: "Sieh dir den ersten Satz an.", explain: "Der Ausflug ging in die Berge." },
      { q: "Wann fuhren sie los?", options: ["Am Abend", "Früh am Morgen", "Am Mittag", "In der Nacht"], correct: 1, hint: "Sieh dir den zweiten Satz an.", explain: "Sie fuhren früh am Morgen los." },
      { q: "Womit fuhren sie?", options: ["Mit dem Auto", "Mit dem Zug", "Mit dem Bus", "Mit dem Fahrrad"], correct: 1, hint: "Sieh dir den zweiten Satz an.", explain: "Sie fuhren mit dem Zug." },
      { q: "Wie war das Wetter?", options: ["Regnerisch", "Sonnig und warm", "Kalt", "Windig"], correct: 1, hint: "Sieh dir den Satz über das Wetter an.", explain: "Das Wetter war sonnig und warm." },
      { q: "Wie lange wanderten sie?", options: ["Eine Stunde", "Drei Stunden", "Fünf Stunden", "Den ganzen Tag"], correct: 1, hint: "Sieh dir den Satz über das Wandern an.", explain: "Sie wanderten drei Stunden." },
      { q: "Wohin wanderten sie?", options: ["Zu einem kleinen See", "Zu einem Dorf", "Zu einer Hütte", "Zu einem Fluss"], correct: 0, hint: "Sieh dir den Satz über das Wandern an.", explain: "Sie wanderten zu einem kleinen See." },
      { q: "Was aßen sie beim Picknick?", options: ["Pizza", "Belegte Brote", "Kuchen", "Obst"], correct: 1, hint: "Sieh dir den Satz über das Picknick an.", explain: "Beim Picknick aßen sie belegte Brote." },
      { q: "Welche Tiere sahen sie?", options: ["Kühe", "Ziegen", "Pferde", "Schafe"], correct: 1, hint: "Sieh dir den Satz über die Tiere an.", explain: "Sie sahen einige Ziegen." },
      { q: "Wie fühlten sie sich am Abend?", options: ["Traurig", "Müde, aber glücklich", "Wütend", "Gelangweilt"], correct: 1, hint: "Sieh dir den vorletzten Satz an.", explain: "Am Abend waren sie müde, aber sehr glücklich." },
      { q: "Mit wem machte der Autor den Ausflug?", options: ["Allein", "Mit Freunden", "Mit der Familie", "Mit der Schule"], correct: 1, hint: "Sieh dir den ersten Satz an.", explain: "Der Autor machte den Ausflug mit Freunden." },
    ],
  },
  {
    level: "A2",
    title: "Mein Lieblingshobby",
    text:
      "Mein Lieblingshobby ist Fotografieren. Ich habe schon als Kind gern Bilder gemacht. Heute besitze ich eine gute Kamera. Am liebsten fotografiere ich die Natur und Tiere. An den Wochenenden gehe ich oft in den Park. Dort warte ich manchmal lange auf das perfekte Foto. Meine besten Bilder hänge ich an die Wand. Fotografieren macht mir viel Spaß.",
    questions: [
      { q: "Was ist das Lieblingshobby des Autors?", options: ["Malen", "Fotografieren", "Kochen", "Lesen"], correct: 1, hint: "Sieh dir den ersten Satz an.", explain: "Das Lieblingshobby ist Fotografieren." },
      { q: "Seit wann macht der Autor gern Bilder?", options: ["Seit letztem Jahr", "Schon als Kind", "Erst seit Kurzem", "Seit dem Studium"], correct: 1, hint: "Sieh dir den zweiten Satz an.", explain: "Der Autor macht schon als Kind gern Bilder." },
      { q: "Was besitzt der Autor heute?", options: ["Ein Fahrrad", "Eine gute Kamera", "Einen Computer", "Ein Auto"], correct: 1, hint: "Sieh dir den dritten Satz an.", explain: "Heute besitzt der Autor eine gute Kamera." },
      { q: "Was fotografiert der Autor am liebsten?", options: ["Autos", "Natur und Tiere", "Gebäude", "Menschen"], correct: 1, hint: "Sieh dir den vierten Satz an.", explain: "Am liebsten fotografiert der Autor Natur und Tiere." },
      { q: "Wohin geht der Autor am Wochenende?", options: ["Ins Kino", "In den Park", "Ins Café", "Zum Markt"], correct: 1, hint: "Sieh dir den Satz über das Wochenende an.", explain: "Am Wochenende geht der Autor oft in den Park." },
      { q: "Worauf wartet der Autor manchmal lange?", options: ["Auf den Bus", "Auf das perfekte Foto", "Auf Freunde", "Auf schönes Wetter"], correct: 1, hint: "Sieh dir den Satz über das Warten an.", explain: "Der Autor wartet manchmal lange auf das perfekte Foto." },
      { q: "Was macht der Autor mit den besten Bildern?", options: ["Er verkauft sie", "Er hängt sie an die Wand", "Er löscht sie", "Er verschenkt sie"], correct: 1, hint: "Sieh dir den vorletzten Satz an.", explain: "Die besten Bilder hängt der Autor an die Wand." },
      { q: "Wie oft geht der Autor in den Park?", options: ["Selten", "Oft an den Wochenenden", "Nie", "Jeden Tag"], correct: 1, hint: "Sieh dir den Satz über das Wochenende an.", explain: "Der Autor geht oft an den Wochenenden in den Park." },
      { q: "Wie findet der Autor das Fotografieren?", options: ["Langweilig", "Es macht viel Spaß", "Zu schwer", "Zu teuer"], correct: 1, hint: "Sieh dir den letzten Satz an.", explain: "Fotografieren macht dem Autor viel Spaß." },
      { q: "Fotografiert der Autor gern Städte?", options: ["Ja, am liebsten", "Nein, am liebsten Natur und Tiere", "Nur Städte", "Nie etwas"], correct: 1, hint: "Sieh dir den vierten Satz an.", explain: "Am liebsten fotografiert der Autor Natur und Tiere, nicht Städte." },
    ],
  },
  {
    level: "B1",
    title: "Öffentliche Verkehrsmittel",
    text:
      "Öffentliche Verkehrsmittel spielen in vielen Städten eine wichtige Rolle. Busse, Straßenbahnen und U-Bahnen bringen täglich Millionen von Menschen zur Arbeit. Wer öffentliche Verkehrsmittel nutzt, spart oft Geld und muss keinen Parkplatz suchen. Außerdem sind Busse und Bahnen besser für die Umwelt als viele einzelne Autos. Allerdings sind sie manchmal überfüllt oder haben Verspätung. Trotzdem entscheiden sich immer mehr Menschen bewusst für Bus und Bahn. Eine gute Verbindung macht das Leben in der Stadt einfacher.",
    questions: [
      { q: "Welche Rolle spielen öffentliche Verkehrsmittel in vielen Städten?", options: ["Keine Rolle", "Eine wichtige Rolle", "Eine kleine Rolle", "Eine schlechte Rolle"], correct: 1, hint: "Sieh dir den ersten Satz an.", explain: "Sie spielen in vielen Städten eine wichtige Rolle." },
      { q: "Welche Verkehrsmittel werden genannt?", options: ["Nur Autos", "Busse, Straßenbahnen und U-Bahnen", "Nur Fahrräder", "Nur Flugzeuge"], correct: 1, hint: "Sieh dir den zweiten Satz an.", explain: "Genannt werden Busse, Straßenbahnen und U-Bahnen." },
      { q: "Was spart man oft, wenn man öffentliche Verkehrsmittel nutzt?", options: ["Zeit", "Geld", "Energie", "Nichts"], correct: 1, hint: "Sieh dir den dritten Satz an.", explain: "Wer öffentliche Verkehrsmittel nutzt, spart oft Geld." },
      { q: "Was muss man nicht suchen?", options: ["Einen Parkplatz", "Eine Adresse", "Ein Ticket", "Einen Weg"], correct: 0, hint: "Sieh dir den dritten Satz an.", explain: "Man muss keinen Parkplatz suchen." },
      { q: "Warum sind Busse und Bahnen besser für die Umwelt?", options: ["Sie sind schneller", "Besser als viele einzelne Autos", "Sie sind billiger", "Sie sind neuer"], correct: 1, hint: "Sieh dir den vierten Satz an.", explain: "Sie sind besser für die Umwelt als viele einzelne Autos." },
      { q: "Was ist ein Nachteil?", options: ["Sie sind zu teuer", "Sie sind manchmal überfüllt oder verspätet", "Sie fahren nie", "Sie sind gefährlich"], correct: 1, hint: "Sieh dir den fünften Satz an.", explain: "Ein Nachteil ist, dass sie manchmal überfüllt sind oder Verspätung haben." },
      { q: "Wie entscheiden sich immer mehr Menschen?", options: ["Gegen Bus und Bahn", "Bewusst für Bus und Bahn", "Nur für das Auto", "Für das Fahrrad"], correct: 1, hint: "Sieh dir den sechsten Satz an.", explain: "Immer mehr Menschen entscheiden sich bewusst für Bus und Bahn." },
      { q: "Was macht das Leben in der Stadt einfacher?", options: ["Ein eigenes Auto", "Eine gute Verbindung", "Mehr Parkplätze", "Weniger Menschen"], correct: 1, hint: "Sieh dir den letzten Satz an.", explain: "Eine gute Verbindung macht das Leben in der Stadt einfacher." },
      { q: "Wie viele Menschen bringen sie täglich zur Arbeit?", options: ["Hunderte", "Millionen", "Dutzende", "Tausende nur"], correct: 1, hint: "Sieh dir den zweiten Satz an.", explain: "Sie bringen täglich Millionen von Menschen zur Arbeit." },
      { q: "Sind öffentliche Verkehrsmittel laut Text umweltfreundlich?", options: ["Nein", "Ja", "Nur Autos sind es", "Der Text sagt es nicht"], correct: 1, hint: "Sieh dir den vierten Satz an.", explain: "Sie sind besser für die Umwelt als viele Autos, also umweltfreundlicher." },
    ],
  },
  {
    level: "B1",
    title: "Ehrenamtliche Arbeit",
    text:
      "Viele Menschen engagieren sich in ihrer Freizeit ehrenamtlich. Sie helfen zum Beispiel in einem Altenheim, betreuen Kinder oder arbeiten bei der Feuerwehr. Ehrenamtliche Arbeit ist unbezahlt, aber sie bringt viele Vorteile. Die Helfer lernen neue Fähigkeiten und lernen interessante Menschen kennen. Außerdem fühlen sie sich nützlich und mit ihrer Gemeinschaft verbunden. Studien zeigen, dass ehrenamtliche Arbeit sogar glücklicher machen kann. Für viele ist das Gefühl, etwas Gutes zu tun, die schönste Belohnung.",
    questions: [
      { q: "Wie engagieren sich viele Menschen in ihrer Freizeit?", options: ["Beruflich", "Ehrenamtlich", "Gar nicht", "Nur online"], correct: 1, hint: "Sieh dir den ersten Satz an.", explain: "Viele Menschen engagieren sich ehrenamtlich." },
      { q: "Wo helfen sie zum Beispiel?", options: ["In einer Bank", "In einem Altenheim", "In einem Geschäft", "In einer Fabrik"], correct: 1, hint: "Sieh dir den zweiten Satz an.", explain: "Sie helfen zum Beispiel in einem Altenheim." },
      { q: "Ist ehrenamtliche Arbeit bezahlt?", options: ["Ja, gut bezahlt", "Nein, sie ist unbezahlt", "Nur manchmal", "Der Text sagt es nicht"], correct: 1, hint: "Sieh dir den dritten Satz an.", explain: "Ehrenamtliche Arbeit ist unbezahlt." },
      { q: "Was lernen die Helfer?", options: ["Neue Fähigkeiten", "Neue Sprachen nur", "Nichts Neues", "Nur Kochen"], correct: 0, hint: "Sieh dir den vierten Satz an.", explain: "Die Helfer lernen neue Fähigkeiten." },
      { q: "Wen lernen sie kennen?", options: ["Berühmte Menschen", "Interessante Menschen", "Nur Kollegen", "Niemanden"], correct: 1, hint: "Sieh dir den vierten Satz an.", explain: "Sie lernen interessante Menschen kennen." },
      { q: "Wie fühlen sich die Helfer?", options: ["Müde und gestresst", "Nützlich und verbunden", "Allein", "Gelangweilt"], correct: 1, hint: "Sieh dir den fünften Satz an.", explain: "Sie fühlen sich nützlich und mit ihrer Gemeinschaft verbunden." },
      { q: "Was zeigen Studien?", options: ["Ehrenamt macht müde", "Ehrenamt kann glücklicher machen", "Ehrenamt ist sinnlos", "Ehrenamt ist teuer"], correct: 1, hint: "Sieh dir den sechsten Satz an.", explain: "Studien zeigen, dass ehrenamtliche Arbeit glücklicher machen kann." },
      { q: "Was ist für viele die schönste Belohnung?", options: ["Das Geld", "Das Gefühl, etwas Gutes zu tun", "Der Ruhm", "Die Freizeit"], correct: 1, hint: "Sieh dir den letzten Satz an.", explain: "Die schönste Belohnung ist das Gefühl, etwas Gutes zu tun." },
      { q: "Was machen manche bei der Feuerwehr?", options: ["Sie arbeiten dort ehrenamtlich", "Sie besuchen sie", "Sie meiden sie", "Sie bezahlen sie"], correct: 0, hint: "Sieh dir den zweiten Satz an.", explain: "Manche arbeiten ehrenamtlich bei der Feuerwehr." },
      { q: "Bringt ehrenamtliche Arbeit Vorteile?", options: ["Nein", "Ja, viele", "Nur Nachteile", "Der Text sagt es nicht"], correct: 1, hint: "Sieh dir den dritten Satz an.", explain: "Ehrenamtliche Arbeit bringt viele Vorteile." },
    ],
  },
  {
    level: "B2",
    title: "Die Bedeutung des Schlafs",
    text:
      "Wenn das Leben stressig wird, verzichten viele Menschen zuerst auf ausreichenden Schlaf. Wissenschaftler warnen jedoch, dass dies ein ernster Fehler ist. Während des Schlafs verarbeitet das Gehirn Erinnerungen, repariert Zellen und baut Schadstoffe ab. Chronischer Schlafmangel wird mit zahlreichen Problemen in Verbindung gebracht, etwa einem geschwächten Immunsystem und Konzentrationsschwierigkeiten. Trotzdem schlafen viele Erwachsene regelmäßig weniger als die empfohlenen sieben bis neun Stunden. Fachleute empfehlen feste Schlafenszeiten und den Verzicht auf Bildschirme vor dem Zubettgehen. So lässt sich die Qualität des Schlafs deutlich verbessern.",
    questions: [
      { q: "Worauf verzichten viele Menschen zuerst bei Stress?", options: ["Auf Essen", "Auf ausreichenden Schlaf", "Auf Sport", "Auf Arbeit"], correct: 1, hint: "Sieh dir den ersten Satz an.", explain: "Bei Stress verzichten viele zuerst auf ausreichenden Schlaf." },
      { q: "Wie bewerten Wissenschaftler diesen Verzicht?", options: ["Als gute Idee", "Als ernsten Fehler", "Als unwichtig", "Als notwendig"], correct: 1, hint: "Sieh dir den zweiten Satz an.", explain: "Wissenschaftler warnen, dass es ein ernster Fehler ist." },
      { q: "Was macht das Gehirn während des Schlafs?", options: ["Nichts", "Es verarbeitet Erinnerungen und repariert Zellen", "Es wächst", "Es verbraucht mehr Energie"], correct: 1, hint: "Sieh dir den dritten Satz an.", explain: "Das Gehirn verarbeitet Erinnerungen, repariert Zellen und baut Schadstoffe ab." },
      { q: "Womit wird chronischer Schlafmangel in Verbindung gebracht?", options: ["Mit besserer Gesundheit", "Mit geschwächtem Immunsystem und Konzentrationsproblemen", "Mit mehr Energie", "Mit schnellerem Denken"], correct: 1, hint: "Sieh dir den vierten Satz an.", explain: "Er wird mit einem geschwächten Immunsystem und Konzentrationsschwierigkeiten in Verbindung gebracht." },
      { q: "Wie viele Stunden Schlaf werden empfohlen?", options: ["Vier bis fünf", "Sieben bis neun", "Zehn bis zwölf", "Drei bis vier"], correct: 1, hint: "Sieh dir den fünften Satz an.", explain: "Empfohlen werden sieben bis neun Stunden." },
      { q: "Schlafen viele Erwachsene genug?", options: ["Ja, immer", "Nein, oft weniger als empfohlen", "Nur Kinder nicht", "Der Text sagt es nicht"], correct: 1, hint: "Sieh dir den fünften Satz an.", explain: "Viele Erwachsene schlafen weniger als empfohlen." },
      { q: "Was empfehlen Fachleute?", options: ["Kaffee am Abend", "Feste Schlafenszeiten", "Schlaf am Tag", "Sport um Mitternacht"], correct: 1, hint: "Sieh dir den sechsten Satz an.", explain: "Fachleute empfehlen feste Schlafenszeiten." },
      { q: "Worauf soll man vor dem Zubettgehen verzichten?", options: ["Auf Lesen", "Auf Bildschirme", "Auf warme Milch", "Auf Ruhe"], correct: 1, hint: "Sieh dir den sechsten Satz an.", explain: "Man soll vor dem Zubettgehen auf Bildschirme verzichten." },
      { q: "Was lässt sich so verbessern?", options: ["Die Qualität des Schlafs", "Die Arbeit", "Das Essen", "Der Sport"], correct: 0, hint: "Sieh dir den letzten Satz an.", explain: "So lässt sich die Qualität des Schlafs verbessern." },
      { q: "Ist Schlaf laut Text wichtig?", options: ["Nein", "Ja, sehr", "Nur für Kinder", "Der Text sagt es nicht"], correct: 1, hint: "Betrachte den ganzen Text.", explain: "Der Text betont, dass Schlaf sehr wichtig ist." },
    ],
  },
  {
    level: "B2",
    title: "Der Onlinehandel",
    text:
      "In den letzten zwei Jahrzehnten hat der Onlinehandel das Einkaufen stark verändert. Kunden können heute Preise vergleichen, Bewertungen lesen und Produkte aus aller Welt bestellen, ohne das Haus zu verlassen. Diese Bequemlichkeit hat jedoch ihren Preis, denn viele kleine Geschäfte können kaum noch mithalten und manche mussten schließen. Außerdem gibt es Kritik an den Verpackungen und den Umweltfolgen der schnellen Lieferungen. Trotz dieser Probleme erwarten die meisten Fachleute, dass der Onlinehandel weiter wächst. Neue Technologien machen das Einkaufen immer schneller und persönlicher.",
    questions: [
      { q: "Was hat der Onlinehandel in den letzten zwei Jahrzehnten verändert?", options: ["Das Reisen", "Das Einkaufen", "Das Kochen", "Das Wohnen"], correct: 1, hint: "Sieh dir den ersten Satz an.", explain: "Der Onlinehandel hat das Einkaufen stark verändert." },
      { q: "Was können Kunden heute tun?", options: ["Nur im Laden kaufen", "Preise vergleichen und Bewertungen lesen", "Nichts vergleichen", "Nur bar bezahlen"], correct: 1, hint: "Sieh dir den zweiten Satz an.", explain: "Kunden können Preise vergleichen und Bewertungen lesen." },
      { q: "Woher können Kunden Produkte bestellen?", options: ["Nur aus ihrer Stadt", "Aus aller Welt", "Nur aus einem Land", "Nur aus dem Nachbarort"], correct: 1, hint: "Sieh dir den zweiten Satz an.", explain: "Kunden können Produkte aus aller Welt bestellen." },
      { q: "Welche Folge hatte diese Bequemlichkeit für kleine Geschäfte?", options: ["Sie wurden beliebter", "Manche mussten schließen", "Sie wurden größer", "Nichts änderte sich"], correct: 1, hint: "Sieh dir den dritten Satz an.", explain: "Viele kleine Geschäfte konnten kaum mithalten und manche mussten schließen." },
      { q: "Woran gibt es Kritik?", options: ["An den Preisen", "An Verpackungen und Umweltfolgen der Lieferungen", "Am Personal", "An der Werbung"], correct: 1, hint: "Sieh dir den vierten Satz an.", explain: "Kritik gibt es an den Verpackungen und den Umweltfolgen der Lieferungen." },
      { q: "Was erwarten die meisten Fachleute?", options: ["Dass der Onlinehandel verschwindet", "Dass der Onlinehandel weiter wächst", "Dass er gleich bleibt", "Dass er verboten wird"], correct: 1, hint: "Sieh dir den fünften Satz an.", explain: "Die meisten Fachleute erwarten, dass der Onlinehandel weiter wächst." },
      { q: "Was machen neue Technologien?", options: ["Sie machen das Einkaufen schwerer", "Sie machen das Einkaufen schneller und persönlicher", "Sie stoppen den Handel", "Sie erhöhen die Preise"], correct: 1, hint: "Sieh dir den letzten Satz an.", explain: "Neue Technologien machen das Einkaufen schneller und persönlicher." },
      { q: "Müssen Kunden das Haus verlassen, um online zu bestellen?", options: ["Ja", "Nein", "Nur bei großen Sachen", "Nur am Wochenende"], correct: 1, hint: "Sieh dir den zweiten Satz an.", explain: "Kunden können bestellen, ohne das Haus zu verlassen." },
      { q: "Wie lange verändert der Onlinehandel schon das Einkaufen?", options: ["Seit zwei Jahrzehnten", "Seit einem Jahr", "Seit fünfzig Jahren", "Seit wenigen Monaten"], correct: 0, hint: "Sieh dir den ersten Satz an.", explain: "Der Onlinehandel verändert das Einkaufen seit zwei Jahrzehnten." },
      { q: "Ist der Onlinehandel laut Text nur positiv?", options: ["Ja", "Nein, er hat auch Nachteile", "Der Text sagt es nicht", "Nur für Geschäfte"], correct: 1, hint: "Betrachte den ganzen Text.", explain: "Der Text nennt auch Nachteile wie geschlossene Geschäfte und Umweltfolgen." },
    ],
  },
  {
    level: "C1",
    title: "Die Psychologie der Gewohnheiten",
    text:
      "Gewohnheiten sind automatische Verhaltensweisen, die einen überraschend großen Teil unseres Alltags bestimmen. Psychologen beschreiben Gewohnheiten als eine Schleife aus einem Auslöser, einer Routine und einer Belohnung. Sobald diese Schleife fest verankert ist, hört das Gehirn auf, bewusst zu entscheiden, und folgt einfach dem Muster. Das spart geistige Energie, kann aber auch unerwünschtes Verhalten verfestigen. Forschungen zeigen, dass man eine Gewohnheit am besten ändert, indem man Auslöser und Belohnung beibehält und nur die Routine ersetzt. Wer diesen Mechanismus versteht, kann gezielt gesündere Gewohnheiten aufbauen.",
    questions: [
      { q: "Was sind Gewohnheiten laut Text?", options: ["Seltene Ereignisse", "Automatische Verhaltensweisen", "Bewusste Entscheidungen", "Schwierige Fähigkeiten"], correct: 1, hint: "Sieh dir den ersten Satz an.", explain: "Gewohnheiten sind automatische Verhaltensweisen." },
      { q: "Aus welchen drei Teilen besteht die Schleife?", options: ["Auslöser, Routine und Belohnung", "Anfang, Mitte und Ende", "Ursache, Wirkung und Ergebnis", "Plan, Tat und Prüfung"], correct: 0, hint: "Sieh dir den zweiten Satz an.", explain: "Die Schleife besteht aus Auslöser, Routine und Belohnung." },
      { q: "Was passiert, wenn die Schleife fest verankert ist?", options: ["Das Gehirn entscheidet jedes Mal neu", "Das Gehirn entscheidet nicht mehr bewusst", "Die Gewohnheit verschwindet", "Die Belohnung ändert sich"], correct: 1, hint: "Sieh dir den dritten Satz an.", explain: "Das Gehirn hört auf, bewusst zu entscheiden, und folgt dem Muster." },
      { q: "Was spart das Folgen des Musters?", options: ["Geistige Energie", "Zeit für Sport", "Geld", "Nichts"], correct: 0, hint: "Sieh dir den vierten Satz an.", explain: "Das Folgen des Musters spart geistige Energie." },
      { q: "Was kann eine feste Schleife auch bewirken?", options: ["Sie kann unerwünschtes Verhalten verfestigen", "Sie verbraucht zu viel Energie", "Sie ist leicht zu ändern", "Sie entfernt Belohnungen"], correct: 0, hint: "Sieh dir den vierten Satz an.", explain: "Eine feste Schleife kann auch unerwünschtes Verhalten verfestigen." },
      { q: "Wie ändert man eine Gewohnheit am besten?", options: ["Man schafft sie ganz ab", "Auslöser und Belohnung beibehalten, nur die Routine ersetzen", "Nur die Belohnung ändern", "Den Auslöser ignorieren"], correct: 1, hint: "Sieh dir den fünften Satz an.", explain: "Am besten behält man Auslöser und Belohnung bei und ersetzt nur die Routine." },
      { q: "Wer kann gezielt gesündere Gewohnheiten aufbauen?", options: ["Wer den Mechanismus versteht", "Nur Ärzte", "Niemand", "Nur Kinder"], correct: 0, hint: "Sieh dir den letzten Satz an.", explain: "Wer diesen Mechanismus versteht, kann gezielt gesündere Gewohnheiten aufbauen." },
      { q: "Wie groß ist der Teil des Alltags, den Gewohnheiten bestimmen?", options: ["Sehr klein", "Überraschend groß", "Gar keiner", "Genau die Hälfte"], correct: 1, hint: "Sieh dir den ersten Satz an.", explain: "Gewohnheiten bestimmen einen überraschend großen Teil des Alltags." },
      { q: "Soll man laut Text eine Gewohnheit ganz abschaffen?", options: ["Ja, immer", "Nein, besser die Routine ersetzen", "Nur gute Gewohnheiten", "Der Text sagt es nicht"], correct: 1, hint: "Sieh dir den fünften Satz an.", explain: "Laut Text ersetzt man besser die Routine, statt die Gewohnheit ganz abzuschaffen." },
      { q: "Wer beschreibt Gewohnheiten als Schleife?", options: ["Ärzte", "Psychologen", "Lehrer", "Journalisten"], correct: 1, hint: "Sieh dir den zweiten Satz an.", explain: "Psychologen beschreiben Gewohnheiten als Schleife." },
    ],
  },
  {
    level: "C1",
    title: "Die Herausforderungen der Urbanisierung",
    text:
      "Da immer mehr Menschen vom Land in die Städte ziehen, stehen Stadtplaner vor wachsenden Herausforderungen. Überfüllung kann die öffentlichen Dienste belasten, die Wohnkosten in die Höhe treiben und die Umweltverschmutzung erhöhen. Gleichzeitig kann gut gesteuerte Urbanisierung große Vorteile bringen, indem sie wirtschaftliche Chancen, Bildung und Gesundheitsversorgung an einem Ort bündelt. Der Schlüssel liegt nach Ansicht vieler Fachleute in einer durchdachten Planung, die bezahlbaren Wohnraum, guten Nahverkehr und Grünflächen berücksichtigt. Städte, die das Wachstum nicht planen, lösen die Probleme später oft nur mit großem Aufwand.",
    questions: [
      { q: "Wohin ziehen immer mehr Menschen?", options: ["Von der Stadt aufs Land", "Vom Land in die Städte", "Ins Ausland", "Ans Meer"], correct: 1, hint: "Sieh dir den ersten Satz an.", explain: "Immer mehr Menschen ziehen vom Land in die Städte." },
      { q: "Wer steht vor wachsenden Herausforderungen?", options: ["Bauern", "Stadtplaner", "Touristen", "Lehrer"], correct: 1, hint: "Sieh dir den ersten Satz an.", explain: "Stadtplaner stehen vor wachsenden Herausforderungen." },
      { q: "Was kann Überfüllung bewirken?", options: ["Dienste belasten und Wohnkosten erhöhen", "Die Umwelt schützen", "Wohnkosten senken", "Die Infrastruktur verbessern"], correct: 0, hint: "Sieh dir den zweiten Satz an.", explain: "Überfüllung kann Dienste belasten und die Wohnkosten in die Höhe treiben." },
      { q: "Was kann gut gesteuerte Urbanisierung bringen?", options: ["Große Vorteile", "Nur Probleme", "Weniger Menschen", "Mehr Landwirtschaft"], correct: 0, hint: "Sieh dir den dritten Satz an.", explain: "Gut gesteuerte Urbanisierung kann große Vorteile bringen." },
      { q: "Was bündelt gute Urbanisierung an einem Ort?", options: ["Chancen, Bildung und Gesundheitsversorgung", "Nur Fabriken", "Nur Straßen", "Nur Geschäfte"], correct: 0, hint: "Sieh dir den dritten Satz an.", explain: "Sie bündelt wirtschaftliche Chancen, Bildung und Gesundheitsversorgung." },
      { q: "Worin liegt der Schlüssel laut Fachleuten?", options: ["In einer durchdachten Planung", "Im Stoppen des Wachstums", "Im Bau von nur Büros", "Im Abriss von Grünflächen"], correct: 0, hint: "Sieh dir den vierten Satz an.", explain: "Der Schlüssel liegt in einer durchdachten Planung." },
      { q: "Was soll die Planung berücksichtigen?", options: ["Bezahlbaren Wohnraum, Nahverkehr und Grünflächen", "Nur Hochhäuser", "Nur Straßen", "Nur Einkaufszentren"], correct: 0, hint: "Sieh dir den vierten Satz an.", explain: "Die Planung soll bezahlbaren Wohnraum, guten Nahverkehr und Grünflächen berücksichtigen." },
      { q: "Was passiert mit Städten ohne Planung?", options: ["Sie lösen Probleme leicht", "Sie lösen Probleme später nur mit großem Aufwand", "Sie hören auf zu wachsen", "Sie haben keine Probleme"], correct: 1, hint: "Sieh dir den letzten Satz an.", explain: "Städte ohne Planung lösen die Probleme später oft nur mit großem Aufwand." },
      { q: "Ist Urbanisierung laut Text nur negativ?", options: ["Ja", "Nein, sie kann auch Vorteile haben", "Der Text sagt es nicht", "Nur für Planer"], correct: 1, hint: "Sieh dir den dritten Satz an.", explain: "Gut gesteuerte Urbanisierung kann auch große Vorteile bringen." },
      { q: "Was kann Überfüllung mit der Umwelt machen?", options: ["Die Umweltverschmutzung erhöhen", "Die Luft reinigen", "Nichts bewirken", "Die Umwelt schützen"], correct: 0, hint: "Sieh dir den zweiten Satz an.", explain: "Überfüllung kann die Umweltverschmutzung erhöhen." },
    ],
  },
  {
    level: "C2",
    title: "Das Problem der Willensfreiheit",
    text:
      "Kaum eine Frage hat Philosophen so beharrlich beschäftigt wie die, ob der Mensch einen freien Willen besitzt. Deterministen argumentieren, dass jedes Ereignis, auch jede menschliche Entscheidung, die zwangsläufige Folge früherer Ursachen ist, die von den Naturgesetzen bestimmt werden. Vertreter der Willensfreiheit hingegen behaupten, dass der Mensch der wahre Urheber seiner Handlungen sein kann. Eine dritte Position, der Kompatibilismus, versucht beide zu versöhnen, indem sie Freiheit nicht als Abwesenheit von Ursachen versteht, sondern als die Fähigkeit, nach den eigenen Motiven ohne äußeren Zwang zu handeln. Diese Debatte ist keineswegs rein akademisch, denn sie hat tiefgreifende Folgen dafür, wie Gesellschaften Verantwortung zuschreiben.",
    questions: [
      { q: "Welche Frage beschäftigt Philosophen laut Text?", options: ["Ob der Mensch einen freien Willen besitzt", "Ob das Universum unendlich ist", "Ob Tiere denken", "Ob die Zeit real ist"], correct: 0, hint: "Sieh dir den ersten Satz an.", explain: "Der Text fragt, ob der Mensch einen freien Willen besitzt." },
      { q: "Was argumentieren Deterministen?", options: ["Jedes Ereignis ist die zwangsläufige Folge früherer Ursachen", "Der Mensch ist völlig frei", "Nichts ist vorhersehbar", "Entscheidungen haben keine Ursachen"], correct: 0, hint: "Sieh dir den zweiten Satz an.", explain: "Deterministen argumentieren, dass jedes Ereignis die zwangsläufige Folge früherer Ursachen ist." },
      { q: "Was bestimmt laut Deterministen die Ursachen?", options: ["Die Naturgesetze", "Der Zufall", "Die Wünsche der Menschen", "Die Regierung"], correct: 0, hint: "Sieh dir den zweiten Satz an.", explain: "Laut Deterministen werden die Ursachen von den Naturgesetzen bestimmt." },
      { q: "Was behaupten Vertreter der Willensfreiheit?", options: ["Der Mensch kann der wahre Urheber seiner Handlungen sein", "Alles ist vorbestimmt", "Freiheit ist eine Illusion", "Nur Naturgesetze zählen"], correct: 0, hint: "Sieh dir den dritten Satz an.", explain: "Sie behaupten, der Mensch könne der wahre Urheber seiner Handlungen sein." },
      { q: "Wie heißt die dritte Position?", options: ["Determinismus", "Kompatibilismus", "Idealismus", "Skeptizismus"], correct: 1, hint: "Sieh dir den vierten Satz an.", explain: "Die dritte Position heißt Kompatibilismus." },
      { q: "Wie versteht der Kompatibilismus Freiheit?", options: ["Als Abwesenheit aller Ursachen", "Als Fähigkeit, nach eigenen Motiven ohne Zwang zu handeln", "Als reinen Zufall", "Als striktes Befolgen der Naturgesetze"], correct: 1, hint: "Sieh dir den vierten Satz an.", explain: "Der Kompatibilismus versteht Freiheit als Fähigkeit, nach eigenen Motiven ohne äußeren Zwang zu handeln." },
      { q: "Was versucht der Kompatibilismus?", options: ["Beide anderen Ansichten abzulehnen", "Determinismus und Willensfreiheit zu versöhnen", "Zu beweisen, dass es keinen Willen gibt", "Die Debatte für immer zu beenden"], correct: 1, hint: "Sieh dir den vierten Satz an.", explain: "Der Kompatibilismus versucht, beide Positionen zu versöhnen." },
      { q: "Warum ist die Debatte nicht rein akademisch?", options: ["Sie betrifft die Zuschreibung von Verantwortung", "Sie hat keine praktische Wirkung", "Sie betrifft nur die Sprache", "Sie betrifft nur Wissenschaftler"], correct: 0, hint: "Sieh dir den letzten Satz an.", explain: "Die Debatte hat Folgen dafür, wie Gesellschaften Verantwortung zuschreiben." },
      { q: "Ist Freiheit laut Kompatibilismus die Abwesenheit von Ursachen?", options: ["Ja", "Nein", "Nur teilweise", "Der Text sagt es nicht"], correct: 1, hint: "Sieh dir den vierten Satz an.", explain: "Der Kompatibilismus versteht Freiheit gerade nicht als Abwesenheit von Ursachen." },
      { q: "Gibt es laut Deterministen echte Wahlfreiheit?", options: ["Ja", "Nein", "Nur für manche", "Nur in Notfällen"], correct: 1, hint: "Sieh dir den zweiten Satz an.", explain: "Laut Deterministen ist jede Entscheidung zwangsläufig, es gibt also keine echte Wahlfreiheit." },
    ],
  },
  {
    level: "C2",
    title: "Die Paradoxien der Globalisierung",
    text:
      "Die Globalisierung, also die zunehmende Verflechtung von Wirtschaft und Kultur über Landesgrenzen hinweg, hat sowohl beispiellosen Wohlstand als auch anhaltende Kritik hervorgebracht. Befürworter schreiben ihr zu, Hunderte Millionen Menschen aus der Armut befreit zu haben, indem sie Entwicklungsländer in die Weltmärkte einband. Kritiker halten dagegen, dass die Vorteile ungleich verteilt seien und dass lokale Kulturen zugunsten eines vereinheitlichten Konsums verdrängt würden. Erschwerend kommt hinzu, dass dieselbe Vernetzung, die das Wachstum antreibt, auch Krisen rasch verbreitet, etwa wenn ein Finanzschock in einer Region das ganze System erfasst. Diese Spannungen zu bewältigen, bleibt eine der zentralen Herausforderungen unserer Zeit.",
    questions: [
      { q: "Wie definiert der Text die Globalisierung?", options: ["Die zunehmende Verflechtung von Wirtschaft und Kultur über Grenzen hinweg", "Das Schließen der Landesgrenzen", "Den Niedergang der Technik", "Das Ende des Handels"], correct: 0, hint: "Sieh dir den ersten Satz an.", explain: "Globalisierung ist die zunehmende Verflechtung von Wirtschaft und Kultur über Landesgrenzen hinweg." },
      { q: "Was hat die Globalisierung hervorgebracht?", options: ["Nur Armut", "Wohlstand und Kritik", "Nur Kriege", "Nichts Neues"], correct: 1, hint: "Sieh dir den ersten Satz an.", explain: "Die Globalisierung hat sowohl Wohlstand als auch Kritik hervorgebracht." },
      { q: "Was schreiben Befürworter der Globalisierung zu?", options: ["Hunderte Millionen aus der Armut befreit zu haben", "Alle Kriege verursacht zu haben", "Den Handel beendet zu haben", "Die Technik verlangsamt zu haben"], correct: 0, hint: "Sieh dir den zweiten Satz an.", explain: "Befürworter schreiben ihr zu, Hunderte Millionen aus der Armut befreit zu haben." },
      { q: "Wie geschah das laut Befürwortern?", options: ["Durch Einbindung von Entwicklungsländern in die Weltmärkte", "Durch Schließung der Märkte", "Durch Stopp der Technik", "Durch höhere Grenzen"], correct: 0, hint: "Sieh dir den zweiten Satz an.", explain: "Es geschah durch die Einbindung von Entwicklungsländern in die Weltmärkte." },
      { q: "Was sagen Kritiker über die Vorteile?", options: ["Sie seien ungleich verteilt", "Sie seien gleich verteilt", "Es gebe keine", "Sie helfen nur den Armen"], correct: 0, hint: "Sieh dir den dritten Satz an.", explain: "Kritiker halten dagegen, dass die Vorteile ungleich verteilt seien." },
      { q: "Was werde laut Kritikern verdrängt?", options: ["Lokale Kulturen", "Alle Kulturen", "Die Weltmärkte", "Die Technik"], correct: 0, hint: "Sieh dir den dritten Satz an.", explain: "Laut Kritikern werden lokale Kulturen zugunsten eines vereinheitlichten Konsums verdrängt." },
      { q: "Was verbreitet die Vernetzung auch rasch?", options: ["Krisen", "Nur Wohlstand", "Nur Ideen", "Nichts"], correct: 0, hint: "Sieh dir den vierten Satz an.", explain: "Dieselbe Vernetzung verbreitet auch Krisen rasch." },
      { q: "Welches Beispiel für eine Krise wird genannt?", options: ["Ein Finanzschock in einer Region", "Ein lokales Fest", "Eine neue Erfindung", "Ein Sportereignis"], correct: 0, hint: "Sieh dir den vierten Satz an.", explain: "Genannt wird ein Finanzschock in einer Region, der das ganze System erfasst." },
      { q: "Was bleibt eine zentrale Herausforderung unserer Zeit?", options: ["Diese Spannungen zu bewältigen", "Die Grenzen zu schließen", "Den Handel zu beenden", "Die Technik zu stoppen"], correct: 0, hint: "Sieh dir den letzten Satz an.", explain: "Diese Spannungen zu bewältigen bleibt eine zentrale Herausforderung unserer Zeit." },
      { q: "Ist die Globalisierung laut Text nur positiv?", options: ["Ja", "Nein, sie ist auch umstritten", "Der Text sagt es nicht", "Nur für Kritiker"], correct: 1, hint: "Sieh dir den ersten Satz an.", explain: "Die Globalisierung hat Wohlstand, aber auch anhaltende Kritik hervorgebracht." },
    ],
  },
];
