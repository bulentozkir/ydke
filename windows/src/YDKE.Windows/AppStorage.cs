using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace YDKE_Windows;

/// <summary>The requested write was rejected because an import invalidated the caller's
/// in-memory pair. Never roll back or retry stale bytes: load BOTH models before resuming.</summary>
internal class ImportReloadRequiredException : IOException
{
    public string FolderPath { get; }

    internal ImportReloadRequiredException(string folder)
        : this(folder, $"An import changed local settings and progress in {folder}. The requested write was NOT performed. Stop studying and reload BOTH models with LoadStateAsync before resuming; do not restore or save the old in-memory data.", null) { }

    protected ImportReloadRequiredException(string folder, string message, Exception? innerException)
        : base(message, innerException) => FolderPath = folder;
}

/// <summary>A committed or unreadable journal blocks state access until roll-forward
/// succeeds. The journal is preserved, never silently replaced by defaults or backups.</summary>
internal sealed class PendingImportException : ImportReloadRequiredException
{
    public string JournalPath { get; }
    public bool IsInvalidJournal { get; }

    internal PendingImportException(string folder, string journalPath, bool isInvalidJournal, Exception innerException)
        : base(folder, isInvalidJournal
            ? $"The pending import journal at {journalPath} is invalid or unsupported. No state files were read or changed by this recovery attempt. Close YDKE and preserve the journal AND the entire data folder. Restore a verified complete YDKE backup to the journal path (keep the damaged original separately), or seek recovery help. Do not just delete the journal: settings and progress may be mixed. Details: {innerException.Message}"
            : $"The pending import at {journalPath} could not be completed or inspected. It must roll FORWARD, not back. Stop studying and do not save old in-memory data. Keep the journal; resolve file locks, permissions or disk-space problems, then reload BOTH models with LoadStateAsync or restart YDKE to retry. Details: {innerException.Message}",
            innerException)
    {
        JournalPath = journalPath;
        IsInvalidJournal = isInvalidJournal;
    }
}

