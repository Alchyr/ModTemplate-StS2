using System.Text.Json;
using Godot;

namespace ModTemplate.ModTemplateCode.Snapshots;

public static class SnapshotManager
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private static string? _runId;

    private static string SnapshotRoot =>
        Path.Combine(OS.GetUserDataDir(), "mod_snapshots");

    private static string RunDir =>
        Path.Combine(SnapshotRoot, _runId ?? "active");

    public static void OnRunStart()
    {
        _runId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N")[..6];
        Directory.CreateDirectory(RunDir);
        MainFile.Logger.Info($"[Snapshot] Run session started: {_runId}");
    }

    public static void Save(RunSnapshot snapshot)
    {
        // Start a session automatically if the run-start hook didn't fire.
        if (_runId is null) OnRunStart();

        snapshot.RunId = _runId!;
        Directory.CreateDirectory(RunDir);
        string path = Path.Combine(RunDir, $"{snapshot.SnapshotId}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(snapshot, JsonOpts));
        MainFile.Logger.Info(
            $"[Snapshot] Saved [{snapshot.SnapshotId}] Floor {snapshot.Floor} " +
            $"HP {snapshot.CurrentHp}/{snapshot.MaxHp} Gold {snapshot.Gold}");
    }

    public static List<RunSnapshot> LoadAll()
    {
        if (!Directory.Exists(RunDir)) return [];
        return [.. Directory
            .GetFiles(RunDir, "*.json")
            .Select(f =>
            {
                try { return JsonSerializer.Deserialize<RunSnapshot>(File.ReadAllText(f)); }
                catch { return null; }
            })
            .OfType<RunSnapshot>()
            .OrderByDescending(s => s.CreatedAt)];
    }

    public static RunSnapshot? LoadLatest() => LoadAll().FirstOrDefault();

    public static void OnRunEnd()
    {
        if (!Directory.Exists(RunDir))
        {
            _runId = null;
            return;
        }
        Directory.Delete(RunDir, recursive: true);
        MainFile.Logger.Info($"[Snapshot] Deleted all snapshots for run {_runId}");
        _runId = null;
    }
}
