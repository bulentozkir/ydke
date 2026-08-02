# Android — Release Runbook

Step-by-step from a fixed `udsp` repo to a published Play Store listing.

**Prerequisite:** all **Blocker** items in
[03-blockers-and-fixes.md](03-blockers-and-fixes.md) are resolved and deployed
to production. B2 (`assetlinks.json`) in particular must be live before the
first build, because the fingerprints are what the build is verified against.

---

## 1. Accounts and costs

| Item | Cost | Notes |
| --- | --- | --- |
| Google Play Developer account | **$25** one-time | Personal or organisation. |
| Identity verification | — | Required for all accounts. Organisation accounts additionally need a **D-U-N-S number**. |
| Custom domain (B5) | Varies | Register before building. |

**Account type matters.** An organisation account avoids the closed-testing gate
described in §5 but requires D-U-N-S registration, which can itself take days.
Choose deliberately.

---

## 2. Toolchain setup

```powershell
# JDK 17 and Android SDK are required; Bubblewrap can install them for you.
npm install -g @bubblewrap/cli
bubblewrap doctor
```

`bubblewrap doctor` must report a healthy JDK and Android SDK before continuing.
Resolve anything it flags now — failures here surface later as opaque Gradle
errors.

---

## 3. Generate the project

```powershell
mkdir twa
cd twa
bubblewrap init --manifest https://<your-domain>/site.webmanifest
```

Answer the prompts carefully. These are hard to change later:

| Prompt | Value | Reversible? |
| --- | --- | --- |
| Application ID | Reverse of your domain, e.g. `com.topwords.trainer` | **No** — permanent once published |
| App name / launcher name | `Top Words` (launcher names truncate ~12 chars) | Yes |
| Display mode | `standalone` | Yes |
| Orientation | Match the manifest after B7 | Yes |
| Status bar colour | `#0f172a` | Yes |
| Signing key | Create new, or reuse an existing upload keystore | **No** — keep the keystore and its password backed up permanently |

> Losing the upload keystore is recoverable through Play support only if Play
> App Signing is enabled. Enable it. Back the keystore up off-machine anyway.

Commit the generated `twa/` directory (excluding the keystore and
`local.properties`) so builds are reproducible.

---

## 4. Build, verify, and sign

```powershell
bubblewrap build
```

Produces `app-release-bundle.aab` (upload this) and `app-release-signed.apk`
(for local testing).

**Verify asset links before uploading anything:**

```powershell
# 1. Upload-key fingerprint used by the local APK
keytool -list -v -keystore android.keystore -alias android
```

Confirm the SHA-256 shown appears in the deployed
`/.well-known/assetlinks.json`, then check Google's verifier:

```
https://digitalassetlinks.googleapis.com/v1/statements:list?source.web.site=https://<your-domain>&relation=delegate_permission/common.handle_all_urls
```

**Install locally and confirm there is no URL bar:**

```powershell
adb install -r app-release-signed.apk
```

A visible address bar means verification failed. Do not proceed — nothing later
in this runbook fixes it.

After the first upload to Play, retrieve the **Play App Signing** SHA-256 from
**Test and release → Setup → App integrity** and add it as a second entry in
`assetlinks.json`. See [03-blockers-and-fixes.md](03-blockers-and-fixes.md) §B2 —
this is the most common cause of "works in testing, URL bar in production".

---

## 5. Play Console testing track requirements

**This is the long pole in the schedule.**

Developers with **personal accounts created after 13 November 2023** must run a
closed test with **at least 12 testers, opted in continuously for at least 14
days**, before they can apply for production access. Production and
pre-registration are disabled in Play Console until that is satisfied.

Source: <https://support.google.com/googleplay/android-developer/answer/14151465>

Sequence:

1. **Internal testing** — optional but recommended. Builds reach testers within
   minutes. Use this to shake out the URL-bar and sign-in issues.
2. **Closed testing** — recruit 12+ real testers. They must **remain opted in**
   for 14 continuous days; someone who joins and leaves resets their own
   contribution. Over-recruit to 15–20.
3. **Apply for production access** — answer three sections: about the closed
   test, about the app, and production readiness. Review is typically ≤ 7 days.
4. **Production** — once granted.

