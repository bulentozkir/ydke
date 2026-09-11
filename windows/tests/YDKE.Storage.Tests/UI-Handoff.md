# Required main/UI integration (not applied by the storage owner)

The storage owner changed only [AppStorage](../../src/YDKE.Windows/AppStorage.cs)
and this new test directory. The expected `FolderPath` and `ApplyImportAsync`
signatures are provided, but **report-and-continue after apply failure is not safe**.
These are the required integration changes for the UI owner; the WinUI app itself
was not built or launched by this storage-only harness.

## 1. Add a fatal storage latch

Minimal safe policy: after a reload-required error, stop the interaction and leave
the page blocked until restart. This avoids attempting a partially handled in-process
reload while old callbacks/undo buffers are live. Restart already invokes storage
roll-forward. An explicit retry UI can be added later, provided it loads/adopts both
models and invalidates all old UI/session state before clearing the latch.

In [MainPage.Study.cs](../../src/YDKE.Windows/MainPage.Study.cs), add:

```csharp
private bool _storageBlocked;

private void BlockStorageUntilRestart(Exception error)
{
    _storageBlocked = true;
    StopGameTimer();
    StopSpeechPlayback();
    _wordLoadCancellation?.Cancel();
    _activeGame = null;
    _cardUndo = null;
    _cardUndoContext = null;
    _revealedCardKey = null;
    _pendingFocus = null;
    _quizChoiceButtons.Clear();
    _quizContinue = null;
    SetStudyBusy(true);
    Notice.IsClosable = false;
    ShowNotice(T("Common.Error"), error.Message, InfoBarSeverity.Error);
}
```

Replace the existing `SetStudyBusy` body, not its callers:

```csharp
private void SetStudyBusy(bool busy)
{
    busy |= _storageBlocked;
    _studyBusy = busy;
    ContentScroll.IsEnabled = !busy; // ScrollViewer is a Control; PageContent is a StackPanel.
    PageContent.IsHitTestVisible = !busy;
    QuickUiLanguage.IsEnabled = !busy;
    QuickStudyLanguage.IsEnabled = !busy;
    QuickLevel.IsEnabled = !busy;
    Navigation.IsPaneToggleButtonVisible = !busy;
}
```

Existing action/navigation/key guards already check `_studyBusy`; keep those guards
on every study/settings/game entry point, including delayed continuations and any
new autosave or unload handler. Do not implement an exit-time stale save when this
latch is set. Clear/abandon the active game so queued game continuations cannot resume.
**Every existing `finally { SetStudyBusy(false); }` must leave the latch effective.**

## 2. Load/adopt the pair at startup

In `OnLoaded` in [MainPage.xaml.cs](../../src/YDKE.Windows/MainPage.xaml.cs), make
the initial guard `if (_initialized || _storageBlocked) return;`. Replace the two
independent field assignments and their catch with:

```csharp
SetStudyBusy(true);
try
{
    var loaded = await _storage.LoadStateAsync();
    (_settings, _progress) = loaded;
}
catch (Exception ex)
{
    BlockStorageUntilRestart(ex);
    return; // No default/mixed study UI, no game command-line startup.
}
finally { SetStudyBusy(false); }
```

Leave the normal appearance/navigation/word-loading setup after this block. No field
is assigned until the paired load succeeds. A corrupt journal must leave a permanent
actionable error rather than a default in-memory study session.

## 3. Preserve typed exceptions at the import call site

In `ImportBackupAsync` in
[MainPage.Library.cs](../../src/YDKE.Windows/MainPage.Library.cs), add a local
`var importCommitted = false;` before its outer `try`. Keep read-only preview,
confirmation, and the separate pre-import safety export. Change the inner apply block
to preserve the exception type instead of wrapping it in an ordinary `IOException`:

```csharp
try
{
    await _storage.ApplyImportAsync(imported.Settings, imported.Progress);
    importCommitted = true;
}
catch (ImportReloadRequiredException) { throw; }
catch (Exception failure)
{
    // Only a pre-commit failure reaches this wrapper.
    throw new IOException($"{failure.Message}\n{safety}", failure);
}

var loaded = await _storage.LoadStateAsync();
(_settings, _progress) = loaded;
```

