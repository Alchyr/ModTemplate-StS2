using HarmonyLib;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using ModTemplate.ModTemplateCode.Checkpoints;

namespace ModTemplate.ModTemplateCode.Patches;

// Run starts when a new singleplayer run is initiated.
[HarmonyPatch(typeof(NGame), nameof(NGame.StartNewSingleplayerRun))]
static class RunStartPatch
{
    [HarmonyPostfix]
    static void Postfix()
    {
        try { CheckpointManager.OnRunStart(); }
        catch (Exception ex) { MainFile.Logger.Info($"[Checkpoint] RunStart error: {ex.Message}"); }
    }
}

// Run continues when the player resumes an existing run from the main menu.
[HarmonyPatch("MegaCrit.Sts2.Core.Nodes.NGame", "LoadRun")]
static class RunContinuePatch
{
    [HarmonyPostfix]
    static void Postfix()
    {
        try { CheckpointManager.OnRunContinue(); }
        catch (Exception ex) { MainFile.Logger.Info($"[Checkpoint] RunContinue error: {ex.Message}"); }
    }
}

// Auto-save a checkpoint at the start of every floor.
// Hook.BeforeRoomEntered is the game's canonical hook point fired before any room logic runs.
[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeRoomEntered))]
static class FloorCheckpointPatch
{
    [HarmonyPostfix]
    static void Postfix(IRunState runState, AbstractRoom room)
    {
        try { CheckpointManager.Save(runState.TotalFloor); }
        catch (Exception ex) { MainFile.Logger.Info($"[Checkpoint] FloorCheckpoint error: {ex.Message}"); }
    }
}
