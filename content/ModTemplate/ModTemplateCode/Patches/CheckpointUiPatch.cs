using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using ModTemplate.ModTemplateCode.Checkpoints;
using ModTemplate.ModTemplateCode.Nodes;

namespace ModTemplate.ModTemplateCode.Patches;

internal static class CheckpointUiPatch
{
    // ── Initialize UI and load checkpoints when a run scene is ready ──────────

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
                    CheckpointUi.Initialize(sceneRoot);

                CheckpointManager.OnRunContinue();
            }
            catch (Exception ex) { MainFile.Logger.Info($"[Checkpoint] NRunReady error: {ex}"); }
        }
    }

    // ── Drive UI every frame ──────────────────────────────────────────────────

    [HarmonyPatch(typeof(NRun), "_Process")]
    static class NRunProcessPatch
    {
        [HarmonyPostfix]
        static void Postfix(double delta)
        {
            try { CheckpointUi.Update(delta); }
            catch (Exception ex) { MainFile.Logger.Info($"[Checkpoint] UI error: {ex.Message}"); }
        }
    }
}
