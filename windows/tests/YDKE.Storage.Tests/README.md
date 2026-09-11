# Crash-recoverable local import regressions

This package-free .NET 10 console harness links the production
[AppStorage](../../src/YDKE.Windows/AppStorage.cs),
[Models](../../src/YDKE.Windows/Models.cs), and
[LearningEngine](../../src/YDKE.Windows/LearningEngine.cs). The only stub is
[AppearancePalette.cs](AppearancePalette.cs), supplying model-default colors;
no persistence, validation, or learning logic is duplicated.

## Run offline

From the repository root, with the SDK selected by [global.json](../../../global.json)
already installed:

```powershell
& .\windows\tests\YDKE.Storage.Tests\Run-Tests.ps1
```

Pass `-DotNetPath` to [Run-Tests.ps1](Run-Tests.ps1) if the installed SDK executable
is elsewhere. The runner never installs software. It uses [NuGet.Config](NuGet.Config)
with **all package/audit sources cleared**, disables telemetry/first-run certificate
setup, and redirects CLI initialization, package caches, and temporary files into
this project's ignored build output. It does not build or launch the WinUI app.

All fixture directories are unique children of this harness's binary output and
are removed after their test. No test invokes the default `AppStorage` constructor,
the user's YDKE data folder, the OS/user-profile temporary directory, or a network API.
The runner restores its process environment when it finishes.

The console prints one `PASS`/`FAIL` per case and `RESULT: n/44 passed`. Exit code
0 means all assertions passed; 1 means an assertion failed; 2 means the 30-second
deadlock guard fired. The PowerShell runner turns any nonzero exit into a terminating
error. Two deny-delete sharing tests require Windows and explicitly report `SKIP`
on other platforms; the target environment for this issue is Windows.

## Transaction contract

1. `ImportAsync(path)` remains a **read-only preview**. It validates the entire
   versioned envelope and returns independent models. It never changes the input,
   local state, or an existing pending journal, including during corruption.
2. `ApplyImportAsync(settings, progress)` synchronously validates and snapshots
   **both** targets before its first await or filesystem operation. Legacy settings
   are normalized in the snapshot (`CloudConnected = false`; the selected level
   is authoritative), never by mutating caller-owned models. Invalid arguments or
   an oversized encoded envelope cause no writes, even when recovery is pending.
3. The canonical transaction record is named **import.pending.json** in
   `FolderPath`. Its content is the **complete `YDKE.Backup` v1 envelope**, with
   both validated target settings and progress embedded. It never references the
   possibly removable input file. The input can disappear after confirmation.
4. Publication of that journal is the **commit point**: write a same-directory
   unique temporary file using `WriteThrough`, `FlushAsync` and `Flush(true)`, then
   atomically rename it into place without overwriting an existing journal.
   No state primary, state backup, or quarantine replacement precedes publication.
5. With `IoGate` held, progress is replaced first, then settings, using the same
   unlocked valid-backup/quarantine writer as normal saves. Each target is flushed
   before its atomic rename. The journal is deleted **only after both complete**.
   Public save/load methods are never called recursively while holding `IoGate`.
6. Every state load and every normal write first checks/replays a pending journal
   under the gate. This includes settings, progress, legacy local cloud-profile
   saves, exports, and a subsequent apply request. Recovery revalidates **both**
   journal targets before inspecting or writing any state or backup.
7. Recovery always **rolls forward**. An already-matching target is skipped so
   retries do not rotate imported bytes over pre-import backups. Interrupted
   backup rotations deduplicate replay sources to retain the three recent valid
   generations. Corrupt sources are preserved byte-for-byte in quarantine before
   replacement; existing valid backups are not replaced with corrupt sources.
8. Recovery failure retains the journal and throws, rather than returning a
   primary, an older backup, or mixed/default state. The interrupted attempt may
   already have replaced one target; **no requested unrelated write runs**.
   There is no hidden automatic retry inside a failed apply. A subsequent load or
   restart retries recovery.
9. Invalid, truncated, oversized, unsupported, duplicate-property, or otherwise
   invalid journal content fails closed. Valid journal sidecars are **not** used
   as fallback: they do not establish the committed transaction. An unreadable
   journal or a directory at its path also blocks access. Metadata access errors
   are not treated as absence. An observed journal that disappears during the
   same process remains a blocker, not permission to serve mixed files.
10. The journal name and every dot-suffixed backup/temp/quarantine name are reserved
    export destinations, as are existing managed state paths. Windows case, trailing
    dot/space, and alternate-data-stream aliases are covered. Orphan journal
    temporary/backup files alone are uncommitted and are preserved, not promoted.

