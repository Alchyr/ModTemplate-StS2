using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
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

// Run continues when the player resumes an existing run from the main menu.
[HarmonyPatch("MegaCrit.Sts2.Core.Nodes.NGame", "LoadRun")]
static class RunContinuePatch
{
    [HarmonyPostfix]
    static void Postfix()
    {
        try { SnapshotManager.OnRunContinue(); }
        catch (Exception ex) { MainFile.Logger.Info($"[Snapshot] RunContinue error: {ex.Message}"); }
    }
}

// Run ends when the player returns to the main menu (covers both death and victory).
// LaunchMainMenu is internal in sts2.dll so we resolve it by name at runtime.
// [HarmonyPatch("MegaCrit.Sts2.Core.Nodes.NGame", "LaunchMainMenu")]
// static class RunEndPatch
// {
//     [HarmonyPrefix]
//     static void Prefix()
//     {
//         try { SnapshotManager.OnRunEnd(); SnapshotUi.Teardown(); }
//         catch (Exception ex) { MainFile.Logger.Info($"[Snapshot] RunEnd error: {ex.Message}"); }
//     }
// }

// Auto-save a snapshot at the start of every floor.
// Hook.BeforeRoomEntered is the game's canonical hook point fired before any room logic runs.
[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeRoomEntered))]
static class FloorSnapshotPatch
{
    [HarmonyPostfix]
    static void Postfix(IRunState runState, AbstractRoom room)
    {
        try { SnapshotPatch.SaveSnapshot(runState.TotalFloor); }
        catch (Exception ex) { MainFile.Logger.Info($"[Snapshot] FloorSnapshot error: {ex.Message}"); }
    }
}

// Auto-press Continue when the main menu loads after a restore so the
// player lands directly back in the restored run without any manual click.
[HarmonyPatch(typeof(NMainMenuContinueButton), "_Ready")]
static class AutoContinuePatch
{
    [HarmonyPostfix]
    static void Postfix(NMainMenuContinueButton __instance)
    {
        if (!SnapshotManager.IsRestoring) return;
        Callable.From(() => __instance.EmitSignal(Button.SignalName.Pressed)).CallDeferred();
    }
}