Practical notes:

- Recruit testers who plausibly resemble the audience — Turkish students
  preparing for YDS/YÖKDİL. Reviewers assess whether testers actually engaged.
- Keep a record of feedback received; the production-access application asks you
  to summarise it.
- Emphasise to testers that they must stay opted in. This is the most common
  reason applications are rejected.
- Verify the rule at submission time. Google has revised it before.

---

## 6. Store listing assets

| Asset | Spec | Notes |
| --- | --- | --- |
| App icon | 512×512 PNG, 32-bit, no alpha | Derived from `icon-512.png` (B1), flattened onto `#0f172a`. |
| Feature graphic | 1024×500 PNG/JPG | Required. No transparency. Avoid text near edges. |
| Phone screenshots | 2–8, min 320 px, max 3840 px, 16:9 or 9:16 | Show flashcards, quiz, games grid, stats. |
| 7" and 10" tablet screenshots | Optional | Required for the "designed for tablets" badge; depends on B7. |
| Short description | ≤ 80 chars | |
| Full description | ≤ 4000 chars | |

**Localisation:** the app is Turkish-first (`manifest lang: "tr"`, all UI copy
bilingual TR·EN). Publish **`tr-TR` as the default listing language** and add
`en-US` as a secondary. Getting this backwards buries the listing for its actual
audience.

Reuse the existing SEO copy from `index.html` — the meta description and JSON-LD
`description` are already well-targeted at YDS / YÖKDİL / ÜDS / TOEFL / telc /
CEFR search intent.

---

## 7. Policy declarations

| Declaration | Answer | Source of truth |
| --- | --- | --- |
| **Privacy policy URL** | `https://<domain>/privacy` | Already exists. |
| **Contains ads** | **No**, once B4 ships | Must match the built artifact. Re-check before each release. |
| **Data Safety — collected** | Email address, name, profile photo URL, user ID | Firebase Auth, Google provider. |
| **Data Safety — collected** | App activity / progress (known words, favourites, streak, history) | Firestore `users/{uid}`. |
| **Data Safety — encryption in transit** | Yes | HTTPS + Firebase. |
| **Data Safety — deletion** | Yes, in-app **and** web URL | `deleteAccountAndProfile()` + B6 URL. |
| **Content rating (IARC)** | Complete the questionnaire | Educational vocabulary content; expect Everyone / 3+. Answer the ads question consistently with the row above. |
| **Target audience** | Not child-directed | Declaring a child audience triggers Families policy and would forbid the current analytics/ads posture entirely. |
| **Government app / financial features** | No | |

The Firestore document also stores `displayName`, `email`, and `photoURL`
(see the seed object in `firebase-client.js`). Declare these — they are
collected *and stored*, not merely passed through.

---

## 8. Pre-submission verification

- [ ] `bubblewrap doctor` clean
- [ ] `assetlinks.json` returns HTTP 200, `application/json`, no redirect
- [ ] Google Digital Asset Links API returns the package with no `errorCode`
- [ ] **Both** upload-key and Play-signing fingerprints present in `assetlinks.json`
- [ ] Installed build shows **no URL bar** on launch and after navigating to 3+ pages
- [ ] Google sign-in completes; `users/{uid}` document is created (B3 regression check)
- [ ] Session survives force-quit and relaunch
- [ ] Zero requests to `pagead2.googlesyndication.com` on any page (B4)
- [ ] Airplane-mode launch reaches cached content, not the offline placeholder
- [ ] Back gesture returns from third-party dictionary links without trapping
- [ ] Play **pre-launch report** reviewed — it runs on real devices and surfaces crashes, accessibility issues, and slow startup
- [ ] Target API level meets the current Play requirement

---

## 9. Update workflow after launch

Because the TWA loads live content, **web deploys reach users immediately with
no store review**. A new `.aab` is only needed when:

- the application ID, name, or icon changes
- `assetlinks.json` fingerprints change
- Play raises the minimum target API level (annual)
- Bubblewrap or `android-browser-helper` ships a fix you need

Re-build with:

```powershell
bubblewrap update   # pulls manifest changes into the Android project
bubblewrap build
```

Keep the `twa/` directory and the keystore under the same lifecycle management
as the site itself.
