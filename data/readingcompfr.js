// Reading Comprehension passages -- French track (CEFR levels A1-C2).
// NOTE: this file's own CEFR scope is A1-C2 (6 levels) even though this
// app's separate French VOCABULARY files (data/wordsa1fr.js..wordsc1fr.js)
// deliberately stop at C1 -- reading comprehension is authored
// independently of those word lists, so there is no requirement for the
// two features to share the same level ceiling.
// All passages and questions below are ORIGINAL content written for this
// app, not copied or adapted from any third-party site (same policy
// already applied to the English/German reading-comprehension tracks).
// Schema matches data/readingcompde.js exactly: { level, title, text,
// questions: [ { q, options: [4], correct, hint, explain } ] }. `correct`
// is the 0-based index into `options` of the right answer. `hint`/
// `explain` are shown via the Hint/Explain buttons in the game UI. All
// text (passage, questions, hint, explain) is in French, matching the
// passage's own language -- only the game's UI chrome stays English, same
// convention as the German/English tracks.
// Not yet wired into readingcomprehension.js (which currently only reads
// currentLang "de" vs "en") -- this is a standalone data file for now,
// same scope as the other 3 new French data files requested alongside it.
// Pilot scope: 2 passages per level x 6 levels = 12 passages, 5 questions
// each = 60 questions (matches the original EN/DE pilot scope of 5
// questions/passage, deliberately NOT the later 10-questions-per-passage
// expansion those two tracks eventually got -- kept consistent within
// this file rather than partially upgrading only the new C2 section).

