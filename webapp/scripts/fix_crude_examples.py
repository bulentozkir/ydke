# -*- coding: utf-8 -*-
"""Replace crude harvested example sentences with clean, hand-written ones.

These entries have a perfectly good headword (toenail, brioche, publican,
langoustine) that the bulk harvest paired with an unsuitable Tatoeba
sentence. The word must be kept, so only `example` is rewritten. Each
replacement is a natural sentence that actually uses the headword, with a
Turkish translation, matching the format used elsewhere in the data files.

Run: python scripts/fix_crude_examples.py --apply
"""
import io
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DATA = os.path.join(ROOT, "data")

# file -> word -> (native sentence, Turkish translation)
FIXES = {
    "wordsa2.js": {
        "lowlife": ("He called the pickpocket a lowlife.",
                    "Yankesiciye aşağılık adam dedi."),
    },
    "wordsb2.js": {
        "acceptably": ("She performed acceptably in her first interview.",
                       "İlk mülakatında kabul edilebilir bir performans gösterdi."),
        "acceptant": ("The committee was acceptant of the new proposal.",
                      "Komite yeni öneriyi kabul etmeye açıktı."),
        "accepter": ("The accepter of the award gave a short speech.",
                     "Ödülü kabul eden kişi kısa bir konuşma yaptı."),
        "accepters": ("The accepters of the plan met on Monday.",
                      "Planı kabul edenler pazartesi günü toplandı."),
        "acceptive": ("He has an acceptive attitude towards change.",
                      "Değişime karşı kabullenici bir tutumu var."),
        "acceptors": ("In chemistry, acceptors receive electrons from donors.",
                      "Kimyada alıcılar, vericilerden elektron alır."),
        "dispensary": ("The village dispensary opens at eight.",
                       "Köy dispanseri sekizde açılıyor."),
        "slanderous": ("He was sued for making slanderous remarks.",
                       "İftira niteliğinde sözler söylediği için dava edildi."),
        "treasonous": ("The general was accused of treasonous acts.",
                       "General vatana ihanet suçlamasıyla suçlandı."),
    },
    "wordsc1.js": {
        "royally": ("The guests were royally entertained at the palace.",
                    "Konuklar sarayda görkemli bir şekilde ağırlandı."),
        "toenail": ("He cut his toenails after the shower.",
                    "Duştan sonra ayak tırnaklarını kesti."),
        "underfoot": ("The fallen leaves crackled underfoot.",
                      "Düşen yapraklar ayak altında çıtırdadı."),
        "zloty": ("The zloty is the currency of Poland.",
                  "Zloti, Polonya'nın para birimidir."),
        "mightily": ("He strove mightily to finish the race.",
                     "Yarışı bitirmek için büyük çaba gösterdi."),
        "socialise": ("Students socialise in the common room after lessons.",
                      "Öğrenciler derslerden sonra ortak salonda sosyalleşir."),
        "aneurismal": ("The surgeon repaired the aneurismal artery.",
                       "Cerrah anevrizmalı atardamarı onardı."),
        "aneurismatic": ("The scan showed aneurismatic changes in the vessel.",
                         "Tarama damarda anevrizmatik değişiklikler gösterdi."),
        "aneurism": ("An aneurism is a bulge in the wall of a blood vessel.",
                     "Anevrizma, damar duvarındaki bir baloncuktur."),
        "agglomeration": ("The city is an agglomeration of small towns.",
                          "Şehir, küçük kasabaların bir yığışımıdır."),
        "auberge": ("We spent the night at a small auberge in the hills.",
                    "Geceyi tepelerdeki küçük bir handa geçirdik."),
        "acceptableness": ("The acceptableness of the offer was debated.",
                           "Teklifin kabul edilebilirliği tartışıldı."),
        "bingle": ("He had a minor bingle in the car park.",
                   "Otoparkta küçük bir kaza yaptı."),
        "ingrown": ("An ingrown nail can be very painful.",
                    "Batık tırnak çok acı verebilir."),
        "insufflation": ("Insufflation is used to inflate the abdomen during surgery.",
                         "İnsüflasyon, ameliyat sırasında karnı şişirmek için kullanılır."),
        "insufflate": ("The surgeon will insufflate the cavity with gas.",
                       "Cerrah boşluğu gazla şişirecek."),
        "infix": ("An infix is inserted inside a word rather than at its edge.",
                  "İç ek, kelimenin kenarına değil içine yerleştirilir."),
        "ingroup": ("People often favour members of their own ingroup.",
                    "İnsanlar genellikle kendi iç gruplarının üyelerini kayırır."),
        "ingrowth": ("The ingrowth of new tissue closed the wound.",
                     "Yeni dokunun içe doğru büyümesi yarayı kapattı."),
        "langouste": ("The restaurant serves fresh langouste in summer.",
                      "Restoran yazın taze böcek ıstakozu servis ediyor."),
        "langoustine": ("We ordered langoustines with garlic butter.",
                        "Sarımsaklı tereyağıyla küçük ıstakoz sipariş ettik."),
    },
    "wordsc2.js": {
        "defamatory": ("The article contained defamatory statements.",
                       "Makalede karalayıcı ifadeler vardı."),
        "disturber": ("The barking dog was a constant disturber of the peace.",
                      "Havlayan köpek sürekli bir huzur bozucuydu."),
        "defamer": ("The defamer was ordered to apologise in public.",
                    "Karalayan kişiden alenen özür dilemesi istendi."),
        "desensitize": ("Repeated exposure can desensitize people to violence.",
                        "Tekrarlanan maruziyet insanları şiddete karşı duyarsızlaştırabilir."),
        "micturate": ("Patients are asked to micturate before the scan.",
                      "Hastalardan tarama öncesinde idrara çıkmaları istenir."),
        "popover": ("She baked popovers for breakfast.",
                    "Kahvaltı için popover pişirdi."),
    },
    "toefl.js": {
        "snafu": ("A scheduling snafu delayed the whole flight.",
                  "Bir planlama karışıklığı tüm uçuşu geciktirdi."),
        "talentless": ("The critic called the play talentless and dull.",
                       "Eleştirmen oyunu yeteneksiz ve sıkıcı buldu."),
        "ostensibly": ("He came ostensibly to help, but he wanted money.",
                       "Görünüşte yardım etmeye geldi ama para istiyordu."),
        "brioche": ("We had warm brioche with jam for breakfast.",
                    "Kahvaltıda reçelli sıcak brioche yedik."),
        "hackbut": ("A hackbut was an early type of firearm.",
                    "Hackbut, erken dönem bir ateşli silah türüydü."),
        "hackberry": ("The hackberry tree gives good shade in summer.",
                      "Çitlembik ağacı yazın iyi gölge verir."),
        "illicitly": ("The goods had been illicitly imported.",
                      "Mallar yasa dışı yollarla ithal edilmişti."),
        "publican": ("The publican served drinks all evening.",
                     "Meyhaneci bütün akşam içki servisi yaptı."),
        "publicise": ("They will publicise the event on local radio.",
                      "Etkinliği yerel radyoda duyuracaklar."),
        "slanderously": ("He spoke slanderously about his rival.",
                         "Rakibi hakkında iftira dolu konuştu."),
        "smashingly": ("The opening night went smashingly well.",
                       "Açılış gecesi harika geçti."),
    },
    "wordsc1gode.js": {
        "epischen": ("Der Roman schildert eine Reise von epischen Ausmaßen.",
                     "Roman, destansı boyutlarda bir yolculuğu anlatıyor."),
    },
    "wordsc2gode.js": {
        "widerstreitend": ("Er musste zwischen widerstreitenden Interessen abwägen.",
                           "Çatışan çıkarlar arasında denge kurmak zorundaydı."),
        "englischsprachigen": ("Sie arbeitet in einem englischsprachigen Umfeld.",
                               "İngilizce konuşulan bir ortamda çalışıyor."),
    },
    "wordsa2fr.js": {
        "prétendument": ("Il a prétendument quitté la ville hier soir.",
                         "Sözde dün akşam şehirden ayrılmış."),
    },
    "wordsb2fr.js": {
        "le basketteur": ("Ce basketteur mesure presque deux mètres.",
                          "Bu basketbolcu neredeyse iki metre boyunda."),
    },
    "wordsc1fr.js": {
        "surnommé": ("Il est surnommé le lion par ses coéquipiers.",
                     "Takım arkadaşları ona aslan lakabını takmış."),
        "chopper": ("Il a réussi à chopper le dernier train.",
                    "Son treni yakalamayı başardı."),
    },
    "wordsc2fr.js": {
        "lesbien": ("Elle milite pour les droits du couple lesbien.",
                    "Lezbiyen çiftlerin hakları için mücadele ediyor."),
    },
}

