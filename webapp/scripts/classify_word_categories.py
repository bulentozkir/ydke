#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""classify_word_categories.py

Replaces the existing `category` field (currently "Vocabulary"/"Phrasal
Verbs"/"TOEFL"/"Partikelverben" -- a "which file did this come from" tag,
which the user said was wrong) with a real SEMANTIC/TOPICAL domain for
each individual word, e.g. Sports, Agriculture, Forestry, Literature,
Technology, Science, etc. -- "the domain or area of the word", independent
of which file/level/language it lives in.

Approach: best-effort KEYWORD-MATCHING heuristic against each entry's
English definition text (the part before " - " -- every data/*.js file
uses this "English. - Türkçe." convention regardless of the word's own
language, so this one classifier works uniformly across EN/DE/FR files).
This is NOT true semantic understanding (no ML/LLM classification was
available in this pipeline) -- it's the same "best-effort, not perfect"
bar already accepted elsewhere in this project for similarly-scaled
heuristics (e.g. the French noun-gender guesser). Most common/function
words (articles, basic verbs like "make"/"go", pronouns, etc.) won't match
any domain's keywords and fall back to "General".

Each domain's keyword list is matched via one combined word-boundary
regex (alternation), case-insensitive, against the definition text; the
domain with the MOST keyword hits wins. Ties broken by DOMAIN_ORDER
(more specific/niche domains listed first, so e.g. "Forestry" beats the
more general "Nature" on a tie).

Usage: python scripts/classify_word_categories.py [--dry-run] [--sample N]
"""
import os
import re
import sys
import glob

DATA_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "data")

# Every file that currently has a `category` field (added by the prior
# add_category_field.py pass) gets reclassified. This is every file in the
# shared word-list system -- data/synant*.js and data/readingcomp*.js were
# never given a category field and are NOT touched here either.
TARGET_FILES = [
    "wordsa1.js", "wordsa2.js", "wordsb1.js", "wordsb2.js", "wordsc1.js", "wordsc2.js",
    "wordsa1gode.js", "wordsa2gode.js", "wordsb1gode.js", "wordsb2gode.js", "wordsc1gode.js", "wordsc2gode.js",
    "wordsa1fr.js", "wordsa2fr.js", "wordsb1fr.js", "wordsb2fr.js", "wordsc1fr.js", "wordsc2fr.js",
    "wordsa1it.js", "wordsa2it.js", "wordsb1it.js", "wordsb2it.js", "wordsc1it.js", "wordsc2it.js",
    "wordsa1es.js", "wordsa2es.js", "wordsb1es.js", "wordsb2es.js", "wordsc1es.js", "wordsc2es.js",
    "wordsa1pt.js", "wordsa2pt.js", "wordsb1pt.js", "wordsb2pt.js", "wordsc1pt.js", "wordsc2pt.js",
    "phrasalverbsen.js", "toefl.js", "partikelverbde.js",
]

# ------------------------------------------------------------- taxonomy
# Order matters for tie-breaking: earlier = wins ties (more specific first).
DOMAIN_KEYWORDS = {
    "Forestry": [
        "forest", "forestry", "woodland", "timber", "logging", "lumber", "sawmill",
        "conifer", "deforestation", "reforestation", "lumberjack", "underbrush",
        "grove", "thicket", "sapling", "tree trunk", "canopy of trees", "fell a tree",
    ],
    "Agriculture": [
        "farm", "farmer", "farming", "crop", "harvest", "livestock", "cattle",
        "barn", "irrigation", "agricultural", "agriculture", "field", "grain",
        "pesticide", "fertilizer", "plow", "plough", "tractor", "orchard",
        "pasture", "granary", "dairy", "poultry", "sow seeds", "cultivate",
        "vineyard", "husbandry", "silo", "rural farm",
    ],
    "Sports": [
        "sport", "game", "athlete", "athletic", "team", "player", "ball",
        "match", "score", "compete", "competition", "championship",
        "tournament", "referee", "coach", "stadium", "gym", "fitness",
        "olympic", "goal", "race", "swim", "run a race", "tennis", "football",
        "soccer", "basketball", "baseball", "golf", "boxing", "wrestling",
        "cyclist", "marathon", "gymnastics", "skiing", "hockey",
    ],
    "Literature": [
        "novel", "poem", "poetry", "poet", "author", "book", "story",
        "literary", "literature", "fiction", "narrative", "verse", "prose",
        "writer", "publish", "chapter", "plot", "protagonist", "manuscript",
        "anthology", "memoir", "essay", "playwright", "epic", "novelist",
        "biography", "publisher",
    ],
    "Technology": [
        "computer", "software", "internet", "digital", "device", "technology",
        "electronic", "programming", "app ", "website", "network", "hardware",
        "robot", "artificial intelligence", "algorithm", "database", "server",
        "smartphone", "app,", "coding", "byte", "processor", "microchip",
        "gadget", "wireless", "download", "upload", "cyber",
    ],
    "Science": [
        "chemistry", "chemical", "physics", "biology", "biological",
        "scientist", "scientific", "experiment", "laboratory", "hypothesis",
        "molecule", "atom", "cell", "organism", "gene", "genetic", "theory of",
        "particle", "chemical reaction", "element", "compound", "microscope",
        "specimen", "enzyme", "protein", "chromosome", "isotope", "catalyst",
        "fungal", "fungus", "bacterium", "photosynthesis", "electron",
        "neutron", "proton", "spore", "spherical aberration", "quantum",
        "gravitational", "radioactive", "chemist",
    ],
    "Medicine": [
        "disease", "illness", "medicine", "medical", "doctor", "hospital",
        "patient", "surgery", "surgeon", "symptom", "treatment", "diagnosis",
        "nurse", "clinic", "infection", "virus", "vaccine", "therapy",
        "medication", "prescription", "physician", "wound", "injury", "cure",
        "ailment", "pharmacy", "anatomy", "sick people", "treat sick",
        "feel unwell", "healthy", "anesthetic", "anaesthetic", "anesthetize",
        "anaesthetize", "tumor", "tumour", "hormone", "antibody", "antibiotic",
        "bacteria", "bacterial", "cancer", "inflammation", "immune system",
        "diabetes", "asthma", "bloodstream", "eosinophil", "melanin",
        "contagious", "epidemic", "dental", "dentist",
    ],
    "Food": [
        "food", "cook", "cooking", "recipe", "kitchen", "ingredient", "dish",
        "meal", "cuisine", "restaurant", "chef", "bake", "baking", "flavor",
        "flavour", "taste", "spice", "sauce", "beverage", "drink", "dessert",
        "eat", "edible", "vegetable", "fruit", "meat", "seasoning", "dine",
    ],
    "Animals": [
        "animal", "mammal", "bird", "fish", "reptile", "insect", "wildlife",
        "species", "predator", "prey", "zoo", "pet", "creature", "breed of",
        "herd", "flock", "nest", "paw", "claw", "fur", "feather", "beak",
        "wing", "tail", "hoof", "dinosaur", "rodent", "feline", "canine",
        "primate", "amphibian", "crustacean", "mollusk", "mollusc", "marsupial",
    ],
    "Nature": [
        "nature", "environment", "ecology", "ecological", "landscape",
        "mountain", "river", "ocean", "sea", "lake", "valley", "desert",
        "wilderness", "ecosystem", "climate change", "natural resource",
        "geology", "volcano", "island", "coast", "cliff", "cave",
    ],
    "Weather": [
        "weather", "climate", "temperature", "rain", "snow", "storm", "wind",
        "cloud", "sunny", "humidity", "forecast", "meteorology", "drought",
        "flood", "thunder", "lightning", "hurricane", "tornado", "frost",
    ],
    "Travel": [
        "travel", "tourist", "tourism", "journey", "trip", "vacation",
        "holiday abroad", "passport", "airport", "hotel", "destination",
        "sightseeing", "voyage", "itinerary", "luggage", "cruise", "resort",
        "backpacking", "excursion",
    ],
    "Transportation": [
        "vehicle", "car", "truck", "bus", "train", "airplane", "aircraft",
        "ship", "boat", "transport", "transportation", "traffic", "highway",
        "railway", "engine", "motor", "driver", "pilot", "passenger seat",
        "bicycle", "motorcycle", "subway", "commute",
    ],
    "Business": [
        "business", "corporate", "marketplace", "trade", "economy",
        "economic", "finance", "financial", "bank", "investment", "profit",
        "revenue", "commerce", "entrepreneur", "industry", "manager",
        "employee", "employer", "salary", "budget", "stock market", "merger",
        "shareholder", "commodity", "businessman", "businesswoman",
        "advertising", "debt", "loan", "mortgage", "invoice", "repayment",
        "creditor", "debtor", "currency", "expenditure", "accountant",
        "auditor", "taxation", "wholesale", "retail",
    ],
    "Law": [
        "law", "legal", "court", "judge", "lawyer", "attorney", "crime",
        "criminal", "police", "justice", "verdict", "lawsuit", "contract",
        "legislation", "constitution", "prosecute", "prison sentence",
        "sentenced to", "witness", "evidence", "jury", "arrest", "prison",
        "statute",
    ],
    "Government": [
        "government", "politics", "political", "election", "president",
        "senate", "parliament", "policy", "diplomat", "vote", "democracy",
        "ministry", "official government", "bureaucracy", "legislature",
        "governor", "campaign", "citizen",
    ],
    "Military": [
        "military", "army", "soldier", "war", "battle", "weapon", "combat",
        "navy", "troop", "general (military)", "warfare", "defense force",
        "artillery", "invasion", "enemy", "fortress", "siege", "commander",
        "regiment",
    ],
    "Religion": [
        "religion", "religious", "god", "church", "prayer", "faith",
        "spiritual", "worship", "priest", "temple", "sacred", "holy",
        "bible", "scripture", "ritual", "belief in god", "divine", "pilgrimage",
        "monk", "clergy",
    ],
    "Education": [
        "school", "student", "teacher", "education", "university", "college",
        "classroom", "academic", "curriculum", "lecture", "exam", "academic degree",
        "university degree", "professor", "study a subject", "textbook",
        "tuition", "scholarship", "graduate", "campus",
    ],
    "Arts": [
        "art", "artist", "painting", "paint a picture", "sculpture", "museum",
        "gallery", "music", "musician", "song", "melody", "instrument",
        "orchestra", "concert", "dance", "theater", "theatre", "film",
        "actor", "actress", "performance", "artistic", "canvas", "compose music",
    ],
    "Family": [
        "family", "parent", "mother", "father", "child", "sibling", "spouse",
        "marriage", "relative", "grandparent", "husband", "wife", "kinship",
        "ancestor", "descendant", "household member", "married", "matrimony",
        "matrimonial", "wedlock", "widow", "widower", "orphan", "nephew",
        "niece", "cousin", "aunt", "uncle", "godparent",
    ],
    "Emotions": [
        "emotion", "feeling", "happy", "sad", "angry", "fear", "joy", "love",
        "hate", "anxious", "excitement", "depression", "grief", "jealousy",
        "affection", "mood", "delight", "sorrow", "pride", "shame",
        "contempt", "disgust", "envy", "resentment", "remorse", "despair",
        "anguish", "gratitude", "compassion", "loneliness",
    ],
    "Clothing": [
        "clothing", "clothes", "garment", "fashion", "dress", "shirt",
        "trousers", "shoe", "fabric", "textile", "tailor", "wear a", "outfit",
        "costume", "wardrobe", "sew", "stitch", "attire", "footwear",
        "headwear", "jacket", "glove", "skirt", "blouse", "sleeve", "collar",
        "knitwear", "apron", "scarf",
    ],
    "Construction": [
        "building", "construction", "architect", "architecture", "structure",
        "brick", "concrete", "foundation", "blueprint", "scaffolding",
        "construct a", "engineer a building", "skyscraper", "carpenter",
    ],
    "Household": [
        "house", "home", "household", "furniture", "kitchen appliance",
        "bedroom", "living room", "domestic", "chore", "cleaning", "utensil",
        "appliance",
    ],
    "Communication": [
        "language", "communicate", "communication", "speak", "speech",
        "conversation", "media", "journalism", "newspaper", "broadcast",
        "message", "telephone", "letter (mail)", "email", "publish news",
        "interview", "linguistic", "postal", "telegram", "correspondence",
        "postage",
    ],
    "Geography": [
        "country", "continent", "region", "border", "map", "geography",
        "geographic", "capital city", "population of", "territory",
        "province", "nation",
    ],
}

# Order used for tie-breaking (first match in this list wins a tie).
DOMAIN_ORDER = list(DOMAIN_KEYWORDS.keys())
GENERAL = "General"
MIX = "Mix"  # reserved -- never assigned to a word; means "no filter" in the UI


def compile_domain_patterns():
    patterns = {}
    for domain, words in DOMAIN_KEYWORDS.items():
        parts = []
        for kw in words:
            kw = kw.strip()
            if not kw:
                continue
            escaped = re.escape(kw)
            # Single-word keywords tolerate a trailing s/es plural ("law" ->
            # also catches "laws") -- strict \bword\b otherwise misses every
            # plural, silently starving on-topic words into "General".
            # Deliberately NOT auto-adding -ing/-ed/-er verb inflections:
            # several keywords here are noun/verb homographs with unrelated
            # meanings when inflected as a verb (e.g. "train" the vehicle
            # vs. "trained" = past tense of "to train/teach" someone,
            # nothing to do with transportation) -- found via QA sampling
            # (see repo memory). Desired inflected forms (farming/farmer,
            # etc.) are added as their own explicit keyword below instead,
            # so each one is a deliberate choice, not a blanket assumption.
            if " " not in kw:
                parts.append(escaped + r"(?:s|es)?")
            else:
                parts.append(escaped)
        pattern = r"\b(?:" + "|".join(parts) + r")\b"
        patterns[domain] = re.compile(pattern, re.IGNORECASE)
    return patterns


DOMAIN_PATTERNS = compile_domain_patterns()


def classify(definition_en):
    best_domain = GENERAL
    best_score = 0
    for domain in DOMAIN_ORDER:
        matches = DOMAIN_PATTERNS[domain].findall(definition_en)
        score = len(matches)
        if score > best_score:
            best_score = score
            best_domain = domain
    return best_domain


ENTRY_RE = re.compile(
    r'category:\s*"[^"]*",(\s*)definition:\s*"((?:[^"\\]|\\.)*)"'
)


def english_part(definition_raw):
    # Un-escape the common \" sequences enough for matching purposes (the
    # classifier only reads it, never writes it back).
    idx = definition_raw.find(" - ")
    return definition_raw[:idx] if idx != -1 else definition_raw


def process_file(path, dry_run, counts):
    with open(path, encoding="utf-8") as f:
        content = f.read()

    def repl(m):
        ws, definition_raw = m.group(1), m.group(2)
        domain = classify(english_part(definition_raw))
        counts[domain] = counts.get(domain, 0) + 1
        return 'category: "' + domain + '",' + ws + 'definition: "' + definition_raw + '"'

    new_content, n = ENTRY_RE.subn(repl, content)
    if not dry_run:
        with open(path, "w", encoding="utf-8") as f:
            f.write(new_content)
    return n


def main():
    dry_run = "--dry-run" in sys.argv
    sample_n = None
    if "--sample" in sys.argv:
        sample_n = int(sys.argv[sys.argv.index("--sample") + 1])

    total_counts = {}
    total_n = 0
    for fname in TARGET_FILES:
        path = os.path.join(DATA_DIR, fname)
        if not os.path.exists(path):
            print(f"[missing] {fname}")
            continue
        counts = {}
        n = process_file(path, dry_run or sample_n is not None, counts)
        total_n += n
        for k, v in counts.items():
            total_counts[k] = total_counts.get(k, 0) + v
        print(f"[{'dry-run' if dry_run else 'write'}] {fname}: {n} entries classified")

    print("\nDomain distribution (all files):")
    for domain, cnt in sorted(total_counts.items(), key=lambda kv: -kv[1]):
        pct = 100.0 * cnt / total_n if total_n else 0
        print(f"  {domain:16s} {cnt:6d}  ({pct:5.1f}%)")
    print(f"\nTOTAL entries classified: {total_n}")


if __name__ == "__main__":
    main()