Without a pending transaction, existing legacy loads, three normal backup
generations, quarantine safeguards, and default-in-memory-only recovery are unchanged.
Normal single-file saves are **not** pair transactions; paired changes must use apply.

### Failure and UI handshake

| Result | Disk meaning | Caller action |
|---|---|---|
| Apply succeeds | Both targets flushed; journal removed | Adopt both targets together; preferably use `LoadStateAsync()` to get the persisted normalized pair. |
| Ordinary argument/I/O error before publication | This apply did not commit | Keep the previous pair; report the failure. |
| `PendingImportException` | Journal committed or cannot safely be inspected/replayed | **No old-memory rollback or stale-save retry.** Block all study/settings operations; reload both or restart after resolving the cause. |
| `ImportReloadRequiredException` (base type, not pending) | A pending import was recovered, or another import invalidated the queued snapshot; **the requested write did not execute** | Discard the stale mutation. Reload both or block until restart, even though the journal may already be gone. |

`PendingImportException : ImportReloadRequiredException : IOException` exposes
`JournalPath` and `IsInvalidJournal`; both types expose `FolderPath`. Catch the
reload-required base **before** existing `IOException`/`Exception` rollback handlers.

`LoadStateAsync()` returns `(UserSettings Settings, ProgressState Progress)` under
one gate and acknowledges the generation only after both loads succeed. The UI must
publish the tuple in one non-awaiting UI-thread step while controls/timers remain
blocked. Individual `LoadSettingsAsync`/`LoadProgressAsync` remain supported: each
first finishes the entire pending transaction. Both must be loaded in the same
generation before that instance may save again. Repeating just one does not count.

`HasPendingImport` is a conservative **advisory** hint, not a permission check.
`RequiresReload` detects invalidated in-memory generations. A successful recovery
can make the first false while the second stays true. A queued stale snapshot can
still throw even after a successful paired load or successful apply has cleared
both hints. **Handle the exception, not just the flags.** Read-only preview does not
acknowledge a reload. Retrying saves without a reload remains blocked.

For the exact main/UI call-site patches and a minimal permanent-until-restart guard,
see [UI-Handoff.md](UI-Handoff.md). Those UI files are deliberately outside this
task's edit ownership. Storage cannot revoke arbitrary mutable UI objects or prevent
an event handler from rendering stale memory: the UI guard is a required integration.

### Manual repair of an invalid journal

Close all YDKE processes. Preserve the original journal **and the complete storage
folder**, including backups/quarantine files, before attempting repair. Restore a
verified complete YDKE backup envelope to the journal path while retaining the
damaged original separately, or seek recovery help. Reopening YDKE then validates
and rolls forward that complete pair. **Do not simply delete the journal or restore
only one primary to bypass the error.** The surviving files may be mixed.

### Scope of the guarantee

- Crash/process-interruption recovery on a local filesystem supporting same-directory
  atomic rename and the requested flush semantics. This is not a promise about
  arbitrary hardware power-loss behavior or a filesystem/device ignoring flushes.
- One application process owns the storage folder. `IoGate` covers separate
  `AppStorage` instances **within** that process, not multiple processes, remote
  filesystems, or external editors racing the transaction.
- Per-folder in-process generations additionally reject old-instance/queued saves.
  They are not persistent version metadata and do not replace the UI handshake.
- Primary contents can be mixed **on disk** at the injected crash point. No storage
  load may *return* either model until the full committed pair has been restored.

## Coverage and fault injection

The internal constructor `AppStorage(string? folder, Action<string>? importCheckpoint)`
keeps the existing public constructor unchanged. The callback runs synchronously
under `IoGate` at these named boundaries, including during recovery:

| Checkpoint | Progress primary | Settings primary | Journal |
|---|---|---|---|
| `after-journal` | Old | Old | Durable full target pair |
| `after-progress` | Imported | Old | Present |
| `after-settings` | Imported | Imported | Present |
| `before-delete` | Imported | Imported | Present |

Tests throw at **all four boundaries** and restart with settings-first, progress-first,
and paired loads. They repeat interrupted recovery, assert backup contents and stable
file timestamps on replay, test real read/rename/delete locks, validate corrupt
journals with valid sidecars present, preserve quarantine bytes including BOMs,
exercise every stale write type, queue reads/writes under the gate, invalidate live
instances, test split-generation acknowledgement, and retain legacy behavior.

The callback must not synchronously wait for any storage API (that would wait for
its own gate). An instance carrying an always-throwing callback will also interrupt
every recovery: use a new non-faulting instance to simulate a real restart. Only
the repeated-interruption tests intentionally reuse the fault behavior.