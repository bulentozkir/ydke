import glob, os, sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
bad = 0
for f in sorted(glob.glob("data/*.js")):
    t = open(f, encoding="utf-8").read()
    for i, line in enumerate(t.split("\n"), 1):
        # count unescaped double-quotes; odd => a string literal is left open
        # at end of line (a raw newline broke it -> JS SyntaxError)
        cnt = 0
        j = 0
        while j < len(line):
            c = line[j]
            if c == "\\":
                j += 2
                continue
            if c == '"':
                cnt += 1
            j += 1
        if cnt % 2 == 1:
            bad += 1
            if bad <= 12:
                print(f"BROKEN {os.path.basename(f)}:{i}  {line.strip()[:90]}")
print(f"\nlines with an unterminated string literal: {bad}")
