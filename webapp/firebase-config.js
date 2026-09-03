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

/* Top Words — public Firebase Web App config.
 * This is NOT a secret (unlike the Admin SDK service-account key discussed
 * elsewhere) — Firebase's client apiKey/appId are meant to be shipped to
 * the browser. Access control is enforced by Firestore Security Rules
 * (see firestore.rules) + Firebase Authentication, not by hiding this file.
 *
 * Get the real values from:
 *   Firebase Console -> Project Settings -> General -> Your apps
 *   -> (Web app) -> SDK setup and configuration -> Config
 * Replace every REPLACE_ME_* placeholder below, then this file is safe to
 * commit/deploy as-is. Loaded only for Profile and one-shot History sync.
 */
window.FIREBASE_CONFIG = {
  apiKey: "AIzaSyDnAC7uD3oh-q8egu_cDLFrUcB09C9_n_g",
  authDomain: "udsp-9fedc.firebaseapp.com",
  projectId: "udsp-9fedc",
  storageBucket: "udsp-9fedc.firebasestorage.app",
  messagingSenderId: "254145717909",
  appId: "1:254145717909:web:e65de4a880dcdedec3dd9c",
  measurementId: "G-QYY10GJSSK"
};
