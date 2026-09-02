/*! Top Words (udsp) — Copyright 2026 Bulent Ozkir, Ahmet Arda Ozkir, Halit Eren Ozkir
 * Licensed under the PolyForm Noncommercial License 1.0.0 — NONCOMMERCIAL USE ONLY.
 * <https://polyformproject.org/licenses/noncommercial/1.0.0>
 *
 * Any commercial use requires prior written permission from the copyright
 * holders. Written permission from any ONE of bulentozkir@hotmail.com,
 * bulentozkir@gmail.com, ahmetardaozkir@gmail.com or haliterenozkir@gmail.com
 * is sufficient and binding on all of them.
 *
 * Required Notice: Copyright 2026 Bulent Ozkir, Ahmet Arda Ozkir, Halit Eren
 * Ozkir (https://udsp.vercel.app)
 * Full terms: see LICENSE and NOTICE in this repository.
 */
/*! Compilation notice — read together with the licence above.
 * The SELECTION, ARRANGEMENT, CEFR levelling, editing and Turkish
 * translations in this file are the copyright holders' own work and ARE
 * covered by the PolyForm Noncommercial licence above. Copying this list, or
 * any substantial part of it, into a commercial product or service requires
 * prior written permission from one of the addresses above.
 *
 * Some individual definitions and example sentences come from open community
 * projects (Wiktionary CC BY-SA 4.0, Tatoeba CC BY 2.0 FR, WordNet and
 * others — see NOTICE). Those items keep THEIR OWN licence and the
 * noncommercial term is not asserted over them.
 */

