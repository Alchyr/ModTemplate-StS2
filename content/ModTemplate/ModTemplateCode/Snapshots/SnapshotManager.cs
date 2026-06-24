using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace ModTemplate.ModTemplateCode.Snapshots;

public static class SnapshotManager
{
    public static int SnapshotCount => _snapshots.Count;

    // Floor → (complete run state at that floor, time it was captured)
    private static readonly Dictionary<int, (SerializableRun RunSave, DateTime SavedAt)> _snapshots = new();

    private static string SnapshotRoot => Path.Combine(OS.GetUserDataDir(), "mod_snapshots");
    private static string ActiveDir    => Path.Combine(SnapshotRoot, "active");

    // ── Run lifecycle ─────────────────────────────────────────────────────────

    public static void OnRunStart()
    {
        _snapshots.Clear();
        if (Directory.Exists(ActiveDir))
            Directory.Delete(ActiveDir, recursive: true);
        Directory.CreateDirectory(ActiveDir);
        MainFile.Logger.Info("[Snapshot] New run started.");
    }

    public static void OnRunContinue()
    {
        _snapshots.Clear();
        if (Directory.Exists(ActiveDir))
        {
            foreach (var dir in Directory.GetDirectories(ActiveDir))
            {
                var name = Path.GetFileName(dir);
                if (!name.StartsWith("floor_") || !int.TryParse(name[6..], out var floor)) continue;

                var snapshotFile = Path.Combine(dir, "snapshot.json");
                var metaFile     = Path.Combine(dir, "meta.json");
                if (!File.Exists(snapshotFile)) continue;

                try
                {
                    var result = JsonSerializationUtility.FromJson<SerializableRun>(
                        File.ReadAllText(snapshotFile));
                    if (!result.Success || result.SaveData == null) continue;
                    var runSave = result.SaveData;

                    var savedAt = File.Exists(metaFile)
                        ? DateTime.Parse(File.ReadAllText(metaFile), null, System.Globalization.DateTimeStyles.RoundtripKind)
                        : Directory.GetCreationTimeUtc(dir);

                    _snapshots[floor] = (runSave, savedAt);
                    MainFile.Logger.Info($"[Snapshot] Restored floor {floor} from disk.");
                }
                catch (Exception ex)
                {
                    MainFile.Logger.Info($"[Snapshot] Could not restore floor {floor}: {ex.Message}");
                }
            }
        }
        MainFile.Logger.Info($"[Snapshot] Continued run | {_snapshots.Count} snapshot(s).");
    }

    public static void OnRunEnd()
    {
        _snapshots.Clear();
        if (Directory.Exists(ActiveDir))
        {
            Directory.Delete(ActiveDir, recursive: true);
            MainFile.Logger.Info("[Snapshot] Run ended, snapshots cleared.");
        }
    }

    // ── Save ──────────────────────────────────────────────────────────────────

    public static void Save(int floor)
    {
        _ = SaveAsync(floor);
    }

    private static async Task SaveAsync(int floor)
    {
        try
        {
            var saveManager = SaveManager.Instance;

            // Wait for any in-flight save so we read the final committed state.
            var pending = saveManager.CurrentRunSaveTask;
            if (pending != null) await pending;

            var readResult = saveManager.LoadRunSave();
            if (!readResult.Success || readResult.SaveData == null)
            {
                MainFile.Logger.Info($"[Snapshot] Save floor {floor}: LoadRunSave failed (status={readResult.Status}).");
                return;
            }

            var runSave = readResult.SaveData;
            bool isNew  = !_snapshots.ContainsKey(floor);
            var savedAt = DateTime.UtcNow;
            _snapshots[floor] = (runSave, savedAt);

            // Persist to disk so snapshots survive game restarts.
            var dir = Path.Combine(ActiveDir, $"floor_{floor:D2}");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "snapshot.json"),
                JsonSerializationUtility.ToJson(runSave));
            File.WriteAllText(Path.Combine(dir, "meta.json"),
                savedAt.ToString("O")); // ISO 8601 round-trip format

            MainFile.Logger.Info($"[Snapshot] {(isNew ? "Saved" : "Overwrote")} floor {floor} ({_snapshots.Count} total).");
        }
        catch (Exception ex)
        {
            MainFile.Logger.Info($"[Snapshot] SaveAsync error: {ex}");
        }
    }

    // ── Enumerate ─────────────────────────────────────────────────────────────

    public static List<RunSnapshot> LoadAll() =>
        [.. _snapshots
            .Select(kvp => new RunSnapshot { Floor = kvp.Key, SavedAt = kvp.Value.SavedAt })
            .OrderByDescending(s => s.Floor)];

    // ── Load ──────────────────────────────────────────────────────────────────

    public static void LoadSnapshot(RunSnapshot snapshot)
    {
        if (!_snapshots.TryGetValue(snapshot.Floor, out var entry))
        {
            MainFile.Logger.Info($"[Snapshot] Load floor {snapshot.Floor}: not found in memory.");
            return;
        }
        MainFile.Logger.Info($"[Snapshot] Starting load for floor {snapshot.Floor}.");
        _ = LoadSnapshotAsync(snapshot.Floor, entry.RunSave);
    }

    private static async Task LoadSnapshotAsync(int floor, SerializableRun runSave)
    {
        try
        {
            var game        = NGame.Instance;
            var saveManager = SaveManager.Instance;
            var runManager  = RunManager.Instance;

            // Wait for any pending save so we don't race against an in-flight write.
            var pending = saveManager.CurrentRunSaveTask;
            if (pending != null)
            {
                MainFile.Logger.Info("[Snapshot] LoadAsync: waiting for pending save...");
                await pending;
            }

            runManager.ActionExecutor.Cancel();
            runManager.ActionQueueSet.Reset();

            // Reconstruct run state directly from the captured object — no file I/O needed.
            RunState runState = RunState.FromSerializable(runSave);

            await game.Transition.FadeOut();

            runManager.CleanUp();

            // Method name changed capitalisation between game versions.
            var setupMethod =
                AccessTools.Method(typeof(RunManager), "SetUpSavedSingleplayer", [typeof(RunState), typeof(SerializableRun)]) ??
                AccessTools.Method(typeof(RunManager), "SetUpSavedSinglePlayer", [typeof(RunState), typeof(SerializableRun)]);
            if (setupMethod != null)
            {
                var result = setupMethod.Invoke(runManager, [runState, runSave]);
                if (result is Task t) await t;
            }
            else
            {
                MainFile.Logger.Info("[Snapshot] LoadAsync: SetUpSavedSingleplayer not found.");
            }

            await game.LoadRun(runState, runSave.PreFinishedRoom);
            await game.Transition.FadeIn();

            MainFile.Logger.Info($"[Snapshot] Load complete for floor {floor}.");
        }
        catch (Exception ex)
        {
            MainFile.Logger.Info($"[Snapshot] LoadAsync error: {ex}");
        }
    }
}