Replace the existing `_settings = imported.Settings; _progress = imported.Progress;`
with that paired load/assignment. Keep the existing successful-import reset of card,
focus, library/query/expanded-word/session state, reload vocabulary, apply appearance
and navigation language, and re-render. Also clear `_cardUndoContext` when clearing
`_cardUndo`. Only show success after the pair has been adopted and the UI refreshed.

Replace the outer report-only catch with:

```csharp
catch (ImportReloadRequiredException ex)
{
    BlockStorageUntilRestart(ex);
}
catch (Exception ex)
{
    if (importCommitted) BlockStorageUntilRestart(ex);
    else ShowNotice(T("Common.Error"), ex.Message, InfoBarSeverity.Error);
}
finally { SetStudyBusy(false); }
```

The `importCommitted` check is important: a successful apply can remove the journal,
then a paired reload or UI refresh can fail with an *ordinary* exception. That must
not enable study using the old pair either. A failed apply after journal publication
always throws `PendingImportException`, which is caught by the base-type handler.

## 4. Never restore stale snapshots in save catch handlers

In `MutateStudyAsync` in [MainPage.Study.cs](../../src/YDKE.Windows/MainPage.Study.cs),
insert this catch **before** the existing catch that deserializes `before`:

```csharp
catch (ImportReloadRequiredException ex)
{
    BlockStorageUntilRestart(ex);
    return false;
}
```

In `SaveUiSettingsAsync` in
[MainPage.Library.cs](../../src/YDKE.Windows/MainPage.Library.cs), insert before
the existing rollback catch:

```csharp
catch (ImportReloadRequiredException ex)
{
    BlockStorageUntilRestart(ex);
}
```

In `ChangeQuickSettingAsync` in
[MainPage.Study.cs](../../src/YDKE.Windows/MainPage.Study.cs), replace the inner
unconditional rollback catch with these two catches:

```csharp
catch (ImportReloadRequiredException) { throw; }
catch
{
    _settings = JsonSerializer.Deserialize<UserSettings>(previous)!;
    throw;
}
```

Add an outer `catch (ImportReloadRequiredException ex) { BlockStorageUntilRestart(ex); }`
before its existing report/sync catch. Add the same typed catch before the report-only
catch in `ExportBackupAsync`: export can be the first write to discover a pending
transaction, even if it did not change a local primary itself.

Audit remaining game/legacy `SaveProgressAsync` call sites and any future storage
wrapper for the same policy: a typed error must abort the action and latch the UI,
not be swallowed as success or wrapped into a generic rollback path. The common
study mutation path above handles games already using `MutateStudyAsync`.

## 5. Optional recovery without restarting

If implementing a retry button instead of restart-only handling:

1. Leave `_storageBlocked` set; stop timers/speech and abandon all active game actions.
2. Await `_storage.LoadStateAsync()` into a **local tuple**. If it throws, keep the
   old fields unpublished, leave the latch set, and show the new actionable error.
3. Assign both fields in one UI-thread statement with no intervening await.
4. Clear all undo, reveal, focus, quiz/game callback, and library context state; reload
   the word context, appearance, navigation language, and rendered content.
5. Only after that entire operation succeeds, clear `_storageBlocked`, restore the
   notice's close behavior, and call `SetStudyBusy(false)`. Do not resume/replay the
   original failed mutation. Do not retry its captured save bytes.

`HasPendingImport == false` is **not** permission to skip this flow. A stale-save
rejection commonly happens after successful recovery has already deleted the journal.
`RequiresReload` is advisory as well; always honor the caught exception.

## UI acceptance checks for the owning agent

- Failure after progress replacement: report a restart/recovery-required error;
  no rating, answer, preference change, keyboard shortcut, timer, or exit autosave
  is allowed to persist old in-memory state afterward.
- Failure before journal deletion: same policy even though both disk primaries are
  already imported. Restart loads the complete imported pair, not the previous pair.
- Corrupt journal at startup: visible non-dismissable error, no default study UI;
  journal/backups remain untouched.
- First post-crash operation is a save or export: recovery completes, the original
  write throws `ImportReloadRequiredException`, and the UI does not restore its
  `before` snapshot or silently retry it.
- Successful import: normalized settings and progress are adopted together; all old
  context/undo callbacks are invalidated before study is re-enabled.