// French Synonyms & Antonyms (self-authored). Combines the previously
// separate data/synonymsfr.js + data/antonymsfr.js into ONE file, in the
// same structural format as data/synanten.js / data/synantde.js:
// - Single flat array, ONE entry per line, minified object literals
//   (no line breaks inside an entry), keys NOT quoted.
// - Schema: { word, level, category, definition, example, synonyms, antonyms }
//   -- note there is NO `pos` field here (unlike wordsXXfr.js), matching the
//   EN/DE synant files exactly. `level` (A1..C2) is a frequency-rank sextile
//   split (see scripts/synant_metadata.py); `category` is a topical domain
//   from scripts/classify_word_categories.py's keyword classifier, run
//   directly against this file's (already-English) `definition` gloss.
//   `synonyms`/`antonyms` are OMITTED (not
//   empty-string) whenever a word doesn't have that relation -- most
//   entries only have one of the two; words that appeared in BOTH source
//   lists (e.g. "grand" was in both the synonyms and antonyms lists) are
//   merged into a single entry carrying both fields.
// - `definition`/`example` use the SAME ";"-separated STRUCTURE as the
//   EN/DE files ("<text>;<Turkish>"), but since this file is hand-authored
//   (not run through scripts/translate_and_build_synant.py like the EN/DE
//   ones), `definition` is an English gloss of the French word (matching
//   this project's usual bilingual convention for hand-authored French
//   content), NOT a native-French dictionary definition like the EN/DE
//   files have for their own languages. Only the FILE FORMAT/schema was
//   matched here, not a full re-authoring into native French definitions.
// synonyms/antonyms are semicolon-separated when multiple values exist.
// Used by Word Morph (wordmorph.js), same as data/synanten.js / data/synantde.js.
window.SYN_ANT_FR = [
{word:"content",level:"B2",category:"Emotions",definition:"Happy, glad.;Mutlu, memnun.",example:"Elle est très contente de son nouveau travail.;Yeni işinden çok memnun.",synonyms:"joyeux; heureux"},
{word:"triste",level:"C1",category:"Emotions",definition:"Sad.;Üzgün.",example:"Il est triste depuis son départ.;Onun gidişinden beri üzgün.",synonyms:"malheureux; chagriné"},
{word:"grand",level:"A2",category:"General",definition:"Big, tall.;Büyük, uzun boylu.",example:"Ils habitent dans un grand appartement.;Büyük bir dairede oturuyorlar.",synonyms:"immense; vaste",antonyms:"petit"},
{word:"petit",level:"A1",category:"General",definition:"Small.;Küçük.",example:"Le chat est tout petit.;Kedi çok küçük.",synonyms:"minuscule; menu"},
{word:"beau",level:"A2",category:"General",definition:"Beautiful.;Güzel.",example:"C'est un beau paysage.;Bu güzel bir manzara.",synonyms:"joli; magnifique",antonyms:"laid"},
{word:"laid",level:"C2",category:"General",definition:"Ugly.;Çirkin.",example:"Ce bâtiment est vraiment laid.;Bu bina gerçekten çirkin.",synonyms:"moche; disgracieux"},
{word:"difficile",level:"B1",category:"General",definition:"Difficult.;Zor.",example:"Cet examen est difficile.;Bu sınav zor.",synonyms:"compliqué; ardu"},
{word:"facile",level:"B1",category:"General",definition:"Easy.;Kolay.",example:"Cette recette est facile à suivre.;Bu tarifi takip etmek kolay.",synonyms:"simple; aisé",antonyms:"difficile"},
{word:"important",level:"B1",category:"General",definition:"Large, considerable, significant.;Büyük, hatırı sayılır, kayda değer.",example:"C'est une décision importante.;Bu önemli bir karar.",synonyms:"essentiel; capital"},
{word:"intelligent",level:"C1",category:"General",definition:"Who is endowed with the faculty of understanding, who is capable of conceiving and reasoning.;Anlama yetisine sahip olan, kavrama ve akıl yürütme yeteneğine sahip olan.",example:"Cette élève est très intelligente.;Bu öğrenci çok zeki.",synonyms:"brillant; futé"},
{word:"fort",level:"B1",category:"General",definition:"Strong.;Güçlü.",example:"Il est très fort physiquement.;O fiziksel olarak çok güçlü.",synonyms:"puissant; robuste",antonyms:"faible"},
{word:"faible",level:"C1",category:"General",definition:"Weak.;Zayıf.",example:"Après la maladie, il se sentait faible.;Hastalıktan sonra kendini zayıf hissetti.",synonyms:"fragile; frêle"},
{word:"riche",level:"C1",category:"General",definition:"Rich.;Zengin.",example:"Sa famille est très riche.;Ailesi çok zengin.",synonyms:"fortuné; aisé",antonyms:"pauvre"},
{word:"gentil",level:"B2",category:"General",definition:"Kind.;Nazik.",example:"Le voisin est très gentil.;Komşu çok nazik.",synonyms:"aimable; bienveillant",antonyms:"méchant"},
{word:"calme",level:"B2",category:"General",definition:"Calm.;Sakin.",example:"La mer est calme ce matin.;Bu sabah deniz sakin.",synonyms:"tranquille; paisible",antonyms:"agité"},
{word:"parler",level:"A1",category:"Communication",definition:"To speak / talk.;Konuşmak.",example:"Nous parlons de nos projets.;Projelerimizden bahsediyoruz.",synonyms:"discuter; converser"},
{word:"manger",level:"B1",category:"Food",definition:"To eat.;Yemek yemek.",example:"Ils mangent ensemble tous les soirs.;Her akşam birlikte yemek yiyorlar.",synonyms:"consommer; se nourrir"},
{word:"regarder",level:"B1",category:"General",definition:"To watch / look at.;İzlemek / bakmak.",example:"Elle regarde le film attentivement.;Filmi dikkatle izliyor.",synonyms:"observer; contempler"},
{word:"commencer",level:"B2",category:"General",definition:"To start.;Başlamak.",example:"Le spectacle commence à huit heures.;Gösteri saat sekizde başlıyor.",synonyms:"débuter; entamer",antonyms:"finir"},
{word:"finir",level:"B2",category:"General",definition:"To finish.;Bitirmek.",example:"Il a fini son travail tôt.;İşini erken bitirdi.",synonyms:"terminer; achever"},
{word:"aider",level:"A1",category:"General",definition:"To help.;Yardım etmek.",example:"Elle aide toujours ses amis.;O her zaman arkadaşlarına yardım eder.",synonyms:"assister; soutenir"},
{word:"chercher",level:"A2",category:"General",definition:"To look for.;Aramak.",example:"Je cherche mes clés partout.;Anahtarlarımı her yerde arıyorum.",synonyms:"rechercher; quêter"},
{word:"trouver",level:"A1",category:"General",definition:"To find.;Bulmak.",example:"Il a trouvé une solution simple.;Basit bir çözüm buldu.",synonyms:"découvrir; dénicher"},
{word:"penser",level:"B1",category:"General",definition:"To think.;Düşünmek.",example:"Je pense souvent à mes vacances.;Sık sık tatilimi düşünüyorum.",synonyms:"réfléchir; songer"},
{word:"aimer",level:"C1",category:"Emotions",definition:"To like / love.;Sevmek.",example:"Elle aime beaucoup la musique classique.;Klasik müziği çok seviyor.",synonyms:"adorer; apprécier",antonyms:"détester"},
{word:"montrer",level:"B2",category:"General",definition:"To show.;Göstermek.",example:"Il m'a montré ses photos de voyage.;Bana seyahat fotoğraflarını gösterdi.",synonyms:"indiquer; exposer"},
{word:"cacher",level:"C1",category:"General",definition:"To hide.;Gizlemek.",example:"Elle cache ses sentiments.;Duygularını gizliyor.",synonyms:"dissimuler; masquer",antonyms:"montrer"},
{word:"construire",level:"C2",category:"General",definition:"To build.;İnşa etmek.",example:"Ils construisent une nouvelle maison.;Yeni bir ev inşa ediyorlar.",synonyms:"bâtir; édifier",antonyms:"détruire"},
{word:"donner",level:"A2",category:"General",definition:"To give.;Vermek.",example:"Il donne des conseils utiles.;Faydalı tavsiyeler veriyor.",synonyms:"offrir; fournir",antonyms:"recevoir"},
{word:"arriver",level:"B1",category:"General",definition:"To arrive.;Varmak.",example:"Le train arrive à la gare.;Tren istasyona varıyor.",synonyms:"parvenir; atteindre",antonyms:"partir"},
{word:"la maison",level:"A1",category:"Household",definition:"House.;Ev.",example:"Notre maison a un grand jardin.;Evimizin büyük bir bahçesi var.",synonyms:"la demeure; le logis"},
{word:"la voiture",level:"A2",category:"Transportation",definition:"Car.;Araba.",example:"Sa voiture est neuve.;Onun arabası yeni.",synonyms:"l'automobile; le véhicule"},
{word:"le travail",level:"A2",category:"General",definition:"Work / job.;İş.",example:"Il aime son travail.;İşini seviyor.",synonyms:"l'emploi; le boulot"},
{word:"l'ami",level:"A2",category:"General",definition:"Friend.;Arkadaş.",example:"C'est mon meilleur ami.;O benim en iyi arkadaşım.",synonyms:"le camarade; le copain",antonyms:"l'ennemi"},
{word:"le problème",level:"A1",category:"General",definition:"Problem.;Sorun.",example:"Nous devons résoudre ce problème.;Bu sorunu çözmeliyiz.",synonyms:"la difficulté; l'ennui"},
{word:"l'idée",level:"A2",category:"General",definition:"Idea.;Fikir.",example:"C'est une excellente idée.;Bu mükemmel bir fikir.",synonyms:"la pensée; la notion"},
{word:"la peur",level:"A1",category:"Emotions",definition:"Fear.;Korku.",example:"Il a une peur des araignées.;Örümceklerden korkuyor.",synonyms:"la crainte; l'angoisse"},
{word:"la joie",level:"C2",category:"Emotions",definition:"Joy.;Sevinç.",example:"Quelle joie de te revoir !;Seni tekrar görmek ne büyük sevinç!",synonyms:"le bonheur; l'allégresse",antonyms:"la tristesse"},
{word:"le succès",level:"C2",category:"General",definition:"Success.;Başarı.",example:"Le film a eu un grand succès.;Film büyük başarı elde etti.",synonyms:"la réussite; le triomphe",antonyms:"l'échec"},
{word:"le début",level:"B2",category:"General",definition:"Beginning.;Başlangıç.",example:"Le début du roman est captivant.;Romanın başlangıcı sürükleyici.",synonyms:"le commencement; l'origine",antonyms:"la fin"},
{word:"la fin",level:"A2",category:"General",definition:"End.;Son.",example:"La fin du film était surprenante.;Filmin sonu şaşırtıcıydı.",synonyms:"la conclusion; le terme"},
{word:"le chemin",level:"B2",category:"General",definition:"Path / way.;Yol.",example:"Ce chemin mène à la forêt.;Bu yol ormana çıkıyor.",synonyms:"la route; le sentier"},
{word:"la ville",level:"A2",category:"General",definition:"City.;Şehir.",example:"Cette ville est très animée.;Bu şehir çok hareketli.",synonyms:"la cité; l'agglomération"},
{word:"la nourriture",level:"C1",category:"Food",definition:"Food.;Yiyecek.",example:"La nourriture ici est délicieuse.;Buradaki yiyecekler lezzetli.",synonyms:"l'alimentation; les aliments"},
{word:"le danger",level:"C1",category:"General",definition:"Jeopardy (danger of loss, harm, or failure).;Tehlike (kaybolma, zarar görme veya başarısızlık tehlikesi).",example:"Ce sentier présente un danger réel.;Bu patika gerçek bir tehlike içeriyor.",synonyms:"le péril; le risque"},
{word:"souvent",level:"B2",category:"General",definition:"Often.;Sık sık.",example:"Nous nous voyons souvent.;Sık sık görüşüyoruz.",synonyms:"fréquemment; régulièrement",antonyms:"rarement"},
{word:"toujours",level:"A1",category:"General",definition:"Always.;Her zaman.",example:"Il est toujours ponctuel.;O her zaman dakiktir.",synonyms:"constamment; sans cesse",antonyms:"jamais"},
{word:"vite",level:"A2",category:"General",definition:"Quickly.;Hızlı / çabuk.",example:"Viens vite, s'il te plaît !;Çabuk gel, lütfen!",synonyms:"rapidement; promptement"},
{word:"beaucoup",level:"A1",category:"General",definition:"A lot.;Çok.",example:"Il a beaucoup voyagé.;Çok seyahat etti.",synonyms:"énormément; considérablement",antonyms:"peu"},
{word:"maintenant",level:"A1",category:"General",definition:"Now.;Şimdi.",example:"Je dois partir maintenant.;Şimdi gitmem gerekiyor.",synonyms:"actuellement; à présent",antonyms:"plus tard"},
{word:"bientôt",level:"B1",category:"General",definition:"Soon.;Yakında.",example:"Nous partons bientôt en vacances.;Yakında tatile gidiyoruz.",synonyms:"prochainement; sous peu"},
{word:"aussi",level:"A1",category:"General",definition:"Also.;Ayrıca / de.",example:"Elle parle aussi espagnol.;O ayrıca İspanyolca da konuşuyor.",synonyms:"également; pareillement"},
{word:"cependant",level:"C2",category:"General",definition:"However.;Ancak.",example:"Nous nous amusons, et cependant la nuit vient.;Eğleniyoruz ama yine de gece geliyor.",synonyms:"toutefois; néanmoins"},
{word:"enfin",level:"A2",category:"General",definition:"Finally.;Sonunda.",example:"Enfin, le train est arrivé.;Sonunda tren geldi.",synonyms:"finalement; à la fin"},
{word:"ensuite",level:"B1",category:"General",definition:"Then / next.;Sonra.",example:"D'abord on mange, ensuite on sort.;Önce yeriz, sonra çıkarız.",synonyms:"puis; après"},
{word:"presque",level:"B1",category:"General",definition:"Almost.;Neredeyse.",example:"J'ai presque fini mon travail.;İşimi neredeyse bitirdim.",synonyms:"quasiment; à peu près"},
{word:"vraiment",level:"A1",category:"General",definition:"Really.;Gerçekten.",example:"C'est vraiment gentil de ta part.;Bu senden gerçekten çok kibarca.",synonyms:"réellement; véritablement"},
{word:"facilement",level:"C2",category:"General",definition:"Easily.;Kolayca.",example:"Il apprend facilement les langues.;Dilleri kolayca öğreniyor.",synonyms:"aisément; sans peine"},
{word:"clairement",level:"C2",category:"General",definition:"Clearly.;Açıkça.",example:"Explique-moi clairement la situation.;Durumu bana açıkça anlat.",synonyms:"distinctement; nettement"},
{word:"simplement",level:"B2",category:"General",definition:"Simply.;Basitçe.",example:"Il a simplement dit non.;Basitçe hayır dedi.",synonyms:"tout bonnement; purement"},
{word:"bon",level:"A1",category:"General",definition:"Good.;İyi.",example:"Ce restaurant est très bon.;Bu restoran çok iyi.",antonyms:"mauvais"},
{word:"chaud",level:"C1",category:"General",definition:"Hot.;Sıcak.",example:"Le café est encore chaud.;Kahve hâlâ sıcak.",antonyms:"froid"},
{word:"rapide",level:"C1",category:"General",definition:"Fast.;Hızlı.",example:"Cette voiture est très rapide.;Bu araba çok hızlı.",antonyms:"lent"},
{word:"jeune",level:"A2",category:"General",definition:"Young.;Genç.",example:"Le chien est encore jeune.;Köpek hâlâ genç.",antonyms:"vieux"},
{word:"heureux",level:"B1",category:"Emotions",definition:"Happy.;Mutlu.",example:"Elle semble très heureuse aujourd'hui.;Bugün çok mutlu görünüyor.",antonyms:"malheureux"},
{word:"propre",level:"B1",category:"General",definition:"Clean.;Temiz.",example:"La cuisine est bien propre.;Mutfak oldukça temiz.",antonyms:"sale"},
{word:"plein",level:"B1",category:"General",definition:"Full.;Dolu.",example:"La bouteille est encore pleine.;Şişe hâlâ dolu.",antonyms:"vide"},
{word:"ouvert",level:"C1",category:"General",definition:"Open.;Açık.",example:"Le magasin est ouvert le dimanche.;Mağaza pazar günleri açık.",antonyms:"fermé"},
{word:"clair",level:"B2",category:"General",definition:"Bright.;Aydınlık.",example:"La pièce est très claire le matin.;Oda sabahları çok aydınlık.",antonyms:"sombre"},
{word:"large",level:"C2",category:"General",definition:"Wide.;Geniş.",example:"Cette avenue est très large.;Bu cadde çok geniş.",antonyms:"étroit"},
{word:"lourd",level:"C2",category:"General",definition:"Heavy.;Ağır.",example:"Cette valise est trop lourde.;Bu bavul çok ağır.",antonyms:"léger"},
{word:"cher",level:"B2",category:"General",definition:"Expensive.;Pahalı.",example:"Cet hôtel est trop cher pour nous.;Bu otel bizim için çok pahalı.",antonyms:"bon marché"},
{word:"simple",level:"B2",category:"General",definition:"Simple, plain, unsophisticated.;Basit, sade, sade.",example:"La solution est en fait très simple.;Çözüm aslında çok basit.",antonyms:"compliqué"},
{word:"monter",level:"C1",category:"General",definition:"To go up.;Çıkmak.",example:"Nous montons les escaliers ensemble.;Merdivenleri birlikte çıkıyoruz.",antonyms:"descendre"},
{word:"entrer",level:"B1",category:"General",definition:"To enter.;Girmek.",example:"Il entre dans la salle en silence.;Odaya sessizce giriyor.",antonyms:"sortir"},
{word:"acheter",level:"B2",category:"General",definition:"To buy.;Satın almak.",example:"Nous allons acheter une nouvelle voiture.;Yeni bir araba satın alacağız.",antonyms:"vendre"},
{word:"gagner",level:"B2",category:"General",definition:"To win.;Kazanmak.",example:"Notre équipe a gagné le match.;Takımımız maçı kazandı.",antonyms:"perdre"},
{word:"ouvrir",level:"C1",category:"General",definition:"To open.;Açmak.",example:"Peux-tu ouvrir la fenêtre ?;Pencereyi açabilir misin?",antonyms:"fermer"},
{word:"accepter",level:"C1",category:"General",definition:"To accept.;Kabul etmek.",example:"Elle a accepté notre invitation.;Davetimizi kabul etti.",antonyms:"refuser"},
{word:"se souvenir",level:"C2",category:"General",definition:"To remember.;Hatırlamak.",example:"Souvenez-vous-en.;Bunu hatırla.",antonyms:"oublier"},
{word:"réussir",level:"C2",category:"General",definition:"To succeed.;Başarmak.",example:"Elle a réussi tous ses examens.;Tüm sınavlarını başardı.",antonyms:"échouer"},
{word:"permettre",level:"C2",category:"General",definition:"To allow.;İzin vermek.",example:"Le règlement permet les visites le soir.;Yönetmelik akşam ziyaretlerine izin veriyor.",antonyms:"interdire"},
{word:"augmenter",level:"C2",category:"General",definition:"To increase.;Artırmak.",example:"Les prix augmentent chaque année.;Fiyatlar her yıl artıyor.",antonyms:"diminuer"},
{word:"attaquer",level:"C2",category:"General",definition:"To attack.;Saldırmak.",example:"L'armée a attaqué à l'aube.;Ordu şafak vakti saldırdı.",antonyms:"défendre"},
{word:"rire",level:"C1",category:"General",definition:"To laugh.;Gülmek.",example:"Nous serons en joyeuse compagnie, nous rirons bien. Venez avec nous, nous rirons. C’est un bon garçon qui aime à rire.;Mutlu bir birliktelik içinde olacağız, çok güleceğiz. Bizimle gelin, gülelim. Gülmeyi seven iyi bir çocuktur.",antonyms:"pleurer"},
{word:"se lever",level:"C2",category:"General",definition:"To get up.;Kalkmak.",example:"Je me lève tôt tous les jours.;Her gün erken kalkıyorum.",antonyms:"se coucher"},
{word:"économiser",level:"C2",category:"General",definition:"To save (money).;Biriktirmek.",example:"Elle économise pour ses vacances.;Tatili için para biriktiriyor.",antonyms:"dépenser"},
{word:"le jour",level:"A1",category:"General",definition:"Day.;Gündüz.",example:"Il travaille le jour et étudie le soir.;Gündüz çalışıyor, akşam ders çalışıyor.",antonyms:"la nuit"},
{word:"la guerre",level:"B1",category:"Military",definition:"War.;Savaş.",example:"Cette région a connu la guerre pendant des années.;Bu bölge yıllarca savaş yaşadı.",antonyms:"la paix"},
{word:"la vérité",level:"A2",category:"General",definition:"Truth.;Gerçek.",example:"Il faut toujours dire la vérité.;Her zaman gerçeği söylemek gerekir.",antonyms:"le mensonge"},
{word:"le nord",level:"C1",category:"General",definition:"North.;Kuzey.",example:"Nous voyageons vers le nord cet été.;Bu yaz kuzeye seyahat ediyoruz.",antonyms:"le sud"},
{word:"l'est",level:"A1",category:"General",definition:"East.;Doğu.",example:"Le soleil se lève à l'est.;Güneş doğudan doğar.",antonyms:"l'ouest"},
{word:"la question",level:"A2",category:"General",definition:"Issue, matter, topic, problem.;Konu, mesele, konu, sorun.",example:"C'est une question difficile.;Bu zor bir soru.",antonyms:"la réponse"},
{word:"ici",level:"A1",category:"General",definition:"Here.;Burada.",example:"Viens ici, s'il te plaît.;Buraya gel, lütfen.",antonyms:"là-bas"},
{word:"tôt",level:"B2",category:"General",definition:"Early.;Erken.",example:"Elle se réveille tôt chaque matin.;Her sabah erken uyanır.",antonyms:"tard"},
{word:"dedans",level:"C1",category:"General",definition:"Inside.;İçeride.",example:"Il fait froid, restons dedans.;Hava soğuk, içeride kalalım.",antonyms:"dehors"},
{word:"dessus",level:"B1",category:"General",definition:"Above / on top.;Üstünde.",example:"Mets le livre dessus la table.;Kitabı masanın üstüne koy.",antonyms:"dessous"},
{word:"devant",level:"A2",category:"General",definition:"In front.;Önünde.",example:"Elle attend devant la porte.;Kapının önünde bekliyor.",antonyms:"derrière"},
{word:"avant",level:"A1",category:"General",definition:"Before.;Önce.",example:"Réfléchis avant de parler.;Konuşmadan önce düşün.",antonyms:"après"},
{word:"joli",level:"A1",category:"General",definition:"Pretty, nice-looking.;Güzel, hoş.",example:"Elle porte une jolie robe bleue.;Güzel mavi bir elbise giyiyor.",synonyms:"beau; mignon",antonyms:"laid"},
{word:"nouveau",level:"A1",category:"General",definition:"New.;Yeni.",example:"J'ai acheté un nouveau téléphone.;Yeni bir telefon aldım.",synonyms:"récent; neuf",antonyms:"vieux; ancien"},
{word:"vieux",level:"A2",category:"General",definition:"Old.;Yaşlı, eski.",example:"Mon grand-père est très vieux.;Büyükbabam çok yaşlı.",synonyms:"âgé; ancien",antonyms:"jeune; nouveau"},
{word:"vrai",level:"A2",category:"General",definition:"True, real.;Doğru, gerçek.",example:"C'est une histoire vraie.;Bu gerçek bir hikaye.",synonyms:"authentique; réel",antonyms:"faux"},
{word:"faux",level:"B1",category:"General",definition:"False, wrong.;Yanlış, sahte.",example:"Le jockey C. a déclaré que cette pouliche avait manifesté le même comportement à Toulouse en provoquant un faux départ.;Jokey C., bu kısrağın yanlış çıkışa neden olarak Toulouse'da da aynı davranışı gösterdiğini açıkladı.",synonyms:"incorrect; erroné",antonyms:"vrai"},
{word:"long",level:"A2",category:"General",definition:"Length.;Uzunluk.",example:"Le film est très long.;Film çok uzun.",synonyms:"allongé; interminable",antonyms:"court"},
{word:"court",level:"A2",category:"General",definition:"Short.;Kısa.",example:"Elle a les cheveux courts.;Saçları kısa.",synonyms:"bref",antonyms:"long"},
{word:"haut",level:"A2",category:"General",definition:"High, tall.;Yüksek.",example:"La montagne est très haute.;Dağ çok yüksek.",synonyms:"élevé",antonyms:"bas"},
{word:"bas",level:"A2",category:"General",definition:"Low.;Alçak.",example:"Le plafond est trop bas.;Tavan çok alçak.",synonyms:"inférieur",antonyms:"haut"},
{word:"froid",level:"A1",category:"Weather",definition:"Cold.;Soğuk.",example:"L'eau est froide.;Su soğuk.",synonyms:"frais; glacial",antonyms:"chaud"},
{word:"sale",level:"A2",category:"General",definition:"Dirty.;Kirli.",example:"Tes mains sont sales.;Ellerin kirli.",synonyms:"crasseux; malpropre",antonyms:"propre"},
{word:"dur",level:"A2",category:"General",definition:"Hard, tough.;Sert, zor.",example:"Le pain est dur.;Ekmek sert.",synonyms:"rigide; ferme",antonyms:"mou"},
{word:"mou",level:"B1",category:"General",definition:"Soft.;Yumuşak.",example:"Le matelas est trop mou.;Yatak çok yumuşak.",synonyms:"tendre; moelleux",antonyms:"dur"},
{word:"doux",level:"A2",category:"General",definition:"Sweet, gentle, soft.;Tatlı, yumuşak.",example:"Que cet air de Caccini est doux !;Bu Caccini melodisi ne kadar tatlı!",synonyms:"tendre; sucré",antonyms:"amer"},
{word:"amer",level:"B2",category:"Food",definition:"Bitter.;Acı.",example:"Ce café est trop amer.;Bu kahve çok acı.",synonyms:"âcre",antonyms:"doux; sucré"},
{word:"léger",level:"A2",category:"General",definition:"Light, not heavy.;Hafif.",example:"Ce sac est très léger.;Bu çanta çok hafif.",synonyms:"fin",antonyms:"lourd"},
{word:"gros",level:"A1",category:"General",definition:"Big, fat.;İri, şişman.",example:"C'est un gros chien.;Bu iri bir köpek.",synonyms:"corpulent; volumineux",antonyms:"mince; maigre"},
{word:"mince",level:"A2",category:"General",definition:"Thin, slim.;İnce, zayıf.",example:"Elle est grande et mince.;O uzun ve ince.",synonyms:"fin; svelte",antonyms:"gros; épais"},
{word:"épais",level:"B1",category:"General",definition:"Thick.;Kalın.",example:"Le mur est très épais.;Duvar çok kalın.",synonyms:"dense",antonyms:"mince; fin"},
{word:"fatigué",level:"A1",category:"General",definition:"Tired.;Yorgun.",example:"Je suis fatigué après le travail.;İşten sonra yorgunum.",synonyms:"épuisé; las",antonyms:"reposé"},
{word:"malade",level:"A1",category:"Medicine",definition:"Sick, ill.;Hasta.",example:"Mon fils est malade aujourd'hui.;Oğlum bugün hasta.",synonyms:"souffrant",antonyms:"sain"},
{word:"fâché",level:"A2",category:"Emotions",definition:"Angry, upset.;Kızgın, dargın.",example:"Il est fâché contre son frère.;Kardeşine kızgın.",synonyms:"en colère; furieux",antonyms:"content"},
{word:"inquiet",level:"B1",category:"Emotions",definition:"Worried, anxious.;Endişeli.",example:"Elle est inquiète pour ses enfants.;Çocukları için endişeli.",synonyms:"anxieux; soucieux",antonyms:"serein"},
{word:"tranquille",level:"A2",category:"Emotions",definition:"Calm, quiet.;Sakin.",example:"Le village est très tranquille.;Köy çok sakin.",synonyms:"paisible; calme",antonyms:"agité"},
{word:"bruyant",level:"B1",category:"General",definition:"Noisy, loud.;Gürültülü.",example:"Le restaurant est trop bruyant.;Restoran çok gürültülü.",synonyms:"sonore",antonyms:"silencieux"},
{word:"silencieux",level:"B1",category:"General",definition:"Silent, quiet.;Sessiz.",example:"La bibliothèque est silencieuse.;Kütüphane sessiz.",synonyms:"muet; calme",antonyms:"bruyant"},
{word:"puissant",level:"B1",category:"General",definition:"Powerful, strong.;Güçlü.",example:"C'est un moteur puissant.;Bu güçlü bir motor.",synonyms:"fort; robuste",antonyms:"faible"},
{word:"courageux",level:"B1",category:"Emotions",definition:"Brave, courageous.;Cesur.",example:"Le pompier est très courageux.;İtfaiyeci çok cesur.",synonyms:"brave; vaillant",antonyms:"lâche"},
{word:"lâche",level:"B2",category:"Emotions",definition:"Cowardly.;Korkak.",example:"Il est trop lâche pour dire la vérité.;Gerçeği söyleyemeyecek kadar korkak.",synonyms:"peureux",antonyms:"courageux"},
{word:"honnête",level:"B1",category:"General",definition:"Honest.;Dürüst.",example:"C'est un homme honnête.;O dürüst bir adam.",synonyms:"sincère; franc",antonyms:"malhonnête"},
{word:"poli",level:"A2",category:"General",definition:"Polite.;Kibar, nazik.",example:"Sois poli avec les invités.;Misafirlere karşı kibar ol.",synonyms:"courtois; aimable",antonyms:"impoli"},
{word:"méchant",level:"A2",category:"General",definition:"Mean, nasty.;Kötü, yaramaz.",example:"Ne sois pas méchant avec ta sœur.;Kız kardeşine kötü davranma.",synonyms:"cruel; vilain",antonyms:"gentil"},
{word:"généreux",level:"B1",category:"General",definition:"Generous.;Cömert.",example:"Elle est généreuse avec ses amis.;Arkadaşlarına karşı cömert.",synonyms:"charitable",antonyms:"avare"},
{word:"avare",level:"B2",category:"General",definition:"Stingy, miserly.;Cimri.",example:"Son oncle est très avare.;Amcası çok cimri.",synonyms:"radin",antonyms:"généreux"},
{word:"utile",level:"A2",category:"General",definition:"Useful.;Yararlı.",example:"Ce livre est très utile.;Bu kitap çok yararlı.",synonyms:"pratique; précieux",antonyms:"inutile"},
{word:"inutile",level:"B1",category:"General",definition:"Useless.;Yararsız.",example:"C'est un objet inutile.;Bu yararsız bir nesne.",synonyms:"vain",antonyms:"utile"},
{word:"nécessaire",level:"B1",category:"General",definition:"Necessary.;Gerekli.",example:"Il est nécessaire de dormir.;Uyumak gereklidir.",synonyms:"indispensable; essentiel",antonyms:"superflu"},
{word:"bizarre",level:"B1",category:"General",definition:"Strange, weird.;Garip.",example:"C'est un bruit bizarre.;Bu garip bir ses.",synonyms:"étrange; curieux",antonyms:"normal"},
{word:"normal",level:"A2",category:"General",definition:"Normal, ordinary.;Normal.",example:"C'est une journée normale.;Bu normal bir gün.",synonyms:"ordinaire; habituel",antonyms:"anormal"},
{word:"célèbre",level:"B1",category:"General",definition:"Famous.;Ünlü.",example:"C'est un acteur célèbre.;Bu ünlü bir oyuncu.",synonyms:"connu; renommé",antonyms:"inconnu"},
{word:"moderne",level:"A2",category:"General",definition:"Modern.;Modern.",example:"Ils habitent une maison moderne.;Modern bir evde oturuyorlar.",synonyms:"contemporain; récent",antonyms:"ancien"},
{word:"ancien",level:"B1",category:"General",definition:"Old, former, ancient.;Eski.",example:"C'est un ancien château.;Bu eski bir şato.",synonyms:"vieux; antique",antonyms:"moderne; nouveau"},
{word:"vide",level:"A2",category:"General",definition:"Empty.;Boş.",example:"La bouteille est vide.;Şişe boş.",synonyms:"inoccupé",antonyms:"plein"},
{word:"rare",level:"B1",category:"General",definition:"Rare, uncommon.;Nadir.",example:"C'est une plante rare.;Bu nadir bir bitki.",synonyms:"peu commun",antonyms:"fréquent"},
{word:"fréquent",level:"B2",category:"General",definition:"Frequent, common.;Sık, yaygın.",example:"Les orages sont fréquents en été.;Yazın fırtınalar sıktır.",synonyms:"courant; habituel",antonyms:"rare"},
{word:"premier",level:"A1",category:"General",definition:"First.;İlk, birinci.",example:"C'est mon premier jour ici.;Bu buradaki ilk günüm.",synonyms:"initial",antonyms:"dernier"},
{word:"dernier",level:"A1",category:"General",definition:"Last.;Son, sonuncu.",example:"C'est le dernier train.;Bu son tren.",synonyms:"ultime; final",antonyms:"premier"},
{word:"meilleur",level:"A2",category:"General",definition:"Better, best.;Daha iyi.",example:"C'est le meilleur restaurant de la ville.;Şehrin en iyi restoranı.",synonyms:"supérieur",antonyms:"pire"},
{word:"profond",level:"B1",category:"General",definition:"Deep.;Derin.",example:"Le lac est très profond.;Göl çok derin.",synonyms:"insondable",antonyms:"superficiel"},
{word:"étroit",level:"B1",category:"General",definition:"Narrow.;Dar.",example:"La rue est très étroite.;Sokak çok dar.",synonyms:"resserré",antonyms:"large"},
{word:"détester",level:"A2",category:"Emotions",definition:"To hate.;Nefret etmek.",example:"Je déteste attendre.;Beklemekten nefret ederim.",synonyms:"haïr; exécrer",antonyms:"aimer; adorer"},
{word:"adorer",level:"A1",category:"Emotions",definition:"To adore, to love.;Bayılmak, çok sevmek.",example:"J'adore le chocolat.;Çikolataya bayılırım.",synonyms:"aimer; chérir",antonyms:"détester"},
{word:"pleurer",level:"A2",category:"Emotions",definition:"To cry.;Ağlamak.",example:"Le bébé pleure la nuit.;Bebek geceleri ağlıyor.",synonyms:"sangloter",antonyms:"rire"},
{word:"crier",level:"A2",category:"Communication",definition:"To shout, to scream.;Bağırmak.",example:"Ne crie pas dans la maison.;Evde bağırma.",synonyms:"hurler",antonyms:"chuchoter"},
{word:"chuchoter",level:"B1",category:"Communication",definition:"To whisper.;Fısıldamak.",example:"Elle chuchote un secret à son ami.;Arkadaşına bir sır fısıldıyor.",synonyms:"murmurer",antonyms:"crier"},
{word:"fermer",level:"A1",category:"General",definition:"To close, to shut.;Kapatmak.",example:"Ferme la porte, s'il te plaît.;Kapıyı kapat, lütfen.",synonyms:"clore",antonyms:"ouvrir"},
{word:"vendre",level:"A1",category:"Business",definition:"To sell.;Satmak.",example:"Il vend des fruits au marché.;Pazarda meyve satıyor.",synonyms:"céder",antonyms:"acheter"},
{word:"prêter",level:"A2",category:"General",definition:"To lend.;Ödünç vermek.",example:"Peux-tu me prêter un stylo ?;Bana bir kalem ödünç verebilir misin?",synonyms:"avancer",antonyms:"emprunter"},
{word:"emprunter",level:"B1",category:"General",definition:"To borrow.;Ödünç almak.",example:"J'ai emprunté un livre à la bibliothèque.;Kütüphaneden bir kitap ödünç aldım.",antonyms:"prêter"},
{word:"perdre",level:"A2",category:"General",definition:"To lose.;Kaybetmek.",example:"J'ai perdu mes clés.;Anahtarlarımı kaybettim.",synonyms:"égarer",antonyms:"gagner; trouver"},
{word:"refuser",level:"A2",category:"General",definition:"To refuse.;Reddetmek.",example:"Il a refusé mon offre.;Teklifimi reddetti.",synonyms:"décliner; rejeter",antonyms:"accepter"},
{word:"terminer",level:"A2",category:"General",definition:"To finish, to end.;Bitirmek.",example:"J'ai terminé mes devoirs.;Ödevlerimi bitirdim.",synonyms:"achever; finir",antonyms:"commencer"},
{word:"descendre",level:"A1",category:"General",definition:"To go down, to descend.;İnmek.",example:"Elle descend l'escalier.;Merdivenden iniyor.",synonyms:"baisser",antonyms:"monter"},
{word:"pousser",level:"A2",category:"General",definition:"To push.;İtmek.",example:"Il faut pousser la porte.;Kapıyı itmek gerekiyor.",antonyms:"tirer"},
{word:"tirer",level:"A2",category:"General",definition:"To pull.;Çekmek.",example:"Tire la corde plus fort.;İpi daha sert çek.",antonyms:"pousser"},
{word:"avancer",level:"B1",category:"General",definition:"To move forward, to advance.;İlerlemek.",example:"La voiture avance lentement.;Araba yavaşça ilerliyor.",synonyms:"progresser",antonyms:"reculer"},
{word:"reculer",level:"B1",category:"General",definition:"To move back, to retreat.;Geri çekilmek.",example:"Recule un peu, s'il te plaît.;Biraz geri çekil, lütfen.",antonyms:"avancer"},
{word:"diminuer",level:"B1",category:"General",definition:"To decrease, to reduce.;Azalmak, azaltmak.",example:"Le prix a diminué.;Fiyat azaldı.",synonyms:"réduire; baisser",antonyms:"augmenter"},
{word:"allumer",level:"A2",category:"General",definition:"To turn on, to light.;Yakmak, açmak.",example:"Allume la lumière, s'il te plaît.;Işığı aç, lütfen.",antonyms:"éteindre"},
{word:"éteindre",level:"A2",category:"General",definition:"To turn off.;Söndürmek, kapatmak.",example:"N'oublie pas d'éteindre la télé.;Televizyonu kapatmayı unutma.",antonyms:"allumer"},
{word:"oublier",level:"A1",category:"General",definition:"To forget.;Unutmak.",example:"J'ai oublié ton nom.;Adını unuttum.",antonyms:"se souvenir"},
{word:"détruire",level:"B1",category:"General",definition:"To destroy.;Yıkmak, yok etmek.",example:"La tempête a détruit la maison.;Fırtına evi yıktı.",synonyms:"démolir; anéantir",antonyms:"construire"},
{word:"ajouter",level:"A2",category:"General",definition:"To add.;Eklemek.",example:"Ajoute un peu de sel.;Biraz tuz ekle.",antonyms:"enlever"},
{word:"enlever",level:"A2",category:"General",definition:"To remove, to take off.;Çıkarmak.",example:"Enlève tes chaussures, s'il te plaît.;Ayakkabılarını çıkar, lütfen.",synonyms:"retirer; ôter",antonyms:"ajouter; mettre"},
{word:"garder",level:"A2",category:"General",definition:"To keep.;Saklamak, tutmak.",example:"Garde la monnaie.;Para üstünü sakla.",synonyms:"conserver",antonyms:"jeter"},
{word:"jeter",level:"A2",category:"General",definition:"To throw away.;Atmak.",example:"Il fit une fort belle lettre, qu’il alla jeter à la poste lui-même, à Darney, bourg à six lieues de Nancy, sur la route de Paris.;Çok güzel bir mektup yazdı ve bunu Paris yolu üzerinde, Nancy'den altı fersah uzaktaki Darney kasabasındaki postaneye kendisi gönderdi.",antonyms:"garder"},
{word:"se reposer",level:"A2",category:"General",definition:"To rest.;Dinlenmek.",example:"Il se repose après le déjeuner.;Öğle yemeğinden sonra dinleniyor.",synonyms:"se détendre",antonyms:"travailler"},
{word:"travailler",level:"A1",category:"Business",definition:"To work.;Çalışmak.",example:"Elle travaille dans une banque.;Bir bankada çalışıyor.",synonyms:"bosser",antonyms:"se reposer"},
{word:"la nuit",level:"A1",category:"General",definition:"Night.;Gece.",example:"Il travaille la nuit.;Geceleri çalışıyor.",antonyms:"le jour"},
{word:"le matin",level:"A1",category:"General",definition:"Morning.;Sabah.",example:"Je bois un café le matin.;Sabahları kahve içerim.",antonyms:"le soir"},
{word:"le soir",level:"A1",category:"General",definition:"Evening.;Akşam.",example:"Nous dînons le soir.;Akşamları akşam yemeği yeriz.",antonyms:"le matin"},
{word:"l'été",level:"A1",category:"Nature",definition:"Summer.;Yaz.",example:"En été, il fait chaud.;Yazın hava sıcak olur.",antonyms:"l'hiver"},
{word:"l'hiver",level:"A1",category:"Nature",definition:"Winter.;Kış.",example:"En hiver, il neige souvent.;Kışın sık sık kar yağar.",antonyms:"l'été"},
{word:"le bonheur",level:"B1",category:"Emotions",definition:"Happiness.;Mutluluk.",example:"Elle rayonne de bonheur.;Mutluluktan parlıyor.",synonyms:"la joie",antonyms:"le malheur"},
{word:"le malheur",level:"B2",category:"Emotions",definition:"Misfortune, unhappiness.;Talihsizlik, mutsuzluk.",example:"Le malheur l'a frappé soudainement.;Talihsizlik onu aniden vurdu.",synonyms:"la tristesse",antonyms:"le bonheur"},
{word:"l'amour",level:"A2",category:"Emotions",definition:"Love.;Aşk, sevgi.",example:"L'amour rend heureux.;Aşk mutlu eder.",synonyms:"l'affection; la passion",antonyms:"la haine"},
{word:"la haine",level:"B2",category:"Emotions",definition:"Hatred.;Nefret.",example:"La haine détruit les relations.;Nefret ilişkileri yok eder.",antonyms:"l'amour"},
{word:"la paix",level:"B1",category:"General",definition:"Peace.;Barış.",example:"Tout le monde veut la paix.;Herkes barış ister.",antonyms:"la guerre"},
{word:"l'ennemi",level:"B1",category:"General",definition:"Enemy.;Düşman.",example:"Ils ont vaincu l'ennemi.;Düşmanı yendiler.",synonyms:"l'adversaire",antonyms:"l'ami"},
{word:"lentement",level:"A2",category:"General",definition:"Slowly.;Yavaşça.",example:"Parle plus lentement, s'il te plaît.;Daha yavaş konuş, lütfen.",synonyms:"doucement",antonyms:"vite; rapidement"},
{word:"bien",level:"A1",category:"General",definition:"Well.;İyi.",example:"Elle chante bien.;Güzel şarkı söylüyor.",synonyms:"correctement",antonyms:"mal"},
{word:"mal",level:"A1",category:"General",definition:"Badly.;Kötü.",example:"Il dort mal.;Kötü uyuyor.",antonyms:"bien"},
{word:"loin",level:"A1",category:"General",definition:"Far.;Uzak.",example:"La gare est loin d'ici.;İstasyon buraya uzak.",antonyms:"près"},
{word:"près",level:"A1",category:"General",definition:"Near, close.;Yakın.",example:"Il habite près de l'école.;Okulun yakınında oturuyor.",synonyms:"proche",antonyms:"loin"},
{word:"jamais",level:"A1",category:"General",definition:"Never.;Asla, hiç.",example:"Je ne mens jamais.;Asla yalan söylemem.",antonyms:"toujours"},
{word:"dehors",level:"A2",category:"General",definition:"Outside.;Dışarıda.",example:"Les enfants jouent dehors.;Çocuklar dışarıda oynuyor.",antonyms:"dedans"},
];
