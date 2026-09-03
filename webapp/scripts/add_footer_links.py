"""Insert the legal/footer link row into every top-level page's <footer>.

A privacy policy has to be clearly labelled and easily accessible from every
page. Idempotent: skips files that already have the row.
"""

import glob
import os

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)

ANCHOR = '    <footer class="site-footer">\n'
BLOCK = (
    '      <nav class="footer-links" aria-label="Site bilgileri ve yasal sayfalar">\n'
    '        <a href="about.html">Hakkında</a>\n'
    '        <a href="help.html">Yardım</a>\n'
    '        <a href="privacy.html">Gizlilik Politikası</a>\n'
    '        <a href="terms.html">Kullanım Şartları</a>\n'
    '        <a href="terms.html#contact">İletişim</a>\n'
    "      </nav>\n"
)

changed, skipped, missing = [], [], []

for path in sorted(glob.glob(os.path.join(ROOT, "*.html"))):
    name = os.path.basename(path)
    with open(path, encoding="utf-8") as fh:
        src = fh.read()
    if 'class="footer-links"' in src:
        skipped.append(name)
        continue
    if src.count(ANCHOR) != 1:
        missing.append(name)
        continue
    with open(path, "w", encoding="utf-8", newline="") as fh:
        fh.write(src.replace(ANCHOR, ANCHOR + BLOCK))
    changed.append(name)

print("changed  %2d: %s" % (len(changed), ", ".join(changed)))
print("skipped  %2d: %s" % (len(skipped), ", ".join(skipped)))
print("NO ANCHOR %2d: %s" % (len(missing), ", ".join(missing)))
