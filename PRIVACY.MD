# YDKE Privacy Policy

**Effective date:** 11 September 2026

This Privacy Policy describes the native Windows application **YDKE - Yabanci Dil Kelime Ezberleme** ("YDKE", "the app", "we", or "us"). It applies to the current native YDKE application, not the retired web application or its former website.

## Summary

- YDKE can be used without an account and without an internet connection.
- Study data is stored on the Windows device by default.
- YDKE has no advertising, ad tracking, analytics, or profiling code.
- Google sign-in is optional. It opens in the user's normal browser.
- Cloud backup is never automatic. Saving to or loading from the cloud requires an explicit action in the app.
- The app does not need a child's name, email address, phone number, date of birth, or account in order to provide Cards, Quiz, Words, or Games.
- Optional Google and cloud features should be started and supervised by a parent or guardian for child users.

## Information Stored Locally

When you use YDKE without cloud features, the app stores data in the app's local Windows data folder. This can include:

- selected interface language, study language, level, text-size and appearance preferences;
- daily practice goal, reduced-motion preference, and untimed-practice preference;
- favorite and manually marked-known vocabulary items;
- card reviews, recall ratings, review dates, mistakes, streaks, daily goals, quiz results, game results, best scores, and resumable sessions; and
- local recovery information needed to complete a safe backup import.

The vocabulary datasets are bundled with the application. YDKE does not send ordinary study activity to a YDKE server.

A local backup created by the user contains settings and learning progress. It does not include the separate Google/Firebase credential store. A local backup is written to the location selected by the user and remains under the user's control.

If the application encounters an unhandled error, it may write a local `crash.log` containing the time and technical exception details. YDKE does not upload this file automatically.

## Optional Google Sign-In and Cloud Backups

Google sign-in is optional and is not required for learning. When a user or a parent/guardian chooses to sign in:

1. Google opens in the selected system browser, Microsoft Edge, or Google Chrome.
2. Google authenticates the account. YDKE does not receive or store the Google password.
3. YDKE exchanges the Google identity result with Firebase Authentication.
4. If the user explicitly chooses **Save to cloud**, YDKE uploads a backup containing the app's settings and learning progress to the YDKE Firebase/Firestore project.

For an optional signed-in account, the cloud service may process or store:

- email address;
- Google display name, when supplied by Google;
- Firebase user identifier (UID);
- Firebase authentication refresh-session information; and
- the settings and learning-progress backup that the user explicitly saves.

The native app uses a separate Firestore collection named `ydke_users`; it does not use the retired web application's `users` collection. The cloud document contains an opaque backup string and an update timestamp rather than exposing each learning field as a separate Firestore field.

**Load from cloud** is also explicit. It does not silently merge or replace local learning. YDKE validates the backup, asks for confirmation, and creates a safety copy before applying it locally.

Signing out removes the active local session and tokens from use. It does not delete local learning and does not delete an existing cloud backup. The app provides a separate action to delete the cloud backup. Deleting that backup does not delete the Google account or the Firebase account itself; those accounts are managed through their respective providers. The current native app does not provide an in-app Firebase account-deletion workflow.

## Credential Protection

YDKE keeps cloud credentials separate from local learning backups. Firebase and legacy Google refresh tokens are protected at rest with Windows Data Protection API (DPAPI) for the current Windows user. The credential file is not included in local backup export/import.

Legacy OAuth client settings may be retained locally for compatibility if that older sign-in path has been used. Do not share the YDKE local data folder or credential file with another person.

Network requests for optional cloud features use HTTPS endpoints operated by Google/Firebase. A temporary loopback listener on the local device is used during the browser sign-in flow; it is bound to loopback, used for the current sign-in attempt, and closed when the attempt ends.

## Children and Family Use

YDKE is designed for children ages 6-12 and does not require personal information or an account for its core learning features.

A parent or guardian should supervise any optional Google sign-in, cloud backup, browser choice, or data deletion action. If a child uses those optional features, the account and cloud data practices described above apply. YDKE does not intentionally request a child's name, phone number, address, date of birth, or school information.

We do not use advertising, behavioral advertising, analytics, or profiling in the native app. Parents or guardians should review Google's terms and privacy information before enabling Google sign-in for a child.

This section describes the product behavior. The publisher should obtain legal review for the jurisdictions in which YDKE is distributed, including requirements that may apply to child-directed services and parental consent.

## Third-Party Services

The optional cloud feature relies on services operated by Google:

- Google Account / Google OAuth for authentication;
- Firebase Authentication for verifying the sign-in result and maintaining the Firebase session; and
- Google Cloud Firestore for an explicitly saved YDKE backup.

These providers may process data under their own terms and privacy policies. YDKE does not control Google's processing, retention, security practices, or regional infrastructure. Review the current Google, Firebase, and Firestore privacy documentation before enabling cloud features.

Windows and the Microsoft Store may also process technical information under Microsoft's own terms when the application is installed, updated, or used. YDKE does not add a separate advertising or analytics SDK.

## What YDKE Does Not Do

The native YDKE app does not:

- require an account to study;
- sell learning data or account data;
- show advertisements;
- load advertising, analytics, or behavioral-tracking scripts;
- automatically upload local learning when a user signs in; or
- upload local crash logs through YDKE's own services.

The app's installed Windows speech feature uses the selected installed Windows speech voice. YDKE does not send speech text to a YDKE server. Windows voice-provider behavior is controlled by Windows and the installed voice configuration.

## Retention and Deletion

Local settings, progress, credentials, and crash logs remain on the device until the user removes them, uninstalls or resets the app, or they are replaced through the app's backup/recovery actions. Local backup files saved outside the app's data folder must be deleted by the user from their selected location.

For cloud data, the user can:

- delete the YDKE cloud backup from the account and backup tools in the app;
- sign out, which clears the active local session while leaving local learning intact; or
- contact the publisher for a privacy request using the support channel published with the YDKE distribution.

Google account and Firebase account records may have provider-controlled retention or backup periods after deletion. YDKE cannot shorten provider retention periods that are outside its control.

## Privacy Requests

For questions or requests about YDKE data, use the publisher support contact shown in the official YDKE Microsoft Store listing or distribution package. The publisher must replace that reference with a monitored privacy email address before publishing this policy as the final Store policy.

## Changes to This Policy

This policy may be updated when YDKE's data practices change or when legal requirements change. The effective date at the top will be updated with material revisions. The policy supplied with a release describes the behavior intended for that release.

## Related Documents

- [Project license](LICENSE)
- [Project notice](NOTICE)
