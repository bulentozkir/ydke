# -*- coding: utf-8 -*-
"""Add the legal pages to the mobile "More" bottom sheet.

The desktop footer carries the privacy/terms links, but styles.css hides
.site-footer entirely below 720px so the height-locked app shell never
forces a scroll. Mobile users would therefore have no reachable privacy
policy -- so the same links are added to the More sheet, which is the app's
mobile navigation.

Idempotent: skips files that already link to privacy.html from the sheet.
"""

import glob
import os

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)

ANCHOR = '        <button type="button" id="more-close" class="more-close-btn">Close</button>\n'
BLOCK = (
    '        <a class="more-link" href="privacy.html">🔒 Gizlilik · Privacy</a>\n'
    '        <a class="more-link" href="terms.html">📄 Şartlar · Terms</a>\n'
    '        <a class="more-link" href="terms.html#contact">✉️ İletişim · Contact</a>\n'
)

changed, skipped, missing = [], [], []

for path in sorted(glob.glob(os.path.join(ROOT, "*.html"))):
    name = os.path.basename(path)
    with open(path, encoding="utf-8") as fh:
        src = fh.read()
    if 'class="more-link" href="privacy.html"' in src:
        skipped.append(name)
        continue
    if src.count(ANCHOR) != 1:
        missing.append(name)
        continue
    with open(path, "w", encoding="utf-8", newline="") as fh:
        fh.write(src.replace(ANCHOR, BLOCK + ANCHOR))
    changed.append(name)

print("changed   %2d: %s" % (len(changed), ", ".join(changed)))
print("skipped   %2d: %s" % (len(skipped), ", ".join(skipped)))
print("NO ANCHOR %2d: %s" % (len(missing), ", ".join(missing)))