window.READING_PASSAGES_FR = [
  // ===================== A1 =====================
  {
    level: "A1",
    title: "Ma famille",
    text:
      "Je m'appelle Sophie. J'ai douze ans. J'ai un frère et une sœur. Mon frère s'appelle Paul. " +
      "Ma sœur s'appelle Claire. Nous habitons à Lyon. J'ai aussi un chat. Il s'appelle Minou.",
    questions: [
      { q: "Comment s'appelle la fille ?", options: ["Sophie", "Claire", "Paul", "Minou"], correct: 0, hint: "Regarde la première phrase.", explain: "Le texte dit : \"Je m'appelle Sophie.\"" },
      { q: "Quel âge a Sophie ?", options: ["Dix ans", "Onze ans", "Douze ans", "Treize ans"], correct: 2, hint: "Regarde la deuxième phrase.", explain: "Le texte dit : \"J'ai douze ans.\"" },
      { q: "Comment s'appelle le frère ?", options: ["Minou", "Paul", "Claire", "Sophie"], correct: 1, hint: "Regarde la phrase sur le frère.", explain: "Le texte dit : \"Mon frère s'appelle Paul.\"" },
      { q: "Où habite la famille ?", options: ["À Paris", "À Lyon", "À Marseille", "À Nice"], correct: 1, hint: "Regarde la phrase avec le nom de la ville.", explain: "Le texte dit : \"Nous habitons à Lyon.\"" },
      { q: "Quel animal a Sophie ?", options: ["Un chien", "Un chat", "Un oiseau", "Un poisson"], correct: 1, hint: "Regarde la dernière phrase.", explain: "Le texte dit : \"J'ai aussi un chat.\"" },
    ],
  },
  {
    level: "A1",
    title: "Ma journée",
    text:
      "Je me réveille à sept heures. Je mange du pain pour le petit-déjeuner. Ensuite, je vais à " +
      "l'école à pied. L'école commence à huit heures. Après l'école, je joue au foot. Le soir, je lis un livre.",
    questions: [
      { q: "À quelle heure se réveille la personne ?", options: ["Six heures", "Sept heures", "Huit heures", "Neuf heures"], correct: 1, hint: "Regarde la première phrase.", explain: "Le texte dit : \"Je me réveille à sept heures.\"" },
      { q: "Que mange-t-elle pour le petit-déjeuner ?", options: ["Du pain", "Des œufs", "Du riz", "Des fruits"], correct: 0, hint: "Regarde la deuxième phrase.", explain: "Le texte dit : \"Je mange du pain pour le petit-déjeuner.\"" },
      { q: "Comment va-t-elle à l'école ?", options: ["En voiture", "En bus", "À pied", "À vélo"], correct: 2, hint: "Regarde la troisième phrase.", explain: "Le texte dit : \"je vais à l'école à pied.\"" },
      { q: "À quelle heure commence l'école ?", options: ["Sept heures", "Huit heures", "Neuf heures", "Dix heures"], correct: 1, hint: "Regarde la phrase sur l'école.", explain: "Le texte dit : \"L'école commence à huit heures.\"" },
      { q: "Que fait-elle le soir ?", options: ["Elle joue au foot", "Elle lit un livre", "Elle mange", "Elle dort"], correct: 1, hint: "Regarde la dernière phrase.", explain: "Le texte dit : \"Le soir, je lis un livre.\"" },
    ],
  },
  // ===================== A2 =====================
  {
    level: "A2",
    title: "Le week-end dernier",
    text:
      "Le week-end dernier, je suis allé au marché avec ma mère. Nous avons acheté des légumes et " +
      "des fruits. Ensuite, nous avons visité ma grand-mère. Elle habite près de chez nous. Le soir, " +
      "toute la famille a mangé ensemble. C'était un très bon week-end.",
    questions: [
      { q: "Où est allé le narrateur ?", options: ["Au marché", "À l'école", "Au cinéma", "Au parc"], correct: 0, hint: "Regarde la première phrase.", explain: "Le texte dit : \"je suis allé au marché avec ma mère.\"" },
      { q: "Qu'est-ce qu'ils ont acheté ?", options: ["Des vêtements", "Des légumes et des fruits", "Des livres", "Des jouets"], correct: 1, hint: "Regarde la deuxième phrase.", explain: "Le texte dit : \"Nous avons acheté des légumes et des fruits.\"" },
      { q: "Qui ont-ils visité ensuite ?", options: ["Un ami", "La grand-mère", "Le professeur", "Le médecin"], correct: 1, hint: "Regarde la troisième phrase.", explain: "Le texte dit : \"nous avons visité ma grand-mère.\"" },
      { q: "Où habite la grand-mère ?", options: ["Loin de chez eux", "Près de chez eux", "À l'étranger", "À la montagne"], correct: 1, hint: "Regarde la phrase sur la grand-mère.", explain: "Le texte dit : \"Elle habite près de chez nous.\"" },
      { q: "Comment était le week-end ?", options: ["Ennuyeux", "Très bon", "Fatigant", "Triste"], correct: 1, hint: "Regarde la dernière phrase.", explain: "Le texte dit : \"C'était un très bon week-end.\"" },
    ],
  },
  {
    level: "A2",
    title: "Un nouvel emploi",
    text:
      "Marc a trouvé un nouvel emploi le mois dernier. Il travaille maintenant dans une banque au " +
      "centre-ville. Chaque matin, il prend le bus à sept heures et demie. Son bureau se trouve au " +
      "cinquième étage. Il aime beaucoup ses nouveaux collègues, mais le travail est parfois stressant.",
    questions: [
      { q: "Quand Marc a-t-il trouvé son emploi ?", options: ["Cette semaine", "Le mois dernier", "L'année dernière", "Hier"], correct: 1, hint: "Regarde la première phrase.", explain: "Le texte dit : \"Marc a trouvé un nouvel emploi le mois dernier.\"" },
      { q: "Où travaille Marc ?", options: ["Dans une école", "Dans une banque", "Dans un restaurant", "Dans un hôpital"], correct: 1, hint: "Regarde la deuxième phrase.", explain: "Le texte dit : \"Il travaille maintenant dans une banque.\"" },
      { q: "Comment va-t-il au travail ?", options: ["En voiture", "En train", "En bus", "À vélo"], correct: 2, hint: "Regarde la phrase sur le matin.", explain: "Le texte dit : \"il prend le bus à sept heures et demie.\"" },
      { q: "À quel étage se trouve son bureau ?", options: ["Premier", "Troisième", "Cinquième", "Septième"], correct: 2, hint: "Regarde la phrase sur le bureau.", explain: "Le texte dit : \"Son bureau se trouve au cinquième étage.\"" },
      { q: "Comment Marc trouve-t-il le travail ?", options: ["Facile", "Ennuyeux", "Parfois stressant", "Impossible"], correct: 2, hint: "Regarde la dernière phrase.", explain: "Le texte dit : \"le travail est parfois stressant.\"" },
    ],
  },
  // ===================== B1 =====================
  {
    level: "B1",
    title: "Les réseaux sociaux",
    text:
      "De nos jours, beaucoup de jeunes passent plusieurs heures par jour sur les réseaux sociaux. " +
      "Certains disent que cela permet de rester en contact avec leurs amis. D'autres pensent que " +
      "c'est une perte de temps et que cela peut causer de l'anxiété. Il est important de trouver un " +
      "équilibre entre la vie numérique et la vie réelle.",
    questions: [
      { q: "Que font beaucoup de jeunes chaque jour ?", options: ["Ils lisent des livres", "Ils passent du temps sur les réseaux sociaux", "Ils font du sport", "Ils dorment"], correct: 1, hint: "Regarde la première phrase.", explain: "Le texte dit : \"beaucoup de jeunes passent plusieurs heures par jour sur les réseaux sociaux.\"" },
      { q: "Quel est un avantage mentionné ?", options: ["Gagner de l'argent", "Rester en contact avec les amis", "Apprendre une langue", "Voyager"], correct: 1, hint: "Regarde la deuxième phrase.", explain: "Le texte dit que cela \"permet de rester en contact avec leurs amis.\"" },
      { q: "Quel est un inconvénient mentionné ?", options: ["Le coût", "L'anxiété", "La fatigue physique", "La faim"], correct: 1, hint: "Regarde la troisième phrase.", explain: "Le texte dit que cela \"peut causer de l'anxiété.\"" },
      { q: "Que pense une partie des gens ?", options: ["Que c'est utile", "Que c'est une perte de temps", "Que c'est gratuit", "Que c'est nouveau"], correct: 1, hint: "Regarde la troisième phrase.", explain: "Le texte dit : \"D'autres pensent que c'est une perte de temps.\"" },
      { q: "Que faut-il trouver, selon le texte ?", options: ["Un travail", "Un équilibre", "Un ami", "Un téléphone"], correct: 1, hint: "Regarde la dernière phrase.", explain: "Le texte dit : \"Il est important de trouver un équilibre.\"" },
    ],
  },
  {
    level: "B1",
    title: "Voyager seul",
    text:
      "Voyager seul peut sembler effrayant au début, mais cela offre aussi de nombreux avantages. " +
      "On apprend à être plus indépendant et à prendre des décisions rapidement. De plus, on rencontre " +
      "souvent plus facilement d'autres voyageurs. Bien sûr, il faut rester prudent et bien préparer " +
      "son itinéraire à l'avance.",
    questions: [
      { q: "Comment peut sembler voyager seul au début ?", options: ["Amusant", "Effrayant", "Ennuyeux", "Facile"], correct: 1, hint: "Regarde la première phrase.", explain: "Le texte dit : \"Voyager seul peut sembler effrayant au début.\"" },
      { q: "Qu'apprend-on en voyageant seul ?", options: ["À cuisiner", "À être plus indépendant", "À conduire", "À nager"], correct: 1, hint: "Regarde la deuxième phrase.", explain: "Le texte dit : \"On apprend à être plus indépendant.\"" },
      { q: "Que fait-on plus facilement, selon le texte ?", options: ["Dormir", "Rencontrer d'autres voyageurs", "Économiser de l'argent", "Travailler"], correct: 1, hint: "Regarde la troisième phrase.", explain: "Le texte dit : \"on rencontre souvent plus facilement d'autres voyageurs.\"" },
      { q: "Que faut-il faire, selon le texte ?", options: ["Voyager en groupe", "Rester prudent", "Éviter les hôtels", "Ne rien planifier"], correct: 1, hint: "Regarde la dernière phrase.", explain: "Le texte dit : \"il faut rester prudent.\"" },
      { q: "Que faut-il préparer à l'avance ?", options: ["Son itinéraire", "Son passeport uniquement", "Son argent", "Ses vêtements"], correct: 0, hint: "Regarde la dernière phrase.", explain: "Le texte dit : \"bien préparer son itinéraire à l'avance.\"" },
    ],
  },
  // ===================== B2 =====================
  {
    level: "B2",
    title: "Le télétravail",
    text:
      "Le télétravail s'est considérablement développé ces dernières années, notamment depuis la " +
      "pandémie. Cette pratique présente des avantages indéniables : moins de temps perdu dans les " +
      "transports, davantage de flexibilité et un meilleur équilibre entre vie professionnelle et vie " +
      "privée. Cependant, certains employés se plaignent d'un isolement social croissant et d'une " +
      "difficulté à séparer le travail du reste de la vie quotidienne. Les entreprises doivent donc " +
      "trouver un juste milieu entre présence au bureau et travail à distance.",
    questions: [
      { q: "Depuis quand le télétravail s'est-il surtout développé ?", options: ["Depuis toujours", "Depuis la pandémie", "Depuis dix ans", "Depuis peu"], correct: 1, hint: "Regarde la première phrase.", explain: "Le texte dit : \"notamment depuis la pandémie.\"" },
      { q: "Quel avantage du télétravail est cité ?", options: ["Un salaire plus élevé", "Moins de temps perdu dans les transports", "Plus de vacances", "Moins de responsabilités"], correct: 1, hint: "Regarde la deuxième phrase.", explain: "Le texte cite \"moins de temps perdu dans les transports\" parmi les avantages." },
      { q: "De quoi certains employés se plaignent-ils ?", options: ["Du salaire", "De l'isolement social", "Du matériel", "Des horaires"], correct: 1, hint: "Regarde la troisième phrase.", explain: "Le texte dit : \"certains employés se plaignent d'un isolement social croissant.\"" },
      { q: "Quelle difficulté est mentionnée ?", options: ["Séparer travail et vie quotidienne", "Trouver un emploi", "Apprendre une langue", "Voyager"], correct: 0, hint: "Regarde la troisième phrase.", explain: "Le texte mentionne \"une difficulté à séparer le travail du reste de la vie quotidienne.\"" },
      { q: "Que doivent faire les entreprises, selon le texte ?", options: ["Interdire le télétravail", "Trouver un juste milieu", "Fermer les bureaux", "Augmenter les salaires"], correct: 1, hint: "Regarde la dernière phrase.", explain: "Le texte dit : \"Les entreprises doivent donc trouver un juste milieu.\"" },
    ],
  },
  {
    level: "B2",
    title: "La consommation responsable",
    text:
      "De plus en plus de consommateurs cherchent à réduire leur impact environnemental. Ils " +
      "privilégient les produits locaux, évitent le plastique à usage unique et achètent des vêtements " +
      "d'occasion. Ce changement d'habitudes s'explique en partie par une prise de conscience " +
      "collective des enjeux climatiques. Néanmoins, certains critiques estiment que ces gestes " +
      "individuels restent insuffisants face à l'ampleur du problème, et qu'une action politique plus " +
      "ambitieuse est nécessaire.",
    questions: [
      { q: "Que cherchent à réduire les consommateurs ?", options: ["Leurs dépenses", "Leur impact environnemental", "Leur temps de travail", "Leur consommation d'eau uniquement"], correct: 1, hint: "Regarde la première phrase.", explain: "Le texte dit : \"cherchent à réduire leur impact environnemental.\"" },
      { q: "Que privilégient-ils ?", options: ["Les produits importés", "Les produits locaux", "Les produits de luxe", "Les produits en plastique"], correct: 1, hint: "Regarde la deuxième phrase.", explain: "Le texte dit : \"Ils privilégient les produits locaux.\"" },
      { q: "Qu'évitent-ils ?", options: ["Les transports en commun", "Le plastique à usage unique", "Les produits locaux", "Les vêtements d'occasion"], correct: 1, hint: "Regarde la deuxième phrase.", explain: "Le texte dit : \"évitent le plastique à usage unique.\"" },
      { q: "Comment ce changement s'explique-t-il ?", options: ["Par la mode", "Par une prise de conscience climatique", "Par le prix", "Par le gouvernement uniquement"], correct: 1, hint: "Regarde la troisième phrase.", explain: "Le texte parle d'\"une prise de conscience collective des enjeux climatiques.\"" },
      { q: "Que pensent certains critiques ?", options: ["Que tout va bien", "Que les gestes individuels sont insuffisants", "Qu'il ne faut rien faire", "Que le problème est résolu"], correct: 1, hint: "Regarde la dernière phrase.", explain: "Le texte dit que ces gestes \"restent insuffisants face à l'ampleur du problème.\"" },
    ],
  },
  // ===================== C1 =====================
  {
    level: "C1",
    title: "L'intelligence artificielle et l'emploi",
    text:
      "L'essor fulgurant de l'intelligence artificielle suscite autant d'espoirs que d'inquiétudes " +
      "quant à l'avenir du marché du travail. D'un côté, certains experts soulignent que cette " +
      "technologie pourrait automatiser un grand nombre de tâches répétitives, libérant ainsi du temps " +
      "pour des activités à plus forte valeur ajoutée. D'un autre côté, d'aucuns redoutent une " +
      "disparition massive d'emplois, en particulier dans les secteurs peu qualifiés. Face à cette " +
      "incertitude, plusieurs gouvernements envisagent des politiques de reconversion professionnelle " +
      "afin d'accompagner cette transition inéluctable.",
    questions: [
      { q: "Que suscite l'essor de l'intelligence artificielle ?", options: ["Uniquement des espoirs", "Des espoirs et des inquiétudes", "Uniquement des inquiétudes", "De l'indifférence"], correct: 1, hint: "Regarde la première phrase.", explain: "Le texte dit qu'elle \"suscite autant d'espoirs que d'inquiétudes.\"" },
      { q: "Que pourrait automatiser l'IA, selon certains experts ?", options: ["Toutes les tâches", "Les tâches répétitives", "Uniquement la recherche", "Rien du tout"], correct: 1, hint: "Regarde la deuxième phrase.", explain: "Le texte dit qu'elle \"pourrait automatiser un grand nombre de tâches répétitives.\"" },
      { q: "Que redoutent d'autres personnes ?", options: ["Une baisse des prix", "Une disparition massive d'emplois", "Une hausse des salaires", "Un manque de technologie"], correct: 1, hint: "Regarde la phrase sur les inquiétudes.", explain: "Le texte dit : \"d'aucuns redoutent une disparition massive d'emplois.\"" },
      { q: "Quels secteurs sont particulièrement concernés ?", options: ["Les secteurs très qualifiés", "Les secteurs peu qualifiés", "Aucun secteur", "Seulement l'agriculture"], correct: 1, hint: "Regarde la phrase sur les emplois.", explain: "Le texte précise \"en particulier dans les secteurs peu qualifiés.\"" },
      { q: "Que envisagent plusieurs gouvernements ?", options: ["D'interdire l'IA", "Des politiques de reconversion professionnelle", "De baisser les impôts", "De fermer les usines"], correct: 1, hint: "Regarde la dernière phrase.", explain: "Le texte dit : \"plusieurs gouvernements envisagent des politiques de reconversion professionnelle.\"" },
    ],
  },
  {
    level: "C1",
    title: "La mémoire collective",
    text:
      "La mémoire collective désigne l'ensemble des souvenirs partagés par un groupe social, qu'il " +
      "s'agisse d'une nation, d'une communauté ou d'une famille. Contrairement à la mémoire " +
      "individuelle, elle se construit et se transmet à travers des récits, des monuments, des " +
      "commémorations et des institutions éducatives. Certains historiens soulignent toutefois que " +
      "cette mémoire n'est jamais totalement neutre : elle résulte souvent d'une sélection, voire " +
      "d'une réinterprétation des événements passés en fonction des enjeux du présent. Ainsi, l'étude " +
      "de la mémoire collective révèle autant de choses sur le passé que sur les préoccupations " +
      "actuelles d'une société.",
    questions: [
      { q: "Que désigne la mémoire collective ?", options: ["Les souvenirs d'une seule personne", "Les souvenirs partagés par un groupe", "Les rêves d'un individu", "Les archives officielles uniquement"], correct: 1, hint: "Regarde la première phrase.", explain: "Le texte dit qu'elle désigne \"l'ensemble des souvenirs partagés par un groupe social.\"" },
      { q: "Comment se transmet la mémoire collective ?", options: ["Uniquement par les livres", "Par des récits, monuments et commémorations", "Par la génétique", "Elle ne se transmet pas"], correct: 1, hint: "Regarde la deuxième phrase.", explain: "Le texte dit qu'elle se transmet \"à travers des récits, des monuments, des commémorations et des institutions éducatives.\"" },
      { q: "Que soulignent certains historiens ?", options: ["Que la mémoire est toujours neutre", "Que la mémoire n'est jamais totalement neutre", "Que la mémoire est inutile", "Que la mémoire est identique partout"], correct: 1, hint: "Regarde la troisième phrase.", explain: "Le texte dit : \"cette mémoire n'est jamais totalement neutre.\"" },
      { q: "De quoi résulte parfois la mémoire collective ?", options: ["D'une sélection des événements", "D'un hasard total", "D'une loi", "D'un vote populaire"], correct: 0, hint: "Regarde la troisième phrase.", explain: "Le texte dit qu'elle \"résulte souvent d'une sélection... des événements passés.\"" },
      { q: "Que révèle l'étude de la mémoire collective, selon le texte ?", options: ["Seulement des faits historiques", "Le passé et les préoccupations actuelles", "Uniquement l'avenir", "Rien d'utile"], correct: 1, hint: "Regarde la dernière phrase.", explain: "Le texte dit qu'elle \"révèle autant de choses sur le passé que sur les préoccupations actuelles.\"" },
    ],
  },
  // ===================== C2 =====================
  {
    level: "C2",
    title: "L'économie de l'attention",
    text:
      "Depuis l'avènement des plateformes numériques, l'attention humaine est devenue une ressource " +
      "âprement disputée, au point que certains économistes n'hésitent plus à parler d'une véritable " +
      "« économie de l'attention ». Les modèles économiques de nombreuses entreprises technologiques " +
      "reposent en effet sur la captation et la monétisation du temps que les utilisateurs consacrent à " +
      "leurs services, ce qui les incite à concevoir des interfaces exploitant délibérément certains " +
      "biais cognitifs, tels que la gratification immédiate ou la peur de manquer une information. Si ces " +
      "mécanismes se révèlent redoutablement efficaces sur le plan commercial, ils suscitent également " +
      "une inquiétude croissante quant à leurs répercussions sur la santé mentale, la capacité de " +
      "concentration et, plus largement, la qualité du débat démocratique. Face à ce constat, certains " +
      "chercheurs plaident pour une régulation contraignante de ces pratiques, tandis que d'autres " +
      "privilégient une approche fondée sur l'éducation aux médias et le développement de " +
      "l'autodiscipline numérique. Quoi qu'il en soit, il apparaît de plus en plus clairement que la " +
      "rareté de l'attention constitue désormais un enjeu de société à part entière, comparable par son " +
      "ampleur aux grandes questions environnementales ou économiques de notre époque.",
    questions: [
      { q: "Que sont certains économistes en train de qualifier de véritable ressource disputée ?", options: ["Le temps de travail", "L'attention humaine", "L'argent", "Les matières premières"], correct: 1, hint: "Regarde la première phrase.", explain: "Le texte dit que l'attention humaine est devenue « une ressource âprement disputée »." },
      { q: "Sur quoi reposent les modèles économiques de nombreuses entreprises technologiques ?", options: ["Sur la vente de produits physiques", "Sur la captation et la monétisation du temps des utilisateurs", "Sur les impôts", "Sur les dons"], correct: 1, hint: "Regarde la deuxième phrase.", explain: "Le texte dit que ces modèles reposent « sur la captation et la monétisation du temps que les utilisateurs consacrent à leurs services »." },
      { q: "Quels biais cognitifs les interfaces exploitent-elles, selon le texte ?", options: ["L'ennui et la fatigue", "La gratification immédiate et la peur de manquer une information", "La mémoire et la logique", "Aucun biais"], correct: 1, hint: "Regarde la deuxième phrase.", explain: "Le texte cite « la gratification immédiate ou la peur de manquer une information »." },
      { q: "Que proposent certains chercheurs face à ce constat ?", options: ["Ne rien faire", "Une régulation contraignante de ces pratiques", "Supprimer Internet", "Ignorer le problème"], correct: 1, hint: "Regarde la phrase sur les chercheurs.", explain: "Le texte dit que certains chercheurs « plaident pour une régulation contraignante de ces pratiques »." },
      { q: "À quoi la rareté de l'attention est-elle comparée dans la dernière phrase ?", options: ["À un jeu", "Aux grandes questions environnementales ou économiques", "À une mode passagère", "À un problème mineur"], correct: 1, hint: "Regarde la dernière phrase.", explain: "Le texte dit que cet enjeu est « comparable par son ampleur aux grandes questions environnementales ou économiques »." },
    ],
  },
  {
    level: "C2",
    title: "La post-vérité et la confiance envers les médias",
    text:
      "L'expression « post-vérité », consacrée en 2016 par les dictionnaires anglophones, désigne un " +
      "contexte dans lequel les faits objectifs pèsent moins sur l'opinion publique que les émotions et " +
      "les convictions personnelles. Ce phénomène, loin d'être totalement inédit, semble néanmoins " +
      "s'être amplifié avec la prolifération des réseaux sociaux, lesquels favorisent la diffusion rapide " +
      "d'informations non vérifiées et la formation de communautés qui ne s'exposent qu'à des contenus " +
      "confirmant leurs croyances préexistantes. Cette dynamique, souvent qualifiée de « bulle de " +
      "filtre », tend à renforcer la polarisation des débats publics et à éroder la confiance envers les " +
      "institutions traditionnellement chargées de vérifier l'information, telles que les médias ou les " +
      "organismes scientifiques. Certains observateurs estiment que la multiplication des dispositifs de " +
      "vérification des faits (« fact-checking ») constitue une réponse insuffisante, dans la mesure où " +
      "elle intervient généralement après la diffusion massive d'une fausse information, alors que " +
      "celle-ci a déjà produit ses effets sur les représentations collectives. D'autres soulignent, à " +
      "l'inverse, que le renforcement de l'esprit critique et de l'éducation aux médias dès le plus jeune " +
      "âge demeure la stratégie la plus prometteuse pour endiguer durablement ce phénomène.",
    questions: [
      { q: "Que désigne l'expression « post-vérité », selon le texte ?", options: ["Un genre littéraire", "Un contexte où les émotions pèsent plus que les faits objectifs", "Une nouvelle technologie", "Un parti politique"], correct: 1, hint: "Regarde la première phrase.", explain: "Le texte dit que cela désigne un contexte où « les faits objectifs pèsent moins sur l'opinion publique que les émotions et les convictions personnelles »." },
      { q: "Qu'est-ce qui a amplifié ce phénomène, selon le texte ?", options: ["La télévision", "La prolifération des réseaux sociaux", "Les livres", "Le cinéma"], correct: 1, hint: "Regarde la deuxième phrase.", explain: "Le texte dit que le phénomène « semble...s'être amplifié avec la prolifération des réseaux sociaux »." },
      { q: "Comment appelle-t-on la dynamique qui renforce la polarisation des débats ?", options: ["Une bulle de filtre", "Un cercle vertueux", "Une zone neutre", "Un consensus"], correct: 0, hint: "Regarde la troisième phrase.", explain: "Le texte dit que cette dynamique est « souvent qualifiée de bulle de filtre »." },
      { q: "Pourquoi certains observateurs jugent-ils le fact-checking insuffisant ?", options: ["Il coûte trop cher", "Il intervient après la diffusion massive de la fausse information", "Il est illégal", "Personne ne le lit"], correct: 1, hint: "Regarde la phrase sur le fact-checking.", explain: "Le texte dit qu'il « intervient généralement après la diffusion massive d'une fausse information »." },
      { q: "Quelle stratégie est jugée la plus prometteuse par d'autres observateurs ?", options: ["L'interdiction des réseaux sociaux", "Le renforcement de l'esprit critique et de l'éducation aux médias", "La censure", "La publicité"], correct: 1, hint: "Regarde la dernière phrase.", explain: "Le texte dit que « le renforcement de l'esprit critique et de l'éducation aux médias...demeure la stratégie la plus prometteuse »." },
    ],
  },
  // ----- extra passages (added to strengthen French coverage) -----
  {
    level: "A1",
    title: "Au marché",
    text:
      "Le samedi matin, Léa va au marché avec sa mère. Elles achètent des fruits et des légumes. " +
      "Léa aime les pommes rouges. Sa mère achète des tomates, des carottes et du pain. Le marchand " +
      "est très gentil. Il donne une banane à Léa. Après, elles rentrent à la maison en bus.",
    questions: [
      { q: "Quand Léa va-t-elle au marché ?", options: ["Le lundi matin", "Le samedi matin", "Le dimanche soir", "Le vendredi"], correct: 1, hint: "Regarde la première phrase.", explain: "Le texte dit : « Le samedi matin, Léa va au marché »." },
      { q: "Avec qui va-t-elle au marché ?", options: ["Son père", "Sa sœur", "Sa mère", "Son ami"], correct: 2, hint: "Regarde la première phrase.", explain: "Le texte dit : « ...avec sa mère »." },
      { q: "Quels fruits Léa aime-t-elle ?", options: ["Les bananes", "Les pommes rouges", "Les oranges", "Les fraises"], correct: 1, hint: "Regarde la phrase sur Léa.", explain: "Le texte dit : « Léa aime les pommes rouges »." },
      { q: "Qu'est-ce que le marchand donne à Léa ?", options: ["Une pomme", "Une tomate", "Une banane", "Du pain"], correct: 2, hint: "Regarde la phrase sur le marchand.", explain: "Le texte dit : « Il donne une banane à Léa »." },
      { q: "Comment rentrent-elles à la maison ?", options: ["À pied", "En bus", "En voiture", "En train"], correct: 1, hint: "Regarde la dernière phrase.", explain: "Le texte dit : « ...elles rentrent à la maison en bus »." },
    ],
  },
  {
    level: "A2",
    title: "Une journée à l'école",
    text:
      "Thomas a treize ans et il va au collège. Le matin, il se lève à sept heures et prend son " +
      "petit-déjeuner. Les cours commencent à huit heures et demie. Thomas préfère les mathématiques " +
      "et l'histoire. À midi, il déjeune à la cantine avec ses amis. L'après-midi, il a un cours de " +
      "sport. Après l'école, il fait ses devoirs, puis il joue au football dans le parc.",
    questions: [
      { q: "Quel âge a Thomas ?", options: ["Douze ans", "Treize ans", "Quatorze ans", "Onze ans"], correct: 1, hint: "Regarde la première phrase.", explain: "Le texte dit : « Thomas a treize ans »." },
      { q: "À quelle heure se lève-t-il ?", options: ["Six heures", "Sept heures", "Huit heures", "Neuf heures"], correct: 1, hint: "Regarde la phrase sur le matin.", explain: "Le texte dit : « il se lève à sept heures »." },
      { q: "Quelles matières préfère-t-il ?", options: ["Le français et l'anglais", "Les mathématiques et l'histoire", "La chimie et la biologie", "La musique et le dessin"], correct: 1, hint: "Regarde la phrase avec le mot « préfère ».", explain: "Le texte dit : « Thomas préfère les mathématiques et l'histoire »." },
      { q: "Où déjeune-t-il à midi ?", options: ["À la maison", "Au restaurant", "À la cantine", "Dans le parc"], correct: 2, hint: "Regarde la phrase sur midi.", explain: "Le texte dit : « il déjeune à la cantine avec ses amis »." },
      { q: "Que fait-il juste après l'école ?", options: ["Il dort", "Il fait ses devoirs", "Il regarde la télé", "Il va au cinéma"], correct: 1, hint: "Regarde la dernière phrase.", explain: "Le texte dit : « Après l'école, il fait ses devoirs »." },
    ],
  },
  {
    level: "B1",
    title: "Les vacances à la mer",
    text:
      "L'été dernier, ma famille et moi avons passé deux semaines dans un petit village au bord de la " +
      "mer. Nous avons loué une maison avec un grand jardin. Chaque matin, nous allions à la plage pour " +
      "nager et construire des châteaux de sable. L'après-midi, mon père louait des vélos et nous " +
      "explorions les environs. Un soir, nous avons mangé du poisson frais dans un restaurant près du " +
      "port. Ces vacances resteront un souvenir inoubliable pour toute la famille.",
    questions: [
      { q: "Où la famille a-t-elle passé ses vacances ?", options: ["À la montagne", "Dans un village au bord de la mer", "À la campagne", "En ville"], correct: 1, hint: "Regarde la première phrase.", explain: "Le texte dit : « ...dans un petit village au bord de la mer »." },
      { q: "Combien de temps sont-ils restés ?", options: ["Une semaine", "Deux semaines", "Un mois", "Trois jours"], correct: 1, hint: "Regarde la première phrase.", explain: "Le texte dit : « ...avons passé deux semaines »." },
      { q: "Que faisaient-ils chaque matin ?", options: ["Ils dormaient", "Ils allaient à la plage", "Ils faisaient les courses", "Ils travaillaient"], correct: 1, hint: "Regarde la phrase sur le matin.", explain: "Le texte dit : « Chaque matin, nous allions à la plage »." },
      { q: "Que louait le père l'après-midi ?", options: ["Des bateaux", "Des vélos", "Des voitures", "Des chevaux"], correct: 1, hint: "Regarde la phrase sur l'après-midi.", explain: "Le texte dit : « mon père louait des vélos »." },
      { q: "Qu'ont-ils mangé au restaurant près du port ?", options: ["De la viande", "Du poisson frais", "Des pâtes", "Une pizza"], correct: 1, hint: "Regarde la phrase sur le restaurant.", explain: "Le texte dit : « nous avons mangé du poisson frais »." },
    ],
  },
  {
    level: "B2",
    title: "Le télétravail",
    text:
      "Depuis quelques années, le télétravail s'est considérablement développé, notamment à la suite de " +
      "la pandémie. Travailler depuis chez soi présente de nombreux avantages : les employés économisent " +
      "du temps de transport, bénéficient d'une plus grande flexibilité et parviennent souvent à mieux " +
      "concilier vie professionnelle et vie privée. Cependant, cette pratique comporte aussi des " +
      "inconvénients. Certains salariés se sentent isolés et éprouvent des difficultés à séparer le " +
      "travail des loisirs. De plus, la communication au sein des équipes peut devenir plus compliquée. " +
      "Pour beaucoup d'entreprises, la solution idéale semble être un modèle hybride, combinant quelques " +
      "jours au bureau et quelques jours à distance.",
    questions: [
      { q: "Qu'est-ce qui a accéléré le développement du télétravail ?", options: ["La pandémie", "Les vacances", "La météo", "Les transports en commun"], correct: 0, hint: "Regarde la première phrase.", explain: "Le texte dit : « ...notamment à la suite de la pandémie »." },
      { q: "Quel avantage du télétravail est cité ?", options: ["Plus de réunions", "Une meilleure conciliation vie pro / vie privée", "Un salaire plus élevé", "Moins de vacances"], correct: 1, hint: "Regarde la liste des avantages.", explain: "Le texte cite le fait de « mieux concilier vie professionnelle et vie privée »." },
      { q: "Quel inconvénient est mentionné ?", options: ["Le sentiment d'isolement", "Le bruit du bureau", "Le manque d'ordinateurs", "Les longs trajets"], correct: 0, hint: "Regarde la partie sur les inconvénients.", explain: "Le texte dit : « Certains salariés se sentent isolés »." },
      { q: "Qu'est-ce qui peut devenir plus compliqué ?", options: ["La communication dans les équipes", "Le paiement des salaires", "L'achat de matériel", "Les congés payés"], correct: 0, hint: "Cherche le mot « communication ».", explain: "Le texte dit : « la communication au sein des équipes peut devenir plus compliquée »." },
      { q: "Quelle solution semble idéale pour beaucoup d'entreprises ?", options: ["Le tout-présentiel", "Le modèle hybride", "La suppression du télétravail", "Le travail de nuit"], correct: 1, hint: "Regarde la dernière phrase.", explain: "Le texte dit : « la solution idéale semble être un modèle hybride »." },
    ],
  },
  {
    level: "C1",
    title: "L'intelligence artificielle au quotidien",
    text:
      "L'intelligence artificielle s'est progressivement immiscée dans notre quotidien, souvent sans que " +
      "nous en ayons pleinement conscience. Des assistants vocaux aux algorithmes de recommandation, en " +
      "passant par les filtres anti-spam et la traduction automatique, ces technologies façonnent " +
      "désormais une multitude de nos interactions. Si leurs bénéfices en termes de confort et " +
      "d'efficacité sont indéniables, elles soulèvent également des interrogations légitimes. La " +
      "protection des données personnelles, les biais susceptibles d'être reproduits, voire amplifiés, " +
      "par les systèmes automatisés, ainsi que les répercussions sur l'emploi alimentent un débat de " +
      "société de plus en plus vif. Face à ces enjeux, de nombreux experts plaident pour une régulation " +
      "qui permettrait de tirer parti de ces innovations tout en préservant les droits fondamentaux des " +
      "citoyens.",
    questions: [
      { q: "Comment l'IA s'est-elle introduite dans le quotidien, selon le texte ?", options: ["De façon très visible", "Souvent sans qu'on en ait pleinement conscience", "Uniquement dans les entreprises", "Grâce à la publicité"], correct: 1, hint: "Regarde la première phrase.", explain: "Le texte dit : « ...souvent sans que nous en ayons pleinement conscience »." },
      { q: "Quel exemple de technologie n'est PAS cité ?", options: ["Les assistants vocaux", "Les filtres anti-spam", "Les voitures de course", "La traduction automatique"], correct: 2, hint: "Compare la liste d'exemples avec les options.", explain: "Le texte cite les assistants vocaux, les algorithmes, les filtres anti-spam et la traduction automatique, mais pas les voitures de course." },
      { q: "Quelle inquiétude concernant les données est mentionnée ?", options: ["Leur coût", "La protection des données personnelles", "Leur stockage physique", "Leur traduction"], correct: 1, hint: "Cherche le mot « données ».", explain: "Le texte évoque « la protection des données personnelles »." },
      { q: "Quel risque lié aux systèmes automatisés est évoqué ?", options: ["La reproduction de biais", "Leur lenteur", "Leur prix élevé", "Leur consommation d'eau"], correct: 0, hint: "Cherche le mot « biais ».", explain: "Le texte parle des « biais susceptibles d'être reproduits, voire amplifiés »." },
      { q: "Que réclament de nombreux experts ?", options: ["L'interdiction totale de l'IA", "Une régulation préservant les droits fondamentaux", "Plus de publicité", "La suppression des données"], correct: 1, hint: "Regarde la dernière phrase.", explain: "Le texte dit que les experts « plaident pour une régulation...préservant les droits fondamentaux »." },
    ],
  },
  {
    level: "C2",
    title: "La biodiversité et son érosion",
    text:
      "L'érosion de la biodiversité constitue, aux côtés du dérèglement climatique, l'un des défis " +
      "environnementaux majeurs de notre époque. Sous l'effet conjugué de la destruction des habitats " +
      "naturels, de la surexploitation des ressources, de la pollution et de l'introduction d'espèces " +
      "invasives, le rythme actuel de disparition des espèces atteindrait un niveau sans précédent " +
      "depuis les grandes extinctions géologiques. Or, cette diversité du vivant n'est pas une simple " +
      "richesse esthétique : elle sous-tend le fonctionnement des écosystèmes dont dépendent, in fine, " +
      "la pollinisation des cultures, la purification de l'eau ou la régulation du climat. Les " +
      "politiques de conservation, longtemps cantonnées à la création d'aires protégées, tendent " +
      "désormais à privilégier une approche plus systémique, intégrant les activités humaines à la " +
      "préservation du vivant. Reste que la mise en œuvre de telles stratégies se heurte à des " +
      "arbitrages économiques et politiques d'une redoutable complexité.",
    questions: [
      { q: "À quel autre défi la biodiversité est-elle comparée ?", options: ["Au dérèglement climatique", "À la pauvreté", "Au chômage", "À l'inflation"], correct: 0, hint: "Regarde la première phrase.", explain: "Le texte parle de la biodiversité « aux côtés du dérèglement climatique »." },
      { q: "Quel facteur d'érosion n'est PAS cité ?", options: ["La destruction des habitats", "La surexploitation des ressources", "Le tourisme spatial", "La pollution"], correct: 2, hint: "Compare la liste des causes avec les options.", explain: "Le texte cite la destruction des habitats, la surexploitation, la pollution et les espèces invasives, mais pas le tourisme spatial." },
      { q: "La biodiversité est-elle une simple richesse esthétique ?", options: ["Oui, uniquement", "Non, elle sous-tend le fonctionnement des écosystèmes", "Oui, mais coûteuse", "Le texte ne le dit pas"], correct: 1, hint: "Cherche l'expression « richesse esthétique ».", explain: "Le texte dit qu'elle « n'est pas une simple richesse esthétique » et qu'elle sous-tend les écosystèmes." },
      { q: "Quel service écosystémique est mentionné ?", options: ["La pollinisation des cultures", "La production de pétrole", "La fabrication d'acier", "Le transport routier"], correct: 0, hint: "Cherche ce dont « dépendent » les écosystèmes.", explain: "Le texte mentionne « la pollinisation des cultures »." },
      { q: "À quoi se heurte la mise en œuvre des stratégies de conservation ?", options: ["À un manque d'espèces", "À des arbitrages économiques et politiques complexes", "À l'absence d'aires protégées", "À la météo"], correct: 1, hint: "Regarde la dernière phrase.", explain: "Le texte dit qu'elle « se heurte à des arbitrages économiques et politiques d'une redoutable complexité »." },
    ],
  },
];