ENTRY_RE = re.compile(r'\n  \{\s*\n?(?:[^{}]|\{[^{}]*\})*?\n?  \},', re.S)
WORD_RE = re.compile(r'word:\s*"((?:\\.|[^"\\])*)"')
EX_RE = re.compile(r'(example:\s*")((?:\\.|[^"\\])*)(")')


def main():
    apply_changes = "--apply" in sys.argv
    total, missing = 0, []

    for fname, table in FIXES.items():
        path = os.path.join(DATA, fname)
        text = io.open(path, encoding="utf-8").read()
        seen = set()

        def repl(m):
            block = m.group(0)
            wm = WORD_RE.search(block)
            if not wm:
                return block
            key = wm.group(1).strip()
            pair = table.get(key) or table.get(key.lower())
            if not pair:
                return block
            seen.add(key.lower())
            new_ex = "%s - %s" % pair
            assert '"' not in new_ex, key
            return EX_RE.sub(lambda _m: 'example: "%s"' % new_ex, block, count=1)

        out = ENTRY_RE.sub(repl, text)
        for want in table:
            if want.lower() not in seen:
                missing.append("%s/%s" % (fname, want))
        total += len(seen)

        if apply_changes and seen:
            if out.count("{") != out.count("}"):
                print("ABORT %s: brace imbalance" % fname)
                return 1
            io.open(path, "w", encoding="utf-8", newline="\n").write(out)
        print("%-20s %d replaced" % (fname, len(seen)))

    print("\ntotal %d example sentences rewritten%s"
          % (total, "" if apply_changes else "  (DRY RUN)"))
    if missing:
        print("NOT FOUND (%d): %s" % (len(missing), ", ".join(missing)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
