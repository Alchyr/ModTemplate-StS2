using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using ModTemplate.ModTemplateCode.Nodes;
using ModTemplate.ModTemplateCode.Snapshots;

namespace ModTemplate.ModTemplateCode.Patches;

// Run starts when a new singleplayer run is initiated.
[HarmonyPatch(typeof(NGame), nameof(NGame.StartNewSingleplayerRun))]
static class RunStartPatch
{
    [HarmonyPostfix]
    static void Postfix()
    {
        try { SnapshotManager.OnRunStart(); }
        catch (Exception ex) { MainFile.Logger.Info($"[Snapshot] RunStart error: {ex.Message}"); }
    }
}

// Run ends when the player returns to the main menu (covers both death and victory).
// LaunchMainMenu is internal in sts2.dll so we resolve it by name at runtime.
[HarmonyPatch("MegaCrit.Sts2.Core.Nodes.NGame", "LaunchMainMenu")]
static class RunEndPatch
{
    [HarmonyPrefix]
    static void Prefix()
    {
        try { SnapshotManager.OnRunEnd(); }
        catch (Exception ex) { MainFile.Logger.Info($"[Snapshot] RunEnd error: {ex.Message}"); }
    }
}

// Add our input-listening node as a child of NGame so it persists for the whole session.
[HarmonyPatch(typeof(NGame), "_Ready")]
static class NGameReadyPatch
{
    [HarmonyPostfix]
    static void Postfix(NGame __instance)
    {
        try
        {
            if (!__instance.HasNode("SnapshotInputNode"))
                __instance.AddChild(new SnapshotInputNode { Name = "SnapshotInputNode" });
            MainFile.Logger.Info("[Snapshot] Input node added to NGame (F5 = save, F9 = restore).");
        }
        catch (Exception ex) { MainFile.Logger.Info($"[Snapshot] NGameReady error: {ex.Message}"); }
    }
}
