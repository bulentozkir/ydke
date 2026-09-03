#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Purge political / religious / sexual / profane bias from the word corpora.

`_diag_bias.py` flags every entry whose example or definition trips one of four
category regexes; `_bias_triage.py` drops the ones where the entry's own
definition is on-topic (a drill for "hellfire" is allowed to mention hell).
What is left was read by hand and split into the four tables below.

The guiding rule is *stance and gratuitousness, not topic*.  A clinical
definition of "scrotum", a historical sentence about the Nuremberg trials and
a dictionary gloss of "blasphemy" all stay.  What goes is the sentence that
drags an unrelated headword into a value judgement -- "plumber" illustrated
with "Not only is there no God...", "describe" illustrated with Clinton and
Lewinsky, "boomerang" illustrated with an obscenity.

Replacements are not invented: they are real sentences harvested from the
Tatoeba bulk export (CC BY 2.0 FR) that contain the headword and that pass
all four bias regexes themselves.  Only the handful of headwords Tatoeba
cannot cover fall back to MANUAL.

    python scripts/fix_bias.py --plan     harvest, report coverage
    python scripts/fix_bias.py --apply    translate + write
"""
import argparse
import collections
import json
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import _udsp_data as U
import _udsp_quality as Q
import _udsp_translate as T
import fix_examples as F
import fix_examples_tatoeba as TB

HERE = os.path.dirname(os.path.abspath(__file__))

# ---------------------------------------------------------------- bias regexes
# Reuse the scanner's patterns verbatim so a replacement can never reintroduce
# what we are removing.
_src = open(os.path.join(HERE, "_diag_bias.py"), encoding="utf-8").read()
_ns = {}
exec(compile(_src.split("CATS = collections.OrderedDict")[0], "_diag_bias", "exec"),
     {"collections": collections, "json": json, "re": re, "sys": sys,
      "__name__": "_patterns"}, _ns)
RX = [re.compile(r"\b(?:" + "|".join(_ns[c]) + r")", re.I | re.U)
      for c in ("PROFANITY", "SEXUAL", "RELIGION", "POLITICS")]

# The scanner's lists are tuned for the corpus, where the Turkish half of a
# field usually gives the game away ("mastürbasyon" is caught even though the
# French "se masturber" is not).  A raw Tatoeba sentence has no Turkish half,
# so vetting candidates needs its own, far blunter net.  Over-rejecting only
# costs another candidate, so this errs heavily towards silence: anything
# touching sex, faith, party politics, war, crime, drink, drugs or death is
# thrown away, in all three source languages.
VETO = re.compile(r"""\b(?:
  damn|hell\b|bloody|screw|suck|crap|jerk|idiot|moron|stupid|dumb
 |b[aâ]tard|salaud|salop\w*|conn?ard|connasse|connerie|merde|putain|foutre
 |foutu|chiotte\w*|cul\b|chatte|bite\b|couille\w*|gueule|mijaur\w*
 |mistkerl|schei\w*|verdammt\w*|verflucht\w*|hure\w*|arsch\w*|blöd\w*
 |sex\w*|sexe|sexuel\w*|erotic|erotisch|[ée]rotique|porn\w*|nude|naked
 |masturb\w*|selbstbefriedig\w*|liebemachen|onan\w*|orgasm\w*
 |penis|vagin\w*|breast\w*|nipple\w*|genital\w*|scrotum|testicl\w*
 |prostitu\w*|brothel|bordel|whore|hooker|rape|raped|viol\b|violé\w*
 |vergewaltig\w*|virgin|pregnan\w*|enceinte|schwanger
 |god|gods|godless|gott\w*|dieu|dios|allah|jesus|christ\w*|muslim|islam\w*
 |jew\w*|juif\w*|jud[ei]\w*|hindu\w*|buddh\w*|athei\w*|heilige\w*|dreieinig\w*
 |katholisch|catholic|protestant|kirche|église|church|temple|mosque|moschee
 |priest|pr[êe]tre|prayer|pri[èe]re|beten|betet|soul|seele|âme\b
 |president|pr[ée]sident\w*|präsident\w*|minister\w*|ministre\w*|chancellor
 |kanzler\w*|government|gouvernement|regierung|parliament|parlement
 |congress|congr[èe]s|senat\w*|election|[ée]lection\w*|wahl\w*|vote|voter
 |party|parti\b|partei|democrat\w*|republican\w*|conservative|labour
 |liberal\w*|communis\w*|kommunis\w*|socialis\w*|sozialis\w*|fasci\w*
 |faschis\w*|nazi\w*|hitler|stalin\w*|mussolini|franco\b|putin|poutine
 |trump|biden|obama|clinton|merkel|macron|erdo|zelens|netanyahu|mao\b
 |lenin|castro|regime|r[ée]gime|dictator|dictateur|diktator\w*|junta
 |sanction\w*|impeach\w*|immigra\w*|racis\w*|racial|ethnic|apartheid
 |israel|isra[ëe]l\w*|palestin\w*|ukrain\w*|russia\w*|russie|russland
 |china|chinese|america\w*|am[ée]rica\w*|amerika\w*|africa\w*|afro
 |war\b|wars|guerre|krieg\w*|army|arm[ée]e|soldier|soldat\w*|militar\w*
 |milit[äa]r\w*|bomb\w*|bombe\w*|missile|weapon\w*|arme\w*|waffe\w*|gun\b
 |guns|rifle|fusil|pistol\w*|terror\w*|attentat|explosion|atrocit\w*
 |kill\w*|tuer|tu[ée]|t[öo]t\w*|murder\w*|meurtre|mord\w*|assassin\w*
 |suicide|selbstmord|death|dead|die\b|died|mort\w*|tod\b|todes|sterb\w*
 |blood\w*|sang\b|blut\w*|torture|folter\w*|mutilat\w*|verstümm\w*
 |wound\w*|blessé\w*|trauma\w*|crime|criminal|criminel\w*|verbrech\w*
 |prison|gef[äa]ngnis|steal|stole|voler|stehl\w*|robber\w*|thief|dieb\w*
 |drug\w*|drogue\w*|droge\w*|narcotic\w*|cocaine|kokain|heroin|hash\w*
 |amphetamin\w*|cannabis|kiff\w*|marijuana|alcohol\w*|alcool\w*|alkohol\w*
 |drunk\w*|ivre|betrunk\w*|beer|bi[èe]re|wine|vin\b|wein\b|whisky|vodka
 |cigarette|zigarette|smok(e|ing)|fumer|rauch\w*|tabac|tobacco
 |piss\w*|pee\b|toilet\w*|klo\b|urin\w*|fart\w*|vomit|kotz\w*|shit\w*
 |fat\b|ugly|moche|h[äa]sslich|obese|dick\b|dumm\w*
 )""", re.I | re.U | re.X)


def clean(s):
    return not any(rx.search(s) for rx in RX)


def _mask(head, s):
    """Blank out the headword so its own senses never trip the veto.

    A drill for "drunkenness" or "narcotic" or "denazify" must be allowed to
    contain that word; everything *else* in the sentence still has to be
    innocuous.
    """
    fh = Q.fold(head)
    out = []
    for tok in re.findall(r"\w+|\W+", s, re.U):
        ft = Q.fold(tok)
        if ft and (ft == fh or (len(fh) >= 4 and TB._fuzzy(fh, ft))):
            out.append(" ")
        else:
            out.append(tok)
    return "".join(out)


def vet(head, s):
    m = _mask(head, s)
    return clean(m) and not VETO.search(m)



# --------------------------------------------------------------------- tables
# Entries whose EXAMPLE is gratuitous and must be re-sourced.
REPLACE = {
    "partikelverbde.js": [
        "abdrucken", "abhören", "ausdenken", "auslösen", "auspowern",
        "einhergehen", "einsaugen", "zukiffen",
    ],
    "phrasalverbsen.js": ["crap up", "shrivel up", "trump out"],
    "phrasalverbsfr.js": ["croire en"],
    "synantde.js": [
        "Abdruck", "Aktivposten", "Allah", "Amtseinführung", "Baracke",
        "Bastard", "Bekräftigung", "Exhumierung", "Geschlechtsreife",
        "Gesellschaftslehre", "Heiliger", "abrüsten", "ausstoßen", "auslösen",
        "beschreiben", "düpieren", "ehelich", "masturbieren",
    ],
    "synanten.js": [
        "accordant", "bestead", "bill", "blasphemy", "caitiff", "course",
        "decry", "disperse", "efflorescence", "erect", "heathenish", "impeach",
        "labyrinth", "libertine", "lout", "orifice", "peerless", "secrete",
        "unequalled", "versatile",
    ],
    "toefl.js": [
        "bogie", "epithalamium", "erotically", "estrogenic", "factotum",
        "federalize", "fellah", "felly", "feminize", "frigidity", "gazump",
        "genealogic", "genitor", "gormless", "gumminess", "hammerlock",
        "heathenism", "hitless", "houseful", "hyperbolically", "obsessively",
        "recantation", "reductio", "relatum", "republish", "resoundingly",
        "rightist", "sclerotic", "scrotal", "supplication",
    ],
    "wordsa1.js": ["plumber"],
    "wordsa1fr.js": ["la relation", "le milieu"],
    "wordsa1gode.js": ["nochmal", "tot", "verflucht"],
    "wordsa2.js": ["afterlife"],
    "wordsa2fr.js": [
        "compromettant", "erroné", "le boomerang", "le fourreau", "le germe",
        "moche", "vénérer",
    ],
    "wordsa2gode.js": [
        "Abdruck", "Aktivposten", "Allah", "Amtseinführung", "anzurufen",
    ],
    "wordsb1.js": ["cretin", "silicone", "weenie"],
    "wordsb1fr.js": [
        "décrire", "effarant", "inégalé", "la concentration", "la destitution",
        "la fouille", "la passation", "le bazar", "le calibrage", "le caviste",
        "le chancelière", "le mijaurée", "le poutine", "le tartarin",
    ],
    "wordsb1gode.js": ["Bekräftigung", "polen", "überschritten"],
    "wordsb2.js": [
        "accordant", "gutless", "hellfire", "megalomaniac", "mutilation",
        "narcotic",
    ],
    "wordsb2fr.js": [
        "acceptable", "destituer", "gravé", "infidèle", "la doublure",
        "la punaise", "le coquard", "le jihadiste", "le malotru",
        "le pochoir", "le plombier", "saloper", "évoluer",
    ],
    "wordsb2gode.js": [
        "Baracke", "auslösen", "belangen", "gesellschaftlich",
        "herumkommandieren", "kreieren",
    ],
    "wordsc1.js": [
        "agitprop", "ambiguously", "antebellum", "appetence", "baster",
        "bodkin", "bullshot", "deuterium", "dishonorable", "drunkenness",
        "grifter", "illegality", "isolationism", "jihadi", "labyrinthian",
        "lasciviousness", "looper", "macron", "malarky", "megalomaniacal",
        "multiplex", "mutilator", "sanctum",
    ],
    "wordsc1fr.js": [
        "cramer", "exterminer", "l'afro", "la promiscuité", "le piaule",
        "le staline", "liquider", "écervelé",
    ],
    "wordsc1gode.js": [
        "Exhumierung", "bekräftigen", "beschreiben", "durchweg", "düpieren",
        "ehelich", "größenwahnsinnig", "prall", "unehelich",
    ],
    "wordsc2.js": [
        "amphetamine", "bestialize", "clitoric", "confabulation", "contrail",
        "cosign", "crappie", "cretinous", "cryptorchidism", "denazify",
        "derangement", "deuteron", "doohickey", "dotard", "dysphemism",
        "dysphemistic", "enlargement", "narcotize", "neolith", "olibanum",
        "plumbic", "prolate",
    ],
    "wordsc2fr.js": [
        "calibrer", "fondamentalement", "l'emblème", "l'omicron", "le goujat",
        "le laquais", "le rebut", "le rousseur", "piger", "socialement", "tory",
    ],
    "wordsc2gode.js": [
        "Geschlechtsreife", "Gesellschaftslehre", "Heiliger", "abdrucken",
        "hirnen", "ideologisch", "kaukasisch", "römisch-katholisch",
        "vaginalen", "vorwitzig",
    ],
}

# Headwords Tatoeba is unlikely to carry, or where the existing example used
# the wrong sense entirely and a targeted sentence is clearer.
MANUAL = {
    ("wordsa1.js", "plumber"):
        "The plumber fixed the leaking pipe under the kitchen sink.",
    ("wordsc2.js", "plumbic"):
        "Plumbic oxide is a compound in which lead has a valence of four.",
    ("wordsb2fr.js", "le plombier"):
        "Le plombier a réparé le robinet de la cuisine.",
    ("wordsa1gode.js", "tot"):
        "Die Pflanze ist tot, weil ich sie zu lange vergessen habe.",
    ("synantde.js", "Allah"):
        "Im Arabischen heißt Gott schlicht Allah.",
    ("wordsa2gode.js", "Allah"):
        "Im Arabischen heißt Gott schlicht Allah.",
    ("wordsc1.js", "macron"):
        "A macron is the straight bar written above a long vowel.",
    ("wordsc1.js", "grifter"):
        "The grifter promised huge returns and then disappeared overnight.",
    ("wordsc2fr.js", "tory"):
        "Un tory est un membre du Parti conservateur britannique.",
    ("wordsb1gode.js", "polen"):
        "Man kann Licht polen, sodass es nur in einer Ebene schwingt.",
    ("wordsb1fr.js", "la concentration"):
        "La concentration est indispensable pour réussir cet exercice.",
    ("wordsb1fr.js", "le poutine"):
        "La poutine est un plat québécois de frites, de fromage et de sauce.",
    ("wordsc1fr.js", "le staline"):
        "Au comptoir, il a commandé un staline, un grand verre de vin rouge.",
    ("wordsc1.js", "bullshot"):
        "A bullshot is a cocktail of vodka and cold beef bouillon.",
    ("wordsc2.js", "crappie"):
        "The crappie is a freshwater fish popular with anglers in the Midwest.",
    ("wordsc2.js", "neolith"):
        "The museum displays a neolith chipped from flint thousands of years ago.",
    ("wordsc1.js", "baster"):
        "She used a baster to spoon the juices back over the roast.",
    ("wordsc1.js", "malarky"):
        "Do not listen to that malarky; none of it is true.",
    ("wordsc2.js", "doohickey"):
        "Hand me that little doohickey that holds the shelf in place.",
    ("wordsc1.js", "labyrinthian"):
        "The old town has labyrinthian alleys that all look the same.",
    ("wordsb1fr.js", "le tartarin"):
        "Ce tartarin raconte des exploits de chasse qu'il n'a jamais vécus.",
    ("wordsb1fr.js", "le chancelière"):
        "La chancelière prononce son discours devant le parlement.",
    ("toefl.js", "relatum"):
        "In this comparison, the second noun is the relatum.",
    ("toefl.js", "hitless"):
        "The pitcher kept the visiting team hitless for six innings.",
    ("wordsc2gode.js", "hirnen"):
        "Ich musste lange hirnen, bis mir die Lösung einfiel.",

    # --- headwords Tatoeba carries no clean sentence for -------------------
    ("partikelverbde.js", "einsaugen"):
        "Der Schwamm kann erstaunlich viel Wasser einsaugen.",
    ("partikelverbde.js", "zukiffen"):
        "Am Wochenende hat er sich mit seinen Freunden zugekifft.",
    ("synantde.js", "Aktivposten"):
        "Ihre Sprachkenntnisse sind ein echter Aktivposten für die Firma.",
    ("wordsa2gode.js", "Aktivposten"):
        "Ihre Sprachkenntnisse sind ein echter Aktivposten für die Firma.",
    ("synantde.js", "Bekräftigung"):
        "Zur Bekräftigung des Vertrags gaben sich beide Seiten die Hand.",
    ("wordsb1gode.js", "Bekräftigung"):
        "Zur Bekräftigung des Vertrags gaben sich beide Seiten die Hand.",
    ("synantde.js", "Exhumierung"):
        "Die Exhumierung wurde von einem Gericht angeordnet.",
    ("wordsc1gode.js", "Exhumierung"):
        "Die Exhumierung wurde von einem Gericht angeordnet.",
    ("synantde.js", "Geschlechtsreife"):
        "Lachse erreichen die Geschlechtsreife nach etwa vier Jahren.",
    ("wordsc2gode.js", "Geschlechtsreife"):
        "Lachse erreichen die Geschlechtsreife nach etwa vier Jahren.",
    ("synantde.js", "Gesellschaftslehre"):
        "Gesellschaftslehre ist ein Schulfach über das Zusammenleben der Menschen.",
    ("wordsc2gode.js", "Gesellschaftslehre"):
        "Gesellschaftslehre ist ein Schulfach über das Zusammenleben der Menschen.",
    ("synantde.js", "abrüsten"):
        "Nach dem Manöver mussten alle Einheiten wieder abrüsten.",
    ("synantde.js", "ausstoßen"):
        "Der alte Schornstein stößt dichten schwarzen Rauch aus.",
    ("synantde.js", "düpieren"):
        "Der Verkäufer versuchte, die Kundin mit falschen Angaben zu düpieren.",
    ("wordsc1gode.js", "düpieren"):
        "Der Verkäufer versuchte, die Kundin mit falschen Angaben zu düpieren.",
    ("wordsc1gode.js", "bekräftigen"):
        "Sie bekräftigte ihr Versprechen, morgen pünktlich zu sein.",
    ("wordsc2gode.js", "kaukasisch"):
        "Georgisch ist eine kaukasische Sprache mit eigener Schrift.",
    ("wordsc2gode.js", "vorwitzig"):
        "Das vorwitzige Kind stellte eine Frage nach der anderen.",

    ("phrasalverbsen.js", "crap up"):
        "Do not crap up the report with careless mistakes.",
    ("phrasalverbsen.js", "shrivel up"):
        "The flowers shrivel up if you forget to water them.",
    ("phrasalverbsen.js", "trump out"):
        "The declarer trumped out before running the long side suit.",
    ("synanten.js", "accordant"):
        "His account is accordant with the evidence we collected.",
    ("wordsb2.js", "accordant"):
        "His account is accordant with the evidence we collected.",
    ("synanten.js", "bestead"):
        "Her long training will bestead her in the coming contest.",
    ("synanten.js", "blasphemy"):
        "In many legal systems, blasphemy is no longer treated as a crime.",
    ("synanten.js", "caitiff"):
        "The caitiff fled the field before the first charge.",
    ("synanten.js", "efflorescence"):
        "A white efflorescence appeared on the damp brick wall.",
    ("synanten.js", "heathenish"):
        "In old chronicles, unfamiliar rites were dismissed as heathenish.",
    ("toefl.js", "heathenism"):
        "The chronicler used heathenism to mean any belief outside his own church.",
    ("synanten.js", "lout"):
        "The lout shoved past everyone waiting in the queue.",
    ("synanten.js", "unequalled"):
        "Her record in the marathon has remained unequalled for a decade.",
    ("toefl.js", "bogie"):
        "He finished the hole with a bogie, one stroke over par.",
    ("toefl.js", "epithalamium"):
        "The poet composed an epithalamium for the couple's wedding.",
    ("toefl.js", "estrogenic"):
        "Some plants contain estrogenic compounds that mimic natural hormones.",
    ("toefl.js", "factotum"):
        "As the office factotum, he handled everything from the mail to repairs.",
    ("toefl.js", "fellah"):
        "The fellah worked his small plot of land beside the river.",
    ("toefl.js", "felly"):
        "The wooden felly of the old cartwheel had begun to crack.",
    ("toefl.js", "feminize"):
        "Certain pollutants can feminize fish in contaminated rivers.",
    ("toefl.js", "frigidity"):
        "The frigidity of her tone made it clear the discussion was over.",
    ("toefl.js", "gazump"):
        "Another buyer tried to gazump them at the last minute.",
    ("toefl.js", "genealogic"):
        "He published a genealogic chart going back six generations.",
    ("toefl.js", "genitor"):
        "In this study, the genitor is the biological father rather than the legal one.",
    ("toefl.js", "gormless"):
        "He gave a gormless stare when asked for the answer.",
    ("toefl.js", "gumminess"):
        "The gumminess of the dough made it hard to roll out.",
    ("toefl.js", "hammerlock"):
        "The wrestler escaped the hammerlock and turned to face his opponent.",
    ("toefl.js", "hyperbolically"):
        "He spoke hyperbolically, calling the short delay a total disaster.",
    ("toefl.js", "recantation"):
        "The article ended with a full recantation of the earlier claim.",
    ("toefl.js", "reductio"):
        "The whole argument collapses into a reductio ad absurdum.",
    ("toefl.js", "resoundingly"):
        "The proposal was resoundingly approved by the committee.",
    ("toefl.js", "sclerotic"):
        "Years without reform had left the old institution sclerotic and slow.",
    ("wordsb1.js", "weenie"):
        "He ordered a weenie with mustard at the stand by the park.",
    ("wordsc1.js", "agitprop"):
        "The exhibition traces the history of agitprop posters in early cinema.",
    ("wordsc1.js", "ambiguously"):
        "The instructions were ambiguously worded and confused everyone.",
    ("wordsc1.js", "antebellum"):
        "The museum preserves an antebellum house with wide verandas.",
    ("wordsc1.js", "appetence"):
        "She felt a strong appetence for fresh fruit after the long hike.",
    ("wordsc1.js", "deuterium"):
        "Deuterium is a heavy isotope of hydrogen used in some reactors.",
    ("wordsc1.js", "illegality"):
        "The court finally ruled on the illegality of the parking charge.",
    ("wordsc1.js", "looper"):
        "A looper caterpillar arches its back as it moves along the twig.",
    ("wordsc1.js", "megalomaniacal"):
        "The villain in the novel has megalomaniacal plans for the city.",
    ("wordsc1.js", "mutilator"):
        "The report finally named the mutilator of the ancient carvings.",
    ("wordsc2.js", "bestialize"):
        "A long war can bestialize even the gentlest of people.",
    ("wordsc2.js", "confabulation"):
        "Confabulation is the invention of memories to fill a gap.",
    ("wordsc2.js", "cosign"):
        "Her father agreed to cosign the loan for the apartment.",
    ("wordsc2.js", "cretinous"):
        "The character is written as cretinous and far too easily fooled.",
    ("wordsc2.js", "derangement"):
        "The storm left the whole timetable in complete derangement.",
    ("wordsc2.js", "deuteron"):
        "A deuteron consists of one proton and one neutron.",
    ("wordsc2.js", "dotard"):
        "In the play, the old king is mocked as a dotard by his courtiers.",
    ("wordsc2.js", "dysphemism"):
        "Calling a modest house a shack is a dysphemism.",
    ("wordsc2.js", "dysphemistic"):
        "The article used dysphemistic language to belittle the proposal.",
    ("wordsc2.js", "narcotize"):
        "The vet will narcotize the animal before the operation.",
    ("wordsc2.js", "olibanum"):
        "Olibanum, better known as frankincense, is a fragrant tree resin.",
    ("wordsc2.js", "prolate"):
        "A rugby ball has a prolate shape, longer than it is wide.",

    ("wordsb1fr.js", "la destitution"):
        "La destitution du directeur a été annoncée hier soir.",
    ("wordsb1fr.js", "la passation"):
        "La passation du marché public a duré plusieurs mois.",
    ("wordsb1fr.js", "le calibrage"):
        "Le calibrage de l'appareil doit être vérifié chaque année.",
    ("wordsb1fr.js", "le caviste"):
        "Le caviste nous a conseillé un vin blanc bien sec.",
    ("wordsb2fr.js", "destituer"):
        "Le conseil a décidé de destituer le trésorier de l'association.",
    ("wordsb2fr.js", "saloper"):
        "Il a salopé son travail et a dû tout recommencer.",
    ("wordsc1fr.js", "la promiscuité"):
        "La promiscuité dans le dortoir rendait le sommeil difficile.",
    ("wordsc2fr.js", "calibrer"):
        "Il faut calibrer la balance avant chaque mesure.",
    ("wordsc2fr.js", "l'omicron"):
        "L'omicron est la quinzième lettre de l'alphabet grec.",

    # --- Tatoeba's best surviving candidate was still unusable ------------
    # Either it carried residual bias, quoted a real political figure, or it
    # illustrated a different word than the headword ("grave" for "gravé",
    # "inégale" for "inégalé", "Belangen" for "belangen").
    ("partikelverbde.js", "abdrucken"):
        "Die Zeitung wird das ganze Interview morgen abdrucken.",
    ("wordsc2gode.js", "abdrucken"):
        "Die Zeitung wird das ganze Interview morgen abdrucken.",
    ("partikelverbde.js", "einhergehen"):
        "Mit dem neuen Amt wird viel Verantwortung einhergehen.",
    ("synantde.js", "Amtseinführung"):
        "Die Amtseinführung der neuen Direktorin findet am Montag statt.",
    ("wordsa2gode.js", "Amtseinführung"):
        "Die Amtseinführung der neuen Direktorin findet am Montag statt.",
    ("wordsb2gode.js", "gesellschaftlich"):
        "Das Museum organisiert viele gesellschaftliche Veranstaltungen.",
    ("wordsc1gode.js", "durchweg"):
        "Die Rückmeldungen zum Kurs waren durchweg positiv.",
    ("wordsc1gode.js", "prall"):
        "Nach dem Einkauf war die Tasche prall gefüllt.",
    ("wordsb2gode.js", "belangen"):
        "Wer den Vertrag bricht, kann dafür rechtlich belangt werden.",
    ("wordsc1gode.js", "größenwahnsinnig"):
        "Der Plan, in zwei Wochen ein ganzes Haus zu bauen, war größenwahnsinnig.",
    ("wordsc2gode.js", "römisch-katholisch"):
        "Die römisch-katholische Kirche hat ihren Sitz in Rom.",

    ("wordsb1.js", "silicone"):
        "The baking tray is lined with a silicone mat.",
    ("wordsb1.js", "cretin"):
        "Historically, cretin was a medical term for severe iodine deficiency.",
    ("wordsc1.js", "lasciviousness"):
        "The novel was banned for what the censors called its lasciviousness.",
    ("wordsc1.js", "bodkin"):
        "She threaded the ribbon through the hem with a bodkin.",
    ("wordsc1.js", "sanctum"):
        "The quiet reading room is the scholar's inner sanctum.",
    ("wordsc2.js", "amphetamine"):
        "Amphetamine is a stimulant that speeds up the nervous system.",
    ("wordsc2.js", "denazify"):
        "After 1945, the Allies set up programmes to denazify public institutions.",
    ("wordsa2.js", "afterlife"):
        "Many ancient cultures buried food and tools for the afterlife.",
    ("toefl.js", "rightist"):
        "In political science, rightist simply labels one end of the spectrum.",
    ("toefl.js", "supplication"):
        "She knelt in supplication before the old queen.",
    ("toefl.js", "federalize"):
        "The plan would federalize the country's ageing rail network.",
    ("synanten.js", "decry"):
        "Teachers continue to decry the shortage of textbooks.",
    ("wordsb2.js", "gutless"):
        "He called the decision gutless and refused to accept it.",
    ("wordsb2.js", "mutilation"):
        "The mutilation of the old manuscript upset the librarians.",

    ("wordsb2fr.js", "le pochoir"):
        "Elle a peint les lettres au pochoir sur la caisse en bois.",
    ("wordsb1fr.js", "le mijaurée"):
        "Cette mijaurée refuse de s'asseoir sur un banc public.",
    ("wordsc2fr.js", "l'emblème"):
        "Le lion est l'emblème de cette ville depuis le Moyen Âge.",
    ("wordsc1fr.js", "exterminer"):
        "Il a fallu exterminer les termites qui rongeaient la charpente.",
    ("wordsc1fr.js", "l'afro"):
        "Elle porte une afro depuis des années et cela lui va très bien.",
    ("wordsc1fr.js", "écervelé"):
        "Cet écervelé a encore oublié ses clés à la maison.",
    ("wordsb1fr.js", "le bazar"):
        "Range un peu ta chambre, c'est un vrai bazar !",
    ("wordsb1fr.js", "la fouille"):
        "La fouille archéologique a duré tout l'été.",
    ("wordsb1fr.js", "inégalé"):
        "Son talent au piano reste inégalé dans toute la région.",
    ("wordsb2fr.js", "la doublure"):
        "La doublure de ce manteau est en soie.",
    ("wordsb2fr.js", "la punaise"):
        "Il a fixé l'affiche au mur avec une punaise.",
    ("wordsb2fr.js", "gravé"):
        "Son nom est gravé sur la plaque de marbre.",
    ("wordsc2fr.js", "le rebut"):
        "Ces pièces défectueuses sont mises au rebut.",
    ("wordsa2fr.js", "vénérer"):
        "Dans ce village, on vénère encore les vieilles traditions.",

    # --- gratuitous examples the definition-aware triage had auto-kept -----
    # The entry's own definition is on-topic, so the triage let the example
    # through; read by hand, these were still war stories, a racist simile or
    # locker-room jokes wrapped around a clinical headword.
    ("synantde.js", "Bastard"):
        "Im Mittelalter bezeichnete Bastard ein Kind unverheirateter Eltern.",
    ("synantde.js", "masturbieren"):
        "Das Buch erklärt Jugendlichen sachlich, was masturbieren bedeutet.",
    ("wordsa1gode.js", "verflucht"):
        "Verflucht, ich habe schon wieder meinen Schlüssel vergessen!",
    ("wordsb2fr.js", "le jihadiste"):
        "Le journaliste a analysé la propagande jihadiste dans son reportage.",
    ("wordsc1.js", "jihadi"):
        "The article examined how jihadi groups recruit followers online.",
    ("toefl.js", "erotically"):
        "The censors objected to the scene because the dancers moved erotically.",
    ("toefl.js", "scrotal"):
        "The doctor ordered a scrotal ultrasound to check for swelling.",
    ("wordsc2.js", "clitoric"):
        "The anatomy textbook includes a labelled diagram of clitoric structure.",
    ("wordsc2.js", "cryptorchidism"):
        "Cryptorchidism is usually corrected with a minor operation in infancy.",
    ("wordsc2gode.js", "vaginalen"):
        "Die Ärztin empfahl einen vaginalen Ultraschall zur Kontrolle.",
    ("partikelverbde.js", "abhören"):
        "Der Lehrer wird morgen die neuen Vokabeln abhören.",
    ("partikelverbde.js", "auspowern"):
        "Die lange Bergwanderung hat uns völlig ausgepowert.",
    ("wordsb2.js", "hellfire"):
        "The old sermons were full of warnings about hellfire.",
    ("wordsb2fr.js", "le malotru"):
        "Ce malotru a bousculé tout le monde sans s'excuser.",
    ("wordsb2fr.js", "le coquard"):
        "Il est rentré du match avec un beau coquard.",
    ("wordsc2fr.js", "le goujat"):
        "Ce goujat n'a même pas tenu la porte derrière lui.",
}

# Machine translation reaches for the crudest Turkish equivalent of these
# ("malotru" and "goujat" both come back as "piç"), which would put back the
# profanity the replacement was meant to remove.  Pinned by hand instead.
MANUAL_TR = {
    ("wordsb2fr.js", "le malotru"):
        "Bu kaba adam kimseden özür dilemeden herkesi itip kaktı.",
    ("wordsb2fr.js", "le coquard"):
        "Maçtan gözünde kocaman bir morlukla döndü.",
    ("wordsc2fr.js", "le goujat"):
        "Bu kaba adam arkasından kapıyı bile tutmadı.",
}

# Definitions that are themselves the problem.  English half; the Turkish half
# is regenerated from it.
FIX_DEFINITION = {
    ("wordsa1.js", "donkey"):
        "A hoofed animal related to the horse, with long ears.",
    ("partikelverbde.js", "ablästern"):
        "To badmouth someone; to run someone down behind their back.",
    ("partikelverbde.js", "herumzicken"):
        "To make a fuss; to be awkward or difficult about something.",
    ("wordsb1fr.js", "l'ânerie"):
        "A stupid remark; a piece of nonsense.",
    ("wordsb2fr.js", "engueuler"):
        "To tell someone off; to bawl someone out.",
    ("wordsb2gode.js", "verschissenen"):
        "Ruined; botched; hopelessly spoiled.",
    ("wordsc1fr.js", "vénère"):
        "Angry; furious.",
    ("wordsb2fr.js", "le coquard"):
        "A black eye; a bruise around the eye.",
    ("wordsb2fr.js", "le justaucorps"):
        "A leotard; a close-fitting one-piece garment.",
    ("wordsc2fr.js", "l'harem"):
        "The women's quarters of a traditional Muslim household.",
    ("wordsc2gode.js", "rollig"):
        "In heat; on heat (of a female cat).",
    ("wordsc1gode.js", "notgeil"):
        "Desperate for attention or affection.",
    ("wordsc1fr.js", "la promiscuité"):
        "Overcrowding; a lack of personal space.",
    ("wordsc2fr.js", "le goujat"):
        "A boor; a rude, ill-mannered man.",
}

# Headwords that carry no teaching value and only exist to be crude, plus
# three ethnic slurs.
DELETE = {
    "partikelverbde.js": ["durchvögeln"],
    "toefl.js": ["ejaculator", "mulatto"],
    "wordsb1fr.js": ["la baiseuse", "le micheton", "switch"],
    "wordsb2.js": ["squaw"],
    "wordsb2fr.js": ["le cunni"],
    "wordsc1fr.js": ["l'enfoirée", "le clito"],
    "wordsc2.js": ["cunnilinctus"],
    "wordsc2fr.js": ["eskimo", "tringler"],
    "wordsc2gode.js": ["aufgeilen", "durchvögeln"],
}

# "Horny" in a zoological definition means made of keratin, but the Turkish
# half of these entries renders it as the sexual sense ("azgın").  Listed
# explicitly rather than detected, because for "geil" and "wuschig" the sexual
# reading is the correct one.
HORNY = {
    "toefl.js": ["elytron", "gorgonian", "hadrosaur", "hadrosaurus", "hagfish",
                 "plumage", "scaley", "scarabaean", "scarabaeid", "scute"],
    "wordsb1.js": ["rattlesnake"],
    "wordsb2.js": ["anteater", "armadillo"],
    "wordsc1.js": ["albuminoid", "baleen", "keratin", "keratoderma"],
    "wordsc2.js": ["cassowary", "cerastes", "ceratin", "chitin", "pangolin"],
}
HORNY_TR = ("azgın", "boynuzsu")



# ----------------------------------------------------------------------- plan
def targets():
    """{file: {word: headword}} for every example that needs re-sourcing."""
    out = {}
    for name, words in REPLACE.items():
        lang = U.lang_of(name)
        got = {}
        for w in words:
            if (name, w) in MANUAL:
                continue
            got[w] = U.headword(w, lang)
        if got:
            out[name] = got
    return out


def plan(from_cache=False):
    cache = T.Cache("bias_examples.json")
    need = targets()
    by_lang = collections.defaultdict(dict)
    for name, words in need.items():
        lang = U.lang_of(name)
        for w, head in words.items():
            by_lang[lang]["%s|%s" % (name, w)] = head

    chosen, missing = {}, []
    for lang in sorted(by_lang):
        pending = {}
        for key, head in by_lang[lang].items():
            got = cache.get(key)
            if from_cache and got:
                chosen[key] = got[0]
            else:
                pending[key] = head
        if pending:
            TB.KEEP = 60          # a strict veto needs a deep candidate pool
            found = TB.harvest(lang, pending)
            for key, cands in found.items():
                ok = [s for s in cands if vet(pending[key], s)]
                s = F.pick(ok) if ok else None
                if s:
                    chosen[key] = s
                    cache.put(key, [s])
        for key in by_lang[lang]:
            if key not in chosen:
                missing.append(key)
        print("  %-3s %4d / %-4d sourced"
              % (lang, sum(1 for k in by_lang[lang] if k in chosen),
                 len(by_lang[lang])))
    cache.save()
    return chosen, missing


# ---------------------------------------------------------------------- apply
def apply(chosen):
    tcache = T.Cache("bias_tr.json")

    # Turkish for the harvested examples, grouped by source language.
    groups = collections.defaultdict(set)
    for key, s in chosen.items():
        groups[U.lang_of(key.split("|", 1)[0])].add(s)
    for (name, w), s in MANUAL.items():
        groups[U.lang_of(name)].add(s)
    tr = {}
    for lang, texts in sorted(groups.items()):
        tr.update(T.translate_many(tcache, sorted(texts), lang, "tr",
                                   progress=True))
    # Definitions are English in every file.
    defs = sorted(set(FIX_DEFINITION.values()))
    tr_def = T.translate_many(tcache, defs, "en", "tr", progress=True)
    tcache.save()

    files = sorted(set(list(REPLACE) + list(DELETE) + list(HORNY)
                       + [n for n, _ in FIX_DEFINITION]
                       + [n for n, _ in MANUAL]))
    stats = collections.Counter()
    for name in files:
        text, entries = U.load(name)
        lang = U.lang_of(name)
        ed = U.Editor(name)
        by_word = {}
        for e in entries:
            by_word.setdefault(U.unescape(e.get("word", "")).strip(), e)
        # The editor applies splices positionally, so two edits to the same
        # field of the same entry would overlap and corrupt the file.
        gone, redefined = set(), set()

        for w in DELETE.get(name, []):
            e = by_word.get(w)
            if e is None:
                stats["delete-missing"] += 1
                print("   ! delete: %s / %s not found" % (name, w))
                continue
            ed.delete(e)
            gone.add(id(e))
            stats["deleted"] += 1

        for w in REPLACE.get(name, []):
            if w in DELETE.get(name, []):
                continue
            e = by_word.get(w)
            if e is None:
                stats["example-missing"] += 1
                print("   ! example: %s / %s not found" % (name, w))
                continue
            s = MANUAL.get((name, w)) or chosen.get("%s|%s" % (name, w))
            if not s:
                stats["example-unsourced"] += 1
                continue
            t = MANUAL_TR.get((name, w)) or tr.get(s)
            if not t:
                stats["example-untranslated"] += 1
                continue
            ed.set_bilingual(e, "example", T.ensure_period(s),
                             T.ensure_period(t))
            stats["examples"] += 1

        for (fn, w), d in FIX_DEFINITION.items():
            if fn != name:
                continue
            e = by_word.get(w)
            if e is None:
                stats["definition-missing"] += 1
                print("   ! definition: %s / %s not found" % (name, w))
                continue
            t = tr_def.get(d)
            if not t:
                stats["definition-untranslated"] += 1
                continue
            ed.set_bilingual(e, "definition", d, T.titlecase_first(t))
            redefined.add(id(e))
            stats["definitions"] += 1

        # keratinous "horny" mistranslated as the sexual sense
        for w in HORNY.get(name, []):
            e = by_word.get(w)
            if e is None or id(e) in gone or id(e) in redefined:
                stats["horny-missing"] += 1
                continue
            val = U.unescape(e.get("definition", ""))
            nat, _, turk = val.partition(U.sep_for(name))
            if HORNY_TR[0] not in turk:
                stats["horny-already-ok"] += 1
                continue
            ed.set_bilingual(e, "definition", nat.strip(),
                             turk.strip().replace(*HORNY_TR))
            stats["horny"] += 1

        ed.save()

    for k, v in sorted(stats.items()):
        print("  %-22s %d" % (k, v))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--plan", action="store_true")
    ap.add_argument("--apply", action="store_true")
    ap.add_argument("--from-cache", action="store_true")
    args = ap.parse_args()

    chosen, missing = plan(from_cache=args.from_cache or args.apply)
    print("\nsourced %d, unsourced %d" % (len(chosen), len(missing)))
    if missing:
        print("\n--- no clean Tatoeba sentence found ---")
        for k in sorted(missing):
            print("   " + k)
    if args.apply:
        print()
        apply(chosen)


if __name__ == "__main__":
    main()
