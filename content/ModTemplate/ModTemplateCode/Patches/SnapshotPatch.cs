using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using ModTemplate.ModTemplateCode.Nodes;
using ModTemplate.ModTemplateCode.Snapshots;

namespace ModTemplate.ModTemplateCode.Patches;

internal static class SnapshotPatch
{
    // ── Add UI when a run scene loads ─────────────────────────────────────────

    [HarmonyPatch(typeof(NRun), "_Ready")]
    static class NRunReadyPatch
    {
        [HarmonyPostfix]
        static void Postfix(NRun __instance)
        {
            try
            {
                var sceneRoot = __instance.GetTree()?.Root;
                if (sceneRoot != null)
                    SnapshotUi.Initialize(sceneRoot);

                // Sync snapshot count now that the run scene is live.
                SnapshotManager.OnRunContinue();
            }
            catch (Exception ex) { MainFile.Logger.Info($"[Snapshot] NRunReady error: {ex}"); }
        }
    }

    // ── Drive UI every frame ──────────────────────────────────────────────────

    [HarmonyPatch(typeof(NRun), "_Process")]
    static class NRunProcessPatch
    {
        [HarmonyPostfix]
        static void Postfix(double delta)
        {
            try { SnapshotUi.Update(delta); }
            catch (Exception ex) { MainFile.Logger.Info($"[Snapshot] UI error: {ex.Message}"); }
        }
    }

    // ── Public surface used by FloorSnapshotPatch and SnapshotUi ─────────────

    public static void SaveSnapshot(int floor)
    {
        try { SnapshotManager.Save(floor); }
        catch (Exception ex) { MainFile.Logger.Info($"[Snapshot] Save error: {ex}"); }
    }

    public static void LoadSnapshot(RunSnapshot snapshot)
    {
        try { SnapshotManager.LoadSnapshot(snapshot); }
        catch (Exception ex) { MainFile.Logger.Info($"[Snapshot] Load snapshot error: {ex}"); }
    }
}
