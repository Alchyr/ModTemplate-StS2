using Godot;

namespace ModTemplate.ModTemplateCode.Snapshots;

public static class SnapshotManager
{
    public static int    SnapshotCount       { get; private set; }
    public static bool   IsRestoring         { get; set; }
    // Set by NRunReadyPatch so Restore can trigger a scene transition
    // without SnapshotManager needing to know about NGame directly.
    public static Action? LaunchMainMenuAction { get; set; }

    private static string? _gameSaveDir;

    private static string SnapshotRoot => Path.Combine(OS.GetUserDataDir(), "mod_snapshots");
    private static string ActiveDir    => Path.Combine(SnapshotRoot, "active");

    // ── Run lifecycle ─────────────────────────────────────────────────────────

    public static void OnRunStart()
    {
        if (Directory.Exists(ActiveDir))
            Directory.Delete(ActiveDir, recursive: true);
        Directory.CreateDirectory(ActiveDir);
        _gameSaveDir  = FindGameSaveDir();
        SnapshotCount = 0;
        IsRestoring   = false;
        MainFile.Logger.Info($"[Snapshot] New run started | saves: {_gameSaveDir ?? "NOT FOUND"}");
    }

    public static void OnRunContinue()
    {
        _gameSaveDir  = FindGameSaveDir();
        SnapshotCount = Directory.Exists(ActiveDir)
            ? Directory.GetDirectories(ActiveDir).Count(d => Path.GetFileName(d).StartsWith("floor_"))
            : 0;
        IsRestoring   = false;
        MainFile.Logger.Info($"[Snapshot] Continued run | {SnapshotCount} snapshot(s) | saves: {_gameSaveDir ?? "NOT FOUND"}");
    }

    public static void OnRunEnd()
    {
        if (IsRestoring)
        {
            // Player triggered a restore — keep snapshots so they survive the
            // trip through the main menu back into the restored run.
            MainFile.Logger.Info("[Snapshot] RunEnd skipped deletion (restore in progress).");
            return;
        }
        if (Directory.Exists(ActiveDir))
        {
            Directory.Delete(ActiveDir, recursive: true);
            MainFile.Logger.Info("[Snapshot] Cleared mod_snapshots.");
        }
        _gameSaveDir  = null;
        SnapshotCount = 0;
    }

    // ── Save ──────────────────────────────────────────────────────────────────

    public static void Save(int floor)
    {
        if (_gameSaveDir is null) _gameSaveDir = FindGameSaveDir();
        if (_gameSaveDir is null)
        {
            MainFile.Logger.Info("[Snapshot] Save skipped: game save dir not found.");
            return;
        }

        var snapshotDir = Path.Combine(ActiveDir, $"floor_{floor:D2}");
        bool isNew = !Directory.Exists(snapshotDir);
        Directory.CreateDirectory(snapshotDir);

        CopyIfExists(Path.Combine(_gameSaveDir, "current_run.save"), snapshotDir);

        if (isNew) SnapshotCount++;
        MainFile.Logger.Info($"[Snapshot] {(isNew ? "Saved" : "Overwrote")} floor {floor}.");
    }

    // ── Load ──────────────────────────────────────────────────────────────────

    public static List<RunSnapshot> LoadAll()
    {
        if (!Directory.Exists(ActiveDir)) return [];
        return [.. Directory.GetDirectories(ActiveDir)
            .Select(d =>
            {
                var name = Path.GetFileName(d);
                if (!name.StartsWith("floor_") || !int.TryParse(name[6..], out var f)) return null;
                return new RunSnapshot { Floor = f, SavedAt = Directory.GetCreationTimeUtc(d), Dir = d };
            })
            .OfType<RunSnapshot>()
            .OrderByDescending(s => s.Floor)];
    }

    // ── Restore ───────────────────────────────────────────────────────────────

    public static void Restore(RunSnapshot snapshot)
    {
        if (_gameSaveDir is null) _gameSaveDir = FindGameSaveDir();
        if (_gameSaveDir is null)
        {
            MainFile.Logger.Info("[Snapshot] Restore failed: game save dir not found.");
            return;
        }
        if (!Directory.Exists(snapshot.Dir))
        {
            MainFile.Logger.Info($"[Snapshot] Restore failed: snapshot dir missing ({snapshot.Dir}).");
            return;
        }

        // Only restore the run save — progress.save holds account-wide stats/unlocks
        // that must not be rolled back when rewinding a floor.
        CopyIfExists(Path.Combine(snapshot.Dir, "current_run.save"), _gameSaveDir);

        IsRestoring = true;
        MainFile.Logger.Info($"[Snapshot] Restored floor {snapshot.Floor} — launching main menu.");
        LaunchMainMenuAction?.Invoke();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string? FindGameSaveDir()
    {
        try
        {
            var files = Directory.GetFiles(OS.GetUserDataDir(), "current_run.save",
                                           SearchOption.AllDirectories);
            var found = files.FirstOrDefault();
            return found is not null ? Path.GetDirectoryName(found) : null;
        }
        catch (Exception ex)
        {
            MainFile.Logger.Info($"[Snapshot] FindGameSaveDir error: {ex.Message}");
            return null;
        }
    }

    private static void CopyIfExists(string src, string destDir)
    {
        if (!File.Exists(src)) return;
        File.Copy(src, Path.Combine(destDir, Path.GetFileName(src)), overwrite: true);
    }
}
