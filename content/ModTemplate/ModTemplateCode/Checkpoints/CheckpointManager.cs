using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace ModTemplate.ModTemplateCode.Checkpoints;

public static class CheckpointManager
{
    public static int CheckpointCount => _checkpoints.Count;

    // Floor → (complete run state at that floor, time it was captured)
    private static readonly Dictionary<int, (SerializableRun RunSave, DateTime SavedAt)> _checkpoints = new();

    private static bool _isLoadingCheckpoint;

    private static string CheckpointRoot => Path.Combine(OS.GetUserDataDir(), "mod_checkpoints");
    private static string ActiveDir      => Path.Combine(CheckpointRoot, "active");

    // ── Run lifecycle ─────────────────────────────────────────────────────────

    public static void OnRunStart()
    {
        _checkpoints.Clear();
        if (Directory.Exists(ActiveDir))
            Directory.Delete(ActiveDir, recursive: true);
        Directory.CreateDirectory(ActiveDir);
        MainFile.Logger.Info("[Checkpoint] New run started.");
    }

    public static void OnRunContinue()
    {
        if (_isLoadingCheckpoint) return;
        _checkpoints.Clear();
        if (Directory.Exists(ActiveDir))
        {
            foreach (var dir in Directory.GetDirectories(ActiveDir))
            {
                var name = Path.GetFileName(dir);
                if (!name.StartsWith("floor_") || !int.TryParse(name[6..], out var floor)) continue;

                var checkpointFile = Path.Combine(dir, "checkpoint.json");
                var metaFile       = Path.Combine(dir, "meta.json");
                if (!File.Exists(checkpointFile)) continue;

                try
                {
                    var result = JsonSerializationUtility.FromJson<SerializableRun>(
                        File.ReadAllText(checkpointFile));
                    if (!result.Success || result.SaveData == null) continue;
                    var runSave = result.SaveData;

                    var savedAt = File.Exists(metaFile)
                        ? DateTime.Parse(File.ReadAllText(metaFile), null, System.Globalization.DateTimeStyles.RoundtripKind)
                        : Directory.GetCreationTimeUtc(dir);

                    _checkpoints[floor] = (runSave, savedAt);
                    MainFile.Logger.Info($"[Checkpoint] Restored floor {floor} from disk.");
                }
                catch (Exception ex)
                {
                    MainFile.Logger.Info($"[Checkpoint] Could not restore floor {floor}: {ex.Message}");
                }
            }
        }
        MainFile.Logger.Info($"[Checkpoint] Continued run | {_checkpoints.Count} checkpoint(s).");
    }

    public static void OnRunEnd()
    {
        _checkpoints.Clear();
        if (Directory.Exists(ActiveDir))
        {
            Directory.Delete(ActiveDir, recursive: true);
            MainFile.Logger.Info("[Checkpoint] Run ended, checkpoints cleared.");
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
                MainFile.Logger.Info($"[Checkpoint] Save floor {floor}: LoadRunSave failed (status={readResult.Status}).");
                return;
            }

            var runSave = readResult.SaveData;
            bool isNew  = !_checkpoints.ContainsKey(floor);
            var savedAt = DateTime.UtcNow;
            _checkpoints[floor] = (runSave, savedAt);

            // Persist to disk so checkpoints survive game restarts.
            var dir = Path.Combine(ActiveDir, $"floor_{floor:D2}");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "checkpoint.json"),
                JsonSerializationUtility.ToJson(runSave));
            File.WriteAllText(Path.Combine(dir, "meta.json"),
                savedAt.ToString("O")); // ISO 8601 round-trip format

            MainFile.Logger.Info($"[Checkpoint] {(isNew ? "Saved" : "Overwrote")} floor {floor} ({_checkpoints.Count} total).");
        }
        catch (Exception ex)
        {
            MainFile.Logger.Info($"[Checkpoint] SaveAsync error: {ex}");
        }
    }

    // ── Enumerate ─────────────────────────────────────────────────────────────

    public static List<RunCheckpoint> LoadAll() =>
        [.. _checkpoints
            .Select(kvp => new RunCheckpoint { Floor = kvp.Key, SavedAt = kvp.Value.SavedAt })
            .OrderByDescending(s => s.Floor)];

    // ── Load ──────────────────────────────────────────────────────────────────

    public static void LoadCheckpoint(RunCheckpoint checkpoint)
    {
        if (!_checkpoints.TryGetValue(checkpoint.Floor, out var entry))
        {
            MainFile.Logger.Info($"[Checkpoint] Load floor {checkpoint.Floor}: not found in memory.");
            return;
        }
        MainFile.Logger.Info($"[Checkpoint] Starting load for floor {checkpoint.Floor}.");
        _ = LoadCheckpointAsync(checkpoint.Floor, entry.RunSave);
    }

    private static async Task LoadCheckpointAsync(int floor, SerializableRun runSave)
    {
        try
        {
            var game        = NGame.Instance;
            var saveManager = SaveManager.Instance;
            var runManager  = RunManager.Instance;

            if (game == null || saveManager == null || runManager == null)
            {
                MainFile.Logger.Info("[Checkpoint] LoadAsync: required instances not available.");
                return;
            }

            // Wait for any pending save so we don't race against an in-flight write.
            var pending = saveManager.CurrentRunSaveTask;
            if (pending != null)
            {
                MainFile.Logger.Info("[Checkpoint] LoadAsync: waiting for pending save...");
                await pending;
            }

            runManager.ActionExecutor.Cancel();
            runManager.ActionQueueSet.Reset();

            // Reconstruct run state directly from the captured object — no file I/O needed.
            RunState runState = RunState.FromSerializable(runSave);

            var transition = game.Transition;
            if (transition != null) await transition.FadeOut();

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
                MainFile.Logger.Info("[Checkpoint] LoadAsync: SetUpSavedSingleplayer not found.");
            }

            _isLoadingCheckpoint = true;
            try { await game.LoadRun(runState, runSave.PreFinishedRoom); }
            finally { _isLoadingCheckpoint = false; }

            if (transition != null) await transition.FadeIn();

            MainFile.Logger.Info($"[Checkpoint] Load complete for floor {floor}.");
        }
        catch (Exception ex)
        {
            MainFile.Logger.Info($"[Checkpoint] LoadAsync error: {ex}");
        }
    }
}
