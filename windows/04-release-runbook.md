# Windows — Release Runbook

Step-by-step from a fixed `udsp` repo to a published Microsoft Store listing.

**Prerequisite:** all **Blocker** items in
[03-blockers-and-fixes.md](03-blockers-and-fixes.md) are resolved. W2 (Partner
Center identity) must be done **before** generating the package, not after.

---

## 1. Accounts and costs

| Item | Cost | Notes |
| --- | --- | --- |
| Microsoft Partner Center developer account | One-time registration fee | Individual and company tiers differ. Microsoft periodically waives or changes this — **verify the current amount at signup** rather than budgeting from this document. |
| Code-signing certificate | **Not required** | Microsoft signs Store-distributed packages. Only a self-signed cert is needed for local sideload testing. |
| Custom domain | Optional here | Recommended for consistency with Android, but not a Windows blocker. |

Account verification is not instant. Start this step first — it runs in parallel
with all the repo fixes.

---

## 2. Reserve the app name and capture identity values

1. Partner Center → **Apps and games** → **New product** → **MSIX or PWA app**.
2. Reserve the name (e.g. `Top Words`). Reserve the Turkish-facing name too if
   it differs.
3. Open **Product management → Product identity** and copy all three values:

   - **Package identity Name** — e.g. `12345Publisher.TopWords`
   - **Publisher** — a GUID-form `CN=...` distinguished name
   - **Publisher display name**

Keep these somewhere durable. They are required inputs to the next step and
cannot be guessed or derived.

---

## 3. Generate the MSIX

1. Go to <https://www.pwabuilder.com> and enter the production URL.
2. Review the report card. Resolve anything it flags as required — it reads the
   same manifest this analysis did, so its findings should corroborate
   [01-app-analysis.md](01-app-analysis.md) §6.
3. Choose **Windows** → **Store package**.
4. Paste the three identity values from §2.
5. Set the window size per [03-blockers-and-fixes.md](03-blockers-and-fixes.md)
   §W10 (~1000×800, min ~360×640).
6. Download **both** artifacts:
   - the **Store-ready** package → for Partner Center
   - the **test / sideload** package → for local verification

The two differ in signing. Submitting the test package is a guaranteed
rejection.

---

## 4. Local verification

Install the **test** package:

```powershell
# Trust the bundled test certificate first (it ships alongside the package)
Add-AppxPackage -Path .\TopWords_x.y.z.0_test.msixbundle
```

Then run the **Windows App Certification Kit**:

```powershell
# Installed with the Windows SDK
& "${env:ProgramFiles(x86)}\Windows Kits\10\App Certification Kit\appcert.exe" `
    reset
& "${env:ProgramFiles(x86)}\Windows Kits\10\App Certification Kit\appcert.exe" `
    test -apptype windowsstoreapp -packagefullname <PackageFullName> -reportoutputpath .\wack-report.xml
```

WACK failures here are the same failures certification will find, but with a
minutes-long feedback loop instead of a days-long one. Do not skip it.

Uninstall cleanly when done:

```powershell
Get-AppxPackage *TopWords* | Remove-AppxPackage
```

---

## 5. Store listing assets

| Asset | Spec | Notes |
| --- | --- | --- |
| Screenshots | **Minimum 1366×768**, PNG | At least one required; up to 10. Capture from the packaged app, not the browser. |
| Store logo | 300×300 PNG | |
| Poster art / hero | 1920×1080 PNG | Optional but improves placement. |
| Description | ≤ 10,000 chars | |
| Short title | ≤ 50 chars | |
| Search terms | Up to 7 | Reuse the keyword research already in `index.html` meta tags. |

**Localisation:** publish **`tr-TR` as the primary listing language** with
`en-US` secondary, matching the app's actual audience. The existing SEO copy in
`index.html` (meta description and JSON-LD `description`) is already tuned for
YDS / YÖKDİL / ÜDS / TOEFL / telc / CEFR intent — reuse it rather than writing
new copy.

Take screenshots **after** the W7 desktop layout fix. Screenshots of a stretched
phone UI on a 1366×768 canvas are the listing's worst possible first impression.

---

## 6. Submission properties and declarations

| Field | Value | Source |
| --- | --- | --- |
| Category | **Education** | Matches manifest `categories`. |
| Privacy policy URL | `https://<domain>/privacy` | Already exists. |
| Website | `https://<domain>` | |
| Support contact | From `terms.html#contact` | Already exists. |
| Age rating | Complete the questionnaire | Educational vocabulary content; expect the lowest bracket. |
| Contains ads | **No**, once W4 ships | Must match the built package. |
| Markets | Worldwide, or Türkiye-first | Consider a staged rollout starting with Türkiye. |
| Pricing | Free | |
| Accessibility declaration | Only after the W9 audit | Do not claim accessibility support that has not been verified. |

**Data handling:** the app collects email address, display name, profile photo
URL, and user ID via Firebase Auth, plus study progress in Firestore. Disclose
this consistently with the Play Data Safety answers so the two store listings do
not contradict each other.

---

## 7. Pre-submission verification

- [ ] Partner Center identity values match the generated package exactly
- [ ] WACK passes with no failures
- [ ] Sideloaded package launches and reaches `home.html`
- [ ] Google sign-in completes; `users/{uid}` created (W3 regression check)
- [ ] Signed-in state survives closing and reopening the app
- [ ] "Load from cloud" restores progress onto the fresh packaged install (W5)
- [ ] Zero requests to `pagead2.googlesyndication.com` on any page (W4)
- [ ] Offline launch (disable network) reaches cached content
- [ ] Window resizes cleanly from ~400 px to maximised on 1080p (W7)
- [ ] Full keyboard traversal: nav, More sheet, category dialog, a game (W9)
- [ ] `speechSynthesis` produces audio for en/de/fr/it/es/pt in WebView2
- [ ] Both light and dark themes render correctly with a visible focus indicator
- [ ] Start tiles and taskbar icon render at 100 %, 150 %, and 200 % scaling (W1)

---

## 8. Update workflow after launch

Because the packaged app loads live content, **web deploys reach users
immediately with no Store review**. A new MSIX is only needed when:

- the app name, identity, or tile assets change
- the declared window size or capabilities change
- PWABuilder or WebView2 requires a package-level change

Regenerate through PWABuilder with the **same** identity values from §2 and
submit an updated package. Increment the version, and keep the WACK step in the
loop each time.

---

## 9. Sequencing note

The Windows track has **no closed-testing gate** — nothing equivalent to Play's
12-testers-for-14-days requirement. Once certification passes, the app can go
live.

That makes Windows the faster path to a real, published store listing, and a
useful proving ground for the shared manifest, auth, and ad-gating fixes before
the Android track commits to a permanent application ID and a
cryptographically-bound origin. Consider shipping Windows first.