/// <summary>Local-only persistence. In-process operations (including separate instances) share
/// one semaphore. Callers own the mutable models and must not edit them during the synchronous
/// snapshot at method entry. Imports use a durable roll-forward journal; individual normal
/// saves are NOT pair transactions. No cross-process lock: one application process must own
/// the folder. Callers serialize pair reloads and publish both models together while UI is blocked.</summary>
internal sealed class AppStorage
{
    public const int ExportVersion = 1;
    public const int MaxFileBytes = 32 * 1024 * 1024;
    internal const string ImportJournalFileName = "import.pending.json";
    private const string ExportFormat = "YDKE.Backup";
    private static readonly SemaphoreSlim IoGate = new(1, 1);
    // In-process invalidation only, not persistent concurrency/version metadata. Existing
    // instances and saves queued before a commit must not overwrite the imported generation.
    private static readonly ConcurrentDictionary<string, ImportGeneration> ImportGenerations = new(StringComparer.OrdinalIgnoreCase);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        AllowDuplicateProperties = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        MaxDepth = 32,
    };

    private readonly string _folder;
    private readonly Action<string>? _importCheckpoint;
    private readonly ImportGeneration _importGeneration;
    private long _acceptedGeneration;
    private long _settingsReadGeneration = -1;
    private long _progressReadGeneration = -1;

    public AppStorage(string? folder = null) : this(folder, null) { }

    // Test-only synchronous checkpoints under IoGate. Callbacks may throw to simulate
    // termination, but must NEVER call another storage operation (it would await this gate).
    internal AppStorage(string? folder, Action<string>? importCheckpoint)
    {
        _folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YDKE")));
        _importCheckpoint = importCheckpoint;
        _importGeneration = ImportGenerations.GetOrAdd(_folder, _ => new ImportGeneration());
        _acceptedGeneration = CurrentGeneration;
        if (Volatile.Read(ref _importGeneration.PendingObserved)) _acceptedGeneration--;
    }

    public string FolderPath => _folder;

    /// <summary>Conservative, read-only UI hint, not permission to save. An inaccessible
    /// journal path counts as pending. False does NOT clear RequiresReload.</summary>
    public bool HasPendingImport
    {
        get
        {
            try { return Volatile.Read(ref _importGeneration.PendingObserved) || JournalExists(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { return true; }
        }
    }

    /// <summary>Sticky after an interrupted/recovered import or another instance's commit.
    /// Clears only after this instance successfully loads BOTH models in the same generation,
    /// or successfully applies a new import. UI must also block actions throughout the reload.</summary>
    public bool RequiresReload => Volatile.Read(ref _acceptedGeneration) != CurrentGeneration;

    private string JournalPath => Path.Combine(_folder, ImportJournalFileName);
    private long CurrentGeneration => Volatile.Read(ref _importGeneration.Value);

    /// <summary>Recovery notice for the UI, retained until cleared. Contains local paths,
    /// not localized resource identifiers.</summary>
    public string? RecoveryMessage { get; private set; }

    public void ClearRecoveryMessage() => RecoveryMessage = null;

    public Task<UserSettings> LoadSettingsAsync() =>
        LoadAsync("settings.json", () => new UserSettings(), DecodeSettings);

    public Task<ProgressState> LoadProgressAsync() =>
        LoadAsync("progress.json", () => new ProgressState(), DecodeProgress);

    /// <summary>Loads both models under one gate, acknowledging this import generation
    /// only if BOTH loads succeed. Use for startup/recovery; publish both models together
    /// on the UI thread while all study, settings, timers and autosaves remain blocked.</summary>
    public async Task<(UserSettings Settings, ProgressState Progress)> LoadStateAsync()
    {
        await IoGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await RecoverPendingImportUnlockedAsync().ConfigureAwait(false);
            var settings = await LoadUnlockedAsync("settings.json", () => new UserSettings(), DecodeSettings).ConfigureAwait(false);
            var progress = await LoadUnlockedAsync("progress.json", () => new ProgressState(), DecodeProgress).ConfigureAwait(false);
            AcceptGenerationUnlocked();
            return (settings, progress);
        }
        finally { IoGate.Release(); }
    }

    public Task SaveSettingsAsync(UserSettings settings)
    {
        var generation = Volatile.Read(ref _acceptedGeneration);
        LearningEngine.ValidateSettings(settings);
        // Clone before clearing the legacy flag; never mutate the caller's settings.
        var snapshot = DecodeSettings(Encode(settings));
        return SaveAsync(Path.Combine(_folder, "settings.json"), Encode(snapshot), DecodeSettings, generation);
    }

    public Task SaveProgressAsync(ProgressState progress)
    {
        var generation = Volatile.Read(ref _acceptedGeneration);
        LearningEngine.ValidateProgress(progress);
        var bytes = Encode(progress);
        _ = DecodeProgress(bytes);
        return SaveAsync(Path.Combine(_folder, "progress.json"), bytes, DecodeProgress, generation);
    }

    /// <summary>Legacy compatibility only: this was always a LOCAL file, never cloud sync.</summary>
    [Obsolete("No cloud sync exists. Use SaveProgressAsync or explicit ExportAsync instead.")]
    public Task SaveCloudProfileAsync(ProgressState progress)
    {
        var generation = Volatile.Read(ref _acceptedGeneration);
        LearningEngine.ValidateProgress(progress);
        var bytes = Encode(progress);
        _ = DecodeProgress(bytes);
        return SaveAsync(Path.Combine(_folder, "cloudprofile.json"), bytes, DecodeProgress, generation);
    }

    /// <summary>Writes a versioned local backup containing both settings and progress.
    /// Existing exports must validate before replacement. Managed storage paths are forbidden.</summary>
    public Task ExportAsync(string path, UserSettings settings, ProgressState progress)
    {
        var generation = Volatile.Read(ref _acceptedGeneration);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        if (IsManagedPath(fullPath)) throw new ArgumentException("Choose a separate export file.", nameof(path));
        var bytes = EncodeExport(settings, progress);
        return SaveAsync(fullPath, bytes, DecodeExport, generation, rejectInvalidDestination: true);
    }

    private static byte[] EncodeExport(UserSettings settings, ProgressState progress)
    {
        LearningEngine.ValidateSettings(settings);
        LearningEngine.ValidateProgress(progress);
        var document = new ExportDocument
        {
            Format = ExportFormat,
            Version = ExportVersion,
            Settings = DecodeSettings(Encode(settings)),
            Progress = DecodeProgress(Encode(progress)),
        };
        var bytes = Encode(document);
        _ = DecodeExport(bytes);
        return bytes;
    }

    /// <summary>Same validated envelope as ExportAsync, with no file I/O. For a non-file
    /// transport (e.g. cloud sync) that owns its own transport security/authentication.</summary>
    public static byte[] EncodeBackup(UserSettings settings, ProgressState progress) => EncodeExport(settings, progress);

    /// <summary>Same validation as ImportAsync, with no file I/O; never touches local storage.
    /// Throws InvalidDataException for malformed/untrusted bytes from any external source.</summary>
    public static (UserSettings Settings, ProgressState Progress) DecodeBackup(byte[] bytes)
    {
        var document = DecodeExport(bytes);
        return (document.Settings, document.Progress);
    }

    /// <summary>Reads and validates only; NEVER changes local storage or the input file.
    /// Preview/confirm the returned independent models, then call ApplyImportAsync.
    /// Deliberately does not recover a pending journal: preview is always read-only.
    /// InvalidDataException means invalid/unsupported JSON; filesystem errors propagate.</summary>
    public async Task<(UserSettings Settings, ProgressState Progress)> ImportAsync(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        await IoGate.WaitAsync().ConfigureAwait(false);
        try
        {
            var document = DecodeExport(await ReadBoundedAsync(Path.GetFullPath(path)).ConfigureAwait(false));
            return (document.Settings, document.Progress);
        }
        finally { IoGate.Release(); }
    }

    /// <summary>Validates/snapshots BOTH targets before any I/O. The atomic, WriteThrough,
    /// flushed journal is the commit point: after it exists, the only outcome is roll-forward.
    /// Success means both files are durable and the journal is removed. Post-commit failure
    /// throws PendingImportException and retains the journal; no automatic rollback/retry.
    /// Load either model to recover; publish the pair only after BOTH loads succeed.
    /// Finding an older pending import recovers it then rejects this request as stale.</summary>
    public Task ApplyImportAsync(UserSettings settings, ProgressState progress)
    {
        var generation = Volatile.Read(ref _acceptedGeneration);
        var bytes = EncodeExport(settings, progress);
        var document = DecodeExport(bytes);
        // Bound both individual encodings BEFORE publishing the commit record, too.
        var settingsBytes = Encode(document.Settings);
        var progressBytes = Encode(document.Progress);
        return ApplyImportCoreAsync(bytes, settingsBytes, progressBytes, generation);
    }

    private async Task ApplyImportCoreAsync(byte[] journal, byte[] settings, byte[] progress, long generation)
    {
        await IoGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await BeforeWriteUnlockedAsync(generation).ConfigureAwait(false);
            Directory.CreateDirectory(_folder);
            var committed = false;
            try
            {
                // Never rotate/replace an existing journal or adopt its sidecars. No state
                // replacement (including backups/quarantine) may precede this commit point.
                await AtomicWriteAsync(JournalPath, journal, overwrite: false).ConfigureAwait(false);
                committed = true;
                ObservePendingImportUnlocked();
                await CompleteImportUnlockedAsync(settings, progress).ConfigureAwait(false);
            }
            catch (Exception ex) when (committed || HasPendingImport)
            {
                ObservePendingImportUnlocked();
                throw new PendingImportException(_folder, JournalPath, isInvalidJournal: false, ex);
            }
            // The caller adopts the supplied pair on success. Other instances and
            // snapshots queued before the commit remain invalid until a full reload.
            AcceptGenerationUnlocked();
        }
        finally { IoGate.Release(); }
    }

    private bool JournalExists()
    {
        // File.Exists silently treats access errors and directories as "absent", which
        // would allow mixed defaults/stale writes. Only an actually missing path is safe.
        try { _ = File.GetAttributes(JournalPath); return true; }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
    }

    // All *Unlocked helpers require IoGate. Never call a public load/save from recovery.
    private async Task<bool> RecoverPendingImportUnlockedAsync()
    {
        var validatingJournal = true;
        try
        {
            if (!JournalExists())
            {
                if (_importGeneration.PendingObserved)
                    throw new InvalidDataException("A previously observed import journal is missing. Restore the full journal before continuing.");
                return false;
            }
            ObservePendingImportUnlocked();
            // Validate the entire original envelope and encode BOTH targets before
            // inspecting/writing either state file or any backup/quarantine file.
            var document = DecodeExport(await ReadBoundedAsync(JournalPath).ConfigureAwait(false));
            var settings = Encode(document.Settings);
            var progress = Encode(document.Progress);
            validatingJournal = false;
            await CompleteImportUnlockedAsync(settings, progress).ConfigureAwait(false);
            Notice($"Completed interrupted import from {JournalPath}. Reload both settings and progress before continuing.");
            return true;
        }
        catch (Exception ex)
        {
            ObservePendingImportUnlocked();
            throw new PendingImportException(_folder, JournalPath, validatingJournal && ex is InvalidDataException, ex);
        }
    }

    private async Task CompleteImportUnlockedAsync(byte[] settings, byte[] progress)
    {
        _importCheckpoint?.Invoke("after-journal");
        await SaveUnlockedAsync(Path.Combine(_folder, "progress.json"), progress, DecodeProgress, skipIdentical: true).ConfigureAwait(false);
        _importCheckpoint?.Invoke("after-progress");
        await SaveUnlockedAsync(Path.Combine(_folder, "settings.json"), settings, DecodeSettings, skipIdentical: true).ConfigureAwait(false);
        _importCheckpoint?.Invoke("after-settings");
        _importCheckpoint?.Invoke("before-delete");
        // Both replacements have completed their flush-to-disk and atomic rename. If
        // deletion fails, the still-present journal makes the next attempt idempotent.
        File.Delete(JournalPath);
        Volatile.Write(ref _importGeneration.PendingObserved, false);
    }

    private void ObservePendingImportUnlocked()
    {
        if (_importGeneration.PendingObserved) return;
        Volatile.Write(ref _importGeneration.PendingObserved, true);
        Interlocked.Increment(ref _importGeneration.Value);
    }

    private void AcceptGenerationUnlocked()
    {
        _settingsReadGeneration = _progressReadGeneration = CurrentGeneration;
        Volatile.Write(ref _acceptedGeneration, CurrentGeneration);
    }

    private async Task BeforeWriteUnlockedAsync(long generation)
    {
        var recovered = await RecoverPendingImportUnlockedAsync().ConfigureAwait(false);
        if (!recovered && !RequiresReload && generation == CurrentGeneration) return;
        // Even successful recovery must reject the bytes captured before it. The base
        // exception still requires a reload when the journal has ALREADY been removed.
        throw new ImportReloadRequiredException(_folder);
    }

    private bool IsManagedPath(string path)
    {
        if (!string.Equals(Path.GetDirectoryName(path), Path.TrimEndingDirectorySeparator(_folder), StringComparison.OrdinalIgnoreCase))
            return false;
        var name = Path.GetFileName(path).TrimEnd(' ', '.');
        return new[] { "settings.json", "progress.json", "cloudprofile.json", ImportJournalFileName }.Any(reserved =>
            name.Equals(reserved, StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith(reserved + ":", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith(reserved + ".", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<T> LoadAsync<T>(string fileName, Func<T> fallback, Func<byte[], T> decode)
    {
        await IoGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await RecoverPendingImportUnlockedAsync().ConfigureAwait(false);
            var result = await LoadUnlockedAsync(fileName, fallback, decode).ConfigureAwait(false);
            if (fileName == "settings.json") _settingsReadGeneration = CurrentGeneration;
            else _progressReadGeneration = CurrentGeneration;
            if (_settingsReadGeneration == CurrentGeneration && _progressReadGeneration == CurrentGeneration)
                AcceptGenerationUnlocked();
            return result;
        }
        finally { IoGate.Release(); }
    }

    private async Task<T> LoadUnlockedAsync<T>(string fileName, Func<T> fallback, Func<byte[], T> decode)
    {
        var path = Path.Combine(_folder, fileName);
        var primaryExists = File.Exists(path);
        if (primaryExists)
        {
            try { return decode(await ReadBoundedAsync(path).ConfigureAwait(false)); }
            catch (InvalidDataException ex) { Notice($"Cannot use {path}: {ex.Message} Original file has not been changed."); }
        }
        // Also recover a missing primary, for example after an interrupted external move.
        for (var index = 1; index <= 3; index++)
        {
            var backup = BackupPath(path, index);
            if (!File.Exists(backup)) continue;
            try
            {
                var recovered = decode(await ReadBoundedAsync(backup).ConfigureAwait(false));
                Notice($"Recovered {fileName} from {backup}. The primary is unchanged until the next explicit save.");
                return recovered;
            }
            catch (InvalidDataException ex) { Notice($"Skipped invalid backup {backup}: {ex.Message}"); }
        }
        if (primaryExists || Enumerable.Range(1, 3).Any(index => File.Exists(BackupPath(path, index))))
            Notice($"No valid backup for {fileName}. Using defaults in memory only; existing files are preserved.");
        return fallback();
    }

    private async Task SaveAsync<T>(string path, byte[] bytes, Func<byte[], T> decode, long generation, bool rejectInvalidDestination = false)
    {
        await IoGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await BeforeWriteUnlockedAsync(generation).ConfigureAwait(false);
            await SaveUnlockedAsync(path, bytes, decode, rejectInvalidDestination).ConfigureAwait(false);
        }
        finally { IoGate.Release(); }
    }

    private async Task SaveUnlockedAsync<T>(string path, byte[] bytes, Func<byte[], T> decode,
        bool rejectInvalidDestination = false, bool skipIdentical = false)
    {
        // Inspect before writing. Invalid sources never enter the backup chain.
        byte[]? previous = null;
        var corrupt = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (File.Exists(path))
        {
            try
            {
                previous = await ReadBoundedAsync(path).ConfigureAwait(false);
                _ = decode(previous);
                // Replayed imports must not rotate an already-imported primary back
                // into the backup chain and evict the pre-import valid generations.
                if (skipIdentical && previous.AsSpan().SequenceEqual(bytes)) return;
            }
            catch (InvalidDataException)
            {
                if (rejectInvalidDestination) throw;
                previous = null;
                corrupt.Add(path);
            }
        }
        var backups = new List<byte[]>();
        if (previous is not null)
        {
            backups.Add(previous);
            for (var index = 1; index <= 3; index++)
            {
                var backupPath = BackupPath(path, index);
                if (!File.Exists(backupPath)) continue;
                try
                {
                    var backup = await ReadBoundedAsync(backupPath).ConfigureAwait(false);
                    _ = decode(backup);
                    // Rotation can be interrupted before the primary rename. Ignore
                    // duplicate replay sources so retries retain pre-import history.
                    if (backups.Count < 3 && (!skipIdentical || !backups.Any(saved => saved.AsSpan().SequenceEqual(backup))))
                        backups.Add(backup);
                }
                catch (InvalidDataException) { corrupt.Add(backupPath); }
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // Preserve byte-for-byte evidence BEFORE replacing a malformed destination.
        // Failure to preserve aborts the save; quarantine files are never auto-deleted.
        foreach (var source in corrupt)
        {
            var preserved = source + ".corrupt-" + Guid.NewGuid().ToString("N");
            File.Copy(source, preserved, overwrite: false);
            Notice($"Preserved invalid file at {preserved} before saving. No corrupt source was discarded.");
        }
        for (var index = backups.Count; index >= 1; index--)
            await AtomicWriteAsync(BackupPath(path, index), backups[index - 1]).ConfigureAwait(false);
        await AtomicWriteAsync(path, bytes).ConfigureAwait(false);
    }

    private static string BackupPath(string path, int index) => path + $".bak{index}";

    private void Notice(string message)
    {
        if (RecoveryMessage?.Contains(message, StringComparison.Ordinal) == true) return;
        RecoveryMessage = RecoveryMessage is null ? message : RecoveryMessage + Environment.NewLine + message;
    }

    private static async Task AtomicWriteAsync(string path, byte[] bytes, bool overwrite = true)
    {
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                64 * 1024, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(bytes).ConfigureAwait(false);
                await stream.FlushAsync().ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }
            // Same-directory atomic rename, without a delete-before-move data-loss window.
            File.Move(temporary, path, overwrite);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static async Task<byte[]> ReadBoundedAsync(string path)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (stream.Length > MaxFileBytes) throw new InvalidDataException("File exceeds the 32 MiB limit.");
        using var buffer = new MemoryStream();
        var chunk = new byte[64 * 1024];
        int count;
        while ((count = await stream.ReadAsync(chunk).ConfigureAwait(false)) != 0)
        {
            if (buffer.Length + count > MaxFileBytes) throw new InvalidDataException("File exceeds the 32 MiB limit.");
            buffer.Write(chunk, 0, count);
        }
        var bytes = buffer.ToArray();
        // Legacy UTF-8 files written by other local tools may contain a BOM.
        return bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }) ? bytes[3..] : bytes;
    }

    private static byte[] Encode<T>(T value)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
        if (bytes.Length > MaxFileBytes) throw new InvalidDataException("File exceeds the 32 MiB limit.");
        return bytes;
    }

    private static T Decode<T>(byte[] bytes, Action<T> validate, params string[] requiredProperties)
    {
        try
        {
            using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 32, AllowDuplicateProperties = false });
            RequireProperties(document.RootElement, requiredProperties);
            var value = JsonSerializer.Deserialize<T>(bytes, JsonOptions);
            if (value is null) throw new InvalidDataException("JSON root cannot be null.");
            validate(value);
            return value;
        }
        catch (JsonException ex) { throw new InvalidDataException("Malformed or unsupported JSON: " + ex.Message, ex); }
    }

    private static void RequireProperties(JsonElement root, params string[] properties)
    {
        if (root.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Expected a JSON object.");
        foreach (var name in properties)
            if (!root.TryGetProperty(name, out _)) throw new InvalidDataException($"Missing required property: {name}.");
    }

    private static UserSettings DecodeSettings(byte[] bytes)
    {
        var settings = Decode<UserSettings>(bytes, LearningEngine.ValidateSettings,
            "UiLanguage", "StudyLanguage", "Level");
        settings.CloudConnected = false;
        return settings;
    }

    private static ProgressState DecodeProgress(byte[] bytes) => Decode<ProgressState>(bytes,
        LearningEngine.ValidateProgress, "KnownWords", "FavoriteWords", "GameBestScores",
        "CorrectAnswers", "WrongAnswers", "StudyStreak", "LastStudyDate");

    private static ExportDocument DecodeExport(byte[] bytes)
    {
        var document = Decode<ExportDocument>(bytes, value =>
        {
            if (value.Format != ExportFormat || value.Version != ExportVersion)
                throw new InvalidDataException("Unsupported YDKE backup format/version.");
            if (value.Settings is null || value.Progress is null)
                throw new InvalidDataException("Backup must contain settings and progress.");
        }, "Format", "Version", "Settings", "Progress");
        // Validate ORIGINAL nested JSON, not reserialized defaults: a truncated {} must
        // not masquerade as a valid empty backup and erase real progress.
        using var json = JsonDocument.Parse(bytes);
        document.Settings = DecodeSettings(System.Text.Encoding.UTF8.GetBytes(json.RootElement.GetProperty("Settings").GetRawText()));
        document.Progress = DecodeProgress(System.Text.Encoding.UTF8.GetBytes(json.RootElement.GetProperty("Progress").GetRawText()));
        return document;
    }

    private sealed class ExportDocument
    {
        public string Format { get; set; } = "";
        public int Version { get; set; }
        public UserSettings Settings { get; set; } = new();
        public ProgressState Progress { get; set; } = new();
    }

    private sealed class ImportGeneration
    {
        public long Value;
        public bool PendingObserved;
    }
}